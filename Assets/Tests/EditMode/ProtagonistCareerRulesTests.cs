using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 主人公の士官学校入学〜任官〜配属（TKO-1・<see cref="ProtagonistCareerRules"/>・#2478）を固定する：
    /// 軍才の相対順で席次/学歴/任官が決まること、配属ゲート（在学中は艦隊配属不可）、後方互換（null安全）。
    /// 群体選抜は <see cref="MilitaryAcademyRules.Funnel"/> を流用するため、結果はその仕様に整合する。
    /// </summary>
    public class ProtagonistCareerRulesTests
    {
        private static Person Cadet(int id, int mil, int year = 800)
        {
            var p = new Person(id, "候補生" + id, Faction.帝国, PersonRole.軍人);
            p.leadership = mil; p.attack = mil; p.defense = mil; p.mobility = mil; // MilitaryAptitude = mil
            p.schoolId = 1; p.graduationYear = year;
            return p;
        }

        // 9名の同期（軍才 10,20,…,90）を作る（N=10 で q1=6/q2=4/q3=1 になる母数）。
        private static List<Person> NineClassmates(int year = 800)
        {
            var list = new List<Person>();
            for (int k = 1; k <= 9; k++) list.Add(Cadet(100 + k, k * 10, year));
            return list;
        }

        [Test]
        public void TopAptitude_GraduatesWarCollege_AndCommissions()
        {
            var hero = Cadet(1, 95); // 全員より高い軍才＝首席
            var outcome = ProtagonistCareerRules.Enroll(hero, NineClassmates());

            Assert.IsTrue(outcome.commissioned, "首席は任官する");
            Assert.AreEqual(MilitaryDegree.大学校卒, outcome.degree, "最上位は大学校卒（参謀）");
            Assert.AreEqual(1, outcome.hammockNumber, "首席のハンモック=1");
            // InitialTier(1)=8 ＋ 大学校卒ボーナス2 = 10
            Assert.AreEqual(10, outcome.rankTier);
            Assert.AreEqual(10, outcome.classSize);
        }

        [Test]
        public void LowAptitude_FailsToCommission()
        {
            var hero = Cadet(1, 5); // 全員より低い＝最下位（rank10）
            var outcome = ProtagonistCareerRules.Enroll(hero, NineClassmates());

            Assert.IsFalse(outcome.commissioned, "最下位は任官しない（退校）");
            Assert.AreEqual(MilitaryDegree.無資格, outcome.degree);
            Assert.AreEqual(0, outcome.rankTier, "未任官は等級0");
            Assert.AreEqual(0, outcome.hammockNumber);
        }

        [Test]
        public void MidAptitude_PlacementFollowsRelativeMilitaryAptitude()
        {
            // N=10 を保ったまま、上に1人(99)だけ置く＝主人公(90)は2番手。
            // q1=6/q2=4/q3=1 ＝大学校卒は首席のみ → 主人公はハンモック2・士官学校卒。
            var classmates = new List<Person>
            {
                Cadet(101, 99), Cadet(102, 80), Cadet(103, 70), Cadet(104, 60),
                Cadet(105, 50), Cadet(106, 40), Cadet(107, 30), Cadet(108, 20), Cadet(109, 10),
            };
            var hero = Cadet(1, 90);
            var outcome = ProtagonistCareerRules.Enroll(hero, classmates);

            Assert.IsTrue(outcome.commissioned);
            Assert.AreEqual(MilitaryDegree.士官学校卒, outcome.degree);
            Assert.AreEqual(2, outcome.hammockNumber);
            Assert.AreEqual(8, outcome.rankTier); // InitialTier(2)=8
        }

        [Test]
        public void CanDeploy_GatedByEnrollmentUntilGraduation()
        {
            var hero = Cadet(1, 95);
            hero.graduationYear = 800;
            ProtagonistCareerRules.Enroll(hero, NineClassmates());

            Assert.IsFalse(ProtagonistCareerRules.CanDeploy(hero, 799), "在学中（卒業前）は艦隊配属不可");
            Assert.IsTrue(ProtagonistCareerRules.CanDeploy(hero, 800), "卒業年以降は配属可");
            Assert.IsTrue(ProtagonistCareerRules.CanDeploy(hero, 805));
        }

        [Test]
        public void NonCommissioned_CannotDeploy()
        {
            var hero = Cadet(1, 5);
            ProtagonistCareerRules.Enroll(hero, NineClassmates());
            Assert.IsFalse(ProtagonistCareerRules.CanDeploy(hero, 810), "退校者は配属不可");
        }

        [Test]
        public void NullProtagonist_ReturnsDefault()
        {
            var outcome = ProtagonistCareerRules.Enroll(null, NineClassmates());
            Assert.IsFalse(outcome.commissioned);
            Assert.AreEqual(MilitaryDegree.無資格, outcome.degree);
        }

        [Test]
        public void EnrollWithClass_GeneratesCohort_AndAlignsSchoolCohort()
        {
            var academy = new Academy(schoolId: 7, faction: Faction.帝国, name: "士官学校", capacity: 12, quality: 0.5f);
            var hero = new Person(1, "主人公", Faction.帝国, PersonRole.軍人);
            hero.leadership = hero.attack = hero.defense = hero.mobility = 99; // 凡庸な同期より明確に上

            var outcome = ProtagonistCareerRules.EnrollWithClass(
                hero, academy, classSize: 12, year: 800, classmateIdStart: 1000, roll: i => 0.5f);

            Assert.IsTrue(outcome.commissioned, "傑出した軍才は任官する");
            Assert.AreEqual(800, outcome.graduationYear);
            Assert.AreEqual(7, hero.schoolId, "入校で制度（学閥キー）が同期に揃う");
            Assert.AreEqual(13, outcome.classSize, "同期12＋主人公1");
        }
    }
}
