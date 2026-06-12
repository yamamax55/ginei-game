using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 高校＝中等教育（#155-157 の土台・<see cref="HighSchoolRules"/>）を固定する：進学率→候補母数倍率、
    /// 高校卒の年産、質→素質上乗せ（上限）、実効教育質の底上げとクランプ。
    /// </summary>
    public class HighSchoolTests
    {
        [Test]
        public void EducationFactor_IsEnrollmentClamped()
        {
            Assert.AreEqual(0.6f, HighSchoolRules.EducationFactor(0.6f), 1e-4f);
            Assert.AreEqual(0f, HighSchoolRules.EducationFactor(-1f), 1e-4f);   // 下限0
            Assert.AreEqual(1f, HighSchoolRules.EducationFactor(2f), 1e-4f);    // 上限1
            // 進学率が高いほど候補が増える（単調）
            Assert.Greater(HighSchoolRules.EducationFactor(0.8f), HighSchoolRules.EducationFactor(0.4f));
        }

        [Test]
        public void AnnualGraduates_ScalesWithYouthAndEnrollment()
        {
            var p = new Province(1, "民主", 100f);
            p.demographics = new Population(30f, 60f, 10f); // youth=30
            float aging = DemographicsRules.VitalRates.Default.youthAging;
            Assert.AreEqual(30f * aging * 0.6f, HighSchoolRules.AnnualGraduates(p, 0.6f), 1e-3f);
            // 進学率0なら卒業0
            Assert.AreEqual(0f, HighSchoolRules.AnnualGraduates(p, 0f), 1e-4f);
            // null 安全
            Assert.AreEqual(0f, HighSchoolRules.AnnualGraduates(null, 0.6f), 1e-4f);
        }

        [Test]
        public void TalentBonus_MonotonicBounded()
        {
            Assert.AreEqual(0f, HighSchoolRules.TalentBonus(0f), 1e-4f);
            Assert.AreEqual(HighSchoolRules.MaxTalentBonus, HighSchoolRules.TalentBonus(1f), 1e-4f);
            Assert.Greater(HighSchoolRules.TalentBonus(0.8f), HighSchoolRules.TalentBonus(0.3f));
        }

        [Test]
        public void EffectiveIntakeQuality_BoostsAndClamps()
        {
            // 良い高校は同じ学校でも入ってくる人材を良くする
            Assert.Greater(HighSchoolRules.EffectiveIntakeQuality(0.5f, 1f),
                           HighSchoolRules.EffectiveIntakeQuality(0.5f, 0f));
            Assert.AreEqual(0.5f, HighSchoolRules.EffectiveIntakeQuality(0.5f, 0f), 1e-4f); // 高校質0なら据え置き
            // 上限1でクランプ
            Assert.AreEqual(1f, HighSchoolRules.EffectiveIntakeQuality(1f, 1f), 1e-4f);
        }
    }
}
