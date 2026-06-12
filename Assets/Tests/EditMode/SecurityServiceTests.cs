using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>警備（#2025・<see cref="SecurityServiceRules"/>）：契約収益(SEC-1)・人件費(SEC-2)・利益(SEC-3)・人件費率(SEC-4)。</summary>
    public class SecurityServiceTests
    {
        [Test]
        public void Revenue_AndLabor()
        {
            Assert.AreEqual(30000f, SecurityServiceRules.ContractRevenue(100, 300f), 1e-1f);
            Assert.AreEqual(20000f, SecurityServiceRules.GuardLaborCost(80, 250f), 1e-1f);
        }

        [Test]
        public void Profit_AndLaborRatio()
        {
            Assert.AreEqual(5000f, SecurityServiceRules.SecurityServiceProfit(30000f, 20000f, 5000f), 1e-1f);
            Assert.AreEqual(0.6667f, SecurityServiceRules.LaborCostRatio(20000f, 30000f), 1e-3f); // 労働集約・低マージン
        }
    }
}
