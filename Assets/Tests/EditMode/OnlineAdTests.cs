using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ネット広告（#2025・<see cref="OnlineAdRules"/>）：インプレッション(AD-1)・クリック(AD-2)・収益(AD-3)・オークション(AD-4)。</summary>
    public class OnlineAdTests
    {
        [Test]
        public void Impressions_Clicks_Revenue()
        {
            Assert.AreEqual(30000f, OnlineAdRules.Impressions(10000f, 3f), 1e-1f); // 到達×頻度
            Assert.AreEqual(600f, OnlineAdRules.Clicks(30000f, 0.02f), 1e-2f); // ×CTR
            Assert.AreEqual(30000f, OnlineAdRules.AdRevenue(600f, 50f), 1e-1f); // ×CPC
        }

        [Test]
        public void SecondPriceAuction()
        {
            Assert.AreEqual(101f, OnlineAdRules.AuctionClearingPrice(100f, 1f), 1e-3f); // 2位+刻み
        }
    }
}
