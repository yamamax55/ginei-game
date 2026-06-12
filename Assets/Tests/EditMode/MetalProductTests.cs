using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>金属製品（#2024・<see cref="MetalProductRules"/>）：加工(MPR-1)・加工マージン(MPR-2)・利益(MPR-3)。</summary>
    public class MetalProductTests
    {
        [Test]
        public void Processing_MarginAndProfit()
        {
            Assert.AreEqual(900f, MetalProductRules.ProcessedOutput(1000f, 0.9f), 1e-3f); // 鋼材→製品
            Assert.AreEqual(25f, MetalProductRules.ProcessingMargin(100f, 60f, 15f), 1e-3f); // 製品−鋼材−加工費
            Assert.AreEqual(12500f, MetalProductRules.MetalProductProfit(900f, 25f, 10000f), 1e-1f); // 22500−固定10000
        }
    }
}
