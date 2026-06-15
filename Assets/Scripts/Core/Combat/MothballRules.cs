using UnityEngine;

namespace Ginei
{
    /// <summary>予備役・モスボール保管の調整係数。</summary>
    public readonly struct MothballParams
    {
        /// <summary>保管中の維持費倍率（現役=1.0 に対して。大幅減が眠らせる動機）。</summary>
        public readonly float upkeepFactor;
        /// <summary>再就役の基礎所要時間（保管期間ゼロでもこれだけはかかる）。</summary>
        public readonly float baseReactivationTime;
        /// <summary>保管期間1単位時間あたりの再就役所要時間の増分（長く眠った艦ほど起こすのに時間）。</summary>
        public readonly float reactivationTimeGrowth;
        /// <summary>再就役整備費の基礎係数（兵力1あたり）。</summary>
        public readonly float reactivationCostBase;
        /// <summary>保管期間1単位時間あたりの整備費増分係数（兵力1あたり）。</summary>
        public readonly float reactivationCostGrowth;
        /// <summary>保守ゼロ時の保管状態の劣化速度（状態/単位時間）。</summary>
        public readonly float decayRate;
        /// <summary>保守満額時の劣化倍率（0..1。保守すれば緩やかになるがゼロにはならない）。</summary>
        public readonly float maintainedDecayScale;
        /// <summary>再就役直後の実効戦力の下限（朽ち果てた艦でも最低これだけは出る）。</summary>
        public readonly float minEffectiveness;

        public MothballParams(float upkeepFactor, float baseReactivationTime, float reactivationTimeGrowth,
                              float reactivationCostBase, float reactivationCostGrowth,
                              float decayRate, float maintainedDecayScale, float minEffectiveness)
        {
            this.upkeepFactor = Mathf.Clamp01(upkeepFactor);
            this.baseReactivationTime = Mathf.Max(0f, baseReactivationTime);
            this.reactivationTimeGrowth = Mathf.Max(0f, reactivationTimeGrowth);
            this.reactivationCostBase = Mathf.Max(0f, reactivationCostBase);
            this.reactivationCostGrowth = Mathf.Max(0f, reactivationCostGrowth);
            this.decayRate = Mathf.Max(0f, decayRate);
            this.maintainedDecayScale = Mathf.Clamp01(maintainedDecayScale);
            this.minEffectiveness = Mathf.Clamp01(minEffectiveness);
        }

        /// <summary>既定＝保管維持費0.2・再就役基礎時間10・時間増0.2・整備費基礎0.1・整備費増0.01・劣化0.02・保守時劣化0.25・実効下限0.3。</summary>
        public static MothballParams Default => new MothballParams(0.2f, 10f, 0.2f, 0.1f, 0.01f, 0.02f, 0.25f, 0.3f);
    }

    /// <summary>
    /// 予備役・モスボール（退蔵保管）の純ロジック。艦隊を眠らせれば維持費は大幅に減るが、
    /// 再就役には時間と整備費がかかり、長く眠った艦ほど起こすのに高くつく。保管中も状態は劣化し
    /// （保守を怠ると朽ちる・保守すれば緩やか）、朽ちた艦は再就役直後の実効戦力が落ちる＝
    /// 「平時の節約は有事の遅れで払う」。経年（<see cref="ShipAgingRules"/>＝艦齢で直しても戻らない劣化）
    /// とは別系統で、ここは保管状態（モスボール中の手入れ）だけを扱う。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MothballRules
    {
        /// <summary>保管中の維持費倍率（0..1）。現役の維持費に掛けて使う＝大幅減が節約の実体。</summary>
        public static float MothballedUpkeepFactor(MothballParams p)
        {
            return p.upkeepFactor;
        }

        public static float MothballedUpkeepFactor() => MothballedUpkeepFactor(MothballParams.Default);

        /// <summary>
        /// 再就役の所要時間＝基礎時間＋保管期間×時間増分。長く眠った艦ほど起こすのに時間がかかる
        /// ＝節約した平時の分だけ有事の参戦が遅れる。
        /// </summary>
        public static float ReactivationTime(float storedDuration, MothballParams p)
        {
            return p.baseReactivationTime + Mathf.Max(0f, storedDuration) * p.reactivationTimeGrowth;
        }

        public static float ReactivationTime(float storedDuration) => ReactivationTime(storedDuration, MothballParams.Default);

        /// <summary>
        /// 再就役の整備費＝兵力×（基礎係数＋保管期間×増分係数）。大艦隊・長期保管ほど起こすのが高い。
        /// </summary>
        public static float ReactivationCost(float fleetStrength, float storedDuration, MothballParams p)
        {
            float ratio = p.reactivationCostBase + Mathf.Max(0f, storedDuration) * p.reactivationCostGrowth;
            return Mathf.Max(0f, fleetStrength) * ratio;
        }

        public static float ReactivationCost(float fleetStrength, float storedDuration)
            => ReactivationCost(fleetStrength, storedDuration, MothballParams.Default);

        /// <summary>
        /// 保管状態の1tick後の値（0..1）。劣化速度は保守努力（0..1）で decayRate（保守ゼロ）から
        /// decayRate×maintainedDecayScale（保守満額）まで線形に緩む＝保守を怠ると朽ち、保守してもゼロにはならない。
        /// </summary>
        public static float ConditionDecayTick(float condition, float maintenanceEffort, float dt, MothballParams p)
        {
            float scale = Mathf.Lerp(1f, p.maintainedDecayScale, Mathf.Clamp01(maintenanceEffort));
            float decay = p.decayRate * scale * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(condition) - decay);
        }

        public static float ConditionDecayTick(float condition, float maintenanceEffort, float dt)
            => ConditionDecayTick(condition, maintenanceEffort, dt, MothballParams.Default);

        /// <summary>
        /// 再就役直後の実効戦力倍率（minEffectiveness..1）＝保管状態に比例。朽ちた艦は出ても弱い。
        /// 基準戦力に掛けて使う（実効値パターン・基準非破壊）。
        /// </summary>
        public static float ReactivatedEffectiveness(float condition, MothballParams p)
        {
            return Mathf.Lerp(p.minEffectiveness, 1f, Mathf.Clamp01(condition));
        }

        public static float ReactivatedEffectiveness(float condition) => ReactivatedEffectiveness(condition, MothballParams.Default);
    }
}
