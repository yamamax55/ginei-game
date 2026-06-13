using UnityEngine;

namespace Ginei
{
    /// <summary>艦齢の調整係数。</summary>
    public readonly struct ShipAgingParams
    {
        /// <summary>設計寿命（この艦齢までは性能満額）。</summary>
        public readonly float designLife;
        /// <summary>設計寿命超過で性能が落ちる速度（超過1単位時間あたりの低下率）。</summary>
        public readonly float degradeRate;
        /// <summary>性能低下の下限（老朽艦でも最低これだけは動く）。</summary>
        public readonly float minPerformance;
        /// <summary>維持費が増える速度（艦齢1単位時間あたりの増加率）。</summary>
        public readonly float upkeepGrowthRate;
        /// <summary>更新（リプレース）を推奨する性能の閾値（これ未満は維持より新造が安い）。</summary>
        public readonly float replaceThreshold;

        public ShipAgingParams(float designLife, float degradeRate, float minPerformance,
                               float upkeepGrowthRate, float replaceThreshold)
        {
            this.designLife = Mathf.Max(0f, designLife);
            this.degradeRate = Mathf.Max(0f, degradeRate);
            this.minPerformance = Mathf.Clamp01(minPerformance);
            this.upkeepGrowthRate = Mathf.Max(0f, upkeepGrowthRate);
            this.replaceThreshold = Mathf.Clamp01(replaceThreshold);
        }

        /// <summary>既定＝設計寿命30・低下率0.02・性能下限0.5・維持費増0.02・更新閾値0.7。</summary>
        public static ShipAgingParams Default => new ShipAgingParams(30f, 0.02f, 0.5f, 0.02f, 0.7f);
    }

    /// <summary>
    /// 艦齢の純ロジック（経年劣化）。艦は設計寿命までは性能満額、超えると性能が漸減し（下限あり）、
    /// 維持費は艦齢とともに単調に増える＝古い艦隊は「動く」が高くつき弱い。性能が更新閾値を割れば
    /// 維持より新造が安い＝更新需要が立つ。損傷の回復（<see cref="RepairRules"/>＝直せば戻る）とは
    /// 別系統で、直しても戻らない経年だけを扱う。倍率は基準値に掛けて使う（実効値パターン・基準非破壊）。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ShipAgingRules
    {
        /// <summary>
        /// 艦齢の性能倍率（minPerformance..1）。設計寿命までは1.0、超過分×低下率で漸減し下限で止まる。
        /// 戦闘・速度の基準値に掛けて使う。
        /// </summary>
        public static float PerformanceFactor(float age, ShipAgingParams p)
        {
            float a = Mathf.Max(0f, age);
            if (a <= p.designLife) return 1f;
            float decline = (a - p.designLife) * p.degradeRate;
            return Mathf.Max(p.minPerformance, 1f - decline);
        }

        public static float PerformanceFactor(float age) => PerformanceFactor(age, ShipAgingParams.Default);

        /// <summary>艦齢の維持費倍率（1..）。艦齢×増加率ぶん高くなる（寿命内でも古いほど手がかかる）。</summary>
        public static float UpkeepFactor(float age, ShipAgingParams p)
        {
            return 1f + Mathf.Max(0f, age) * p.upkeepGrowthRate;
        }

        public static float UpkeepFactor(float age) => UpkeepFactor(age, ShipAgingParams.Default);

        /// <summary>更新需要が立っているか＝性能倍率が更新閾値未満（維持より新造が安い）。</summary>
        public static bool NeedsReplacement(float age, ShipAgingParams p)
        {
            return PerformanceFactor(age, p) < p.replaceThreshold;
        }

        public static bool NeedsReplacement(float age) => NeedsReplacement(age, ShipAgingParams.Default);

        /// <summary>更新需要が立つ艦齢（性能が閾値を割る境界）。閾値が下限以下なら永遠に立たない＝無限大。</summary>
        public static float ReplacementAge(ShipAgingParams p)
        {
            if (p.replaceThreshold <= p.minPerformance) return float.PositiveInfinity;
            if (p.degradeRate <= 0f) return float.PositiveInfinity;
            return p.designLife + (1f - p.replaceThreshold) / p.degradeRate;
        }

        /// <summary>
        /// 艦隊の平均性能倍率＝各艦齢の性能倍率の兵力加重平均。strengths と ages は同じ長さ（短い方に合わせる）。
        /// 空・総兵力0は1.0（補正なし）。
        /// </summary>
        public static float FleetPerformanceFactor(float[] ages, float[] strengths, ShipAgingParams p)
        {
            if (ages == null || strengths == null) return 1f;
            int count = Mathf.Min(ages.Length, strengths.Length);
            float sumW = 0f;
            float sumF = 0f;
            for (int i = 0; i < count; i++)
            {
                float w = Mathf.Max(0f, strengths[i]);
                if (w <= 0f) continue;
                sumW += w;
                sumF += w * PerformanceFactor(ages[i], p);
            }
            if (sumW <= 0f) return 1f;
            return sumF / sumW;
        }

        public static float FleetPerformanceFactor(float[] ages, float[] strengths)
            => FleetPerformanceFactor(ages, strengths, ShipAgingParams.Default);
    }
}
