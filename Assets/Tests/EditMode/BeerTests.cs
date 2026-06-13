using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ビール（#2025・<see cref="BeerRules"/>）：酒税(BEER-1)・税転嫁(BEER-2)・シェア数量(BEER-3)・利益(BEER-4)。</summary>
    public class BeerTests
    {
        [Test]
        public void Tax_AndPassThrough()
        {
            Assert.AreEqual(200000f, BeerRules.LiquorTax(1000f, 200f), 1e-1f);
            Assert.AreEqual(400f, BeerRules.PriceAfterTaxPassThrough(300f, 100f, 1.0f), 1e-3f); // 全額転嫁
            Assert.AreEqual(350f, BeerRules.PriceAfterTaxPassThrough(300f, 100f, 0.5f), 1e-3f); // 半分吸収
        }

        [Test]
        public void Share_AndProfit()
        {
            Assert.AreEqual(4000f, BeerRules.MarketShareVolume(10000f, 0.4f), 1e-1f);
            Assert.AreEqual(300f, BeerRules.BeerProfit(1000f, 400f, 300f), 1e-3f); // 酒税抜き
        }
    }
}
