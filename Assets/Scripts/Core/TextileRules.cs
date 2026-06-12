using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 繊維製品メーカーのロジック（東証33業種「繊維製品」・#2024・純ロジック・唯一の窓口）：ファッション需要の流行変動（TEX-1）／
    /// 季節商品の陳腐化＝値下げ処分ロス（TEX-2）／低マージン×新興国コスト競合（TEX-3）／利益（TEX-4）。労働集約。製造（#2016）・
    /// 小売（#2017）・消費（#1951）へ接続。マクロ近似。test-first。
    /// </summary>
    public static class TextileRules
    {
        /// <summary>季節/流行需要＝基準需要×トレンド係数（流行に乗れば増・外せば減＝ファッションの水もの）。非負。</summary>
        public static float SeasonalDemand(float baseDemand, float trendFactor)
            => Mathf.Max(0f, Mathf.Max(0f, baseDemand) * Mathf.Max(0f, trendFactor));

        /// <summary>陳腐化の値下げ損＝売れ残り×原価×値下げ率（季節を過ぎた在庫を投げ売る損）。</summary>
        public static float ObsolescenceMarkdownLoss(float unsold, float unitCost, float markdownRate)
            => Mathf.Max(0f, unsold) * Mathf.Max(0f, unitCost) * Mathf.Clamp01(markdownRate);

        /// <summary>低コスト国の脅威＝(自国コスト−輸入コスト)/自国コスト（プラスは価格劣位＝新興国に押される）。自国0以下は0。</summary>
        public static float LowCostCountryThreat(float domesticCost, float importCost)
            => domesticCost <= 0f ? 0f : Mathf.Max(0f, domesticCost - importCost) / domesticCost;

        /// <summary>繊維利益＝販売数×(価格−原価)。</summary>
        public static float TextileProfit(float units, float price, float cost)
            => Mathf.Max(0f, units) * (price - cost);
    }
}
