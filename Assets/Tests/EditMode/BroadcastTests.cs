using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>放送・メディア（#2025・<see cref="BroadcastRules"/>）：視聴率(BC-1)・広告枠(BC-2)・購読(BC-3)・利益(BC-4)。</summary>
    public class BroadcastTests
    {
        [Test]
        public void Rating_AndAdRevenue()
        {
            Assert.AreEqual(0.2f, BroadcastRules.AudienceRating(2000000f, 10000000f), 1e-4f); // 視聴率20%
            Assert.AreEqual(50000f, BroadcastRules.AdRevenue(500f, 100f), 1e-1f); // GRP×単価
        }

        [Test]
        public void Subscription_AndProfit()
        {
            Assert.AreEqual(500000f, BroadcastRules.SubscriptionRevenue(10000, 50f), 1e-1f);
            // 広告5万+購読50万−制作30万−固定10万 = 15万
            Assert.AreEqual(150000f, BroadcastRules.BroadcastProfit(50000f, 500000f, 300000f, 100000f), 1e-1f);
        }
    }
}
