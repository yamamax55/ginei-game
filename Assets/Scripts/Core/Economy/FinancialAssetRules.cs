using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 金融資産の評価・収益の純ロジック（NFIN-3・#2070・実効値パターン）。
    /// 時価＝口数×単価、収益＝口数×（配当/クーポン/分配）。原資産#185/#161/#2003 から時価・配当を同期し、
    /// 価格が0に張り付けば<b>紙くず化</b>（配当も0）。含み損益＝時価−取得原価。test-first。
    /// </summary>
    public static class FinancialAssetRules
    {
        /// <summary>時価＝口数×単価（負はクランプ）。</summary>
        public static float MarketValue(FinancialHolding h)
            => h == null ? 0f : Mathf.Max(0f, h.units) * Mathf.Max(0f, h.unitPrice);

        /// <summary>年間収益＝口数×1口収益（配当金/クーポン/分配金）。紙くずは incomePerUnit=0 ゆえ0。</summary>
        public static float AnnualIncome(FinancialHolding h)
            => h == null ? 0f : Mathf.Max(0f, h.units) * Mathf.Max(0f, h.incomePerUnit);

        /// <summary>利回り＝1口収益/単価（単価0は0）。</summary>
        public static float Yield(FinancialHolding h)
            => (h == null || h.unitPrice <= 0f) ? 0f : Mathf.Max(0f, h.incomePerUnit) / h.unitPrice;

        /// <summary>含み損益＝時価−取得原価（マイナス=含み損）。</summary>
        public static float UnrealizedPnL(FinancialHolding h)
            => h == null ? 0f : MarketValue(h) - h.bookCost;

        /// <summary>紙くずか＝単価が0以下（暴落#185 で価値消失）。</summary>
        public static bool IsWorthless(FinancialHolding h)
            => h != null && h.unitPrice <= 0f;

        /// <summary>
        /// 原資産から時価・収益を同期（時価評価）。単価0以下なら紙くず化＝単価/収益とも0に張り付ける。
        /// </summary>
        public static void MarkToMarket(FinancialHolding h, float unitPrice, float incomePerUnit)
        {
            if (h == null) return;
            if (unitPrice <= 0f)
            {
                h.unitPrice = 0f;
                h.incomePerUnit = 0f; // 紙くず＝配当も止まる
                return;
            }
            h.unitPrice = unitPrice;
            h.incomePerUnit = Mathf.Max(0f, incomePerUnit);
        }
    }
}
