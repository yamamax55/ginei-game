using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 出版社のロジック（業種細分化・その他製品 #2024 ／情報通信の出版サブ業種・#2025・純ロジック・唯一の窓口）：書籍売上（PUB-1）／
    /// 著者印税（PUB-2）／返品損（PUB-3＝委託販売の返品リスク）／利益（PUB-4）。
    /// 委託販売ゆえ刷ったぶんが売れず返品される損失を抱える＝印税（著者へ）と印刷費が原価。電子化（紙#2024の需要減）と対の知のコンテンツ業。マクロ近似。test-first。
    /// </summary>
    public static class PublishingRules
    {
        /// <summary>書籍売上＝販売部数×定価。</summary>
        public static float BookSalesRevenue(int copies, float price)
            => Mathf.Max(0, copies) * Mathf.Max(0f, price);

        /// <summary>著者印税＝売上×印税率（著者への支払い＝コンテンツの源泉コスト）。</summary>
        public static float AuthorRoyalty(float salesRevenue, float royaltyRate)
            => Mathf.Max(0f, salesRevenue) * Mathf.Clamp01(royaltyRate);

        /// <summary>返品損＝出荷部数×返品率×製造原価（委託販売＝売れずに戻る本の損失）。</summary>
        public static float ReturnLoss(int shippedCopies, float returnRate, float unitCost)
            => Mathf.Max(0, shippedCopies) * Mathf.Clamp01(returnRate) * Mathf.Max(0f, unitCost);

        /// <summary>出版利益＝売上−印税−製造原価−固定費。</summary>
        public static float PublishingProfit(float salesRevenue, float royalty, float productionCost, float fixedCost)
            => salesRevenue - Mathf.Max(0f, royalty) - Mathf.Max(0f, productionCost) - Mathf.Max(0f, fixedCost);
    }
}
