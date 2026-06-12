using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>玩具・エンタメ（#2025・<see cref="ToyEntertainmentRules"/>）：版権料(TOY-1)・ヒット売上(TOY-2)・人気減衰(TOY-3)・利益(TOY-4)。</summary>
    public class ToyEntertainmentTests
    {
        [Test]
        public void Royalty_AndHitRevenue()
        {
            Assert.AreEqual(50000f, ToyEntertainmentRules.LicenseRoyalty(1000000f, 0.05f), 1e-1f); // 不労所得的版権料
            Assert.AreEqual(300000000f, ToyEntertainmentRules.HitToyRevenue(100000, 3000f), 1e1f);
        }

        [Test]
        public void Lifecycle_AndProfit()
        {
            // 1000×0.8^2 = 640（ブームは去る）
            Assert.AreEqual(640f, ToyEntertainmentRules.CharacterLifecycleSales(1000f, 2, 0.2f), 1e-2f);
            Assert.AreEqual(100000f, ToyEntertainmentRules.ToyProfit(300000f, 50000f, 200000f, 50000f), 1e-1f);
        }
    }
}
