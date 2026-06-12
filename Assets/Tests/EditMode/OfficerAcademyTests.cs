using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 士官学校の卒業ロジック（#155 LIFE-5・<see cref="OfficerAcademyRules"/>）を固定する：徴募源と定員で輩出数が決まる／
    /// 卒業生は軍人で席次（首席=1・軍才順）と初期階級が付く／質が高い校は良将を出す／徴募源が乏しいと出せない。
    /// </summary>
    public class OfficerAcademyTests
    {
        private static Academy Academy(float quality = 0.5f, int capacity = 8)
            => new Academy(schoolId: 1, faction: Faction.帝国, name: "士官学校", capacity: capacity, quality: quality);

        [Test]
        public void Intake_LimitedByPoolAndCapacity()
        {
            var a = Academy(capacity: 8);
            // 徴募源が潤沢＝定員で頭打ち
            Assert.AreEqual(8, OfficerAcademyRules.Intake(a, 1000f));
            // 徴募源が乏しい＝プール律速（pool×CadetFraction）
            int expectedSmall = Mathf.FloorToInt(20f * OfficerAcademyRules.CadetFraction);
            Assert.AreEqual(expectedSmall, OfficerAcademyRules.Intake(a, 20f));
            // 徴募源ゼロ＝出せない
            Assert.AreEqual(0, OfficerAcademyRules.Intake(a, 0f));
        }

        [Test]
        public void GraduateCohort_ProducesRankedMilitaryOfficers()
        {
            var a = Academy(quality: 0.5f);
            // 素質をばらす（cadet i ごとに違う roll）
            var grads = OfficerAcademyRules.GraduateCohort(a, graduationYear: 800, intake: 5, idStart: 100,
                roll: i => (i * 0.17f) % 1f);
            Assert.AreEqual(5, grads.Count);

            for (int i = 0; i < grads.Count; i++)
            {
                Person p = grads[i];
                Assert.AreEqual(PersonRole.軍人, p.role);            // 軍人として輩出
                Assert.AreEqual(a.schoolId, p.schoolId);            // 学閥＝同窓の出所
                Assert.AreEqual(800, p.graduationYear);             // 同期の出所
                Assert.AreEqual(800 - OfficerAcademyRules.GraduationAge, p.birthYear);
                Assert.AreEqual(i + 1, p.hammockNumber);            // 席次は 1..N（連番）
            }
            // 首席(hammock 1)は軍才が最高・初期階級も最上位以上
            Assert.GreaterOrEqual(grads[0].MilitaryAptitude, grads[4].MilitaryAptitude);
            Assert.GreaterOrEqual(grads[0].rankTier, grads[4].rankTier);
            // 席次は軍才の降順（首席ほど強い）
            for (int i = 1; i < grads.Count; i++)
                Assert.GreaterOrEqual(grads[i - 1].MilitaryAptitude, grads[i].MilitaryAptitude);
        }

        [Test]
        public void HigherQualityAcademy_ProducesBetterGraduates()
        {
            // 同じ素質roll・同じ席次でも、質の高い校の方が能力が高い（名門は良将を出す）。
            var elite = Academy(quality: 1f);
            var weak = Academy(quality: 0f);
            var ge = OfficerAcademyRules.GraduateCohort(elite, 800, 1, 1, _ => 0.5f);
            var gw = OfficerAcademyRules.GraduateCohort(weak, 800, 1, 1, _ => 0.5f);
            Assert.Greater(ge[0].MilitaryAptitude, gw[0].MilitaryAptitude);
        }

        [Test]
        public void GraduateCohort_ZeroIntake_Empty()
        {
            var a = Academy();
            Assert.AreEqual(0, OfficerAcademyRules.GraduateCohort(a, 800, 0, 1, _ => 0.5f).Count);
            Assert.AreEqual(0, OfficerAcademyRules.GraduateCohort(null, 800, 5, 1, _ => 0.5f).Count);
        }

        [Test]
        public void StatFor_MapsTalentToRange()
        {
            Assert.AreEqual(OfficerAcademyRules.StatFloor, OfficerAcademyRules.StatFor(0f));
            Assert.AreEqual(OfficerAcademyRules.StatCeil, OfficerAcademyRules.StatFor(1f));
        }
    }
}
