using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>鉄鋼メーカー（#2024・<see cref="SteelRules"/>）：高炉(STL-1)・スプレッド(STL-2)・装置産業利益(STL-3)・電炉(STL-4)・稼働率(STL-5)。</summary>
    public class SteelTests
    {
        [Test]
        public void BlastFurnace_OutputSpreadProfit()
        {
            Assert.AreEqual(700f, SteelRules.CrudeSteelOutput(1000f, 0.7f), 1e-3f); // 鉄鉱石→粗鋼
            Assert.AreEqual(30f, SteelRules.SteelSpread(80f, 50f), 1e-3f);
            Assert.AreEqual(16000f, SteelRules.BlastFurnaceProfit(700f, 30f, 5000f), 1e-3f); // 21000−固定5000
        }

        [Test]
        public void ElectricFurnace_AndUtilization()
        {
            Assert.AreEqual(450f, SteelRules.ElectricFurnaceOutput(500f, 0.9f), 1e-3f); // スクラップ循環
            Assert.AreEqual(0.7f, SteelRules.CapacityUtilization(700f, 1000f), 1e-3f);
        }
    }
}
