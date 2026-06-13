using UnityEngine;

namespace Ginei
{
    /// <summary>戒厳令の調整係数。</summary>
    public readonly struct MartialLawParams
    {
        /// <summary>布告直後の騒乱鎮静速度（per dt・即効性が売り）。</summary>
        public readonly float orderRestoreRate;
        /// <summary>継続が正統性を削る速度（per dt）。</summary>
        public readonly float legitimacyDrainRate;
        /// <summary>継続が希望を削る速度（per dt・銃剣の下に未来は見えない）。</summary>
        public readonly float hopeDrainRate;
        /// <summary>副作用が本体の利得を超え始める継続時間（これを超えた戒厳は自傷）。</summary>
        public readonly float diminishingTime;

        public MartialLawParams(float orderRestoreRate, float legitimacyDrainRate, float hopeDrainRate, float diminishingTime)
        {
            this.orderRestoreRate = Mathf.Max(0f, orderRestoreRate);
            this.legitimacyDrainRate = Mathf.Max(0f, legitimacyDrainRate);
            this.hopeDrainRate = Mathf.Max(0f, hopeDrainRate);
            this.diminishingTime = Mathf.Max(0f, diminishingTime);
        }

        /// <summary>既定＝鎮静0.2・正統性減0.01・希望減0.015・限界時間30。</summary>
        public static MartialLawParams Default => new MartialLawParams(0.2f, 0.01f, 0.015f, 30f);
    }

    /// <summary>
    /// 戒厳令の純ロジック（公然の強権＝時限措置）。布告は騒乱を即座に鎮めるが、続けるほど正統性と希望を
    /// 蝕み、限界時間を超えた戒厳は治める当のものを壊す＝早く解くのが正しい使い方。秘密警察
    /// （<see cref="SecurityRules"/>＝恒常の隠れた抑圧）とは別系統＝公然・時限の非常措置。
    /// 国家緊急権（憲法停止）はバックログの別テーマ。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MartialLawRules
    {
        /// <summary>騒乱の1tick後の値（0..1）。戒厳下は鎮静速度で減る（即効）。非戒厳は変えない（他系統の仕事）。</summary>
        public static float UnrestTick(float unrest, bool martialLaw, float dt, MartialLawParams p)
        {
            float u = Mathf.Clamp01(unrest);
            if (!martialLaw) return u;
            return Mathf.Clamp01(u - p.orderRestoreRate * Mathf.Max(0f, dt));
        }

        public static float UnrestTick(float unrest, bool martialLaw, float dt)
            => UnrestTick(unrest, martialLaw, dt, MartialLawParams.Default);

        /// <summary>
        /// 継続の正統性コスト（per duration）。限界時間までは線形、超えた分は倍率2で加速する
        /// （戒厳の常態化は二重に効く）。布告からの継続時間 duration を渡す。
        /// </summary>
        public static float LegitimacyCost(float duration, MartialLawParams p)
        {
            float d = Mathf.Max(0f, duration);
            float within = Mathf.Min(d, p.diminishingTime);
            float beyond = Mathf.Max(0f, d - p.diminishingTime);
            const float OverstayMultiplier = 2f; // 限界超過の加速倍率
            return p.legitimacyDrainRate * (within + beyond * OverstayMultiplier);
        }

        public static float LegitimacyCost(float duration) => LegitimacyCost(duration, MartialLawParams.Default);

        /// <summary>継続の希望コスト（同じ形・係数違い）。</summary>
        public static float HopeCost(float duration, MartialLawParams p)
        {
            float d = Mathf.Max(0f, duration);
            float within = Mathf.Min(d, p.diminishingTime);
            float beyond = Mathf.Max(0f, d - p.diminishingTime);
            const float OverstayMultiplier = 2f;
            return p.hopeDrainRate * (within + beyond * OverstayMultiplier);
        }

        public static float HopeCost(float duration) => HopeCost(duration, MartialLawParams.Default);

        /// <summary>戒厳が自傷フェーズに入ったか＝限界時間を超えて継続している。</summary>
        public static bool IsOverstaying(float duration, MartialLawParams p)
        {
            return Mathf.Max(0f, duration) > p.diminishingTime;
        }

        public static bool IsOverstaying(float duration) => IsOverstaying(duration, MartialLawParams.Default);

        /// <summary>
        /// 解除推奨か＝騒乱が鎮静閾値以下（既定0.1）に落ちた、または自傷フェーズに入った。
        /// 「もう用は済んだ／続けるだけ損」のどちらかで解くのが正しい。
        /// </summary>
        public static bool ShouldLift(float unrest, float duration, MartialLawParams p, float calmThreshold = 0.1f)
        {
            return Mathf.Clamp01(unrest) <= Mathf.Clamp01(calmThreshold) || IsOverstaying(duration, p);
        }

        public static bool ShouldLift(float unrest, float duration)
            => ShouldLift(unrest, duration, MartialLawParams.Default);
    }
}
