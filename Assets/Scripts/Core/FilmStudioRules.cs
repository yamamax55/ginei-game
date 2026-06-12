using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 映画製作スタジオのロジック（業種細分化・情報通信 #2024 のコンテンツサブ業種・#2025・純ロジック・唯一の窓口）：興行収入の配分（FILM-1）／
    /// 旧作ライブラリのライセンス収入（FILM-2＝過去作が稼ぎ続ける資産）／製作費の予算超過（FILM-3）／利益（FILM-4）。
    /// 興行収入を劇場と分け合い、製作費の博打（超過リスク）を負うが、旧作ライブラリは長く稼ぐ資産＝IP（玩具#2025）の供給源。マクロ近似。test-first。
    /// </summary>
    public static class FilmStudioRules
    {
        /// <summary>興行配分＝総興行収入×スタジオ取り分率（残りは劇場の取り分＝ウィンドウで配分が変わる）。</summary>
        public static float BoxOfficeShare(float grossBoxOffice, float studioShareRate)
            => Mathf.Max(0f, grossBoxOffice) * Mathf.Clamp01(studioShareRate);

        /// <summary>ライブラリ・ライセンス収入＝作品数×平均ライセンス料（旧作を配信#2025/放送#2025へ貸す資産収入）。</summary>
        public static float LibraryLicensingRevenue(int titles, float avgLicenseFee)
            => Mathf.Max(0, titles) * Mathf.Max(0f, avgLicenseFee);

        /// <summary>予算超過＝max(0, 実支出−予算)（製作費の博打＝超過は利益を食う）。</summary>
        public static float BudgetOverrun(float plannedBudget, float actualSpend)
            => Mathf.Max(0f, actualSpend - plannedBudget);

        /// <summary>映画スタジオ利益＝興行配分+ライセンス収入−製作費−宣伝費（宣伝費が興行を左右する先行投資）。</summary>
        public static float FilmStudioProfit(float boxOfficeRevenue, float licensingRevenue, float productionCost, float marketingCost)
            => boxOfficeRevenue + Mathf.Max(0f, licensingRevenue) - Mathf.Max(0f, productionCost) - Mathf.Max(0f, marketingCost);
    }
}
