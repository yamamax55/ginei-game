using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>宇宙鉄道（#2025・<see cref="SpaceRailwayRules"/>）：旅客運輸(RAIL-1)・非運輸収入(RAIL-2)・乗車率(RAIL-3)・利益(RAIL-4)。</summary>
    public class SpaceRailwayTests
    {
        [Test]
        public void Passenger_AndNonTransport()
        {
            Assert.AreEqual(500000f, SpaceRailwayRules.PassengerRevenue(10000f, 50f), 1e-1f); // 星系間旅客
            Assert.AreEqual(500000f, SpaceRailwayRules.NonTransportRevenue(200000f, 300000f), 1e-1f); // 駅ナカ+沿線開発
        }

        [Test]
        public void LoadFactor_AndProfit()
        {
            Assert.AreEqual(0.8f, SpaceRailwayRules.LoadFactor(8000f, 10000f), 1e-4f);
            // 運輸50万+非運輸50万−運営70万 = 30万（沿線開発が薄利の運輸を補う）
            Assert.AreEqual(300000f, SpaceRailwayRules.RailwayProfit(500000f, 500000f, 700000f), 1e-1f);
        }
    }
}
