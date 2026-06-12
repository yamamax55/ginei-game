using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>その他製品（#2024・<see cref="DiversifiedProductRules"/>）：合計収益(OTH-1)・分散(OTH-2)・ブランド(OTH-3)。</summary>
    public class DiversifiedProductTests
    {
        [Test]
        public void Portfolio_RevenueAndDiversification()
        {
            Assert.AreEqual(1000f, DiversifiedProductRules.ProductLineRevenue(new List<float> { 300f, 200f, 500f }), 1e-3f);
            Assert.AreEqual(0.72f, DiversifiedProductRules.PortfolioDiversification(new List<float> { 300f, 300f, 300f, 100f }), 1e-3f);
            Assert.AreEqual(0f, DiversifiedProductRules.PortfolioDiversification(new List<float> { 1000f }), 1e-3f); // 一本足
        }

        [Test]
        public void Brand()
        {
            Assert.AreEqual(130f, DiversifiedProductRules.BrandedPrice(100f, 0.6f, 0.5f), 1e-3f);
        }
    }
}
