using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 物流3PL（サードパーティ・ロジスティクス）のロジック（業種細分化・倉庫運輸 #2024 の一括受託サブ業種・#2025・純ロジック・唯一の窓口）：
    /// 荷主の物流を丸ごと受託する契約物流収入（3PL-1）／庫内オペレーションコスト（3PL-2）／自前資産を持たないアセットライト・マージン（3PL-3）／利益（3PL-4）。
    /// 倉庫（#2024）・陸運（#2024）を組み合わせ荷主の物流を一括代行＝資産を持たず運営力で稼ぐ。各業種の物流アウトソース先。マクロ近似。test-first。
    /// </summary>
    public static class ThirdPartyLogisticsRules
    {
        /// <summary>契約物流収入＝取扱物量×単価（荷主の物流を丸ごと受託＝ストック型の受託収入）。</summary>
        public static float ContractLogisticsRevenue(float handledVolume, float ratePerUnit)
            => Mathf.Max(0f, handledVolume) * Mathf.Max(0f, ratePerUnit);

        /// <summary>庫内オペコスト＝作業時間×時給+設備費（ピッキング・仕分けの人件費が主体）。</summary>
        public static float WarehouseOpsCost(float laborHours, float wageRate, float equipmentCost)
            => Mathf.Max(0f, laborHours) * Mathf.Max(0f, wageRate) + Mathf.Max(0f, equipmentCost);

        /// <summary>アセットライト・マージン＝(収益−運営費)/収益（自前倉庫を持たず受託で稼ぐ身軽さ）。収益0以下は0。</summary>
        public static float AssetLightMargin(float revenue, float operatingCost)
            => revenue <= 0f ? 0f : (revenue - Mathf.Max(0f, operatingCost)) / revenue;

        /// <summary>3PL利益＝契約収益−庫内オペコスト−固定費。</summary>
        public static float LogisticsProfit(float revenue, float opsCost, float fixedCost)
            => revenue - Mathf.Max(0f, opsCost) - Mathf.Max(0f, fixedCost);
    }
}
