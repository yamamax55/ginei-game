using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// SIer・ソフトウェア受託開発のロジック（業種細分化・情報通信 #2024 の受託サブ業種・#2025・純ロジック・唯一の窓口）：工数×単価の受託収入（SI-1）／
    /// 技術者の稼働率（SI-2）／工数超過の赤字（SI-3＝見積りを外すと赤字案件）／利益（SI-4）。
    /// 人月（工数）×単価で請け負う労働集約＝技術者を遊ばせない稼働率が命、見積り工数を超えると人件費だけ膨らみ赤字。SaaS#2025（自社製品）と対の受託モデル。マクロ近似。test-first。
    /// </summary>
    public static class SystemIntegratorRules
    {
        /// <summary>受託収入＝人月（工数）×人月単価（規模を人月で見積り、単価を掛けて請負額を出す）。</summary>
        public static float ProjectRevenue(float manMonths, float unitPrice)
            => Mathf.Max(0f, manMonths) * Mathf.Max(0f, unitPrice);

        /// <summary>稼働率＝稼働工数/総稼働可能工数（技術者を遊ばせないほど効率的）。総工数0以下は0。</summary>
        public static float Utilization(float billableHours, float availableHours)
            => availableHours <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, billableHours) / availableHours);

        /// <summary>工数超過の赤字＝max(0, 実工数−見積工数)×人月原価（見積りを外すと人件費だけ膨らむデスマーチ）。</summary>
        public static float OverrunLoss(float plannedManMonths, float actualManMonths, float costPerManMonth)
            => Mathf.Max(0f, actualManMonths - plannedManMonths) * Mathf.Max(0f, costPerManMonth);

        /// <summary>SIer利益＝受託収入−人件費−固定費。</summary>
        public static float SiProfit(float revenue, float laborCost, float fixedCost)
            => revenue - Mathf.Max(0f, laborCost) - Mathf.Max(0f, fixedCost);
    }
}
