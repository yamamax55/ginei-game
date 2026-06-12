using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// GDP（国内総生産・#1951・<see cref="GdpRules"/>）を固定する：三面等価(GDP-1)、名目/実質とデフレータ(GDP-2)、
    /// 成長率・需給ギャップ(GDP-3)、一人当たり(GDP-4)、企業からの集計(GDP-5)。
    /// </summary>
    public class GdpTests
    {
        private static GdpAccounts Sample(float priceLevel = 1f, float potential = 0f)
            => new GdpAccounts(consumption: 600f, investment: 200f, government: 150f,
                               exports: 100f, imports: 50f, priceLevel: priceLevel, potentialOutput: potential);

        // ===== GDP-1 三面等価 =====
        [Test]
        public void ThreeApproaches_Agree()
        {
            var a = Sample();
            Assert.AreEqual(50f, GdpRules.NetExports(a), 1e-3f);          // 100-50
            Assert.AreEqual(1000f, GdpRules.ExpenditureGDP(a), 1e-3f);    // 600+200+150+50
            // 分配面：雇用者報酬700＋営業余剰250＋純間接税50 = 1000
            Assert.AreEqual(1000f, GdpRules.IncomeGDP(700f, 250f, 50f), 1e-3f);
            // 生産面：付加価値 {400,350,250} = 1000
            Assert.AreEqual(1000f, GdpRules.ProductionGDP(new List<float> { 400f, 350f, 250f }), 1e-3f);
            Assert.AreEqual(380f, GdpRules.ValueAdded(500f, 120f), 1e-3f); // 産出500−中間投入120
        }

        // ===== GDP-2 名目 vs 実質 =====
        [Test]
        public void Real_Deflator_Inflation()
        {
            var a = Sample(priceLevel: 1.25f);
            Assert.AreEqual(1000f, GdpRules.NominalGDP(a), 1e-3f);
            Assert.AreEqual(800f, GdpRules.RealGDP(1000f, 1.25f), 1e-3f);   // 名目/物価
            Assert.AreEqual(1000f, GdpRules.RealGDP(1000f, 0f), 1e-3f);     // 物価0は名目そのまま
            Assert.AreEqual(125f, GdpRules.Deflator(1000f, 800f), 1e-3f);   // 名目/実質×100
            Assert.AreEqual(0f, GdpRules.Deflator(1000f, 0f), 1e-3f);
            Assert.AreEqual(0.04167f, GdpRules.InflationRate(125f, 120f), 1e-3f);
            Assert.AreEqual(0f, GdpRules.InflationRate(125f, 0f), 1e-3f);
        }

        // ===== GDP-3 成長率・需給ギャップ =====
        [Test]
        public void Growth_OutputGap_Recession()
        {
            Assert.AreEqual(0.04f, GdpRules.GrowthRate(1040f, 1000f), 1e-4f);
            Assert.AreEqual(0f, GdpRules.GrowthRate(1040f, 0f), 1e-4f);
            Assert.AreEqual(0.04f, GdpRules.OutputGap(1040f, 1000f), 1e-4f);   // 過熱
            Assert.AreEqual(-0.04f, GdpRules.OutputGap(960f, 1000f), 1e-4f);   // 不況
            Assert.AreEqual(0f, GdpRules.OutputGap(960f, 0f), 1e-4f);
            // GdpAccounts から直接：実質1000 vs 潜在800 = +0.25
            var a = Sample(priceLevel: 1f, potential: 800f);
            Assert.AreEqual(0.25f, GdpRules.OutputGap(a), 1e-4f);
            Assert.IsTrue(GdpRules.IsRecession(-0.04f));
            Assert.IsFalse(GdpRules.IsRecession(0.04f));
        }

        // ===== GDP-4 一人当たり =====
        [Test]
        public void PerCapita_AndLivingStandard()
        {
            Assert.AreEqual(5f, GdpRules.PerCapita(1000f, 200f), 1e-3f);
            Assert.AreEqual(0f, GdpRules.PerCapita(1000f, 0f), 1e-3f);
            Assert.AreEqual(4f, GdpRules.RealPerCapita(1000f, 1.25f, 200f), 1e-3f); // 実質800/200
            Assert.AreEqual(1.25f, GdpRules.LivingStandardFactor(5f, 4f), 1e-3f);
            Assert.AreEqual(1f, GdpRules.LivingStandardFactor(5f, 0f), 1e-3f);      // 基準なしは1.0
        }

        // ===== GDP-5 企業からの集計 =====
        [Test]
        public void AggregateValueAdded_FromEnterprises()
        {
            var e1 = new Enterprise();                     // 既定：従業員100/資本1000/生産性1/賃金1
            var e2 = new Enterprise { employees = 200f };
            float price = 2f;
            // 付加価値＝賃金＋利潤＝売上（中間投入を除く＝二重計算しない）
            Assert.AreEqual(EnterpriseRules.Revenue(e1, price), GdpRules.ValueAddedOf(e1, price), 1e-3f);
            // 集計＝各企業の付加価値の和
            float expected = GdpRules.ValueAddedOf(e1, price) + GdpRules.ValueAddedOf(e2, price);
            Assert.AreEqual(expected, GdpRules.AggregateValueAdded(new List<Enterprise> { e1, e2 }, price), 1e-3f);
            // null 安全：null リスト＝0、null 要素はスキップ
            Assert.AreEqual(0f, GdpRules.AggregateValueAdded(null, price), 1e-4f);
            Assert.AreEqual(GdpRules.ValueAddedOf(e1, price),
                GdpRules.AggregateValueAdded(new List<Enterprise> { e1, null }, price), 1e-3f);
        }
    }
}
