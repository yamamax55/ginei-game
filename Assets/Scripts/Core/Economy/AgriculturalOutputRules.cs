using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 農業生産の純ロジック。耕地・農業技術・天候から食料生産量を算出し、
    /// 需要と比較して飢饉の深刻度を判定する。
    /// MonoBehaviour/シーン非依存・決定論・後方互換。
    /// </summary>
    public static class AgriculturalOutputRules
    {
        // ゼロ除算回避用の微小値
        private const float Epsilon = 1e-6f;

        /// <summary>農業生産の調整パラメータ。</summary>
        public readonly struct Params
        {
            /// <summary>耕地単位あたりの基準収量。</summary>
            public readonly float landYield;
            /// <summary>農業技術の寄与重み（技術01に掛ける）。</summary>
            public readonly float techWeight;
            /// <summary>天候不良による減産の振れ幅（0..1）。</summary>
            public readonly float weatherSwing;

            public Params(float landYield, float techWeight, float weatherSwing)
            {
                this.landYield = landYield;
                this.techWeight = techWeight;
                this.weatherSwing = weatherSwing;
            }

            /// <summary>既定値（収量1/技術寄与0.5/天候振れ0.4）。</summary>
            public static Params Default => new Params(1f, 0.5f, 0.4f);
        }

        /// <summary>
        /// 食料生産量。
        /// landYield*arableLand*(1+farmTech01*techWeight)*(1 - weatherSwing*(1-clamp01(weather01)))、最小0。
        /// </summary>
        public static float FoodOutput(float arableLand, float farmTech01, float weather01, Params p)
        {
            // 技術ブースト（技術が高いほど収量増）
            float techFactor = 1f + Mathf.Max(0f, farmTech01) * p.techWeight;
            // 天候ペナルティ（天候が悪い=weather01が小さいほど減産）
            float weatherFactor = 1f - p.weatherSwing * (1f - Mathf.Clamp01(weather01));
            float output = p.landYield * Mathf.Max(0f, arableLand) * techFactor * weatherFactor;
            return Mathf.Max(0f, output); // 生産量は非負
        }

        /// <summary>食料生産量（既定パラメータ）。</summary>
        public static float FoodOutput(float arableLand, float farmTech01, float weather01)
            => FoodOutput(arableLand, farmTech01, weather01, Params.Default);

        /// <summary>
        /// 飢饉深刻度。clamp01((demand-output)/max(epsilon,demand))。
        /// 生産が需要以上なら0、不足ぶんを0..1で返す。
        /// </summary>
        public static float FamineSeverity(float foodOutput, float foodDemand)
        {
            if (foodOutput >= foodDemand) return 0f; // 余剰なら飢饉なし
            float denom = Mathf.Max(Epsilon, foodDemand);
            return Mathf.Clamp01((foodDemand - foodOutput) / denom);
        }

        /// <summary>飢饉判定（深刻度が閾値を超えるか）。</summary>
        public static bool IsFamine(float foodOutput, float foodDemand, float threshold)
            => FamineSeverity(foodOutput, foodDemand) > threshold;
    }
}
