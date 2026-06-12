using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>物流不動産REIT（#2025・<see cref="LogisticsReitRules"/>）：施設賃料(LREIT-1)・開発利回り(LREIT-2)・集中リスク(LREIT-3)・利益(LREIT-4)。</summary>
    public class LogisticsReitTests
    {
        [Test]
        public void Rent_AndYield()
        {
            Assert.AreEqual(47500f, LogisticsReitRules.FacilityRent(10000f, 5f, 0.95f), 1e-1f); // 面積×坪賃料×稼働
            Assert.AreEqual(0.095f, LogisticsReitRules.BuildToSuitYield(4750f, 50000f), 1e-4f);
        }

        [Test]
        public void Concentration_AndProfit()
        {
            Assert.AreEqual(0.6f, LogisticsReitRules.TenantConcentrationRisk(3000f, 5000f), 1e-4f); // 大口EC/3PL依存
            Assert.AreEqual(30000f, LogisticsReitRules.LogisticsReitProfit(47500f, 10000f, 7500f), 1e-1f);
        }
    }
}
