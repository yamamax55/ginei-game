using UnityEngine;

namespace Ginei
{
    /// <summary>強行軍の調整係数。</summary>
    public readonly struct ForcedMarchParams
    {
        /// <summary>全力強行（intensity=1）の速度倍率上限（1.0以上・強行ほど速い）。</summary>
        public readonly float maxSpeedBoost;
        /// <summary>全力強行時の疲労蓄積速度（疲労/時間・intensity に比例して積む）。</summary>
        public readonly float fatigueGainRate;
        /// <summary>休息（intensity=0）時の疲労回復速度（疲労/時間）。蓄積より遅い＝借りた速さの返済は高くつく。</summary>
        public readonly float fatigueRecoveryRate;
        /// <summary>疲労困憊（fatigue=1）時に戦闘倍率から削られる最大幅（0..1）。</summary>
        public readonly float maxCombatPenalty;
        /// <summary>落伍が始まる疲労閾値（0..1）。これ以下なら無理が利く＝ミッターマイヤー的速攻の安全圏。</summary>
        public readonly float stragglerThreshold;
        /// <summary>落伍率の最大幅（疲労最大×全力強行のとき・per dt）。</summary>
        public readonly float stragglerScale;

        public ForcedMarchParams(float maxSpeedBoost, float fatigueGainRate, float fatigueRecoveryRate,
                                 float maxCombatPenalty, float stragglerThreshold, float stragglerScale)
        {
            this.maxSpeedBoost = Mathf.Max(1f, maxSpeedBoost);
            this.fatigueGainRate = Mathf.Max(0f, fatigueGainRate);
            this.fatigueRecoveryRate = Mathf.Max(0f, fatigueRecoveryRate);
            this.maxCombatPenalty = Mathf.Clamp01(maxCombatPenalty);
            this.stragglerThreshold = Mathf.Clamp01(stragglerThreshold);
            this.stragglerScale = Mathf.Clamp01(stragglerScale);
        }

        /// <summary>既定＝速度上限1.5倍・蓄積0.1・回復0.05（蓄積の半分）・戦闘減幅0.5・落伍閾値0.6・落伍幅0.1。</summary>
        public static ForcedMarchParams Default => new ForcedMarchParams(1.5f, 0.1f, 0.05f, 0.5f, 0.6f, 0.1f);
    }

    /// <summary>
    /// 強行軍の純ロジック＝「速さは疲労で買う」。行軍強度（marchIntensity 0..1）を上げるほど到着は早まるが
    /// 疲労が積み上がり、到着直後の戦闘倍率が下がる（疲れた軍は弱い）。閾値を超えてなお無理を重ねると
    /// 落伍兵が出て兵力そのものが目減りする＝ミッターマイヤー的な速攻は「閾値の内側で駆ける」疲労管理の芸。
    /// <see cref="StrategicFleet"/>（移動そのもの）は不変＝ここは速度/戦闘/落伍の係数算出のみで、
    /// 適用（速度への乗算・兵力減算・疲労値の保持）は呼び出し側が行う。
    /// 行軍に起因する公然の損耗（落伍）を扱い、士気・補給・長期従軍による無言の損耗は
    /// <see cref="DesertionRules"/>（脱走）が別系統で扱う。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ForcedMarchRules
    {
        /// <summary>行軍速度倍率＝1.0〜maxSpeedBoost を強度で線形補間（強行ほど速い・強度0で等速）。</summary>
        public static float SpeedFactor(float marchIntensity, ForcedMarchParams p)
        {
            return 1f + Mathf.Clamp01(marchIntensity) * (p.maxSpeedBoost - 1f);
        }

        public static float SpeedFactor(float marchIntensity) => SpeedFactor(marchIntensity, ForcedMarchParams.Default);

        /// <summary>
        /// 疲労の1tick後の値（0..1）。強行中（intensity&gt;0）は強度×蓄積速度×dt で積み、
        /// 通常行軍/休息（intensity=0）なら回復速度×dt で抜く。回復は蓄積より遅い＝無理のツケは倍払い。
        /// </summary>
        public static float FatigueTick(float fatigue, float marchIntensity, float dt, ForcedMarchParams p)
        {
            float f = Mathf.Clamp01(fatigue);
            float intensity = Mathf.Clamp01(marchIntensity);
            float t = Mathf.Max(0f, dt);
            if (intensity > 0f) return Mathf.Clamp01(f + intensity * p.fatigueGainRate * t);
            return Mathf.Clamp01(f - p.fatigueRecoveryRate * t);
        }

        public static float FatigueTick(float fatigue, float marchIntensity, float dt)
            => FatigueTick(fatigue, marchIntensity, dt, ForcedMarchParams.Default);

        /// <summary>
        /// 到着直後の戦闘倍率（1=万全〜1−maxCombatPenalty=疲労困憊）。疲労に比例して線形に削る。
        /// 速さで稼いだ時間は、着いた瞬間の弱さで支払う。
        /// </summary>
        public static float CombatPenalty(float fatigue, ForcedMarchParams p)
        {
            return 1f - Mathf.Clamp01(fatigue) * p.maxCombatPenalty;
        }

        public static float CombatPenalty(float fatigue) => CombatPenalty(fatigue, ForcedMarchParams.Default);

        /// <summary>
        /// 落伍率（per dt・兵力の目減り割合は呼び出し側が乗算）。疲労が閾値以下なら0＝無理が利く範囲。
        /// 閾値超過分（0..1 正規化）×強度×落伍幅＝「疲れた軍をさらに駆けさせる」ほど兵が脱落する。
        /// 休息中（intensity=0）は疲労していても落伍しない。
        /// </summary>
        public static float StragglerRatio(float fatigue, float marchIntensity, ForcedMarchParams p)
        {
            float f = Mathf.Clamp01(fatigue);
            if (f <= p.stragglerThreshold) return 0f;
            float denom = 1f - p.stragglerThreshold;
            if (denom <= 0f) return 0f;
            float excess = (f - p.stragglerThreshold) / denom;
            return excess * Mathf.Clamp01(marchIntensity) * p.stragglerScale;
        }

        public static float StragglerRatio(float fatigue, float marchIntensity)
            => StragglerRatio(fatigue, marchIntensity, ForcedMarchParams.Default);

        /// <summary>現在疲労を完全に抜くまでの休息所要時間。回復速度0なら無限大（休めない軍は戻らない）。</summary>
        public static float RecoveryTime(float fatigue, ForcedMarchParams p)
        {
            float f = Mathf.Clamp01(fatigue);
            if (f <= 0f) return 0f;
            if (p.fatigueRecoveryRate <= 0f) return float.PositiveInfinity;
            return f / p.fatigueRecoveryRate;
        }

        public static float RecoveryTime(float fatigue) => RecoveryTime(fatigue, ForcedMarchParams.Default);
    }
}
