using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>報道・通信社（#2025・<see cref="NewsAgencyRules"/>）：購読(NEWS-1)・配信料(NEWS-2)・世論影響度(NEWS-3)・利益(NEWS-4)。</summary>
    public class NewsAgencyTests
    {
        [Test]
        public void Subscription_AndWire()
        {
            Assert.AreEqual(10000000f, NewsAgencyRules.SubscriptionRevenue(50000, 200f), 1e1f);
            Assert.AreEqual(5000000f, NewsAgencyRules.WireServiceRevenue(100, 50000f), 1e1f); // 他メディアへ配信
        }

        [Test]
        public void Influence_AndProfit()
        {
            Assert.AreEqual(800000f, NewsAgencyRules.InfluenceReach(1000000f, 0.8f), 1e-1f); // 部数×信頼度→世論#113
            Assert.AreEqual(5000000f, NewsAgencyRules.NewsAgencyProfit(10000000f, 5000000f, 8000000f, 2000000f), 1e1f);
        }
    }
}
