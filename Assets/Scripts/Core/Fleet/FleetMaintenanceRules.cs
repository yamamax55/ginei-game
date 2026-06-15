using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 艦艇整備・補給廠のロジック（業種細分化・輸送用機器 #2024 ／サービスの整備MROサブ業種を本作設定へ・#2025・純ロジック・唯一の窓口）：整備受託収入（MRO-1）／
    /// 整備回転能力（MRO-2＝船渠数×回転）／部品マークアップ収入（MRO-3）／利益（MRO-4）。
    /// 造船（#2024）が新造なら、こちらは既存艦の修理・整備で稼ぐ＝兵站（#92）と直結。船渠の回転能力が受託量を律速し、部品の上乗せが利益を厚くする。マクロ近似。test-first。
    /// </summary>
    public static class FleetMaintenanceRules
    {
        /// <summary>整備受託収入＝整備隻数×1隻あたり整備料。</summary>
        public static float MaintenanceRevenue(int shipsServiced, float serviceFeePerShip)
            => Mathf.Max(0, shipsServiced) * Mathf.Max(0f, serviceFeePerShip);

        /// <summary>整備回転能力＝船渠数×(期間/1隻あたり所要日数)（船渠をどれだけ回せるかが受託量の上限）。所要日数0以下は0。</summary>
        public static float TurnaroundCapacity(int docks, float daysPerJob, float periodDays)
            => daysPerJob <= 0f ? 0f : Mathf.Max(0, docks) * (Mathf.Max(0f, periodDays) / daysPerJob);

        /// <summary>部品マークアップ収入＝部品原価×マークアップ率（交換部品の上乗せ＝整備の利益の柱）。</summary>
        public static float PartsMarkupRevenue(float partsCost, float markupRate)
            => Mathf.Max(0f, partsCost) * Mathf.Max(0f, markupRate);

        /// <summary>補給廠利益＝整備収入+部品収入−工賃−固定費。</summary>
        public static float MroProfit(float serviceRevenue, float partsRevenue, float laborCost, float fixedCost)
            => serviceRevenue + Mathf.Max(0f, partsRevenue) - Mathf.Max(0f, laborCost) - Mathf.Max(0f, fixedCost);
    }
}
