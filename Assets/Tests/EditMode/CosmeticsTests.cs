using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>化粧品（#2025・<see cref="CosmeticsRules"/>）：ブランド価格(COSM-1)・粗利率(COSM-2)・広告需要(COSM-3)・利益(COSM-4)。</summary>
    public class CosmeticsTests
    {
        [Test]
        public void Brand_AndMargin()
        {
            Assert.AreEqual(250f, CosmeticsRules.BrandedPrice(100f, 1.5f), 1e-3f); // 原価100→ブランド2.5倍
            Assert.AreEqual(0.8f, CosmeticsRules.GrossMarginRate(250f, 50f), 1e-4f); // 高粗利
        }

        [Test]
        public void AdDemand_AndProfit()
        {
            Assert.AreEqual(1200f, CosmeticsRules.AdDrivenDemand(1000f, 100f, 2f), 1e-3f); // 広告で需要増
            Assert.AreEqual(400f, CosmeticsRules.CosmeticsProfit(1000f, 200f, 300f, 100f), 1e-3f); // 広告が主費目
        }
    }
}
