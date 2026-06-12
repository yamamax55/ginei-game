using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ガラス・土石（#2024・<see cref="CeramicsRules"/>）：セメント(GLS-1)・窯業利益(GLS-2)・地域独占(GLS-3)・建設連動(GLS-4)。</summary>
    public class CeramicsTests
    {
        [Test]
        public void Cement_AndKilnProfit()
        {
            Assert.AreEqual(600f, CeramicsRules.CementOutput(1000f, 0.6f), 1e-3f);
            Assert.AreEqual(8000f, CeramicsRules.KilnProfit(600f, 30f, 10000f), 1e-1f); // 18000−固定10000
        }

        [Test]
        public void LocalMonopoly_AndConstructionLink()
        {
            Assert.AreEqual(50f, CeramicsRules.LocalMonopolyRange(100f, 2f), 1e-3f); // 重量物＝商圏が狭く地域独占
            Assert.AreEqual(500f, CeramicsRules.ConstructionLinkedDemand(1000f, 0.5f), 1e-3f); // 建設連動
        }
    }
}
