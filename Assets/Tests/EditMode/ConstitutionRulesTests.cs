using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 立憲主義（#170）を固定する：憲法的拘束（権力分立／法の支配）で基準権力が逓減し（基準値は非破壊）、
    /// 権利保護は正統性を底上げ、法の支配＋権利保護がしきい値以上で立憲君主制。値は徹底して clamp。
    /// </summary>
    public class ConstitutionRulesTests
    {
        private static readonly ConstitutionParams P = ConstitutionParams.Default; // sep0.4 / law0.3 / minRatio0.25 / rights0.3

        // --- ConstrainedAuthority ---

        [Test]
        public void ConstrainedAuthority_NoConstraint_ReturnsBaseUnchanged()
        {
            // 制約ゼロ（独裁集権）＝基準権力そのまま（基準値非破壊）
            var c = new Constitution(0f, 0f, 0f);
            Assert.AreEqual(100f, ConstitutionRules.ConstrainedAuthority(100f, c, P), 1e-3f);
        }

        [Test]
        public void ConstrainedAuthority_PartialConstraint_Reduces()
        {
            // 権力分立0.5・法の支配0.5 → reduction = 0.4*0.5 + 0.3*0.5 = 0.35
            // ratio = Lerp(1, 0.25, 0.35) = 1 - 0.75*0.35 = 0.7375
            var c = new Constitution(0.5f, 0.2f, 0.5f);
            Assert.AreEqual(73.75f, ConstitutionRules.ConstrainedAuthority(100f, c, P), 1e-2f);
        }

        [Test]
        public void ConstrainedAuthority_FullConstraint_ClampsToMinRatio()
        {
            // 分立1・法治1 → reduction = 0.4+0.3 = 0.7（clamp01で据え置き）
            // ratio = Lerp(1, 0.25, 0.7) = 1 - 0.75*0.7 = 0.475
            var c = new Constitution(1f, 0.5f, 1f);
            Assert.AreEqual(47.5f, ConstitutionRules.ConstrainedAuthority(100f, c, P), 1e-2f);

            // reduction が 1 を超えても下限比率 minAuthorityRatio で頭打ち（過剰逓減しない）
            var prm = new ConstitutionParams(1f, 1f, 0.25f, 0.3f); // 分立1+法治1=2 → clamp01=1
            Assert.AreEqual(25f, ConstitutionRules.ConstrainedAuthority(100f, c, prm), 1e-3f);
        }

        [Test]
        public void ConstrainedAuthority_NullConstitution_ReturnsBase()
        {
            Assert.AreEqual(50f, ConstitutionRules.ConstrainedAuthority(50f, null, P), 1e-4f);
        }

        // --- RightsLegitimacy ---

        [Test]
        public void RightsLegitimacy_ScalesWithRightsProtection()
        {
            // 権利保護0 → ボーナス0、権利保護1 → rightsLegitimacyGain(0.3)
            Assert.AreEqual(0f, ConstitutionRules.RightsLegitimacy(new Constitution(0f, 0f, 0f), P), 1e-4f);
            Assert.AreEqual(0.15f, ConstitutionRules.RightsLegitimacy(new Constitution(0f, 0.5f, 0f), P), 1e-4f);
            Assert.AreEqual(0.3f, ConstitutionRules.RightsLegitimacy(new Constitution(0f, 1f, 0f), P), 1e-4f);
            Assert.AreEqual(0f, ConstitutionRules.RightsLegitimacy(null, P), 1e-4f);
        }

        // --- IsConstitutionalMonarchy ---

        [Test]
        public void IsConstitutionalMonarchy_RequiresBothLawAndRights()
        {
            // 法の支配・権利保護ともに既定しきい値0.5以上で立憲君主制
            Assert.IsTrue(ConstitutionRules.IsConstitutionalMonarchy(new Constitution(0f, 0.6f, 0.7f)));
            // 権利保護が足りない＝非該当（人治の専制君主）
            Assert.IsFalse(ConstitutionRules.IsConstitutionalMonarchy(new Constitution(0f, 0.2f, 0.9f)));
            // 法の支配が足りない＝非該当
            Assert.IsFalse(ConstitutionRules.IsConstitutionalMonarchy(new Constitution(0f, 0.9f, 0.2f)));
            // 境界値（ちょうどしきい値）は含む
            Assert.IsTrue(ConstitutionRules.IsConstitutionalMonarchy(new Constitution(0f, 0.5f, 0.5f), 0.5f));
            Assert.IsFalse(ConstitutionRules.IsConstitutionalMonarchy(null));
        }

        // --- データ側の clamp ---

        [Test]
        public void Constitution_Ctor_ClampsFieldsToUnit()
        {
            var c = new Constitution(2f, -1f, 5f);
            Assert.AreEqual(1f, c.powerSeparation, 1e-4f);
            Assert.AreEqual(0f, c.rightsProtection, 1e-4f);
            Assert.AreEqual(1f, c.ruleOfLaw, 1e-4f);
        }
    }
}
