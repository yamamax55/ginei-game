using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>半導体（#2025・<see cref="SemiconductorRules"/>）：歩留まり(SEMI-1)・チップ産出(SEMI-2)・微細化設備投資(SEMI-3)・利益(SEMI-4)。</summary>
    public class SemiconductorTests
    {
        [Test]
        public void Yield_AndOutput()
        {
            Assert.AreEqual(0.8f, SemiconductorRules.WaferYield(800f, 1000f), 1e-4f);
            Assert.AreEqual(80000f, SemiconductorRules.ChipOutput(100f, 1000f, 0.8f), 1e-1f); // 100枚×1000ダイ×0.8
        }

        [Test]
        public void Capex_AndProfit()
        {
            Assert.AreEqual(2250f, SemiconductorRules.CapexPerNode(1000f, 2, 0.5f), 1e-1f); // 1000×1.5^2
            Assert.AreEqual(5000f, SemiconductorRules.SemiconductorProfit(1000f, 10f, 3000f, 2000f), 1e-1f); // 1万−償却3千−固定2千
        }
    }
}
