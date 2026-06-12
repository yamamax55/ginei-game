using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>外食（#2025・<see cref="RestaurantRules"/>）：FLコスト(REST-1)・FL比率(REST-2)・店舗売上(REST-3)・利益(REST-4)。</summary>
    public class RestaurantTests
    {
        [Test]
        public void Fl_AndSales()
        {
            Assert.AreEqual(500f, RestaurantRules.FlCost(300f, 200f), 1e-3f);
            Assert.AreEqual(0.5f, RestaurantRules.FlRatio(300f, 200f, 1000f), 1e-4f);
            Assert.AreEqual(150000f, RestaurantRules.StoreSales(50, 3f, 1000f), 1e-1f); // 50席×3回転×単価1000
        }

        [Test]
        public void Profit()
        {
            Assert.AreEqual(200f, RestaurantRules.RestaurantProfit(1000f, 500f, 300f), 1e-3f);
        }
    }
}
