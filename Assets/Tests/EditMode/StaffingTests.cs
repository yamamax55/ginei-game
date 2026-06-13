using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>人材派遣（#2025・<see cref="StaffingRules"/>）：スプレッド(STF-1)・マージン率(STF-2)・収益(STF-3)・利益(STF-4)。</summary>
    public class StaffingTests
    {
        [Test]
        public void Spread_AndMargin()
        {
            Assert.AreEqual(10f, StaffingRules.StaffingSpread(30f, 20f), 1e-3f);
            Assert.AreEqual(0.3333f, StaffingRules.StaffingMarginRate(30f, 20f), 1e-3f); // 中抜き率
        }

        [Test]
        public void Revenue_AndProfit()
        {
            Assert.AreEqual(480000f, StaffingRules.StaffingRevenue(100, 160f, 30f), 1e-1f); // 100人×160h×30
            Assert.AreEqual(110000f, StaffingRules.StaffingProfit(100, 160f, 10f, 50000f), 1e-1f); // 160000−固定5万
        }
    }
}
