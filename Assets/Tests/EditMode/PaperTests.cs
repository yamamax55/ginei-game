using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>パルプ・紙（#2024・<see cref="PaperRules"/>）：パルプ(PPR-1)・古紙(PPR-2)・製紙利益(PPR-3)・デジタル化(PPR-4)。</summary>
    public class PaperTests
    {
        [Test]
        public void Pulp_RecycledAndProfit()
        {
            Assert.AreEqual(500f, PaperRules.PulpOutput(1000f, 0.5f), 1e-3f);
            Assert.AreEqual(320f, PaperRules.RecycledOutput(400f, 0.8f), 1e-3f);
            Assert.AreEqual(5000f, PaperRules.PaperMillProfit(500f, 20f, 5000f), 1e-1f); // 10000−固定5000
        }

        [Test]
        public void DigitalDecline()
        {
            Assert.AreEqual(700f, PaperRules.DigitalDeclineDemand(1000f, 0.3f), 1e-3f); // 電子化で3割減
        }
    }
}
