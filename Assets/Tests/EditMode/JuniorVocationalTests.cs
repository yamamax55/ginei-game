using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 短大（<see cref="JuniorCollegeRules"/>・行政中堅）と専門学校（<see cref="VocationalSchoolRules"/>・実務specialist）を固定する：
    /// 定員と候補で輩出数、文民・有力者（席次なし）・若い卒業・中堅天井、短大=文才寄り/専門=実務寄り。
    /// </summary>
    public class JuniorVocationalTests
    {
        private static JuniorCollege JC(float q = 0.6f, int cap = 6)
            => new JuniorCollege(16, Faction.帝国, "短大", cap, q);

        private static VocationalSchool VS(float q = 0.6f, int cap = 6)
            => new VocationalSchool(18, Faction.帝国, "専門", cap, q);

        [Test]
        public void Intake_LimitedByPoolAndCapacity()
        {
            Assert.AreEqual(6, JuniorCollegeRules.Intake(JC(cap: 6), 1000f));
            Assert.AreEqual(0, JuniorCollegeRules.Intake(JC(), 0f));
            Assert.AreEqual(6, VocationalSchoolRules.Intake(VS(cap: 6), 1000f));
            int small = Mathf.FloorToInt(20f * UniversityRules.CandidateFraction);
            Assert.AreEqual(small, VocationalSchoolRules.Intake(VS(), 20f));
        }

        [Test]
        public void JuniorCollege_AdministrativeMidTier_NoRank()
        {
            var grads = JuniorCollegeRules.GraduateCohort(JC(0.6f), 800, 5, 100, i => (i * 0.17f) % 1f);
            Assert.AreEqual(5, grads.Count);
            foreach (var p in grads)
            {
                Assert.AreEqual(PersonRole.文民, p.role);
                Assert.AreEqual(16, p.schoolId);
                Assert.AreEqual(0, p.examRank);                     // 有力者＝科挙の席次なし
                Assert.AreEqual(0, p.hammockNumber);                // 士官学校でもない
                Assert.AreEqual(800 - JuniorCollegeRules.GraduationAge, p.birthYear);
                Assert.Greater(p.CivilAptitude, p.TechnicalAptitude); // 文才（行政）寄り
                Assert.LessOrEqual(p.operation, JuniorCollegeRules.MidStatCeil); // 中堅天井
            }
            for (int i = 1; i < grads.Count; i++)
                Assert.GreaterOrEqual(grads[i - 1].CivilAptitude, grads[i].CivilAptitude);
        }

        [Test]
        public void VocationalSchool_PracticalMidTier()
        {
            var grads = VocationalSchoolRules.GraduateCohort(VS(0.6f), 800, 5, 100, i => (i * 0.23f) % 1f);
            Assert.AreEqual(5, grads.Count);
            foreach (var p in grads)
            {
                Assert.AreEqual(PersonRole.文民, p.role);
                Assert.AreEqual(0, p.examRank);
                Assert.AreEqual(800 - VocationalSchoolRules.GraduationAge, p.birthYear);
                Assert.Greater(p.TechnicalAptitude, p.CivilAptitude); // 実務寄り
                Assert.GreaterOrEqual(p.production, p.research);       // 計画/生産＞研究
                Assert.LessOrEqual(p.production, JuniorCollegeRules.MidStatCeil); // 中堅天井（大学78より低い）
            }
        }

        [Test]
        public void MidTier_LowerCeilingThanUniversity()
        {
            // 同じ最高素質でも短大の天井(65)は大学(78)より低い＝credential の階層
            Assert.Less(JuniorCollegeRules.MidStatCeil, UniversityRules.StatCeil);
            Assert.AreEqual(JuniorCollegeRules.MidStatCeil, JuniorCollegeRules.StatFor(1f));
        }

        [Test]
        public void NullAndZero_Empty()
        {
            Assert.AreEqual(0, JuniorCollegeRules.GraduateCohort(null, 800, 5, 1, _ => 0.5f).Count);
            Assert.AreEqual(0, VocationalSchoolRules.GraduateCohort(VS(), 800, 0, 1, _ => 0.5f).Count);
        }
    }
}
