using UnityEngine;

namespace Ginei
{
    /// <summary>即応態勢の調整係数。</summary>
    public readonly struct ReadinessParams
    {
        /// <summary>警戒0（休暇・休養中）の維持費倍率（0..1＝平時より安い）。</summary>
        public readonly float restUpkeepFactor;
        /// <summary>警戒1（最高警戒）の維持費倍率（1以上＝高くつく）。</summary>
        public readonly float fullAlertUpkeepFactor;
        /// <summary>警戒0のときの出撃遅延（最大値。警戒1で0になる）。</summary>
        public readonly float maxResponseDelay;
        /// <summary>持続可能水準を超えた警戒の疲労蓄積率（超過1.0あたり per dt）。</summary>
        public readonly float fatigueRate;
        /// <summary>持続可能水準を下回る警戒での疲労回復率（不足1.0あたり per dt）。</summary>
        public readonly float recoveryRate;
        /// <summary>疲労が増えも減りもしない持続可能な警戒水準（0..1）。</summary>
        public readonly float sustainableAlert;

        public ReadinessParams(float restUpkeepFactor, float fullAlertUpkeepFactor,
                               float maxResponseDelay, float fatigueRate, float recoveryRate,
                               float sustainableAlert)
        {
            this.restUpkeepFactor = Mathf.Clamp01(restUpkeepFactor);
            this.fullAlertUpkeepFactor = Mathf.Max(1f, fullAlertUpkeepFactor);
            this.maxResponseDelay = Mathf.Max(0f, maxResponseDelay);
            this.fatigueRate = Mathf.Max(0f, fatigueRate);
            this.recoveryRate = Mathf.Max(0f, recoveryRate);
            this.sustainableAlert = Mathf.Clamp01(sustainableAlert);
        }

        /// <summary>既定＝休暇維持費0.6/最高警戒2.0・最大遅延60・疲労率0.02・回復率0.04・持続可能警戒0.5。</summary>
        public static ReadinessParams Default => new ReadinessParams(0.6f, 2f, 60f, 0.02f, 0.04f, 0.5f);
    }

    /// <summary>
    /// 即応態勢（警戒水準）の純ロジック。警戒を上げるほど維持費がかさみ・出撃は速く・奇襲に強いが、
    /// 持続可能水準を超えた警戒は将兵を疲労させ、疲労した艦隊の実効警戒は緩む＝常時最高警戒は不可能で、
    /// 警戒のメリハリ管理が要る。緩めれば安く休めるが、奇襲に弱く出遅れる（休暇中の艦隊は出遅れる）。
    /// <see cref="VeterancyRules"/>（長期の熟練＝経験の蓄積）とは別の短期の警戒状態であり、
    /// 奇襲そのものの解決は <see cref="AmbushRules"/> が担う＝本クラスの実効警戒を警戒度入力として渡す想定。
    /// 倍率は基準値に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ReadinessRules
    {
        /// <summary>
        /// 警戒水準の維持費倍率＝restUpkeepFactor〜fullAlertUpkeepFactor の線形補間。
        /// 休暇（警戒0）は安く、最高警戒（警戒1）は高くつく。
        /// </summary>
        public static float UpkeepFactor(float alertLevel, ReadinessParams p)
        {
            return Mathf.Lerp(p.restUpkeepFactor, p.fullAlertUpkeepFactor, Mathf.Clamp01(alertLevel));
        }

        public static float UpkeepFactor(float alertLevel) => UpkeepFactor(alertLevel, ReadinessParams.Default);

        /// <summary>
        /// 出撃までの遅れ＝(1−警戒水準)×maxResponseDelay。休暇中（警戒0）は最大遅延、
        /// 最高警戒（警戒1）は即応＝0。
        /// </summary>
        public static float ResponseDelay(float alertLevel, ReadinessParams p)
        {
            return (1f - Mathf.Clamp01(alertLevel)) * p.maxResponseDelay;
        }

        public static float ResponseDelay(float alertLevel) => ResponseDelay(alertLevel, ReadinessParams.Default);

        /// <summary>
        /// 奇襲を受けたときの脆弱性（0..1）＝1−警戒水準。<see cref="AmbushRules.AmbushChance"/> の
        /// 警戒度入力と接続する想定（疲労を織り込むなら <see cref="EffectiveAlert"/> を渡す）。
        /// </summary>
        public static float SurpriseVulnerability(float alertLevel)
        {
            return 1f - Mathf.Clamp01(alertLevel);
        }

        /// <summary>
        /// 疲労の時間遷移。持続可能水準を超えた警戒は超過ぶん×fatigueRate で疲労を積み、
        /// 下回れば不足ぶん×recoveryRate で回復する＝最高警戒は長く張れない。戻り値は新しい疲労（0..1）。
        /// </summary>
        public static float FatigueTick(float fatigue, float alertLevel, float dt, ReadinessParams p)
        {
            float excess = Mathf.Clamp01(alertLevel) - p.sustainableAlert;
            float rate = excess >= 0f ? excess * p.fatigueRate : excess * p.recoveryRate;
            return Mathf.Clamp01(Mathf.Clamp01(fatigue) + rate * Mathf.Max(0f, dt));
        }

        public static float FatigueTick(float fatigue, float alertLevel, float dt)
            => FatigueTick(fatigue, alertLevel, dt, ReadinessParams.Default);

        /// <summary>
        /// 疲労を織り込んだ実効警戒（0..1）＝警戒水準×(1−疲労)。張り詰めた弦は緩む＝
        /// 疲弊しきった艦隊は名目上の最高警戒でも実際には何も見えていない。
        /// </summary>
        public static float EffectiveAlert(float alertLevel, float fatigue)
        {
            return Mathf.Clamp01(alertLevel) * (1f - Mathf.Clamp01(fatigue));
        }

        /// <summary>疲労と釣り合う持続可能な警戒水準＝これ以下なら無限に張り続けられる。</summary>
        public static float SustainableAlert(ReadinessParams p)
        {
            return p.sustainableAlert;
        }
    }
}
