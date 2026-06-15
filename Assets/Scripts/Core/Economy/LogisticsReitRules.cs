using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 物流不動産REIT（物流施設特化型）のロジック（業種細分化・REIT #2025 ×物流3PL #2025 のサブ業種・#2025・純ロジック・唯一の窓口）：
    /// 大型物流施設の賃料（LREIT-1）／ビルド・トゥ・スーツの開発利回り（LREIT-2）／テナント集中リスク（LREIT-3）／利益（LREIT-4）。
    /// EC（#2025）・3PL（#2025）を借り手に大型倉庫を貸す＝REIT#2025の物流特化版（少数の大口テナントに依存＝集中リスクが住宅系と異なる）。マクロ近似。test-first。
    /// </summary>
    public static class LogisticsReitRules
    {
        /// <summary>施設賃料＝延床面積×坪賃料×稼働率（大型倉庫の賃料収入）。</summary>
        public static float FacilityRent(float floorArea, float rentPerArea, float occupancy)
            => Mathf.Max(0f, floorArea) * Mathf.Max(0f, rentPerArea) * Mathf.Clamp01(occupancy);

        /// <summary>ビルド・トゥ・スーツ開発利回り＝年間賃料/開発コスト（テナント専用に建て長期賃貸＝安定利回り）。コスト0以下は0。</summary>
        public static float BuildToSuitYield(float annualRent, float developmentCost)
            => developmentCost <= 0f ? 0f : Mathf.Max(0f, annualRent) / developmentCost;

        /// <summary>テナント集中リスク＝最大テナント賃料/総賃料（1社のEC/3PLに依存するほど退去で空室が痛い）。総賃料0以下は0。</summary>
        public static float TenantConcentrationRisk(float largestTenantRent, float totalRent)
            => totalRent <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, largestTenantRent) / totalRent);

        /// <summary>物流REIT利益＝賃料収入−運営費−支払利息（借入で大型施設を建てるほど金利に敏感）。</summary>
        public static float LogisticsReitProfit(float rentIncome, float operatingCost, float interestExpense)
            => rentIncome - Mathf.Max(0f, operatingCost) - Mathf.Max(0f, interestExpense);
    }
}
