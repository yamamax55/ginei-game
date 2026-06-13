using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>SaaS（#2025・<see cref="SaasRules"/>）：MRR(SAAS-1)・ARR(SAAS-2)・チャーン(SAAS-3)・NRR(SAAS-4)。</summary>
    public class SaasTests
    {
        [Test]
        public void Mrr_AndArr()
        {
            Assert.AreEqual(50000f, SaasRules.MonthlyRecurringRevenue(1000, 50f), 1e-1f);
            Assert.AreEqual(600000f, SaasRules.AnnualRecurringRevenue(50000f), 1e-1f);
        }

        [Test]
        public void Churn_AndNrr()
        {
            Assert.AreEqual(2500f, SaasRules.ChurnedRevenue(50000f, 0.05f), 1e-1f);
            // (1000+200-100)/1000 = 1.1 ＞ 1.0 ＝ 純拡大
            Assert.AreEqual(1.1f, SaasRules.NetRevenueRetention(1000f, 200f, 100f), 1e-4f);
        }
    }
}
