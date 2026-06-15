using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// フィットネス・レジャー会員制クラブのロジック（業種細分化・サービス #2024 の会員制サブ業種・#2025・純ロジック・唯一の窓口）：会費収入（FIT-1）／
    /// 解約（チャーン・FIT-2）／施設稼働率（FIT-3＝幽霊会員が支える低稼働高採算）／利益（FIT-4）。
    /// 払うが来ない「幽霊会員」が低稼働でも採算を支える会員制サブスク＝解約率の管理が命（SaaS#2025と同型だが物理施設の固定費を持つ）。マクロ近似。test-first。
    /// </summary>
    public static class FitnessClubRules
    {
        /// <summary>会費収入＝会員数×月会費（来ても来なくても徴収できるサブスク収入）。</summary>
        public static float MembershipRevenue(int members, float monthlyFee)
            => Mathf.Max(0, members) * Mathf.Max(0f, monthlyFee);

        /// <summary>解約会員数＝会員数×解約率（チャーン＝穴の開いたバケツ＝新規獲得と綱引き）。</summary>
        public static float ChurnedMembers(int members, float churnRate)
            => Mathf.Max(0, members) * Mathf.Clamp01(churnRate);

        /// <summary>施設稼働率＝平均来館者/収容力（低稼働でも会費は入る＝幽霊会員が高採算を生む）。収容力0以下は0。</summary>
        public static float CapacityUtilization(float avgAttendance, float capacity)
            => capacity <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, avgAttendance) / capacity);

        /// <summary>フィットネス利益＝会費収入−来館変動費×来館数−固定費（来館が少ないほど変動費が浮く＝幽霊会員が利益源）。</summary>
        public static float FitnessProfit(float membershipRevenue, float variableCostPerVisit, float totalVisits, float fixedCost)
            => membershipRevenue - Mathf.Max(0f, variableCostPerVisit) * Mathf.Max(0f, totalVisits) - Mathf.Max(0f, fixedCost);
    }
}
