using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>フィットネス・レジャー（#2025・<see cref="FitnessClubRules"/>）：会費(FIT-1)・解約(FIT-2)・稼働率(FIT-3)・利益(FIT-4)。</summary>
    public class FitnessClubTests
    {
        [Test]
        public void Membership_AndChurn()
        {
            Assert.AreEqual(40000000f, FitnessClubRules.MembershipRevenue(5000, 8000f), 1e0f);
            Assert.AreEqual(200f, FitnessClubRules.ChurnedMembers(5000, 0.04f), 1e-2f);
        }

        [Test]
        public void Utilization_AndProfit()
        {
            Assert.AreEqual(0.3f, FitnessClubRules.CapacityUtilization(300f, 1000f), 1e-4f); // 低稼働＝幽霊会員
            // 会費4000万 − 変動費100×来館9万 − 固定3000万 = 100万
            Assert.AreEqual(1000000f, FitnessClubRules.FitnessProfit(40000000f, 100f, 90000f, 30000000f), 1e0f);
        }
    }
}
