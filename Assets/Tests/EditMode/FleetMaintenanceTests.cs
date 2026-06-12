using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>艦艇整備・補給廠（#2025・<see cref="FleetMaintenanceRules"/>）：整備収入(MRO-1)・回転能力(MRO-2)・部品マークアップ(MRO-3)・利益(MRO-4)。</summary>
    public class FleetMaintenanceTests
    {
        [Test]
        public void Revenue_AndTurnaround()
        {
            Assert.AreEqual(10000000f, FleetMaintenanceRules.MaintenanceRevenue(50, 200000f), 1e1f);
            Assert.AreEqual(50f, FleetMaintenanceRules.TurnaroundCapacity(5, 10f, 100f), 1e-3f); // 5船渠×(100日/10日)
        }

        [Test]
        public void Parts_AndProfit()
        {
            Assert.AreEqual(1200000f, FleetMaintenanceRules.PartsMarkupRevenue(3000000f, 0.4f), 1e0f);
            Assert.AreEqual(3200000f, FleetMaintenanceRules.MroProfit(10000000f, 1200000f, 6000000f, 2000000f), 1e1f);
        }
    }
}
