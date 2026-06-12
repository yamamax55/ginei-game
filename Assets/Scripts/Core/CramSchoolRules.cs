using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 教育・学習塾のロジック（業種細分化・サービス #2024 の教育サブ業種・#2025・純ロジック・唯一の窓口）：月謝収入（EDU-1）／
    /// 講師人件費（EDU-2）／生徒講師比（EDU-3）／利益（EDU-4）。
    /// 生徒数×月謝で稼ぎ講師人件費が主コスト＝労働集約。教育チェーン（小学校〜大学）とは別の私教育＝人材の素質（#155-157）を底上げしうる民間サービス。マクロ近似。test-first。
    /// </summary>
    public static class CramSchoolRules
    {
        /// <summary>月謝収入＝生徒数×月謝（生徒の頭数がそのまま収入）。</summary>
        public static float TuitionRevenue(int students, float monthlyFee)
            => Mathf.Max(0, students) * Mathf.Max(0f, monthlyFee);

        /// <summary>講師人件費＝講師数×給与（労働集約＝最大の費目）。</summary>
        public static float InstructorCost(int instructors, float salary)
            => Mathf.Max(0, instructors) * Mathf.Max(0f, salary);

        /// <summary>生徒講師比＝生徒数/講師数（高いほど効率的だが質は下がる＝少人数制との綱引き）。講師0以下は0。</summary>
        public static float StudentToInstructorRatio(int students, int instructors)
            => instructors <= 0 ? 0f : (float)Mathf.Max(0, students) / instructors;

        /// <summary>学習塾利益＝月謝収入−講師人件費−固定費（教室の家賃）。</summary>
        public static float CramSchoolProfit(float tuitionRevenue, float instructorCost, float fixedCost)
            => tuitionRevenue - Mathf.Max(0f, instructorCost) - Mathf.Max(0f, fixedCost);
    }
}
