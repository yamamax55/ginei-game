using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 国家・惑星の物資需要と消費（#2077）：原単位(STATEDEM-1)/惑星需要(2)/国家需要(3)/充足(4)/効果(5)/Tick(6)。
    /// </summary>
    public class StateConsumptionTests
    {
        // --- STATEDEM-1 原単位 ---
        [Test]
        public void Admin_RatesPerCapita()
        {
            Assert.AreEqual(0.02f, AdministrationConsumptionRules.UpkeepRate(ResourceType.物資, AdminFunction.行政), 1e-4f);
            Assert.AreEqual(0.07f, AdministrationConsumptionRules.PerCapitaRate(ResourceType.物資), 1e-4f); // 0.02+0.03+0.02
            Assert.AreEqual(0f, AdministrationConsumptionRules.PerCapitaRate(ResourceType.弾薬), 1e-4f);    // 行政は弾薬を消費しない
            Assert.AreEqual(0.04f, AdministrationConsumptionRules.PerCapitaRate(ResourceType.燃料), 1e-4f); // 0.01+0.02+0.01
        }

        // --- STATEDEM-2 惑星需要 ---
        [Test]
        public void Planet_DemandScalesWithPopulation()
        {
            var p = new Province(1, "", 100f);
            Assert.AreEqual(7f, PlanetMaterialDemandRules.Demand(p, ResourceType.物資), 1e-3f);  // 100×0.07
            Assert.AreEqual(0f, PlanetMaterialDemandRules.Demand(p, ResourceType.弾薬), 1e-3f);
            Assert.AreEqual(4f, PlanetMaterialDemandRules.Demand(p, ResourceType.燃料), 1e-3f);  // 100×0.04
            Assert.AreEqual(11f, PlanetMaterialDemandRules.TotalDemand(p), 1e-3f);
        }

        // --- STATEDEM-3 国家需要（集約＋中央overhead） ---
        [Test]
        public void State_AggregatePlusCentralOverhead()
        {
            var provs = new List<Province> { new Province(1, "", 100f) };
            Assert.AreEqual(7f, StateMaterialDemandRules.AggregateDemand(provs, ResourceType.物資), 1e-3f);
            Assert.AreEqual(5f, StateMaterialDemandRules.CentralOverhead(1, ResourceType.物資), 1e-3f);
            Assert.AreEqual(12f, StateMaterialDemandRules.TotalStateDemand(provs, 1, ResourceType.物資), 1e-3f); // 7+5
            Assert.AreEqual(6f, StateMaterialDemandRules.TotalStateDemand(provs, 1, ResourceType.燃料), 1e-3f);  // 4+2
            Assert.AreEqual(0f, StateMaterialDemandRules.TotalStateDemand(provs, 1, ResourceType.弾薬), 1e-3f);
        }

        // --- STATEDEM-4 充足/消費/最小律 ---
        [Test]
        public void Fulfillment_ConsumeAndOverall()
        {
            Assert.AreEqual(1f, StateConsumptionFulfillmentRules.Fulfillment(12f, 12f), 1e-4f);
            Assert.AreEqual(0.5f, StateConsumptionFulfillmentRules.Fulfillment(6f, 12f), 1e-4f);
            Assert.AreEqual(1f, StateConsumptionFulfillmentRules.Fulfillment(0f, 0f), 1e-4f); // 需要0は満たされ済み
            Assert.AreEqual(6f, StateConsumptionFulfillmentRules.Shortage(6f, 12f), 1e-4f);

            var stock = new ResourceStockpile(6f, 0f, 6f);
            float shortage = StateConsumptionFulfillmentRules.Consume(stock, ResourceType.物資, 12f);
            Assert.AreEqual(6f, shortage, 1e-3f);  // 6 しか引けず 6 不足
            Assert.AreEqual(0f, stock.supplies, 1e-3f);

            // 総合充足＝最小律（物資0.5・燃料1・弾薬は需要0で1）→0.5
            var stock2 = new ResourceStockpile(6f, 0f, 6f);
            Assert.AreEqual(0.5f, StateConsumptionFulfillmentRules.OverallFulfillment(stock2, 12f, 0f, 6f), 1e-4f);
        }

        // --- STATEDEM-5 効果 ---
        [Test]
        public void Effect_Penalties()
        {
            Assert.AreEqual(0f, StateConsumptionEffectRules.StabilityPenalty(1f), 1e-4f);
            Assert.AreEqual(10f, StateConsumptionEffectRules.StabilityPenalty(0.5f), 1e-4f); // 0.5×20
            Assert.AreEqual(-7.5f, StateConsumptionEffectRules.SupportDelta(0.5f), 1e-4f);   // -0.5×15
            Assert.AreEqual(0.75f, StateConsumptionEffectRules.OutputFactor(0.5f), 1e-4f);   // Lerp(0.5,1,0.5)
            Assert.AreEqual(0.5f, StateConsumptionEffectRules.OutputFactor(0f), 1e-4f);      // 下限
            Assert.AreEqual(1f, StateConsumptionEffectRules.OutputFactor(1f), 1e-4f);
        }

        // --- STATEDEM-6 Tick ---
        [Test]
        public void Tick_ConsumesAndReportsShortage()
        {
            var provs = new List<Province> { new Province(1, "", 100f) };
            var stock = new ResourceStockpile(6f, 0f, 6f); // 物資6（需要12に対し不足）・燃料6（需要6で充足）
            var r = StateConsumptionTickRules.TickState(provs, 1, stock);
            Assert.AreEqual(0.5f, r.overall, 1e-4f);          // 最小律＝物資が不足
            Assert.AreEqual(6f, r.shortageSupplies, 1e-3f);   // 物資 6 不足
            Assert.AreEqual(0f, r.shortageFuel, 1e-3f);       // 燃料は足りた
            Assert.IsTrue(r.HasShortage);
            Assert.AreEqual(0f, stock.supplies, 1e-3f);       // 在庫は消費されて0
            Assert.AreEqual(0f, stock.fuel, 1e-3f);

            // 在庫 null でも安全＝需要が丸ごと不足
            var r2 = StateConsumptionTickRules.TickState(provs, 1, null);
            Assert.AreEqual(12f, r2.shortageSupplies, 1e-3f);
            Assert.AreEqual(0f, r2.overall, 1e-4f);
        }
    }
}
