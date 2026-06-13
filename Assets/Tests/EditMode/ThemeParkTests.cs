using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>テーマパーク（#2025・<see cref="ThemeParkRules"/>）：入園料(PARK-1)・園内消費(PARK-2)・稼働率(PARK-3)・利益(PARK-4)。</summary>
    public class ThemeParkTests
    {
        [Test]
        public void Gate_AndInPark()
        {
            Assert.AreEqual(80000000f, ThemeParkRules.GateRevenue(10000, 8000f), 1e1f);
            Assert.AreEqual(50000000f, ThemeParkRules.InParkSpend(10000, 5000f), 1e1f); // 飲食・グッズ
        }

        [Test]
        public void Utilization_AndProfit()
        {
            Assert.AreEqual(0.8f, ThemeParkRules.CapacityUtilization(10000, 12500f), 1e-4f);
            Assert.AreEqual(30000000f, ThemeParkRules.ThemeParkProfit(80000000f, 50000000f, 60000000f, 40000000f), 1e1f);
        }
    }
}
