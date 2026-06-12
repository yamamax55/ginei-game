using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 可処分所得の配分と財産の蓄積・運用（PFIN-3/4・#2056・#917/#185 連携・純ロジック）。
    /// 俸給#1969−消費＝可処分を特性（<see cref="FinanceTraitRules"/>）で貯金/投資/浪費へ配分し、財産を更新。
    /// 投資は資本利潤率 r#917 で増えるが暴落#185 で毀損（負リターン）、貯金は堅実、浪費は残らない。test-first。
    /// </summary>
    public static class PersonWealthRules
    {
        /// <summary>可処分所得＝max(0, 俸給−消費)。</summary>
        public static float DisposableIncome(float salary, float consumption)
            => Mathf.Max(0f, salary - consumption);

        /// <summary>貯金額＝可処分×貯蓄率。</summary>
        public static float Saved(float disposable, FinancialTrait t)
            => Mathf.Max(0f, disposable) * FinanceTraitRules.SaveRate(t);

        /// <summary>投資額＝可処分×投資率。</summary>
        public static float Invested(float disposable, FinancialTrait t)
            => Mathf.Max(0f, disposable) * FinanceTraitRules.InvestRate(t);

        /// <summary>浪費額＝可処分×浪費率（追加消費＝生活水準/人望へ・財産には残らない）。</summary>
        public static float Spent(float disposable, FinancialTrait t)
            => Mathf.Max(0f, disposable) * FinanceTraitRules.SpendRate(t);

        /// <summary>投資収益＝投資額×リターン率（r#917・負＝暴落#185 の損失）。</summary>
        public static float InvestmentReturn(float invested, float returnRate)
            => Mathf.Max(0f, invested) * returnRate;

        /// <summary>
        /// 1年後の財産＝max(0, 財産 + 貯金額 + 投資額×(1+リターン率))。
        /// 貯金型は堅実に増え、投資型は好況で急増・暴落で毀損、浪費型はほぼ増えない（浪費分は財産に残らない）。
        /// </summary>
        public static float WealthAfterYear(float wealth, float disposable, FinancialTrait t, float investReturnRate)
        {
            float saved = Saved(disposable, t);
            float invested = Invested(disposable, t);
            float gain = saved + invested * (1f + investReturnRate);
            return Mathf.Max(0f, wealth + gain);
        }
    }
}
