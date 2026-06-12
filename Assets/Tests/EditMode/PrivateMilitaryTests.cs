using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>PMC（#2025・<see cref="PrivateMilitaryRules"/>）：契約収入(PMC-1)・戦力供給(PMC-2)・戦死補償(PMC-3)・利益(PMC-4)。</summary>
    public class PrivateMilitaryTests
    {
        [Test]
        public void Contract_AndStrengthSupply()
        {
            Assert.AreEqual(300000f, PrivateMilitaryRules.ContractRevenue(1000f, 10f, 30), 1e-1f); // 戦力×日額×日数
            Assert.AreEqual(1000f, PrivateMilitaryRules.MercenaryStrengthSupplied(5, 200f), 1e-1f); // FleetPool#148へ供給
        }

        [Test]
        public void Casualty_AndProfit()
        {
            Assert.AreEqual(250000f, PrivateMilitaryRules.CasualtyCompensation(50, 5000f), 1e-1f);
            Assert.AreEqual(100000f, PrivateMilitaryRules.PmcProfit(300000f, 120000f, 30000f, 50000f), 1e-1f);
        }
    }
}
