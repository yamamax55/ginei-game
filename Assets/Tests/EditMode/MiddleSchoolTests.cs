using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 中学校＝前期中等教育（#155-157 の土台・<see cref="MiddleSchoolRules"/>）を固定する：進学率→母数倍率、
    /// 中学校卒の年産、質→素質上乗せ（高校より小）、教育チェーン（中学校×高校）の複利。
    /// </summary>
    public class MiddleSchoolTests
    {
        [Test]
        public void EducationFactor_IsEnrollmentClamped()
        {
            Assert.AreEqual(0.8f, MiddleSchoolRules.EducationFactor(0.8f), 1e-4f);
            Assert.AreEqual(0f, MiddleSchoolRules.EducationFactor(-1f), 1e-4f);
            Assert.AreEqual(1f, MiddleSchoolRules.EducationFactor(3f), 1e-4f);
        }

        [Test]
        public void AnnualGraduates_ScalesWithYouthAndEnrollment()
        {
            var p = new Province(1, "民主", 100f);
            p.demographics = new Population(30f, 60f, 10f);
            float aging = DemographicsRules.VitalRates.Default.youthAging;
            Assert.AreEqual(30f * aging * 0.8f, MiddleSchoolRules.AnnualGraduates(p, 0.8f), 1e-3f);
            Assert.AreEqual(0f, MiddleSchoolRules.AnnualGraduates(null, 0.8f), 1e-4f);
        }

        [Test]
        public void TalentBonus_SmallerThanHighSchool()
        {
            Assert.AreEqual(MiddleSchoolRules.MaxTalentBonus, MiddleSchoolRules.TalentBonus(1f), 1e-4f);
            // 中学校の寄与は高校より小さい（より基礎的）
            Assert.Less(MiddleSchoolRules.TalentBonus(1f), HighSchoolRules.TalentBonus(1f));
        }

        [Test]
        public void EducationChain_Compounds()
        {
            // 進学率は中学校×高校で複利（裾野が狭いと上級教育の母数が細る）
            float chain = MiddleSchoolRules.EducationFactor(0.8f) * HighSchoolRules.EducationFactor(0.5f);
            Assert.AreEqual(0.4f, chain, 1e-4f);

            // 実効質は段階的に積む（学校質→高校→中学校）
            float baseQ = 0.5f;
            float afterHigh = HighSchoolRules.EffectiveIntakeQuality(baseQ, 1f); // +0.15
            float afterMiddle = MiddleSchoolRules.EffectiveIntakeQuality(afterHigh, 1f); // +0.10
            Assert.AreEqual(0.5f + 0.15f + 0.10f, afterMiddle, 1e-4f);
            Assert.Greater(afterMiddle, afterHigh);
        }
    }
}
