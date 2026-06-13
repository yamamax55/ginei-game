using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 軍要求物資（#2049）：原単位(MILSUP-1)/総需要(MILSUP-2)/充足(MILSUP-3)/レディネス(MILSUP-4)/物流(MILSUP-5)/Tick(MILSUP-6)。
    /// </summary>
    public class MilitarySupplyTests
    {
        // --- MILSUP-1 原単位（活動別） ---
        [Test]
        public void Upkeep_ActivityDriven()
        {
            Assert.AreEqual(0.50f, MilitarySupplyRules.UpkeepRate(ResourceType.弾薬, MilitaryActivity.交戦), 1e-4f); // 交戦=弾薬激増
            Assert.AreEqual(0.30f, MilitarySupplyRules.UpkeepRate(ResourceType.燃料, MilitaryActivity.移動), 1e-4f); // 移動=燃料
            Assert.AreEqual(0.10f, MilitarySupplyRules.UpkeepRate(ResourceType.物資, MilitaryActivity.待機), 1e-4f); // 常時=糧食
            Assert.AreEqual(50f, MilitarySupplyRules.Upkeep(100f, ResourceType.弾薬, MilitaryActivity.交戦), 1e-2f);
            Assert.AreEqual(2f, MilitarySupplyRules.Upkeep(100f, ResourceType.弾薬, MilitaryActivity.待機), 1e-2f); // 待機は弾薬ほぼ不要
        }

        // --- MILSUP-2 部隊の総需要・梯団集約 ---
        [Test]
        public void Demand_FleetAndAggregate()
        {
            var engaged = new StrategicFleet(1, 2, Faction.帝国) { strength = 100, engaged = true };
            var idle = new StrategicFleet(2, 3, Faction.帝国) { strength = 100 };
            Assert.AreEqual(MilitaryActivity.交戦, MilitaryDemandRules.ActivityOf(engaged));
            Assert.AreEqual(MilitaryActivity.待機, MilitaryDemandRules.ActivityOf(idle));
            Assert.AreEqual(50f, MilitaryDemandRules.FleetDemand(engaged, ResourceType.弾薬), 1e-2f); // 交戦で弾薬需要大
            Assert.AreEqual(2f, MilitaryDemandRules.FleetDemand(idle, ResourceType.弾薬), 1e-2f);
            var fleets = new List<StrategicFleet> { engaged, idle };
            Assert.AreEqual(52f, MilitaryDemandRules.AggregateDemand(fleets, ResourceType.弾薬), 1e-2f); // 梯団集約
        }

        // --- MILSUP-3 充足・欠乏・損耗 ---
        [Test]
        public void Fulfillment_ShortageAttrition()
        {
            Assert.AreEqual(0.8f, MilitarySupplyFulfillmentRules.Fulfillment(80f, 100f), 1e-4f);
            Assert.AreEqual(20f, MilitarySupplyFulfillmentRules.Shortage(80f, 100f), 1e-2f);
            Assert.AreEqual(50f, MilitarySupplyFulfillmentRules.Consume(50f, 100f), 1e-2f);
            Assert.AreEqual(2.5f, MilitarySupplyFulfillmentRules.AttritionFromShortage(100f, 0.5f, 0.05f), 1e-4f); // 補給5割で損耗
        }

        // --- MILSUP-4 戦闘力・機動・継戦 ---
        [Test]
        public void Readiness_FirepowerMobilitySustainment()
        {
            Assert.AreEqual(0.1f, MilitaryReadinessRules.FirepowerFactor(0f), 1e-4f);  // 弾切れで火力ほぼ0
            Assert.AreEqual(1.0f, MilitaryReadinessRules.FirepowerFactor(1f), 1e-4f);
            Assert.AreEqual(0.1f, MilitaryReadinessRules.MobilityFactor(0f), 1e-4f);    // 燃料切れで機動ほぼ0
            Assert.AreEqual(0.5f, MilitaryReadinessRules.SustainmentFactor(0f), 1e-4f); // 糧食切れでも即崩壊はしない
            Assert.AreEqual(0.5f, MilitaryReadinessRules.OverallReadiness(0.8f, 0.5f, 1.0f), 1e-4f); // 最も欠けた物資が律速
        }

        // --- MILSUP-5 補給線・通商破壊 ---
        [Test]
        public void Logistics_DeliveryRaidCutoff()
        {
            Assert.AreEqual(60f, MilitaryLogisticsRules.DeliveredSupply(100f, 80f, 60f), 1e-2f); // 補給線容量で律速
            Assert.AreEqual(30f, MilitaryLogisticsRules.RaidedSupply(60f, 0.5f), 1e-2f);         // 通商破壊で半減
            Assert.IsFalse(MilitaryLogisticsRules.IsCutOff(true, false));  // 所有回廊で到達・ZOCなし＝補給
            Assert.IsTrue(MilitaryLogisticsRules.IsCutOff(false, false));  // 到達不可＝切断
            Assert.IsTrue(MilitaryLogisticsRules.IsCutOff(true, true));    // ZOC遮断＝切断
            Assert.AreEqual(30f, MilitaryLogisticsRules.PrioritizeFront(100f, 30f), 1e-2f);      // 前線優先配分
        }

        // --- MILSUP-6 Tick（補給/補給切れ） ---
        [Test]
        public void Tick_SuppliedRecovers_CutOffAttrites()
        {
            var f = new StrategicFleet(1, 2, Faction.帝国) { strength = 100, supply = 0.5f };
            int lost = MilitarySupplyTickRules.TickFleet(f, true, 0.34f, 0.2f, 0.05f); // 補給で回復
            Assert.AreEqual(0.84f, f.supply, 1e-4f);
            Assert.AreEqual(0, lost);

            // 補給切れ＝枯渇＋損耗（滅びの時計）
            var g = new StrategicFleet(2, 3, Faction.帝国) { strength = 100, supply = 1.0f };
            int lost2 = MilitarySupplyTickRules.TickFleet(g, false, 0.34f, 0.2f, 0.05f);
            Assert.AreEqual(0.8f, g.supply, 1e-4f);             // 1.0→0.8
            Assert.AreEqual(1, lost2);                          // 100×0.05×0.2=1
            Assert.AreEqual(99, g.strength);

            // 切れ続けると補給が枯れ損耗が拡大
            for (int i = 0; i < 5; i++) MilitarySupplyTickRules.TickFleet(g, false);
            Assert.Less(g.supply, 0.2f);
            Assert.Less(g.strength, 99);
        }
    }
}
