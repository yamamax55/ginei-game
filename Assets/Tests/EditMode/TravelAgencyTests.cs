using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>宇宙旅行代理店（#2025・<see cref="TravelAgencyRules"/>）：送客手数料(TRVL-1)・パッケージマージン(TRVL-2)・収益(TRVL-3)・利益(TRVL-4)。</summary>
    public class TravelAgencyTests
    {
        [Test]
        public void Commission_AndPackageMargin()
        {
            Assert.AreEqual(10000f, TravelAgencyRules.BookingCommission(100000f, 0.1f), 1e-1f);
            Assert.AreEqual(0.25f, TravelAgencyRules.DynamicPackageMargin(100f, 75f), 1e-4f); // 交通+宿を組んで付加価値
        }

        [Test]
        public void Revenue_AndProfit()
        {
            Assert.AreEqual(10000f, TravelAgencyRules.PackageRevenue(100, 100f), 1e-1f);
            Assert.AreEqual(7500f, TravelAgencyRules.TravelAgencyProfit(10000f, 2500f, 5000f), 1e-1f); // 手数料1万+粗利2500−固定5千
        }
    }
}
