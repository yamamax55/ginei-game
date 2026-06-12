using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ドラッグストア（#2025・<see cref="DrugstoreRules"/>）：調剤(DRG-1)・混合粗利(DRG-2)・集客(DRG-3)・利益(DRG-4)。</summary>
    public class DrugstoreTests
    {
        [Test]
        public void Dispensing_AndBlendedGross()
        {
            Assert.AreEqual(50000f, DrugstoreRules.DispensingRevenue(100, 500f), 1e-1f);
            // 1000×(0.4×0.4 + 0.6×0.2) = 1000×0.28 = 280
            Assert.AreEqual(280f, DrugstoreRules.BlendedGrossProfit(1000f, 0.4f, 0.4f, 0.2f), 1e-3f);
        }

        [Test]
        public void LossLeader_AndProfit()
        {
            Assert.AreEqual(1100f, DrugstoreRules.FoodLossLeaderTraffic(1000f, 0.2f, 0.5f), 1e-3f); // 食品安売りで集客
            Assert.AreEqual(200f, DrugstoreRules.DrugstoreProfit(500f, 300f), 1e-3f);
        }
    }
}
