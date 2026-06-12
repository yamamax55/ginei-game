using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>衛星通信（#2025・<see cref="SatelliteCommsRules"/>）：中継器リース(SAT-1)・覆域(SAT-2)・データ通信(SAT-3)・利益(SAT-4)。</summary>
    public class SatelliteCommsTests
    {
        [Test]
        public void Lease_AndCoverage()
        {
            Assert.AreEqual(10000000f, SatelliteCommsRules.TransponderLeaseRevenue(50, 200000f), 1e1f);
            Assert.AreEqual(0.75f, SatelliteCommsRules.ConstellationCoverage(50, 0.015f, 1.0f), 1e-3f); // 50基×0.015
            Assert.AreEqual(1.0f, SatelliteCommsRules.ConstellationCoverage(100, 0.015f, 1.0f), 1e-3f); // 上限クランプ
        }

        [Test]
        public void Service_AndProfit()
        {
            Assert.AreEqual(5000000f, SatelliteCommsRules.DataServiceRevenue(100000, 50f), 1e1f);
            Assert.AreEqual(4000000f, SatelliteCommsRules.SatelliteProfit(10000000f, 5000000f, 8000000f, 3000000f), 1e1f);
        }
    }
}
