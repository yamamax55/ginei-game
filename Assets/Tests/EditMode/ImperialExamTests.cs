using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 科挙の多段選抜（#156 LIFE-6 細分化・<see cref="ImperialExamRules"/>）を固定する：段ごとの合格枠で篩う漏斗、
    /// 功名（生員/挙人/貢士/進士）と等級、首席＝状元、文才順の選抜、受験生成の一貫。
    /// </summary>
    public class ImperialExamTests
    {
        private static Person Sitter(int id, int civil)
        {
            var p = new Person(id, "受験生", Faction.帝国, PersonRole.文民);
            p.operation = civil; p.intelligence = civil; // CivilAptitude = civil
            return p;
        }

        [Test]
        public void DegreeAndTier_Ladder()
        {
            Assert.AreEqual(ExamDegree.生員, ImperialExamRules.DegreeFor(ExamStage.童試));
            Assert.AreEqual(ExamDegree.挙人, ImperialExamRules.DegreeFor(ExamStage.郷試));
            Assert.AreEqual(ExamDegree.貢士, ImperialExamRules.DegreeFor(ExamStage.会試));
            Assert.AreEqual(ExamDegree.進士, ImperialExamRules.DegreeFor(ExamStage.殿試));
            // 等級は功名で単調（進士＞貢士＞挙人＞生員）
            Assert.Greater(ImperialExamRules.TierFor(ExamDegree.進士), ImperialExamRules.TierFor(ExamDegree.貢士));
            Assert.Greater(ImperialExamRules.TierFor(ExamDegree.貢士), ImperialExamRules.TierFor(ExamDegree.挙人));
            Assert.Greater(ImperialExamRules.TierFor(ExamDegree.挙人), ImperialExamRules.TierFor(ExamDegree.生員));
        }

        [Test]
        public void QuotaPassing_ShrinksByStage()
        {
            // 童試は半分、郷試は1割＝狭き門ほど通らない（殿試は全員＝順位だけ）
            Assert.AreEqual(50, ImperialExamRules.QuotaPassing(100, ExamStage.童試));
            Assert.AreEqual(10, ImperialExamRules.QuotaPassing(100, ExamStage.郷試));
            Assert.AreEqual(30, ImperialExamRules.QuotaPassing(100, ExamStage.会試));
            Assert.AreEqual(100, ImperialExamRules.QuotaPassing(100, ExamStage.殿試));
            Assert.AreEqual(0, ImperialExamRules.QuotaPassing(0, ExamStage.童試));
        }

        [Test]
        public void Funnel_SievesByMerit_TopBecomesJinshi()
        {
            // 才能をばらした100人を篩う：上位だけが上位功名へ、最終の少数が進士
            var sitters = new List<Person>();
            for (int i = 0; i < 100; i++) sitters.Add(Sitter(i + 1, 10 + i)); // civil 10..109（高いほど優秀）

            var result = ImperialExamRules.Funnel(sitters, SeniorityRules.SeniorityParams.Default);

            // 漏斗：童試50→郷試5→会試2(ceil1.5)→殿試=進士2
            int 生員 = 0, 挙人 = 0, 貢士 = 0, 進士 = 0, 無 = 0;
            foreach (var p in result)
                switch (p.examDegree)
                {
                    case ExamDegree.生員: 生員++; break;
                    case ExamDegree.挙人: 挙人++; break;
                    case ExamDegree.貢士: 貢士++; break;
                    case ExamDegree.進士: 進士++; break;
                    default: 無++; break;
                }
            // 進士は最少・無資格(童試落第)が最多（狭き門）
            Assert.Greater(進士, 0);
            Assert.AreEqual(50, 無); // 童試で半分落第
            Assert.GreaterOrEqual(生員 + 挙人 + 貢士 + 進士, 進士);
            Assert.Greater(無, 進士);

            // 最優秀(civil 109)が状元（進士・examRank 1）で最高等級
            Person top = result[0]; // Funnel はソート済（先頭が最優秀）
            Assert.AreEqual(ExamDegree.進士, top.examDegree);
            Assert.AreEqual(1, top.examRank);                       // 状元
            Assert.Greater(top.rankTier, ImperialExamRules.TierFor(ExamDegree.進士) - 1); // 状元は+1
        }

        [Test]
        public void Funnel_Empty_Safe()
        {
            Assert.AreEqual(0, ImperialExamRules.Funnel(null, SeniorityRules.SeniorityParams.Default).Count);
            Assert.AreEqual(0, ImperialExamRules.Funnel(new List<Person>(), SeniorityRules.SeniorityParams.Default).Count);
        }

        [Test]
        public void RunExamSession_GeneratesAndSieves()
        {
            var u = new University(schoolId: 3, faction: Faction.同盟, name: "大学", track: CareerTrack.科挙, capacity: 8, quality: 0.6f);
            var results = ImperialExamRules.RunExamSession(u, 800, 40, 1, i => (i * 0.13f) % 1f);
            Assert.AreEqual(40, results.Count);
            // 全員 文民・学校/卒年が刻まれる
            foreach (var p in results)
            {
                Assert.AreEqual(PersonRole.文民, p.role);
                Assert.AreEqual(3, p.schoolId);
                Assert.AreEqual(800, p.graduationYear);
            }
            // 少なくとも1人は進士に到達する（漏斗は最低1名通す）
            int 進士 = 0;
            foreach (var p in results) if (p.examDegree == ExamDegree.進士) 進士++;
            Assert.GreaterOrEqual(進士, 1);
            // 称号で命名される（状元が居る）
            bool has状元 = false;
            foreach (var p in results) if (p.name.Contains("状元")) has状元 = true;
            Assert.IsTrue(has状元);
        }
    }
}
