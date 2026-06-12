using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>鉱業精錬コンビナート（#2025・<see cref="SmeltingComplexRules"/>）：一貫産出(SMLT-1)・自山鉱優位(SMLT-2)・一貫マージン(SMLT-3)・利益(SMLT-4)。</summary>
    public class SmeltingComplexTests
    {
        [Test]
        public void Output_AndCaptiveSavings()
        {
            Assert.AreEqual(9000f, SmeltingComplexRules.IntegratedOutput(10000f, 0.9f), 1e-1f); // 鉱石→金属
            Assert.AreEqual(20000f, SmeltingComplexRules.CaptiveOreSavings(50f, 30f, 1000f), 1e-1f); // 市場50−自前30
        }

        [Test]
        public void Margin_AndProfit()
        {
            Assert.AreEqual(50f, SmeltingComplexRules.IntegratedMargin(100f, 30f, 20f), 1e-3f); // 採掘+製錬を取り込む
            Assert.AreEqual(300000f, SmeltingComplexRules.ComplexProfit(900000f, 300000f, 200000f, 100000f), 1e-1f);
        }
    }
}
