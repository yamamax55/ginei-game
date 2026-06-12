using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>食品スーパー（#2025・<see cref="SupermarketRules"/>）：鮮度ロス(SPMK-1)・混合粗利(SPMK-2)・日商(SPMK-3)・利益(SPMK-4)。</summary>
    public class SupermarketTests
    {
        [Test]
        public void FreshLoss_AndBlendedMargin()
        {
            Assert.AreEqual(15000f, SupermarketRules.FreshFoodLoss(1000f, 850f, 100f), 1e-1f); // 売れ残り150×原価100
            // 0.4×0.35 + 0.6×0.25 = 0.14+0.15 = 0.29
            Assert.AreEqual(0.29f, SupermarketRules.BlendedMargin(0.4f, 0.35f, 0.25f), 1e-4f);
        }

        [Test]
        public void DailySales_AndProfit()
        {
            Assert.AreEqual(6000000f, SupermarketRules.DailySales(2000, 3000f), 1e-1f);
            Assert.AreEqual(85000f, SupermarketRules.SupermarketProfit(300000f, 15000f, 200000f), 1e-1f);
        }
    }
}
