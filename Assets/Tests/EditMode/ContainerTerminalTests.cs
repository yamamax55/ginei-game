using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>宇宙コンテナターミナル（#2025・<see cref="ContainerTerminalRules"/>）：処理量(TERM-1)・荷役料(TERM-2)・積替比率(TERM-3)・利益(TERM-4)。</summary>
    public class ContainerTerminalTests
    {
        [Test]
        public void Throughput_AndHandling()
        {
            Assert.AreEqual(30000f, ContainerTerminalRules.Throughput(10, 30f, 100f), 1e-1f); // クレーン×処理×稼働
            Assert.AreEqual(3000000f, ContainerTerminalRules.HandlingRevenue(30000f, 100f), 1e1f);
        }

        [Test]
        public void Transshipment_AndProfit()
        {
            Assert.AreEqual(0.6f, ContainerTerminalRules.TransshipmentShare(18000f, 30000f), 1e-4f); // 中継ハブ機能
            Assert.AreEqual(1000000f, ContainerTerminalRules.TerminalProfit(3000000f, 1000000f, 500000f, 500000f), 1e1f);
        }
    }
}
