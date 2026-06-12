using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// POP要求物資（#2042）：カテゴリ(POPDEM-1)/総需要(POPDEM-2)/充足(POPDEM-3)/生活水準・マズロー(POPDEM-4)/市場駆動(POPDEM-5)/Tick(POPDEM-6)。
    /// </summary>
    public class PopConsumptionTests
    {
        // --- POPDEM-1 カテゴリ基盤 ---
        [Test]
        public void Goods_PerCapitaElasticityNeedTiers()
        {
            Assert.AreEqual(1.0f, ConsumptionGoodsRules.PerCapitaDemand(ConsumptionCategory.必需), 1e-4f);
            Assert.AreEqual(0.2f, ConsumptionGoodsRules.PerCapitaDemand(ConsumptionCategory.奢侈), 1e-4f);
            Assert.AreEqual(0.1f, ConsumptionGoodsRules.IncomeElasticity(ConsumptionCategory.必需), 1e-4f); // 必需は硬直
            Assert.AreEqual(1.0f, ConsumptionGoodsRules.IncomeElasticity(ConsumptionCategory.奢侈), 1e-4f); // 奢侈は弾力的
            Assert.IsTrue(ConsumptionGoodsRules.IsNecessity(ConsumptionCategory.必需));
            CollectionAssert.AreEqual(new[] { (int)NeedLevel.生理, (int)NeedLevel.安全 }, ConsumptionGoodsRules.NeedTiers(ConsumptionCategory.必需));
        }

        // --- POPDEM-2 総需要（必需は硬直・奢侈は弾力的） ---
        [Test]
        public void Demand_NecessityRigid_LuxuryElastic()
        {
            Assert.AreEqual(100f, ConsumptionDemandRules.TotalDemand(100f, ConsumptionCategory.必需, 1.0f), 1e-2f);
            Assert.AreEqual(90f, ConsumptionDemandRules.TotalDemand(100f, ConsumptionCategory.必需, 0.0f), 1e-2f);  // 困窮でも約9割要る
            Assert.AreEqual(20f, ConsumptionDemandRules.TotalDemand(100f, ConsumptionCategory.奢侈, 1.0f), 1e-2f);
            Assert.AreEqual(0f, ConsumptionDemandRules.TotalDemand(100f, ConsumptionCategory.奢侈, 0.0f), 1e-2f);   // 無所得で奢侈需要0
            Assert.AreEqual(40f, ConsumptionDemandRules.TotalDemand(100f, ConsumptionCategory.奢侈, 2.0f), 1e-2f);  // 富裕で倍
        }

        // --- POPDEM-3 充足率・不足・飢餓 ---
        [Test]
        public void Fulfillment_AndFamine()
        {
            Assert.AreEqual(0.8f, ConsumptionFulfillmentRules.Fulfillment(80f, 100f), 1e-4f);
            Assert.AreEqual(1.0f, ConsumptionFulfillmentRules.Fulfillment(120f, 100f), 1e-4f);
            Assert.AreEqual(1.0f, ConsumptionFulfillmentRules.Fulfillment(50f, 0f), 1e-4f);
            Assert.AreEqual(20f, ConsumptionFulfillmentRules.Shortage(80f, 100f), 1e-2f);
            Assert.AreEqual(0.2f, ConsumptionFulfillmentRules.FamineSeverity(0.8f), 1e-4f); // 必需2割不足=飢餓
            Assert.AreEqual(0f, ConsumptionFulfillmentRules.FamineSeverity(1.0f), 1e-4f);
        }

        // --- POPDEM-4 生活水準・マズロー階層 ---
        [Test]
        public void Welfare_MaslowHierarchy()
        {
            Assert.AreEqual(1.0f, ConsumptionWelfareRules.LivingStandard(1f, 1f, 1f), 1e-4f);   // 全充足
            Assert.AreEqual(0.2f, ConsumptionWelfareRules.LivingStandard(0.2f, 1f, 1f), 1e-4f); // 飢餓＝奢侈満タンでも低い
            Assert.AreEqual(0.75f, ConsumptionWelfareRules.LivingStandard(1f, 0.5f, 0f), 1e-4f); // 食えて快適半分
            // マズロー：飢えていれば奢侈は無意味＝(満腹+少し快適) > (飢餓+満タンの奢侈)
            Assert.Greater(ConsumptionWelfareRules.LivingStandard(1f, 0.5f, 0f),
                           ConsumptionWelfareRules.LivingStandard(0.2f, 1f, 1f));
            // いま最も足りない欲求＝飢餓なら生理（#403 窓口）
            Assert.AreEqual(NeedLevel.生理, ConsumptionWelfareRules.DominantUnmetNeed(0.2f, 1f, 1f));
            Assert.AreEqual(NeedLevel.自己実現, ConsumptionWelfareRules.DominantUnmetNeed(1f, 1f, 0.2f));
            Assert.AreEqual(0.5f, ConsumptionWelfareRules.SupportDelta(0.75f, 0.5f, 2f), 1e-4f);
        }

        // --- POPDEM-5 市場・物流の需要駆動 ---
        [Test]
        public void Market_PriceRisesWithShortage_BlockadeCutsSupply()
        {
            float shortage = ConsumptionMarketRules.DemandDrivenPrice(50f, 100f, 10f);  // 不足
            float balanced = ConsumptionMarketRules.DemandDrivenPrice(100f, 100f, 10f); // 均衡
            float surplus = ConsumptionMarketRules.DemandDrivenPrice(100f, 50f, 10f);   // 過剰
            Assert.Greater(shortage, balanced); // 不足で価格↑
            Assert.Greater(balanced, surplus);  // 過剰で価格↓
            Assert.AreEqual(40f, ConsumptionMarketRules.SuppliedAfterBlockade(100f, 0.6f), 1e-2f); // 封鎖6割で供給4割
            Assert.AreEqual(30f, ConsumptionMarketRules.TradeInflow(50f, 30f, 100f), 1e-2f); // 余剰30が不足50を埋める
        }

        // --- POPDEM-6 Tick オーケストレータ ---
        [Test]
        public void Tick_WellFed_HighLivingStandard()
        {
            var p = new Province(1, "", 100f);
            // 購買力1.0：必需需要100/快適50/奢侈20。供給：必需100(満)・快適20(0.4)・奢侈5(0.25)
            PopConsumptionTickRules.TickYear(p, 1.0f, 100f, 20f, 5f);
            // LivingStandard(1, 0.4, 0.25) = 0.6 + 0.3×0.4 + 0.1×0.1 = 0.73
            Assert.AreEqual(0.73f, p.livingStandard, 1e-3f);
            Assert.AreEqual(0f, p.foodShortage, 1e-4f);
        }

        [Test]
        public void Tick_Starving_FamineAndLowStandard()
        {
            var p = new Province(2, "", 100f);
            // 必需供給50（需要100）→充足0.5→飢餓0.5
            PopConsumptionTickRules.TickYear(p, 1.0f, 50f, 20f, 5f);
            Assert.AreEqual(0.5f, p.foodShortage, 1e-4f);
            Assert.Less(p.livingStandard, 0.4f); // 飢餓で生活水準は大きく低下
        }
    }
}
