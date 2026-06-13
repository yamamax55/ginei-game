using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>電気機器/半導体（#2024・<see cref="ElectronicsRules"/>）：シリコンサイクル(ELC-1)・世代優位(ELC-2)・価格下落(ELC-3)・循環利益(ELC-4)。</summary>
    public class ElectronicsTests
    {
        [Test]
        public void SiliconCycle_PriceAndGeneration()
        {
            Assert.AreEqual(120f, ElectronicsRules.ChipPrice(1200f, 1000f, 100f), 1e-3f); // 需給で価格上下
            Assert.AreEqual(2f, ElectronicsRules.GenerationAdvantage(5f, 3f), 1e-3f);     // 2世代先行
            Assert.AreEqual(70f, ElectronicsRules.PriceErosion(100f, 6f, 0.05f, 0.3f), 1e-3f); // 半年で価格下落
        }

        [Test]
        public void CycleProfit_FeastOrFamine()
        {
            // 好況局面：数量100×(価格120−単価80)−固定2000 = 2000
            Assert.AreEqual(2000f, ElectronicsRules.SiliconCycleProfit(120f, 80f, 100f, 2000f), 1e-1f);
            // 不況局面：価格が単価割れで大赤字
            Assert.AreEqual(-4000f, ElectronicsRules.SiliconCycleProfit(60f, 80f, 100f, 2000f), 1e-1f);
        }
    }
}
