using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 電力網の純ロジック。発電量と需要を比較し、供給不足による
    /// ブラウンアウト（電力制限）が経済産出に課す減衰を算出する。
    /// MonoBehaviour/シーン非依存・決定論・後方互換。
    /// </summary>
    public static class EnergyGridRules
    {
        // ゼロ除算回避用の微小値
        private const float Epsilon = 1e-6f;

        /// <summary>電力網の調整パラメータ。</summary>
        public readonly struct Params
        {
            /// <summary>望ましい予備率（参考値・将来の余裕判定用）。</summary>
            public readonly float reserveMargin;
            /// <summary>ブラウンアウト深刻度が産出に与えるペナルティ係数。</summary>
            public readonly float brownoutPenalty;

            public Params(float reserveMargin, float brownoutPenalty)
            {
                this.reserveMargin = reserveMargin;
                this.brownoutPenalty = brownoutPenalty;
            }

            /// <summary>既定値（予備率0.15/ペナルティ1.0）。</summary>
            public static Params Default => new Params(0.15f, 1f);
        }

        /// <summary>供給比＝generation/max(epsilon,demand)。</summary>
        public static float SupplyRatio(float generation, float demand)
        {
            float denom = Mathf.Max(Epsilon, demand);
            return Mathf.Max(0f, generation) / denom;
        }

        /// <summary>
        /// ブラウンアウト深刻度。供給が需要以上(supplyRatio>=1)なら0、
        /// 不足なら clamp01(1 - supplyRatio) を返す（0..1）。
        /// </summary>
        public static float BrownoutSeverity(float supplyRatio, Params p)
        {
            if (supplyRatio >= 1f) return 0f; // 供給十分なら停電なし
            return Mathf.Clamp01(1f - supplyRatio);
        }

        /// <summary>ブラウンアウト深刻度（既定パラメータ）。</summary>
        public static float BrownoutSeverity(float supplyRatio)
            => BrownoutSeverity(supplyRatio, Params.Default);

        /// <summary>
        /// 経済産出倍率。clamp01(1 - brownoutSeverity*brownoutPenalty)。
        /// 停電が無ければ1.0、深刻なほど低下（≦1）。
        /// </summary>
        public static float OutputFactor(float brownoutSeverity, Params p)
        {
            float bs = Mathf.Clamp01(brownoutSeverity);
            return Mathf.Clamp01(1f - bs * p.brownoutPenalty);
        }

        /// <summary>経済産出倍率（既定パラメータ）。</summary>
        public static float OutputFactor(float brownoutSeverity)
            => OutputFactor(brownoutSeverity, Params.Default);
    }
}
