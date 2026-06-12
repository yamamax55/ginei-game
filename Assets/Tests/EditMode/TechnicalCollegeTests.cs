using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 高専＝高等専門学校（#157 LIFE-7・<see cref="TechnicalCollegeRules"/>）を固定する：定員と候補で輩出数、
    /// 実技重視の技術者（テクノクラート・席次なし・若い卒業年）、技術力順、名門ほど良才。
    /// </summary>
    public class TechnicalCollegeTests
    {
        private static TechnicalCollege College(float quality = 0.6f, int capacity = 6)
            => new TechnicalCollege(schoolId: 14, faction: Faction.帝国, name: "高専", capacity: capacity, quality: quality);

        [Test]
        public void Intake_LimitedByPoolAndCapacity()
        {
            var c = College(capacity: 6);
            Assert.AreEqual(6, TechnicalCollegeRules.Intake(c, 1000f));
            int expectedSmall = Mathf.FloorToInt(20f * UniversityRules.CandidateFraction);
            Assert.AreEqual(expectedSmall, TechnicalCollegeRules.Intake(c, 20f));
            Assert.AreEqual(0, TechnicalCollegeRules.Intake(c, 0f));
        }

        [Test]
        public void Graduates_PracticalTechnicians_NoExamRank_Younger()
        {
            var c = College(quality: 0.6f);
            var grads = TechnicalCollegeRules.GraduateCohort(c, 800, 5, 100, i => (i * 0.17f) % 1f);
            Assert.AreEqual(5, grads.Count);
            foreach (var p in grads)
            {
                Assert.AreEqual(PersonRole.文民, p.role);
                Assert.AreEqual(c.schoolId, p.schoolId);
                Assert.AreEqual(0, p.examRank);                       // テクノクラート＝科挙の席次なし
                Assert.AreEqual(800 - TechnicalCollegeRules.GraduationAge, p.birthYear); // 5年制＝若い
                // 実技（技術/生産）＞研究＝実務寄り、技才＞文才
                Assert.GreaterOrEqual(p.engineering, p.research);
                Assert.Greater(p.TechnicalAptitude, p.CivilAptitude);
            }
            // 技術力の降順
            for (int i = 1; i < grads.Count; i++)
                Assert.GreaterOrEqual(grads[i - 1].TechnicalAptitude, grads[i].TechnicalAptitude);
        }

        [Test]
        public void HigherQuality_ProducesBetterTechnicians()
        {
            var elite = College(quality: 1f);
            var weak = College(quality: 0f);
            var ge = TechnicalCollegeRules.GraduateCohort(elite, 800, 1, 1, _ => 0.5f);
            var gw = TechnicalCollegeRules.GraduateCohort(weak, 800, 1, 1, _ => 0.5f);
            Assert.Greater(ge[0].TechnicalAptitude, gw[0].TechnicalAptitude);
        }

        [Test]
        public void ZeroIntakeAndNull_Empty()
        {
            Assert.AreEqual(0, TechnicalCollegeRules.GraduateCohort(College(), 800, 0, 1, _ => 0.5f).Count);
            Assert.AreEqual(0, TechnicalCollegeRules.GraduateCohort(null, 800, 5, 1, _ => 0.5f).Count);
        }
    }
}
