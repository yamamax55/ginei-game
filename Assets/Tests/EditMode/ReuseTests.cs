using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>中古品・リユース（#2025・<see cref="ReuseRules"/>）：買取原価(REUSE-1)・再販マージン(REUSE-2)・消化率(REUSE-3)・利益(REUSE-4)。</summary>
    public class ReuseTests
    {
        [Test]
        public void Buyback_AndMargin()
        {
            Assert.AreEqual(30000f, ReuseRules.BuybackCost(1000, 30f), 1e-1f);
            Assert.AreEqual(0.7f, ReuseRules.ResaleMargin(100f, 30f), 1e-4f); // 安く買い高く売る
        }

        [Test]
        public void SellThrough_AndProfit()
        {
            Assert.AreEqual(0.8f, ReuseRules.SellThroughInventory(800f, 1000f), 1e-4f);
            Assert.AreEqual(30000f, ReuseRules.ReuseProfit(80000f, 30000f, 20000f), 1e-1f);
        }
    }
}
