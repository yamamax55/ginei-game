using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 主人公の立身出世の永続（P1-c #2477）＝<see cref="ProtagonistCareerSerializer"/> の round-trip を固定する（test-first）。
    /// 階級・能力・武勲台帳・在席の主命・月次ループ状態・戦果インボックスが保存→復元で保たれる（「続きから」で進捗を失わない）。
    /// </summary>
    public class ProtagonistCareerSerializerTests
    {
        private Person MakeHero()
        {
            var p = new Person(900001, "テスト提督", Faction.同盟, PersonRole.軍人)
            {
                rankTier = 6,
                leadership = 80, attack = 72, defense = 64, mobility = 90, operation = 55, intelligence = 61,
            };
            return p;
        }

        [Test]
        public void Capture_NullHero_HasNoData()
        {
            ProtagonistCareerSave d = ProtagonistCareerSerializer.Capture(null, null, null, 0, 0, 0f, 0, 0);
            Assert.IsFalse(d.hasData);
        }

        [Test]
        public void RoundTrip_PersonFields_Preserved()
        {
            Person hero = MakeHero();
            ProtagonistCareerSave d = ProtagonistCareerSerializer.Capture(hero, null, null, 12, 870005, 0f, 0, 0);

            var restored = new Person(900001, "テスト提督", Faction.同盟, PersonRole.軍人);
            ProtagonistCareerSerializer.ApplyPerson(d, restored);

            Assert.AreEqual(6, restored.rankTier);
            Assert.AreEqual(80, restored.leadership);
            Assert.AreEqual(72, restored.attack);
            Assert.AreEqual(64, restored.defense);
            Assert.AreEqual(90, restored.mobility);
            Assert.AreEqual(55, restored.operation);
            Assert.AreEqual(61, restored.intelligence);
            Assert.AreEqual(12, d.lastCouncilMonth);
            Assert.AreEqual(870005, d.nextMandateId);
        }

        [Test]
        public void RoundTrip_Merit_Preserved()
        {
            Person hero = MakeHero();
            var merit = new MeritRecord(900001);
            MeritRecordRules.Record(merit, ExploitKind.撃沈, 5, MeritRecordRules.MeritRecordParams.Default);
            MeritRecordRules.Record(merit, ExploitKind.旗艦撃破, 1, MeritRecordRules.MeritRecordParams.Default);
            merit.meritPromotionsApplied = 2;

            ProtagonistCareerSave d = ProtagonistCareerSerializer.Capture(hero, merit, null, 0, 0, 0f, 0, 0);
            MeritRecord restored = ProtagonistCareerSerializer.ToMerit(d);

            Assert.AreEqual(merit.points, restored.points, 1e-4f);
            Assert.AreEqual(merit.exploitCount, restored.exploitCount);
            Assert.AreEqual(2, restored.meritPromotionsApplied);
        }

        [Test]
        public void RoundTrip_Mandate_Preserved()
        {
            Person hero = MakeHero();
            var mandate = new SovereignMandate
            {
                id = 42, faction = Faction.同盟, issuerId = 900000, assigneeId = 900001,
                kind = MandateKind.占領, status = MandateStatus.遂行中,
                issuedMonth = 3, dueMonth = 9, objective = "イゼルローン",
            };

            ProtagonistCareerSave d = ProtagonistCareerSerializer.Capture(hero, null, mandate, 0, 0, 0f, 0, 0);
            Assert.IsTrue(d.hasMandate);
            SovereignMandate restored = ProtagonistCareerSerializer.ToMandate(d);

            Assert.IsNotNull(restored);
            Assert.AreEqual(42, restored.id);
            Assert.AreEqual(Faction.同盟, restored.faction);
            Assert.AreEqual(900000, restored.issuerId);
            Assert.AreEqual(900001, restored.assigneeId);
            Assert.AreEqual(MandateKind.占領, restored.kind);
            Assert.AreEqual(MandateStatus.遂行中, restored.status);
            Assert.AreEqual(3, restored.issuedMonth);
            Assert.AreEqual(9, restored.dueMonth);
            Assert.AreEqual("イゼルローン", restored.objective);
        }

        [Test]
        public void RoundTrip_NoMandate_IsNull()
        {
            Person hero = MakeHero();
            ProtagonistCareerSave d = ProtagonistCareerSerializer.Capture(hero, null, null, 0, 0, 0f, 0, 0);
            Assert.IsFalse(d.hasMandate);
            Assert.IsNull(ProtagonistCareerSerializer.ToMandate(d));
        }

        [Test]
        public void RoundTrip_Growth_Preserved()
        {
            Person hero = MakeHero();
            var growth = new Growth(GrowthArchetype.老練型, 137.5f);
            ProtagonistCareerSave d = ProtagonistCareerSerializer.Capture(hero, null, null, 0, 0, 0f, 0, 0, growth);
            Assert.IsTrue(d.hasGrowth);
            Assert.AreEqual(137.5f, d.growthExperience, 1e-4f);
            Assert.AreEqual((int)GrowthArchetype.老練型, d.growthArchetype);
        }

        [Test]
        public void Capture_NoGrowth_HasGrowthFalse()
        {
            Person hero = MakeHero();
            ProtagonistCareerSave d = ProtagonistCareerSerializer.Capture(hero, null, null, 0, 0, 0f, 0, 0);
            Assert.IsFalse(d.hasGrowth);
        }

        [Test]
        public void RoundTrip_BattleInbox_Preserved()
        {
            Person hero = MakeHero();
            ProtagonistCareerSave d = ProtagonistCareerSerializer.Capture(hero, null, null, 0, 0, 1234.5f, 3, 4);
            Assert.AreEqual(1234.5f, d.pendingBattleDamage, 1e-4f);
            Assert.AreEqual(3, d.pendingBattleVictories);
            Assert.AreEqual(4, d.pendingBattleCount);
        }
    }
}
