using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 月次評定（TKO-10・<see cref="MonthlyCouncilRules"/>・#2487）を固定する：期限超過主命の失敗確定、保留武勲昇進の
    /// 段階確定とペース上限、主命なし時の発令（roll 制御）、主命あり時は新規発令しないこと。委譲先（TKO-2/9）の整合。
    /// </summary>
    public class MonthlyCouncilRulesTests
    {
        private static MeritRecordRules.MeritRecordParams EP => MeritRecordRules.MeritRecordParams.Default;
        private static SovereignMandateRules.MandateParams MP => SovereignMandateRules.MandateParams.Default;
        private static MonthlyCouncilRules.CouncilParams CP => MonthlyCouncilRules.CouncilParams.Default;

        private static FactionData MakeFaction()
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = new List<FactionData.RankEntry>
            {
                new FactionData.RankEntry(5, "准将"), new FactionData.RankEntry(6, "少将"),
                new FactionData.RankEntry(7, "中将"), new FactionData.RankEntry(8, "大将"),
                new FactionData.RankEntry(10, "元帥"),
            };
            return f;
        }

        private static Person Lord() => new Person(1, "君主", Faction.帝国, PersonRole.軍人) { isSovereign = true, rankTier = 10 };
        private static Person Hero(int tier = 5) => new Person(2, "主人公", Faction.帝国, PersonRole.軍人) { rankTier = tier };

        // 連続値を返す roll 源。
        private static Func<float> Rolls(params float[] vals)
        {
            int i = 0;
            return () => vals[Mathf.Min(i++, vals.Length - 1)];
        }

        [Test]
        public void OverdueMandate_FailsAndAddsGrudge()
        {
            var hero = Hero();
            var active = SovereignMandateRules.Issue(50, Lord(), hero, MandateKind.防衛, "", 0, MP);
            var g = new PersonRelationGraph();

            // 現通算月10＞期限6 ＝期限超過。新規発令はしない（roll 0.9≥0.5）。
            var outcome = MonthlyCouncilRules.Hold(hero, new MeritRecord(2), MakeFaction(), active, Lord(),
                currentMonth: 10, newMandateId: 99, roll: Rolls(0.9f), meritPrm: EP, mandatePrm: MP, councilPrm: CP, relations: g);

            Assert.IsTrue(outcome.mandateResolved);
            Assert.IsFalse(outcome.mandateSucceeded);
            Assert.AreEqual(MandateStatus.失敗, active.status);
            Assert.IsFalse(outcome.mandateIssued, "roll 高で新規発令しない");
            Assert.AreEqual(MP.grudgeOnFail, g.Get(1, 2, RelationKind.遺恨).strength, 1e-4);
        }

        [Test]
        public void PendingMeritPromotion_AppliedOneStepPerCouncil()
        {
            var hero = Hero(5);
            var merit = new MeritRecord(2) { points = 120f }; // 2段ぶん
            var outcome = MonthlyCouncilRules.Hold(hero, merit, MakeFaction(), null, Lord(),
                currentMonth: 1, newMandateId: 99, roll: Rolls(0.9f), meritPrm: EP, mandatePrm: MP, councilPrm: CP);

            Assert.IsTrue(outcome.promoted);
            Assert.AreEqual(6, outcome.newRankTier, "准将→少将（1段のみ＝ペース制御）");
            Assert.AreEqual(6, hero.rankTier);
            Assert.AreEqual(1, merit.meritPromotionsApplied);
            Assert.IsTrue(MeritRecordRules.HasPendingPromotion(merit, EP), "残り1段は翌月へ");
        }

        [Test]
        public void NoMandate_IssuesNewWhenRollPasses()
        {
            var hero = Hero();
            // roll(): 1回目=発令判定(0.1<0.5で発令)、2回目=種別(0.0→出陣)
            var outcome = MonthlyCouncilRules.Hold(hero, new MeritRecord(2), MakeFaction(), null, Lord(),
                currentMonth: 3, newMandateId: 77, roll: Rolls(0.1f, 0.0f), meritPrm: EP, mandatePrm: MP, councilPrm: CP);

            Assert.IsTrue(outcome.mandateIssued);
            Assert.IsNotNull(outcome.issuedMandate);
            Assert.AreEqual(77, outcome.issuedMandate.id);
            Assert.AreEqual(MandateKind.出陣, outcome.issuedMandate.kind);
            Assert.AreEqual(MandateStatus.拝命, outcome.issuedMandate.status);
            Assert.AreEqual(9, outcome.issuedMandate.dueMonth); // 3 + 6
        }

        [Test]
        public void NoMandate_DoesNotIssueWhenRollFails()
        {
            var hero = Hero();
            var outcome = MonthlyCouncilRules.Hold(hero, new MeritRecord(2), MakeFaction(), null, Lord(),
                currentMonth: 3, newMandateId: 77, roll: Rolls(0.9f), meritPrm: EP, mandatePrm: MP, councilPrm: CP);
            Assert.IsFalse(outcome.mandateIssued, "roll≥issueChance は発令しない");
        }

        [Test]
        public void OpenMandate_BlocksNewIssue()
        {
            var hero = Hero();
            var active = SovereignMandateRules.Issue(50, Lord(), hero, MandateKind.出陣, "", 0, MP); // due=6
            var outcome = MonthlyCouncilRules.Hold(hero, new MeritRecord(2), MakeFaction(), active, Lord(),
                currentMonth: 3, newMandateId: 77, roll: Rolls(0.1f, 0.0f), meritPrm: EP, mandatePrm: MP, councilPrm: CP);

            Assert.IsFalse(outcome.mandateResolved, "期限内＝決着しない");
            Assert.IsFalse(outcome.mandateIssued, "未決の主命があれば新規発令しない");
        }

        [Test]
        public void NullProtagonist_Safe()
        {
            var outcome = MonthlyCouncilRules.Hold(null, null, MakeFaction(), null, Lord(),
                1, 1, Rolls(0.1f), EP, MP, CP);
            Assert.IsFalse(outcome.promoted);
            Assert.IsFalse(outcome.mandateIssued);
        }
    }
}
