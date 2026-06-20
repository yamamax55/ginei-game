using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 公衆衛生（保健支出）の純ロジック。
    /// 保健支出は健康水準を逓減的に高め、健康は生産性を押し上げ死亡率を下げる。
    /// 既存ゲーム型に依存せず float のみで完結する自己完結スカラ計算（test-first）。
    /// </summary>
    public static class PublicHealthRules
    {
        /// <summary>公衆衛生の調整パラメータ。</summary>
        public readonly struct Params
        {
            /// <summary>保健支出の効率（大きいほど少額で健康水準が伸びる）。</summary>
            public readonly float spendEfficiency;
            /// <summary>健康水準による生産性ボーナス（最大上乗せ率）。</summary>
            public readonly float productivityBonus;
            /// <summary>健康水準による死亡率低減（最大低減率）。</summary>
            public readonly float mortalityReduction;

            public Params(float spendEfficiency, float productivityBonus, float mortalityReduction)
            {
                this.spendEfficiency = spendEfficiency;
                this.productivityBonus = productivityBonus;
                this.mortalityReduction = mortalityReduction;
            }

            /// <summary>既定パラメータ。</summary>
            public static Params Default => new Params(1f, 0.5f, 0.6f);
        }

        /// <summary>
        /// 健康水準（0..1）。支出が増えるほど 1 へ逓減的に近づく。
        /// clamp01(1 - exp(-保健支出 * 効率))。支出 0 で 0。
        /// </summary>
        public static float HealthLevel(float healthSpendPerCapita, Params p)
        {
            float spend = Mathf.Max(0f, healthSpendPerCapita); // 負の支出は 0 扱い
            float level = 1f - Mathf.Exp(-spend * p.spendEfficiency);
            return Mathf.Clamp01(level);
        }

        /// <summary>既定パラメータ版。</summary>
        public static float HealthLevel(float healthSpendPerCapita)
            => HealthLevel(healthSpendPerCapita, Params.Default);

        /// <summary>
        /// 生産性倍率。健康水準が高いほど生産性が上がる（>= 1）。
        /// 1 + clamp01(health01) * 生産性ボーナス。
        /// </summary>
        public static float ProductivityFactor(float health01, Params p)
            => 1f + Mathf.Clamp01(health01) * p.productivityBonus;

        /// <summary>既定パラメータ版。</summary>
        public static float ProductivityFactor(float health01)
            => ProductivityFactor(health01, Params.Default);

        /// <summary>
        /// 死亡率倍率。健康水準が高いほど死亡率が下がる（&lt;= 1）。
        /// clamp01(1 - clamp01(health01) * 死亡率低減)。
        /// </summary>
        public static float MortalityFactor(float health01, Params p)
            => Mathf.Clamp01(1f - Mathf.Clamp01(health01) * p.mortalityReduction);

        /// <summary>既定パラメータ版。</summary>
        public static float MortalityFactor(float health01)
            => MortalityFactor(health01, Params.Default);
    }
}
