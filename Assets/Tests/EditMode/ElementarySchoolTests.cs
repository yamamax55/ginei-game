using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 小学校＝初等教育（#155-157 の土台の根・<see cref="ElementarySchoolRules"/>）を固定する：就学率→母数倍率、
    /// 小学校卒の年産、質→素質上乗せ（最小＝中学校より小）、教育チェーン（小×中×高）の複利の根。
    /// </summary>
    public class ElementarySchoolTests
    {
        [Test]
        public void EducationFactor_IsEnrollmentClamped()
        {
            Assert.AreEqual(0.95f, ElementarySchoolRules.EducationFactor(0.95f), 1e-4f);
            Assert.AreEqual(0f, ElementarySchoolRules.EducationFactor(-1f), 1e-4f);
            Assert.AreEqual(1f, ElementarySchoolRules.EducationFactor(5f), 1e-4f);
        }

        [Test]
        public void AnnualGraduates_ScalesWithYouthAndEnrollment()
        {
            var p = new Province(1, "民主", 100f);
            p.demographics = new Population(30f, 60f, 10f);
            float aging = DemographicsRules.VitalRates.Default.youthAging;
            Assert.AreEqual(30f * aging * 0.9f, ElementarySchoolRules.AnnualGraduates(p, 0.9f), 1e-3f);
            Assert.AreEqual(0f, ElementarySchoolRules.AnnualGraduates(null, 0.9f), 1e-4f);
        }

        [Test]
        public void TalentBonus_SmallestOfTheTiers()
        {
            Assert.AreEqual(ElementarySchoolRules.MaxTalentBonus, ElementarySchoolRules.TalentBonus(1f), 1e-4f);
            // 初等教育の寄与は中学校・高校より小さい（最も基礎的）
            Assert.Less(ElementarySchoolRules.TalentBonus(1f), MiddleSchoolRules.TalentBonus(1f));
            Assert.Less(ElementarySchoolRules.TalentBonus(1f), HighSchoolRules.TalentBonus(1f));
        }

        [Test]
        public void EducationChain_CompoundsFromRoot()
        {
            // 進学率は小×中×高で複利（裾野＝初等が細ると全体が細る）
            float chain = ElementarySchoolRules.EducationFactor(0.9f)
                        * MiddleSchoolRules.EducationFactor(0.8f)
                        * HighSchoolRules.EducationFactor(0.5f);
            Assert.AreEqual(0.9f * 0.8f * 0.5f, chain, 1e-4f);

            // 実効質は段階的に積む（学校→高校0.15→中学0.10→小学0.05）
            float q = 0.5f;
            q = HighSchoolRules.EffectiveIntakeQuality(q, 1f);
            q = MiddleSchoolRules.EffectiveIntakeQuality(q, 1f);
            q = ElementarySchoolRules.EffectiveIntakeQuality(q, 1f);
            Assert.AreEqual(0.5f + 0.15f + 0.10f + 0.05f, q, 1e-4f);
        }
    }
}
