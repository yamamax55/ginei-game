using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>配車・ライドシェア（#2025・<see cref="RideHailingRules"/>）：総取扱額(RIDE-1)・手数料(RIDE-2)・サージ(RIDE-3)・利益(RIDE-4)。</summary>
    public class RideHailingTests
    {
        [Test]
        public void Bookings_AndPlatformRevenue()
        {
            Assert.AreEqual(150000000f, RideHailingRules.GrossBookings(100000, 1500f), 1e2f);
            Assert.AreEqual(30000000f, RideHailingRules.PlatformRevenue(150000000f, 0.2f), 1e1f);
        }

        [Test]
        public void Surge_AndProfit()
        {
            Assert.AreEqual(1500f, RideHailingRules.SurgePrice(1000f, 2.0f, 0.5f), 1e-2f); // 需給2倍×感応0.5→1.5倍
            Assert.AreEqual(1000f, RideHailingRules.SurgePrice(1000f, 0.8f, 0.5f), 1e-2f); // 供給過剰はサージ無し
            Assert.AreEqual(150000000f, RideHailingRules.RideHailingProfit(300000000f, 100000000f, 50000000f), 1e2f);
        }
    }
}
