using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 葬祭・冠婚（儀礼サービス）のロジック（業種細分化・サービス #2024 の儀礼サブ業種・#2025・純ロジック・唯一の窓口）：高単価低頻度の施行収入（CER-1）／
    /// 互助会の前受金（CER-2＝積立で需要を囲い込む）／高粗利率（CER-3）／利益（CER-4）。
    /// 一生に数度の高単価・低頻度サービス＝互助会の前受金（負債だが資金繰りを支える）で顧客を囲い込み高粗利で稼ぐ。人口動態（#153・高齢化）に連動。マクロ近似。test-first。
    /// </summary>
    public static class CeremonyRules
    {
        /// <summary>施行収入＝施行件数×平均単価（葬儀・婚礼など一件あたりの単価が高い）。</summary>
        public static float CeremonyRevenue(int ceremonies, float avgUnitPrice)
            => Mathf.Max(0, ceremonies) * Mathf.Max(0f, avgUnitPrice);

        /// <summary>互助会前受金＝会員数×月掛金×積立月数（将来の施行を前払いで囲い込む＝負債だが資金源）。</summary>
        public static float PrepaidDeposits(int members, float monthlyDeposit, int months)
            => Mathf.Max(0, members) * Mathf.Max(0f, monthlyDeposit) * Mathf.Max(0, months);

        /// <summary>高粗利率＝(単価−直接原価)/単価（儀礼は付加価値が高く高粗利）。単価0以下は0。</summary>
        public static float HighMarginRate(float unitPrice, float directCost)
            => unitPrice <= 0f ? 0f : (unitPrice - Mathf.Max(0f, directCost)) / unitPrice;

        /// <summary>儀礼サービス利益＝施行収入−直接原価−固定費（式場の固定費を高単価で回収）。</summary>
        public static float CeremonyProfit(float revenue, float directCost, float fixedCost)
            => revenue - Mathf.Max(0f, directCost) - Mathf.Max(0f, fixedCost);
    }
}
