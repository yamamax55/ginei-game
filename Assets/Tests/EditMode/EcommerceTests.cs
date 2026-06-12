using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>EC（#2025・<see cref="EcommerceRules"/>）：テイクレート(EC-1)・物流費(EC-2)・ロングテール(EC-3)・利益(EC-4)。</summary>
    public class EcommerceTests
    {
        [Test]
        public void TakeRate_AndLogistics()
        {
            Assert.AreEqual(10000f, EcommerceRules.TakeRateRevenue(100000f, 0.1f), 1e-1f); // GMV×手数料率
            Assert.AreEqual(5000f, EcommerceRules.LogisticsCost(1000, 5f), 1e-1f);
        }

        [Test]
        public void LongTail_AndProfit()
        {
            Assert.AreEqual(0.4f, EcommerceRules.LongTailShare(400f, 1000f), 1e-4f);
            Assert.AreEqual(3000f, EcommerceRules.EcommerceProfit(10000f, 5000f, 2000f), 1e-1f);
        }
    }
}
