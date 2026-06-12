using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>造船（#2025・<see cref="ShipbuildingRules"/>）：受注額(SHIP-1)・為替感応(SHIP-2)・受注残(SHIP-3)・採算(SHIP-4)。</summary>
    public class ShipbuildingTests
    {
        [Test]
        public void Contract_AndForex()
        {
            Assert.AreEqual(500000f, ShipbuildingRules.ContractValue(100000f, 5f), 1e-1f); // 10万DWT×トン5
            Assert.AreEqual(550000f, ShipbuildingRules.ForexAdjustedRevenue(500000f, 1.1f), 1e-1f); // 円安で増
        }

        [Test]
        public void Backlog_AndProfit()
        {
            Assert.AreEqual(1200f, ShipbuildingRules.BacklogAfterOrders(1000f, 500f, 300f), 1e-3f); // 受注残
            Assert.AreEqual(50000f, ShipbuildingRules.ShipbuildingProfit(550000f, 400000f, 100000f), 1e-1f);
        }
    }
}
