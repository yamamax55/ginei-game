using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>電力小売（#2025・<see cref="PowerRetailRules"/>）：卸調達(PWR-1)・燃調費(PWR-2)・マージン(PWR-3)・利益(PWR-4)。</summary>
    public class PowerRetailTests
    {
        [Test]
        public void Procurement_AndFuelAdjustment()
        {
            Assert.AreEqual(20000f, PowerRetailRules.WholesaleProcurementCost(1000f, 20f), 1e-1f);
            Assert.AreEqual(20f, PowerRetailRules.FuelAdjustedPrice(15f, 5f, 1.0f), 1e-3f); // 燃料高を転嫁
        }

        [Test]
        public void Margin_AndProfit()
        {
            Assert.AreEqual(5f, PowerRetailRules.RetailMargin(25f, 20f), 1e-3f);
            Assert.AreEqual(3000f, PowerRetailRules.PowerRetailProfit(25000f, 20000f, 2000f), 1e-1f);
        }
    }
}
