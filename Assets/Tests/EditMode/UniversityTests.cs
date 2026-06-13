using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 大学の卒業ロジック（#156/#157 LIFE-6/7・<see cref="UniversityRules"/>）を固定する：候補と定員で輩出数が決まる／
    /// 文民を輩出（科挙＝文官は examRank と文才・テクノクラート＝技術者は技才で席次なし）／質が高い校は良才を出す。
    /// </summary>
    public class UniversityTests
    {
        private static University Univ(CareerTrack track, float quality = 0.5f, int capacity = 8)
            => new University(schoolId: 3, faction: Faction.同盟, name: "大学", track: track, capacity: capacity, quality: quality);

        [Test]
        public void Intake_LimitedByPoolAndCapacity()
        {
            var u = Univ(CareerTrack.科挙, capacity: 8);
            Assert.AreEqual(8, UniversityRules.Intake(u, 1000f));
            int expectedSmall = Mathf.FloorToInt(20f * UniversityRules.CandidateFraction);
            Assert.AreEqual(expectedSmall, UniversityRules.Intake(u, 20f));
            Assert.AreEqual(0, UniversityRules.Intake(u, 0f));
        }

        [Test]
        public void Keikyo_ProducesRankedCivilOfficials()
        {
            var u = Univ(CareerTrack.科挙, quality: 0.5f);
            var grads = UniversityRules.GraduateCohort(u, 800, 5, 100, i => (i * 0.17f) % 1f);
            Assert.AreEqual(5, grads.Count);
            for (int i = 0; i < grads.Count; i++)
            {
                Person p = grads[i];
                Assert.AreEqual(PersonRole.文民, p.role);            // 文民として輩出
                Assert.AreEqual(u.schoolId, p.schoolId);
                Assert.AreEqual(800, p.graduationYear);
                Assert.AreEqual(i + 1, p.examRank);                  // 科挙＝合格順位（文官版ハンモック）が付く
                Assert.Greater(p.CivilAptitude, 0f);
            }
            // 首席は文才が最高・席次は文才の降順
            for (int i = 1; i < grads.Count; i++)
                Assert.GreaterOrEqual(grads[i - 1].CivilAptitude, grads[i].CivilAptitude);
        }

        [Test]
        public void Technocrat_ProducesTechnicalGraduates_NoExamRank()
        {
            var u = Univ(CareerTrack.テクノクラート, quality: 0.8f);
            var grads = UniversityRules.GraduateCohort(u, 800, 3, 100, i => (i * 0.3f) % 1f);
            Assert.AreEqual(3, grads.Count);
            foreach (var p in grads)
            {
                Assert.AreEqual(PersonRole.文民, p.role);
                Assert.AreEqual(0, p.examRank);                      // テクノクラートは科挙の席次を持たない
                Assert.Greater(p.TechnicalAptitude, p.CivilAptitude); // 技才＞文才
            }
            // 席次は技才の降順
            for (int i = 1; i < grads.Count; i++)
                Assert.GreaterOrEqual(grads[i - 1].TechnicalAptitude, grads[i].TechnicalAptitude);
        }

        [Test]
        public void HigherQuality_ProducesBetterGraduates()
        {
            var elite = Univ(CareerTrack.科挙, quality: 1f);
            var weak = Univ(CareerTrack.科挙, quality: 0f);
            var ge = UniversityRules.GraduateCohort(elite, 800, 1, 1, _ => 0.5f);
            var gw = UniversityRules.GraduateCohort(weak, 800, 1, 1, _ => 0.5f);
            Assert.Greater(ge[0].CivilAptitude, gw[0].CivilAptitude);
        }

        [Test]
        public void ZeroIntakeAndNull_Empty()
        {
            Assert.AreEqual(0, UniversityRules.GraduateCohort(Univ(CareerTrack.科挙), 800, 0, 1, _ => 0.5f).Count);
            Assert.AreEqual(0, UniversityRules.GraduateCohort(null, 800, 5, 1, _ => 0.5f).Count);
        }
    }
}
