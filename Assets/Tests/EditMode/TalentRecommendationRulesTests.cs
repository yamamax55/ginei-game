using System.Collections.Generic;
using NUnit.Framework;
using Ginei;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>特技の自動推薦：素養→最高格、得意素養の代表特技選び、決定論・素養ゲート整合。</summary>
    public class TalentRecommendationRulesTests
    {
        private static AdmiralData Admiral(int attack, int intelligence, int leadership, int operation)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.attack = attack; a.intelligence = intelligence; a.leadership = leadership; a.operation = operation;
            a.staffOfficers = new AdmiralData[0];
            return a;
        }

        [SetUp]
        public void Setup() => TalentCatalog.ResetToDefaults();

        [TearDown]
        public void Cleanup() => TalentCatalog.ResetToDefaults();

        [Test]
        public void BestGrade_MatchesRequiredStatThresholds()
        {
            Assert.AreEqual(TalentGrade.神, TalentRecommendationRules.BestGrade(90));
            Assert.AreEqual(TalentGrade.特, TalentRecommendationRules.BestGrade(89));
            Assert.AreEqual(TalentGrade.特, TalentRecommendationRules.BestGrade(75));
            Assert.AreEqual(TalentGrade.上, TalentRecommendationRules.BestGrade(74));
            Assert.AreEqual(TalentGrade.上, TalentRecommendationRules.BestGrade(55));
            Assert.AreEqual(TalentGrade.中, TalentRecommendationRules.BestGrade(54));
            Assert.AreEqual(TalentGrade.中, TalentRecommendationRules.BestGrade(35));
            Assert.AreEqual(TalentGrade.初, TalentRecommendationRules.BestGrade(34));
            Assert.AreEqual(TalentGrade.初, TalentRecommendationRules.BestGrade(0));
        }

        [Test]
        public void Recommend_NullOrNonPositiveMax_IsEmpty()
        {
            Assert.AreEqual(0, TalentRecommendationRules.Recommend(null).Count);
            Assert.AreEqual(0, TalentRecommendationRules.Recommend(Admiral(80, 80, 80, 80), 0).Count);
        }

        [Test]
        public void Recommend_TopAspect_PicksSignaturePassive_AtBestGrade()
        {
            // 武勇が突出＝得意分野。代表（特性・常時）＝鬼神を、扱える最高格（攻撃80→特）で。
            var a = Admiral(attack: 80, intelligence: 60, leadership: 40, operation: 20);
            var rec = TalentRecommendationRules.Recommend(a, 1);
            Assert.AreEqual(1, rec.Count);
            Assert.AreEqual("鬼神", rec[0].defId);
            Assert.AreEqual(TalentGrade.特, rec[0].grade);
        }

        [Test]
        public void Recommend_CoversTopAspects_Deterministic_NoDuplicates()
        {
            var a = Admiral(attack: 80, intelligence: 60, leadership: 40, operation: 20);
            var rec = TalentRecommendationRules.Recommend(a, 2);
            Assert.AreEqual(2, rec.Count);
            // 1位=武勇→鬼神（特）、2位=知略→特性常時の id 最小＝先見（上：知略60）。
            Assert.AreEqual("鬼神", rec[0].defId);
            Assert.AreEqual(TalentGrade.特, rec[0].grade);
            Assert.AreEqual("先見", rec[1].defId);
            Assert.AreEqual(TalentGrade.上, rec[1].grade);

            // defId は重複しない。
            var ids = new HashSet<string>();
            foreach (var t in rec) Assert.IsTrue(ids.Add(t.defId));
        }

        [Test]
        public void Recommend_AllResults_AreWieldableByAdmiral()
        {
            var a = Admiral(attack: 95, intelligence: 70, leadership: 55, operation: 30);
            var rec = TalentRecommendationRules.Recommend(a, 3);
            Assert.AreEqual(3, rec.Count);
            foreach (var t in rec)
                Assert.IsTrue(TalentRules.CanWield(t, a), $"{t.defId} を扱えるべき");
        }

        [Test]
        public void Recommend_LowStatAdmiral_GetsBeginnerGradeTalents()
        {
            // 凡将（全素養30）でも代表特技を初級で持つ（RequiredStat初=0＝誰でも扱える）。
            var a = Admiral(attack: 30, intelligence: 30, leadership: 30, operation: 30);
            var rec = TalentRecommendationRules.Recommend(a, 2);
            Assert.AreEqual(2, rec.Count);
            foreach (var t in rec)
            {
                Assert.AreEqual(TalentGrade.初, t.grade);
                Assert.IsTrue(TalentRules.CanWield(t, a));
            }
        }
    }
}
