using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>サービス会社（#2024・<see cref="ServiceRules"/>）：稼働時間と収益(SVC-1)・人件費利益(SVC-2)・稼働率(SVC-3)。</summary>
    public class ServiceTests
    {
        [Test]
        public void BillableHours_AndRevenue()
        {
            Assert.AreEqual(16000f, ServiceRules.BillableHours(10f, 2000f, 0.8f), 1e-1f); // 10人×2000h×稼働0.8
            Assert.AreEqual(800000f, ServiceRules.ServiceRevenue(16000f, 50f), 1e-0f);
        }

        [Test]
        public void Profit_AndUtilization()
        {
            Assert.AreEqual(200000f, ServiceRules.LaborProfit(800000f, 500000f, 100000f), 1e-0f); // 人件費が主コスト
            Assert.AreEqual(0.8f, ServiceRules.UtilizationRate(16000f, 20000f), 1e-4f);
        }
    }
}
