using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>燃料小売（#2025・<see cref="FuelRetailRules"/>）：店頭マージン(FUEL-1)・燃料利益(FUEL-2)・油外収益(FUEL-3)・利益(FUEL-4)。</summary>
    public class FuelRetailTests
    {
        [Test]
        public void Margin_AndFuelProfit()
        {
            Assert.AreEqual(15f, FuelRetailRules.FuelGrossMargin(160f, 145f), 1e-3f); // 薄利
            Assert.AreEqual(1500000f, FuelRetailRules.FuelSalesProfit(100000f, 15f), 1e-1f);
        }

        [Test]
        public void Ancillary_AndStationProfit()
        {
            Assert.AreEqual(1300000f, FuelRetailRules.AncillaryRevenue(1000, 800f, 500000f), 1e-1f); // 洗車80万+店50万
            Assert.AreEqual(2000000f, FuelRetailRules.StationProfit(1500000f, 1300000f, 800000f), 1e-1f);
        }
    }
}
