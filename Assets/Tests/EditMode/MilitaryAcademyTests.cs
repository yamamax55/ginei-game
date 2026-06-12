using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 軍学校の多段選抜（#155 LIFE-5 細分化・<see cref="MilitaryAcademyRules"/>）を固定する：段ごとの進級枠の漏斗、
    /// 学歴（幼年学校卒/士官学校卒/大学校卒）、任官者の席次と等級、大学校卒（参謀）の上乗せ、軍才順の選抜。
    /// </summary>
    public class MilitaryAcademyTests
    {
        private static Person Cadet(int id, int military)
        {
            var p = new Person(id, "候補生", Faction.帝国, PersonRole.軍人);
            p.leadership = military; p.attack = military; p.defense = military; p.mobility = military; // MilitaryAptitude = military
            return p;
        }

        private static Academy Academy(float quality = 0.5f)
            => new Academy(schoolId: 1, faction: Faction.帝国, name: "士官学校", capacity: 8, quality: quality);

        [Test]
        public void DegreeLadder_AndQuotaShrinks()
        {
            Assert.AreEqual(MilitaryDegree.幼年学校卒, MilitaryAcademyRules.DegreeFor(MilitarySchoolStage.幼年学校));
            Assert.AreEqual(MilitaryDegree.士官学校卒, MilitaryAcademyRules.DegreeFor(MilitarySchoolStage.士官学校));
            Assert.AreEqual(MilitaryDegree.大学校卒, MilitaryAcademyRules.DegreeFor(MilitarySchoolStage.大学校));
            // float 誤差ガード込みで枠が正しい
            Assert.AreEqual(60, MilitaryAcademyRules.QuotaPassing(100, MilitarySchoolStage.幼年学校));
            Assert.AreEqual(60, MilitaryAcademyRules.QuotaPassing(100, MilitarySchoolStage.士官学校));
            Assert.AreEqual(25, MilitaryAcademyRules.QuotaPassing(100, MilitarySchoolStage.大学校));
            Assert.AreEqual(0, MilitaryAcademyRules.QuotaPassing(0, MilitarySchoolStage.幼年学校));
            // 任官＝士官学校卒以上
            Assert.IsTrue(MilitaryAcademyRules.IsCommissioned(MilitaryDegree.士官学校卒));
            Assert.IsTrue(MilitaryAcademyRules.IsCommissioned(MilitaryDegree.大学校卒));
            Assert.IsFalse(MilitaryAcademyRules.IsCommissioned(MilitaryDegree.幼年学校卒));
        }

        [Test]
        public void Funnel_SievesByMerit_TopBecomesStaff()
        {
            var cadets = new List<Person>();
            for (int i = 0; i < 100; i++) cadets.Add(Cadet(i + 1, 10 + i)); // 軍才 10..109

            var result = MilitaryAcademyRules.Funnel(cadets, SeniorityRules.SeniorityParams.Default);

            int 退校 = 0, 幼 = 0, 士 = 0, 参 = 0;
            foreach (var p in result)
                switch (p.militaryDegree)
                {
                    case MilitaryDegree.大学校卒: 参++; break;
                    case MilitaryDegree.士官学校卒: 士++; break;
                    case MilitaryDegree.幼年学校卒: 幼++; break;
                    default: 退校++; break;
                }
            // 漏斗：幼年学校60→士官学校36→大学校9。最終学歴は排他＝大学校9/士官27/幼24/退校40
            Assert.AreEqual(9, 参);
            Assert.AreEqual(27, 士);
            Assert.AreEqual(24, 幼);
            Assert.AreEqual(40, 退校);

            // 任官者（士官学校卒以上）＝36名は席次1..36が付く
            int commissioned = 参 + 士;
            Assert.AreEqual(36, commissioned);

            // 最優秀(軍才109)が首席（大学校卒・席次1）で参謀ボーナス付き等級
            Person top = result[0];
            Assert.AreEqual(MilitaryDegree.大学校卒, top.militaryDegree);
            Assert.AreEqual(1, top.hammockNumber);
            int baseTier = SeniorityRules.InitialTier(1, SeniorityRules.SeniorityParams.Default);
            Assert.AreEqual(baseTier + MilitaryAcademyRules.WarCollegeTierBonus, top.rankTier);
        }

        [Test]
        public void Funnel_Empty_Safe()
        {
            Assert.AreEqual(0, MilitaryAcademyRules.Funnel(null, SeniorityRules.SeniorityParams.Default).Count);
            Assert.AreEqual(0, MilitaryAcademyRules.Funnel(new List<Person>(), SeniorityRules.SeniorityParams.Default).Count);
        }

        [Test]
        public void RunMilitarySession_GeneratesAndCommissions()
        {
            var a = Academy(quality: 0.6f);
            var results = MilitaryAcademyRules.RunMilitarySession(a, 800, 20, 1, i => (i * 0.13f) % 1f);
            Assert.AreEqual(20, results.Count);
            foreach (var p in results)
            {
                Assert.AreEqual(PersonRole.軍人, p.role);
                Assert.AreEqual(1, p.schoolId);
                Assert.AreEqual(800, p.graduationYear);
            }
            // 任官者が居て、その者だけ席次が付く（幼年/退校は席次0）
            int commissioned = 0;
            foreach (var p in results)
            {
                if (MilitaryAcademyRules.IsCommissioned(p.militaryDegree))
                {
                    commissioned++;
                    Assert.Greater(p.hammockNumber, 0);
                }
                else
                {
                    Assert.AreEqual(0, p.hammockNumber);
                }
            }
            Assert.Greater(commissioned, 0);
        }
    }
}
