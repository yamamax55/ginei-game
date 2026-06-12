using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>化学メーカー（#2024・<see cref="ChemicalRules"/>）：稼働率(CHM-1)・市況スプレッド(CHM-2)・汎用/スペシャリティ(CHM-3)・スケール(CHM-4)。</summary>
    public class ChemicalTests
    {
        [Test]
        public void PlantUtilization_OperatingLeverage()
        {
            Assert.AreEqual(600f, ChemicalRules.PlantProfit(1000f, 0.8f, 2f, 1000f), 1e-3f); // 能力1000×稼働0.8×2−固定1000
            Assert.AreEqual(0.5f, ChemicalRules.BreakEvenUtilization(1000f, 1000f, 2f), 1e-4f); // 損益分岐稼働率
        }

        [Test]
        public void Spread_AndSpecialty()
        {
            Assert.AreEqual(40f, ChemicalRules.Spread(100f, 60f), 1e-3f);
            Assert.AreEqual(20f, ChemicalRules.EffectiveMargin(true, 5f, 20f), 1e-3f);  // スペシャリティ＝高マージン
            Assert.AreEqual(5f, ChemicalRules.EffectiveMargin(false, 5f, 20f), 1e-3f);  // 汎用＝薄利
        }

        [Test]
        public void ScaleMerit()
        {
            Assert.AreEqual(9f, ChemicalRules.ScaleUnitCost(10f, 200f, 100f, 0.2f), 1e-3f); // 2倍能力で単価↓
            Assert.AreEqual(10f, ChemicalRules.ScaleUnitCost(10f, 100f, 100f, 0.2f), 1e-3f); // 基準＝据え置き
        }
    }
}
