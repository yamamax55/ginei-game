using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// サービス会社のロジック（東証33業種「サービス業」・#2024・純ロジック・唯一の窓口）：労働集約＝人の稼働で稼ぐ：稼働時間と
    /// サービス収益（SVC-1）／人件費が主コストの利益（SVC-2）／稼働率（SVC-3）。人件費は俸給（#1969）、労働力は職業（#110）へ
    /// 接続。マクロ近似（設備でなく人が資産）。test-first。
    /// </summary>
    public static class ServiceRules
    {
        /// <summary>稼働時間＝従業員数×1人あたり総時間×稼働率（売上を生む billable な時間）。</summary>
        public static float BillableHours(float staff, float hoursPerStaff, float utilization)
            => Mathf.Max(0f, staff) * Mathf.Max(0f, hoursPerStaff) * Mathf.Clamp01(utilization);

        /// <summary>サービス収益＝稼働時間×時間単価。</summary>
        public static float ServiceRevenue(float billableHours, float hourlyRate)
            => Mathf.Max(0f, billableHours) * Mathf.Max(0f, hourlyRate);

        /// <summary>サービス利益＝収益−人件費−その他費用（労働集約＝人件費が主コスト）。</summary>
        public static float LaborProfit(float revenue, float laborCost, float otherCost)
            => revenue - Mathf.Max(0f, laborCost) - Mathf.Max(0f, otherCost);

        /// <summary>稼働率＝稼働時間/利用可能時間（人を遊ばせないことが収益の鍵）。利用可能0以下は0。</summary>
        public static float UtilizationRate(float billableHours, float availableHours)
            => availableHours <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, billableHours) / availableHours);
    }
}
