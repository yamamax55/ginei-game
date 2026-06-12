using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>コンビニ（#2025・<see cref="ConvenienceStoreRules"/>）：FCロイヤリティ(CVS-1)・本部収益(CVS-2)・廃棄ロス(CVS-3)・ドミナント(CVS-4)。</summary>
    public class ConvenienceStoreTests
    {
        [Test]
        public void Royalty_AndHeadquarters()
        {
            Assert.AreEqual(500f, ConvenienceStoreRules.FranchiseRoyalty(1000f, 0.5f), 1e-3f);
            Assert.AreEqual(50000f, ConvenienceStoreRules.HeadquartersRevenue(100, 500f), 1e-1f);
        }

        [Test]
        public void Waste_AndDominant()
        {
            Assert.AreEqual(5000f, ConvenienceStoreRules.WasteLoss(50, 100f), 1e-1f);
            Assert.AreEqual(0.75f, ConvenienceStoreRules.DominantShare(30, 40), 1e-4f); // 地域集中出店
        }
    }
}
