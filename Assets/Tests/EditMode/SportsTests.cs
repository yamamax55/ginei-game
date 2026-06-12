using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>スポーツ興行（#2025・<see cref="SportsRules"/>）：入場料(SPRT-1)・放映権(SPRT-2)・グッズ(SPRT-3)・利益(SPRT-4)。</summary>
    public class SportsTests
    {
        [Test]
        public void ThreeRevenueStreams()
        {
            Assert.AreEqual(150000000f, SportsRules.GateRevenue(30000, 5000f), 1e1f);
            Assert.AreEqual(50000000f, SportsRules.BroadcastRightsRevenue(50, 1000000f), 1e1f);
            Assert.AreEqual(100000000f, SportsRules.MerchandiseRevenue(100000, 1000f), 1e1f);
        }

        [Test]
        public void Profit()
        {
            // 入場料1.5億+放映権0.5億+グッズ1億−運営2.5億 = 0.5億
            Assert.AreEqual(50000000f, SportsRules.SportsProfit(150000000f, 50000000f, 100000000f, 250000000f), 1e1f);
        }
    }
}
