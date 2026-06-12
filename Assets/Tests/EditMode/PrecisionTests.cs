using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>精密機器（#2024・<see cref="PrecisionRules"/>）：ニッチ高採算(PRC-1)・R&D集約(PRC-2)・技術障壁(PRC-3)。</summary>
    public class PrecisionTests
    {
        [Test]
        public void Niche_HighMargin()
        {
            Assert.AreEqual(30000f, PrecisionRules.NicheProfit(100f, 500f, 200f), 1e-1f); // 少量×高単価
            Assert.AreEqual(0.6f, PrecisionRules.GrossMarginRate(500f, 200f), 1e-4f);     // 高粗利率
        }

        [Test]
        public void Rd_AndTechLead()
        {
            Assert.AreEqual(0.15f, PrecisionRules.RdIntensity(1500f, 10000f), 1e-4f);
            Assert.AreEqual(120f, PrecisionRules.TechLeadPremium(5f, 3f, 100f, 0.1f), 1e-3f); // 2リードで+20%
        }
    }
}
