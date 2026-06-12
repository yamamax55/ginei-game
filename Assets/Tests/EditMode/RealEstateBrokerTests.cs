using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>不動産仲介（#2025・<see cref="RealEstateBrokerRules"/>）：手数料(BROK-1)・両手(BROK-2)・成約率(BROK-3)・利益(BROK-4)。</summary>
    public class RealEstateBrokerTests
    {
        [Test]
        public void Commission_AndBothSides()
        {
            Assert.AreEqual(1500000f, RealEstateBrokerRules.BrokerageCommission(50000000f, 0.03f), 1e1f);
            Assert.AreEqual(3000000f, RealEstateBrokerRules.BothSidesCommission(50000000f, 0.03f), 1e1f); // 売主買主双方
        }

        [Test]
        public void Conversion_AndProfit()
        {
            Assert.AreEqual(0.3f, RealEstateBrokerRules.ListingConversion(30, 100), 1e-4f);
            Assert.AreEqual(500000f, RealEstateBrokerRules.BrokerProfit(1500000f, 600000f, 400000f), 1e-1f);
        }
    }
}
