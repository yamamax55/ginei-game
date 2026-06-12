using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>出版（#2025・<see cref="PublishingRules"/>）：書籍売上(PUB-1)・著者印税(PUB-2)・返品損(PUB-3)・利益(PUB-4)。</summary>
    public class PublishingTests
    {
        [Test]
        public void Sales_AndRoyalty()
        {
            Assert.AreEqual(150000000f, PublishingRules.BookSalesRevenue(100000, 1500f), 1e2f);
            Assert.AreEqual(15000000f, PublishingRules.AuthorRoyalty(150000000f, 0.1f), 1e1f);
        }

        [Test]
        public void Returns_AndProfit()
        {
            Assert.AreEqual(18000000f, PublishingRules.ReturnLoss(120000, 0.3f, 500f), 1e1f); // 返品36000×原価500
            Assert.AreEqual(35000000f, PublishingRules.PublishingProfit(150000000f, 15000000f, 80000000f, 20000000f), 1e2f);
        }
    }
}
