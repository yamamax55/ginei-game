using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>宇宙太陽光発電（#2025・<see cref="SpaceSolarRules"/>）：軌道発電(SSOL-1)・送電(SSOL-2)・売電(SSOL-3)・利益(SSOL-4)。</summary>
    public class SpaceSolarTests
    {
        [Test]
        public void Generation_AndBeaming()
        {
            Assert.AreEqual(4200f, SpaceSolarRules.OrbitalGeneration(10000f, 1.4f, 0.3f), 1e-1f); // 面積×強度×効率
            Assert.AreEqual(2100f, SpaceSolarRules.BeamingDelivery(4200f, 0.5f), 1e-1f); // 伝送ロス
        }

        [Test]
        public void Sales_AndProfit()
        {
            Assert.AreEqual(42000f, SpaceSolarRules.PowerSalesRevenue(2100f, 20f), 1e-1f);
            Assert.AreEqual(15000f, SpaceSolarRules.SpaceSolarProfit(42000f, 15000f, 7000f, 5000f), 1e-1f);
        }
    }
}
