using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// アパレルSPA（製造小売）のロジック（業種細分化・小売 #2017 ×繊維 #2024 の製販一体サブ業種・#2025・純ロジック・唯一の窓口）：在庫消化率（APRL-1）／
    /// 売れ残りの値引き処分損（APRL-2）／製販一体で中間マージンを取り込む垂直統合マージン（APRL-3）／利益（APRL-4）。
    /// 企画・製造・販売を一気通貫（SPA）＝中間マージンを取り込み高粗利だが、流行（繊維#2024の季節需要）を外すと在庫が値引き損に化ける。マクロ近似。test-first。
    /// </summary>
    public static class ApparelSpaRules
    {
        /// <summary>在庫消化率＝販売数/生産数（売り切るほど良い＝アパレルの生命線）。生産数0以下は0。</summary>
        public static float SellThroughRate(float soldUnits, float producedUnits)
            => producedUnits <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, soldUnits) / producedUnits);

        /// <summary>値引き処分損＝売れ残り数×原価×値引き深度（流行を外した在庫はシーズン末に投げ売り）。</summary>
        public static float MarkdownLoss(float unsoldUnits, float costPerUnit, float markdownDepth)
            => Mathf.Max(0f, unsoldUnits) * Mathf.Max(0f, costPerUnit) * Mathf.Clamp01(markdownDepth);

        /// <summary>垂直統合マージン＝(小売価格−製造原価)/小売価格（製販一体で卸の中間マージンも取り込む＝高粗利）。価格0以下は0。</summary>
        public static float VerticalMargin(float retailPrice, float manufacturingCost)
            => retailPrice <= 0f ? 0f : (retailPrice - Mathf.Max(0f, manufacturingCost)) / retailPrice;

        /// <summary>アパレル利益＝売上−原価−値引き損−固定費。</summary>
        public static float ApparelProfit(float sales, float cogs, float markdownLoss, float fixedCost)
            => sales - Mathf.Max(0f, cogs) - Mathf.Max(0f, markdownLoss) - Mathf.Max(0f, fixedCost);
    }
}
