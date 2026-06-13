using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ゴム製品（#2024・<see cref="RubberRules"/>）：タイヤ需要(RUB-1)・スプレッド(RUB-2)・利益(RUB-3)・補修下支え(RUB-4)。</summary>
    public class RubberTests
    {
        [Test]
        public void TireDemand_AndSpread()
        {
            Assert.AreEqual(1000f, RubberRules.TireDemand(200f, 800f), 1e-3f); // 新車200＋補修800
            Assert.AreEqual(40f, RubberRules.RubberSpread(100f, 60f), 1e-3f);
        }

        [Test]
        public void Profit_AndReplacementCushion()
        {
            Assert.AreEqual(20000f, RubberRules.RubberProfit(1000f, 40f, 20000f), 1e-1f); // 40000−固定20000
            Assert.AreEqual(0.8f, RubberRules.ReplacementShare(800f, 1000f), 1e-4f); // 補修8割＝景気耐性
        }
    }
}
