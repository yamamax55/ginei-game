using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>宇宙保険（#2025・<see cref="SpaceInsuranceRules"/>）：船体保険料(SINS-1)・戦争危険割増(SINS-2)・保険金(SINS-3)・引受損益(SINS-4)。</summary>
    public class SpaceInsuranceTests
    {
        [Test]
        public void Premium_AndWarRisk()
        {
            Assert.AreEqual(30000f, SpaceInsuranceRules.HullPremium(1000000f, 0.02f, 1.5f), 1e-1f); // 危険海域1.5倍
            Assert.AreEqual(50000f, SpaceInsuranceRules.WarRiskSurcharge(1000000f, 0.05f), 1e-1f); // 前線割増
        }

        [Test]
        public void Claim_AndUnderwriting()
        {
            Assert.AreEqual(600000f, SpaceInsuranceRules.ClaimPayout(0.6f, 1000000f), 1e-1f); // 分損6割
            Assert.AreEqual(10000f, SpaceInsuranceRules.UnderwritingResult(80000f, 60000f, 10000f), 1e-1f);
        }
    }
}
