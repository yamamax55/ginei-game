using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 医療・介護のロジック（業種細分化・サービス #2024 の公定価格サブ業種・#2025・純ロジック・唯一の窓口）：公定価格収益＝サービス量×公定単価（HEAL-1）／
    /// 病床稼働率（HEAL-2）／人手不足が稼働を制約（HEAL-3）／利益（HEAL-4）。
    /// 自由に値付けできず診療報酬・介護報酬という公定価格で収益が決まる＝人手不足（労働市場#1957・人口オーナス#153）が供給を縛る。マクロ近似。test-first。
    /// </summary>
    public static class HealthcareRules
    {
        /// <summary>公定価格収益＝サービス提供量×公定単価（診療報酬/介護報酬＝価格は国が決める）。</summary>
        public static float RegulatedRevenue(float serviceUnits, float officialUnitPrice)
            => Mathf.Max(0f, serviceUnits) * Mathf.Max(0f, officialUnitPrice);

        /// <summary>病床/施設稼働率＝利用中/総定員（埋めるほど固定費を回収）。総定員0以下は0。</summary>
        public static float OccupancyRate(float occupied, float total)
            => total <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, occupied) / total);

        /// <summary>人手不足係数＝min(1, 確保人員/必要人員)（人が足りないと定員があっても受け入れられない＝供給制約）。必要0以下は1。</summary>
        public static float StaffShortageFactor(float requiredStaff, float availableStaff)
            => requiredStaff <= 0f ? 1f : Mathf.Clamp01(Mathf.Max(0f, availableStaff) / requiredStaff);

        /// <summary>医療・介護利益＝公定収益−人件費−固定費（公定価格ゆえ人件費高騰を価格転嫁できず圧迫）。</summary>
        public static float HealthcareProfit(float revenue, float laborCost, float fixedCost)
            => revenue - Mathf.Max(0f, laborCost) - Mathf.Max(0f, fixedCost);
    }
}
