using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>航空会社（#2024・<see cref="AirlineRules"/>）：ロードファクター(AIR-1)・損益分岐(AIR-2)・便採算(AIR-3)。</summary>
    public class AirlineTests
    {
        [Test]
        public void LoadFactor_RevenueAndBreakEven()
        {
            Assert.AreEqual(16000f, AirlineRules.PassengerRevenue(200f, 0.8f, 100f), 1e-1f); // 座席200×稼働0.8×運賃100
            Assert.AreEqual(0.4f, AirlineRules.BreakEvenLoadFactor(8000f, 200f, 100f), 1e-4f); // 損益分岐稼働率
        }

        [Test]
        public void FlightProfit()
        {
            Assert.AreEqual(4000f, AirlineRules.FlightProfit(16000f, 4000f, 8000f), 1e-1f); // 運賃−燃料−固定
        }
    }
}
