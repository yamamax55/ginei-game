using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>アパレルSPA（#2025・<see cref="ApparelSpaRules"/>）：在庫消化率(APRL-1)・値引き損(APRL-2)・垂直マージン(APRL-3)・利益(APRL-4)。</summary>
    public class ApparelSpaTests
    {
        [Test]
        public void SellThrough_AndMarkdown()
        {
            Assert.AreEqual(0.8f, ApparelSpaRules.SellThroughRate(800f, 1000f), 1e-4f);
            Assert.AreEqual(6000f, ApparelSpaRules.MarkdownLoss(200f, 50f, 0.6f), 1e-1f); // 売れ残りを6割引で処分
        }

        [Test]
        public void Vertical_AndProfit()
        {
            Assert.AreEqual(0.7f, ApparelSpaRules.VerticalMargin(100f, 30f), 1e-4f); // 製販一体で高粗利
            Assert.AreEqual(400f, ApparelSpaRules.ApparelProfit(1000f, 300f, 100f, 200f), 1e-1f);
        }
    }
}
