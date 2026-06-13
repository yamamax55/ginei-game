using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 食品スーパーのロジック（業種細分化・小売 #2017 の生鮮サブ業種・#2025・純ロジック・唯一の窓口）：生鮮の鮮度ロス（SPMK-1）／
    /// 生鮮×加工食品の混合粗利（SPMK-2）／日商＝客数×客単価（SPMK-3）／利益（SPMK-4）。
    /// 鮮度の落ちる生鮮（食料品#2024）を扱い廃棄ロスと隣り合わせ＝低粗利・高回転・地域密着。コンビニ#2025より客単価が高く来店頻度で稼ぐ。マクロ近似。test-first。
    /// </summary>
    public static class SupermarketRules
    {
        /// <summary>生鮮の鮮度ロス＝(仕入数−販売数)×原価（売れ残った生鮮は鮮度が落ち廃棄＝発注精度が利益を左右）。非負。</summary>
        public static float FreshFoodLoss(float stockedUnits, float soldUnits, float costPerUnit)
            => Mathf.Max(0f, Mathf.Max(0f, stockedUnits) - Mathf.Max(0f, soldUnits)) * Mathf.Max(0f, costPerUnit);

        /// <summary>混合粗利率＝生鮮比率×生鮮粗利+(1−生鮮比率)×加工食品粗利（生鮮は集客の低粗利・加工食品で稼ぐ）。</summary>
        public static float BlendedMargin(float freshShare, float freshMargin, float groceryMargin)
        {
            float fresh = Mathf.Clamp01(freshShare);
            return fresh * Mathf.Clamp01(freshMargin) + (1f - fresh) * Mathf.Clamp01(groceryMargin);
        }

        /// <summary>日商＝客数×客単価（高回転・地域密着＝来店頻度が命）。</summary>
        public static float DailySales(int customers, float avgBasket)
            => Mathf.Max(0, customers) * Mathf.Max(0f, avgBasket);

        /// <summary>食品スーパー利益＝粗利−鮮度ロス−固定費。</summary>
        public static float SupermarketProfit(float grossProfit, float freshLoss, float fixedCost)
            => grossProfit - Mathf.Max(0f, freshLoss) - Mathf.Max(0f, fixedCost);
    }
}
