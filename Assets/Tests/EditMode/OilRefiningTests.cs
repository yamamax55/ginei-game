using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>石油精製（#2024・<see cref="OilRefiningRules"/>）：クラックスプレッド(OIL-1)・精製(OIL-2)・製油所利益(OIL-3)・在庫評価損益(OIL-4)。</summary>
    public class OilRefiningTests
    {
        [Test]
        public void CrackSpread_RefineAndProfit()
        {
            Assert.AreEqual(30f, OilRefiningRules.CrackSpread(100f, 70f), 1e-3f); // 製品−原油
            Assert.AreEqual(900f, OilRefiningRules.RefinedOutput(1000f, 0.9f), 1e-3f);
            Assert.AreEqual(7000f, OilRefiningRules.RefineryProfit(900f, 30f, 20000f), 1e-1f); // 27000−固定20000
        }

        [Test]
        public void InventoryGainLoss()
        {
            Assert.AreEqual(500f, OilRefiningRules.InventoryGainLoss(5000f, 0.1f), 1e-1f);  // 原油高で評価益
            Assert.AreEqual(-500f, OilRefiningRules.InventoryGainLoss(5000f, -0.1f), 1e-1f); // 原油安で評価損
        }
    }
}
