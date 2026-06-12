using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>医療・介護（#2025・<see cref="HealthcareRules"/>）：公定収益(HEAL-1)・稼働率(HEAL-2)・人手不足(HEAL-3)・利益(HEAL-4)。</summary>
    public class HealthcareTests
    {
        [Test]
        public void Regulated_AndOccupancy()
        {
            Assert.AreEqual(100000f, HealthcareRules.RegulatedRevenue(1000f, 100f), 1e-1f); // 公定単価
            Assert.AreEqual(0.9f, HealthcareRules.OccupancyRate(90f, 100f), 1e-4f);
        }

        [Test]
        public void Shortage_AndProfit()
        {
            Assert.AreEqual(0.8f, HealthcareRules.StaffShortageFactor(10f, 8f), 1e-4f); // 人が足りず供給制約
            Assert.AreEqual(1f, HealthcareRules.StaffShortageFactor(0f, 5f), 1e-4f);    // 必要0は制約なし
            Assert.AreEqual(10000f, HealthcareRules.HealthcareProfit(100000f, 70000f, 20000f), 1e-1f);
        }
    }
}
