using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 人物の財産の暦境界オーケストレータ（PFIN-6・#2056 配線・純ロジック）。
    /// 人物（<see cref="Person"/>）の財産を1年ぶん回す＝俸給#1969→消費（階級#14 期待・PFIN-2）→可処分→特性で配分（PFIN-3）→財産更新（PFIN-4）。
    /// <b>GalaxyView の年次Tick から呼ぶ薄い窓口</b>＝判定は各 PFIN ルールへ委譲（<see cref="PopConsumptionTickRules"/> と同型）。人物粒度・暦境界Tick・後方互換。test-first。
    /// </summary>
    public static class PersonFinanceTickRules
    {
        public const float DefaultBaseNeed = 80f; // 基準の年間消費需要（階級で増える）

        /// <summary>
        /// 1年ぶんの財産更新。俸給と投資リターン率を受け取り、消費・配分・財産更新を回す（<see cref="Person.wealth"/> を更新）。
        /// 投資リターン率は呼び側が資本#917/暴落#185 から渡す（投資型は変動が大きい）。
        /// </summary>
        public static void TickYear(Person p, float salary, float investReturnRate, float baseNeed)
        {
            if (p == null) return;
            float need = PersonDemandRules.ConsumptionNeed(p.rankTier, baseNeed);
            float disposable = PersonWealthRules.DisposableIncome(salary, need);
            p.wealth = PersonWealthRules.WealthAfterYear(p.wealth, disposable, p.financialTrait, investReturnRate);
        }

        /// <summary>既定の基準需要での1年Tick。</summary>
        public static void TickYear(Person p, float salary, float investReturnRate)
            => TickYear(p, salary, investReturnRate, DefaultBaseNeed);

        public const float MonthsPerYear = 12f;

        /// <summary>
        /// 1ヶ月ぶんの財産更新＝<b>俸給の月払い</b>。月俸（年俸÷12 相当）と月次の投資リターン率を受け取り、
        /// 月割りの消費（年間需要÷12）を引いて可処分を特性で配分し財産を更新する。<paramref name="baseNeed"/> は
        /// 年間基準需要（内部で月割り）。年次 <see cref="TickYear"/> と同じ式を月粒度で回すだけ（合計は年次と整合）。
        /// </summary>
        public static void TickMonth(Person p, float monthlySalary, float investReturnRate, float baseNeed)
        {
            if (p == null) return;
            float monthlyNeed = PersonDemandRules.ConsumptionNeed(p.rankTier, baseNeed) / MonthsPerYear;
            float disposable = PersonWealthRules.DisposableIncome(monthlySalary, monthlyNeed);
            p.wealth = PersonWealthRules.WealthAfter(p.wealth, disposable, p.financialTrait, investReturnRate);
        }

        /// <summary>既定の基準需要での1ヶ月Tick。</summary>
        public static void TickMonth(Person p, float monthlySalary, float investReturnRate)
            => TickMonth(p, monthlySalary, investReturnRate, DefaultBaseNeed);
    }
}
