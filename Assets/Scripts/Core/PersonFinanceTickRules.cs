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
    }
}
