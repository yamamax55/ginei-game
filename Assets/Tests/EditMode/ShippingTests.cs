using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>海運会社（#2024・<see cref="ShippingRules"/>）：運賃市況(SHP-1)・船腹過剰(SHP-2)・航海採算(SHP-3)・戦時リスク(SHP-4)。</summary>
    public class ShippingTests
    {
        [Test]
        public void FreightRate_SupplyDemand()
        {
            Assert.AreEqual(120f, ShippingRules.FreightRate(1200f, 1000f, 100f), 1e-3f); // 需要超過＝運賃高
            Assert.AreEqual(80f, ShippingRules.FreightRate(800f, 1000f, 100f), 1e-3f);   // 船腹過剰＝運賃安
            Assert.AreEqual(0.2f, ShippingRules.OvercapacityRatio(1200f, 1000f), 1e-4f); // 2割過剰
            Assert.AreEqual(0f, ShippingRules.OvercapacityRatio(800f, 1000f), 1e-4f);
        }

        [Test]
        public void Voyage_AndWarRisk()
        {
            Assert.AreEqual(50f, ShippingRules.VoyageProfit(120f, 40f, 30f), 1e-3f); // 運賃−燃料−用船
            Assert.AreEqual(20f, ShippingRules.WarRiskSurcharge(100f, 0.1f, 2f), 1e-3f); // 通商破壊リスク割増
        }
    }
}
