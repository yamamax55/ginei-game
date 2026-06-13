using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>CDR-3 人材登用：在野発見（情報）・説得（思想差/厚遇/相性）・三顧の礼（反復累積）。</summary>
    public class RecruitmentRulesTests
    {
        [Test]
        public void DiscoveryChance_ScalesWithIntelligence()
        {
            Assert.AreEqual(0.3f, RecruitmentRules.DiscoveryChance(100f, 0.2f), 1e-4f);
            Assert.AreEqual(0.2f, RecruitmentRules.DiscoveryChance(50f, 0.2f), 1e-4f);
            Assert.AreEqual(0.1f, RecruitmentRules.DiscoveryChance(0f, 0.2f), 1e-4f);
        }

        [Test]
        public void PersuasionChance_HospitalityAffinityVsIdeology()
        {
            Assert.AreEqual(0.8f, RecruitmentRules.PersuasionChance(0f, 1f, 1f), 1e-4f);
            Assert.AreEqual(0f, RecruitmentRules.PersuasionChance(1f, 0f, 0f), 1e-4f);   // 思想隔絶＝靡かない
            Assert.AreEqual(0.3f, RecruitmentRules.PersuasionChance(0.5f, 0.5f, 0.5f), 1e-4f);

            Assert.IsTrue(RecruitmentRules.Persuade(0.3f, 0.2f));
            Assert.IsFalse(RecruitmentRules.Persuade(0.3f, 0.4f));
        }

        [Test]
        public void RepeatedPersuasion_SankoNoRei()
        {
            Assert.AreEqual(0.657f, RecruitmentRules.RepeatedPersuasionChance(0.3f, 3), 1e-3f); // 1-0.7^3
            Assert.AreEqual(0.5f, RecruitmentRules.RepeatedPersuasionChance(0.5f, 1), 1e-4f);
            Assert.AreEqual(0f, RecruitmentRules.RepeatedPersuasionChance(0.5f, 0), 1e-4f);
        }
    }
}
