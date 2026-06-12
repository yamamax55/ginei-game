using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>種苗・農業資材（#2025・<see cref="AgriInputRules"/>）：種苗売上(AGRI-1)・収量向上価値(AGRI-2)・形質ロイヤリティ(AGRI-3)・利益(AGRI-4)。</summary>
    public class AgriInputTests
    {
        [Test]
        public void Seed_AndUpliftValue()
        {
            Assert.AreEqual(5000000f, AgriInputRules.SeedSalesRevenue(100000, 50f), 1e0f);
            Assert.AreEqual(20000f, AgriInputRules.YieldUpliftValue(1000f, 0.2f, 100f), 1e-1f); // 農家に生む価値
        }

        [Test]
        public void Royalty_AndProfit()
        {
            Assert.AreEqual(300000f, AgriInputRules.RoyaltyTraitRevenue(10000f, 30f), 1e-1f); // 毎作課金
            Assert.AreEqual(3000000f, AgriInputRules.AgriInputProfit(5000000f, 300000f, 1500000f, 800000f), 1e0f);
        }
    }
}
