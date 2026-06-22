using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 君主からの主命（TKO-9・<see cref="SovereignMandateRules"/>・#2486）を固定する：発令資格ゲート、期限、期限超過、
    /// 達成→武勲（<see cref="MeritRecordRules"/>）と恩義/親愛、失敗→遺恨、種別ごとの武勲規模。
    /// </summary>
    public class SovereignMandateRulesTests
    {
        private static SovereignMandateRules.MandateParams MP => SovereignMandateRules.MandateParams.Default;
        private static MeritRecordRules.MeritRecordParams EP => MeritRecordRules.MeritRecordParams.Default;

        private static Person Lord(int id = 1)
            => new Person(id, "君主", Faction.帝国, PersonRole.軍人) { isSovereign = true, rankTier = 12 };

        private static Person Retainer(int id = 2, Faction f = Faction.帝国)
            => new Person(id, "臣下", f, PersonRole.軍人) { rankTier = 7 };

        [Test]
        public void CanIssue_RequiresSovereignSameFactionAliveOther()
        {
            Assert.IsTrue(SovereignMandateRules.CanIssue(Lord(), Retainer()));
            Assert.IsFalse(SovereignMandateRules.CanIssue(Retainer(9), Retainer()), "非君主は発令不可");
            Assert.IsFalse(SovereignMandateRules.CanIssue(Lord(), Retainer(2, Faction.同盟)), "他勢力へは不可");
            var dead = Retainer(); dead.deathYear = 800;
            Assert.IsFalse(SovereignMandateRules.CanIssue(Lord(), dead), "故人へは不可");
            var lord = Lord(5); var self = lord;
            Assert.IsFalse(SovereignMandateRules.CanIssue(lord, self), "自分自身へは不可");
        }

        [Test]
        public void Issue_SetsDueByDuration_AndPending()
        {
            var m = SovereignMandateRules.Issue(100, Lord(), Retainer(), MandateKind.占領, "辺境星系", 10, MP);
            Assert.IsNotNull(m);
            Assert.AreEqual(MandateStatus.拝命, m.status);
            Assert.AreEqual(10, m.issuedMonth);
            Assert.AreEqual(16, m.dueMonth); // 10 + 既定6ヶ月
            Assert.AreEqual(Faction.帝国, m.faction);
        }

        [Test]
        public void IsOverdue_OnlyWhileOpenAndPastDue()
        {
            var m = SovereignMandateRules.Issue(1, Lord(), Retainer(), MandateKind.防衛, "", 0, MP);
            Assert.IsFalse(SovereignMandateRules.IsOverdue(m, 6), "期限内");
            Assert.IsTrue(SovereignMandateRules.IsOverdue(m, 7), "期限超過");
            SovereignMandateRules.Fail(m, MP);
            Assert.IsFalse(SovereignMandateRules.IsOverdue(m, 99), "決着済みは超過扱いしない");
        }

        [Test]
        public void MeritMagnitude_VariesByKind()
        {
            Assert.AreEqual(25f, SovereignMandateRules.MeritMagnitudeFor(MandateKind.占領, MP), 1e-4);
            Assert.AreEqual(20f, SovereignMandateRules.MeritMagnitudeFor(MandateKind.出陣, MP), 1e-4);
            Assert.AreEqual(4f, SovereignMandateRules.MeritMagnitudeFor(MandateKind.練兵, MP), 1e-4);
        }

        [Test]
        public void Complete_RecordsMerit_AndReturnsFavor()
        {
            var m = SovereignMandateRules.Issue(1, Lord(), Retainer(), MandateKind.占領, "", 0, MP);
            var merit = new MeritRecord(2);
            float favor = SovereignMandateRules.Complete(m, merit, EP, MP);

            Assert.AreEqual(MandateStatus.達成, m.status);
            Assert.AreEqual(25f, merit.points, 1e-4, "占領の達成＝任務達成 magnitude25");
            Assert.AreEqual(1, merit.exploitCount);
            Assert.AreEqual(MP.favorOnSuccess, favor, 1e-4);

            // 二重達成はしない
            Assert.AreEqual(0f, SovereignMandateRules.Complete(m, merit, EP, MP), 1e-4);
            Assert.AreEqual(25f, merit.points, 1e-4);
        }

        [Test]
        public void Fail_SetsStatus_AndReturnsGrudge()
        {
            var m = SovereignMandateRules.Issue(1, Lord(), Retainer(), MandateKind.鎮圧, "", 0, MP);
            float grudge = SovereignMandateRules.Fail(m, MP);
            Assert.AreEqual(MandateStatus.失敗, m.status);
            Assert.AreEqual(MP.grudgeOnFail, grudge, 1e-4);
        }

        [Test]
        public void ApplyOutcomeFavor_SuccessAddsGratitude_FailureAddsGrudge()
        {
            var g = new PersonRelationGraph();
            var m = SovereignMandateRules.Issue(1, Lord(1), Retainer(2), MandateKind.出陣, "", 0, MP);

            SovereignMandateRules.ApplyOutcomeFavor(g, m, +0.25f);
            Assert.AreEqual(0.25f, g.Get(2, 1, RelationKind.恩義).strength, 1e-4, "臣下→君主の恩義");
            Assert.AreEqual(0.25f, g.Get(1, 2, RelationKind.親愛).strength, 1e-4, "君主→臣下の親愛");

            SovereignMandateRules.ApplyOutcomeFavor(g, m, -0.2f);
            Assert.AreEqual(0.2f, g.Get(1, 2, RelationKind.遺恨).strength, 1e-4, "君主→臣下の遺恨");
        }
    }
}
