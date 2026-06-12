using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>非鉄金属（#2024・<see cref="NonferrousRules"/>）：製錬(NFE-1)・製錬マージン(NFE-2)・製錬利益(NFE-3)。</summary>
    public class NonferrousTests
    {
        [Test]
        public void Smelting_MetalAndMargin()
        {
            Assert.AreEqual(950f, NonferrousRules.SmeltedMetal(1000f, 0.95f), 1e-3f); // 鉱石→地金
            Assert.AreEqual(30f, NonferrousRules.SmeltingMargin(100f, 60f, 10f), 1e-3f); // 地金−鉱石−製錬料
        }

        [Test]
        public void NonferrousProfit_MarketLinked()
        {
            Assert.AreEqual(23500f, NonferrousRules.NonferrousProfit(950f, 30f, 5000f), 1e-1f); // 28500−固定5000
        }
    }
}
