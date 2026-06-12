using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// POP要求物資の暦境界オーケストレータ（POPDEM-6・#2042 配線・純ロジック）。
    /// 惑星（<see cref="Province"/>）の消費需要を1期ぶん回す＝購買力#1969×人口#153 で需要（POPDEM-2）→供給（生産#93/補給#94/市場#179）と突合せ（POPDEM-3）
    /// →マズロー階層#403 で生活水準#181（POPDEM-4）→`Province.livingStandard`/`foodShortage` に保持。
    /// <b>GalaxyView の年次Tick から呼ぶ薄い窓口</b>＝重い判定は各 POPDEM ルールへ委譲（<see cref="PopLaborTickRules"/> と同型）。集約・暦境界Tick・後方互換。test-first。
    /// </summary>
    public static class PopConsumptionTickRules
    {
        /// <summary>
        /// 1期ぶんの消費需要処理。供給はカテゴリ別に渡す（呼び側が生産#93/在庫/補給#94/市場#179 から算出）。
        /// 必需不足は飢餓（<see cref="Province.foodShortage"/>）、充足は生活水準（<see cref="Province.livingStandard"/>）に反映。
        /// </summary>
        public static void TickYear(Province p, float purchasingPower, float necessitySupply, float comfortSupply, float luxurySupply)
        {
            if (p == null) return;
            float pop = (p.demographics != null) ? p.demographics.Total : p.population;
            float necD = ConsumptionDemandRules.TotalDemand(pop, ConsumptionCategory.必需, purchasingPower);
            float comD = ConsumptionDemandRules.TotalDemand(pop, ConsumptionCategory.快適, purchasingPower);
            float luxD = ConsumptionDemandRules.TotalDemand(pop, ConsumptionCategory.奢侈, purchasingPower);

            float necF = ConsumptionFulfillmentRules.Fulfillment(necessitySupply, necD);
            float comF = ConsumptionFulfillmentRules.Fulfillment(comfortSupply, comD);
            float luxF = ConsumptionFulfillmentRules.Fulfillment(luxurySupply, luxD);

            p.livingStandard = ConsumptionWelfareRules.LivingStandard(necF, comF, luxF);
            p.foodShortage = ConsumptionFulfillmentRules.FamineSeverity(necF);
        }
    }
}
