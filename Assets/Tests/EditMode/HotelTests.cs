using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ホテル（#2025・<see cref="HotelRules"/>）：稼働率(HTL-1)・RevPAR(HTL-2)・客室収益(HTL-3)・利益(HTL-4)。</summary>
    public class HotelTests
    {
        [Test]
        public void Occupancy_AndRevPar()
        {
            Assert.AreEqual(0.8f, HotelRules.OccupancyRate(80f, 100f), 1e-4f);
            Assert.AreEqual(8000f, HotelRules.RevPar(0.8f, 10000f), 1e-1f); // 稼働×ADR
            Assert.AreEqual(800000f, HotelRules.RoomRevenue(100, 8000f), 1e-1f);
        }

        [Test]
        public void Profit()
        {
            Assert.AreEqual(300000f, HotelRules.HotelProfit(800000f, 500000f), 1e-1f); // 高固定費レバレッジ
        }
    }
}
