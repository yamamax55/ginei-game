using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 化粧品会社のロジック（業種細分化・化学 #2024 のブランド消費財サブ業種・#2025・純ロジック・唯一の窓口）：ブランド価格＝原価×(1+ブランドプレミアム)（COSM-1）／
    /// 高粗利率（COSM-2）／広告が需要を生む（COSM-3）／広告主費の利益（COSM-4）。
    /// 原価は安いがブランド・広告宣伝費が主コスト＝中身でなくイメージを売る高粗利・広告依存。化学#2024の川下。マクロ近似。test-first。
    /// </summary>
    public static class CosmeticsRules
    {
        /// <summary>ブランド価格＝原価×(1+ブランドプレミアム)（中身の原価より遥かに高く売る）。</summary>
        public static float BrandedPrice(float baseCost, float brandPremiumRate)
            => Mathf.Max(0f, baseCost) * (1f + Mathf.Max(0f, brandPremiumRate));

        /// <summary>粗利率＝(価格−原価)/価格（化粧品は極めて高い＝原価が安い）。価格0以下は0。</summary>
        public static float GrossMarginRate(float price, float cogs)
            => price <= 0f ? 0f : (price - Mathf.Max(0f, cogs)) / price;

        /// <summary>広告が生む需要＝基礎需要+広告費×広告効果（広告を打つほど売れる＝広告依存）。</summary>
        public static float AdDrivenDemand(float baseDemand, float adSpend, float adEffectiveness)
            => Mathf.Max(0f, baseDemand) + Mathf.Max(0f, adSpend) * Mathf.Max(0f, adEffectiveness);

        /// <summary>化粧品利益＝売上−原価−広告宣伝費−固定費（広告費が最大の費目）。</summary>
        public static float CosmeticsProfit(float sales, float cogs, float adSpend, float fixedCost)
            => sales - Mathf.Max(0f, cogs) - Mathf.Max(0f, adSpend) - Mathf.Max(0f, fixedCost);
    }
}
