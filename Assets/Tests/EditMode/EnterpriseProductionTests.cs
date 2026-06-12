using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 企業の需要と生産（#2084）：投入係数(FIRMPROD-1)/需要(2)/制約・最小律(3)/実産出・稼働率(4)/市場(5)/Tick(6)。
    /// </summary>
    public class EnterpriseProductionTests
    {
        // --- FIRMPROD-1 投入係数 ---
        [Test]
        public void Input_Coefficients()
        {
            Assert.AreEqual(0.5f, EnterpriseInputRules.InputCoefficient(ProductionInput.原材料), 1e-4f);
            Assert.AreEqual(0.3f, EnterpriseInputRules.InputCoefficient(ProductionInput.エネルギー), 1e-4f);
            Assert.AreEqual(75f, EnterpriseInputRules.InputDemand(150f, ProductionInput.原材料), 1e-3f);  // 150×0.5
            Assert.AreEqual(45f, EnterpriseInputRules.InputDemand(150f, ProductionInput.エネルギー), 1e-3f);
            Assert.AreEqual(20f, EnterpriseInputRules.InputDemand(100f, ProductionInput.資本財, 2f), 1e-3f); // 100×0.1×2
        }

        // --- FIRMPROD-2 投入需要（計画産出から） ---
        [Test]
        public void Demand_FromPlannedOutput()
        {
            var e = new Enterprise { employees = 100f, capital = 1000f, productivity = 1f };
            Assert.AreEqual(150f, EnterpriseRules.Output(e), 1e-3f); // 100×1×(1+1000×0.0005)=150
            Assert.AreEqual(75f, EnterpriseDemandRules.Demand(e, ProductionInput.原材料), 1e-3f);
            Assert.AreEqual(60f, EnterpriseDemandRules.DemandForOutput(200f, ProductionInput.エネルギー), 1e-3f);
        }

        // --- FIRMPROD-3 制約・最小律・ボトルネック ---
        [Test]
        public void Constraint_MinimumLawAndBottleneck()
        {
            Assert.AreEqual(150f, ProductionConstraintRules.MaxOutputFromInput(75f, 0.5f), 1e-3f);
            Assert.AreEqual(float.MaxValue, ProductionConstraintRules.MaxOutputFromInput(10f, 0f)); // 係数0は無制約

            // 投入十分＝計画どおり
            Assert.AreEqual(150f, ProductionConstraintRules.ConstrainedOutput(150f, 75f, 45f, 1000f), 1e-3f);
            // 原材料不足（30→最大60）＝減産
            Assert.AreEqual(60f, ProductionConstraintRules.ConstrainedOutput(150f, 30f, 45f, 1000f), 1e-3f);

            var binding = ProductionConstraintRules.BindingInput(150f, 30f, 45f, 1000f, out bool constrained);
            Assert.IsTrue(constrained);
            Assert.AreEqual(ProductionInput.原材料, binding);
            ProductionConstraintRules.BindingInput(150f, 75f, 45f, 1000f, out bool ok);
            Assert.IsFalse(ok); // 計画どおり作れる
        }

        // --- FIRMPROD-4 実産出・稼働率・消費 ---
        [Test]
        public void Production_RealizedUtilizationConsumed()
        {
            Assert.AreEqual(60f, EnterpriseProductionRules.RealizedOutput(150f, 30f, 45f, 1000f), 1e-3f);
            Assert.AreEqual(0.4f, EnterpriseProductionRules.CapacityUtilization(60f, 150f), 1e-4f); // 60/150
            Assert.AreEqual(1f, EnterpriseProductionRules.CapacityUtilization(150f, 150f), 1e-4f);
            Assert.AreEqual(0f, EnterpriseProductionRules.CapacityUtilization(50f, 0f), 1e-4f);
            Assert.AreEqual(30f, EnterpriseProductionRules.InputConsumed(60f, ProductionInput.原材料), 1e-3f); // 60×0.5
        }

        // --- FIRMPROD-5 市場（投入コスト・利潤） ---
        [Test]
        public void Market_CostRevenueProfit()
        {
            // 100×(0.5×2 + 0.3×1 + 0.1×3) = 100×1.6 = 160
            Assert.AreEqual(160f, EnterpriseMarketRules.InputCost(100f, 2f, 1f, 3f), 1e-3f);
            Assert.AreEqual(500f, EnterpriseMarketRules.Revenue(100f, 5f), 1e-3f);
            Assert.AreEqual(290f, EnterpriseMarketRules.OperatingProfit(100f, 5f, 2f, 1f, 3f, 50f), 1e-3f); // 500-160-50
            Assert.AreEqual(100f, EnterpriseMarketRules.MarketSupply(100f), 1e-3f);
        }

        // --- FIRMPROD-6 Tick ---
        [Test]
        public void Tick_ProduceAndConsume()
        {
            var pr = EnterpriseProductionTickRules.Produce(150f, 30f, 45f, 1000f);
            Assert.AreEqual(60f, pr.realizedOutput, 1e-3f);
            Assert.AreEqual(0.4f, pr.utilization, 1e-4f);
            Assert.IsTrue(pr.inputConstrained);
            Assert.AreEqual(ProductionInput.原材料, pr.binding);

            // 企業オーバーロード（投入十分なら計画どおり）
            var e = new Enterprise { employees = 100f, capital = 1000f, productivity = 1f };
            var pr2 = EnterpriseProductionTickRules.Produce(e, 75f, 45f, 1000f);
            Assert.AreEqual(150f, pr2.realizedOutput, 1e-3f);
            Assert.IsFalse(pr2.inputConstrained);

            // 実産出ぶんを在庫から消費（原材料→物資 60×0.5=30、エネルギー→燃料 60×0.3=18）
            var stock = new ResourceStockpile(100f, 0f, 100f);
            EnterpriseProductionTickRules.Consume(stock, 60f);
            Assert.AreEqual(70f, stock.supplies, 1e-3f);
            Assert.AreEqual(82f, stock.fuel, 1e-3f);
        }
    }
}
