using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>繊維製品（#2024・<see cref="TextileRules"/>）：流行需要(TEX-1)・陳腐化(TEX-2)・新興国競合(TEX-3)。</summary>
    public class TextileTests
    {
        [Test]
        public void Fashion_AndObsolescence()
        {
            Assert.AreEqual(700f, TextileRules.SeasonalDemand(1000f, 0.7f), 1e-3f); // 流行外れで減
            Assert.AreEqual(2500f, TextileRules.ObsolescenceMarkdownLoss(100f, 50f, 0.5f), 1e-1f); // 季節遅れの値下げ損
        }

        [Test]
        public void LowCostThreat_AndProfit()
        {
            Assert.AreEqual(0.5f, TextileRules.LowCostCountryThreat(100f, 50f), 1e-4f); // 新興国に対し5割割高
            Assert.AreEqual(2000f, TextileRules.TextileProfit(1000f, 12f, 10f), 1e-1f);
        }
    }
}
