using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>宇宙港運営（#2025・<see cref="SpaceportRules"/>）：着陸料(PORT-1)・免税店(PORT-2)・旅客施設料(PORT-3)・利益(PORT-4)。</summary>
    public class SpaceportTests
    {
        [Test]
        public void Aero_AndNonAero()
        {
            Assert.AreEqual(50000f, SpaceportRules.LandingFeeRevenue(1000, 50f), 1e-1f); // 宇宙鉄道/空運の発着
            Assert.AreEqual(200000f, SpaceportRules.ConcessionRevenue(100000f, 10f, 0.2f), 1e-1f); // 免税店テナント料
            Assert.AreEqual(500000f, SpaceportRules.PassengerServiceCharge(100000f, 5f), 1e-1f);
        }

        [Test]
        public void Profit()
        {
            Assert.AreEqual(100000f, SpaceportRules.SpaceportProfit(50000f, 200000f, 150000f), 1e-1f); // 航空系+非航空系−運営
        }
    }
}
