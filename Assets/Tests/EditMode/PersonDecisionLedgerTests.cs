using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 人物の決裁履歴：その人物が決裁した内容を最新から最大20件残す（古いものは捨てる）。
    /// <see cref="PersonRingiRules.Adjudicate"/> が決裁確定時に記録することも固定する。
    /// </summary>
    public class PersonDecisionLedgerTests
    {
        [SetUp]
        public void Setup() => PersonDecisionLedger.Clear();

        private static Person P(int id, Faction f, int tier)
            => new Person { id = id, faction = f, rankTier = tier };

        [Test]
        public void Record_NewestFirst()
        {
            PersonDecisionLedger.Record(7, 100, "A", true, "tax.cut", Faction.同盟);
            PersonDecisionLedger.Record(7, 101, "B", false, "tax.hike", Faction.同盟);
            var hist = PersonDecisionLedger.Recent(7);
            Assert.AreEqual(2, hist.Count);
            Assert.AreEqual(101, hist[0].petitionId); // 最新が先頭
            Assert.AreEqual("B", hist[0].title);
            Assert.IsFalse(hist[0].approved);
            Assert.AreEqual(100, hist[1].petitionId);
            Assert.IsTrue(hist[1].approved);
        }

        [Test]
        public void Record_RetainsLatest20_DropsOldest()
        {
            for (int i = 1; i <= 25; i++)
                PersonDecisionLedger.Record(7, i, "案" + i, true, "tax.cut", Faction.同盟);

            var hist = PersonDecisionLedger.Recent(7);
            Assert.AreEqual(20, hist.Count);             // 最新20件だけ残る
            Assert.AreEqual(25, hist[0].petitionId);     // 先頭＝最新
            Assert.AreEqual(6, hist[19].petitionId);     // 最古は petitionId=6（1〜5は捨てられた）
            Assert.AreEqual(20, PersonDecisionLedger.Count(7));
        }

        [Test]
        public void DifferentPersons_AreIsolated()
        {
            PersonDecisionLedger.Record(7, 1, "X", true, "", Faction.同盟);
            PersonDecisionLedger.Record(8, 2, "Y", true, "", Faction.帝国);
            Assert.AreEqual(1, PersonDecisionLedger.Count(7));
            Assert.AreEqual(1, PersonDecisionLedger.Count(8));
            Assert.AreEqual(0, PersonDecisionLedger.Count(999)); // 未記録は空
        }

        [Test]
        public void Record_PersonZero_Ignored()
        {
            PersonDecisionLedger.Record(0, 1, "無名", true, "", Faction.同盟);
            Assert.AreEqual(0, PersonDecisionLedger.Count(0));
        }

        [Test]
        public void Adjudicate_RecordsDecision()
        {
            var boss = P(1, Faction.同盟, 8);
            var sub = P(2, Faction.同盟, 6);
            var pet = PersonRingiRules.RaiseTo(50, "増援", sub, boss, "tax.hike");
            pet.status = PetitionStatus.決裁待ち;

            Assert.IsTrue(PersonRingiRules.Adjudicate(boss, pet, approve: true));
            var hist = PersonDecisionLedger.Recent(boss.id);
            Assert.AreEqual(1, hist.Count);
            Assert.AreEqual(50, hist[0].petitionId);
            Assert.IsTrue(hist[0].approved);
            Assert.AreEqual("増援", hist[0].title);
        }

        [Test]
        public void Adjudicate_Failed_DoesNotRecord()
        {
            var boss = P(1, Faction.同盟, 8);
            var other = P(3, Faction.同盟, 9);
            var sub = P(2, Faction.同盟, 6);
            var pet = PersonRingiRules.RaiseTo(50, "案", sub, boss, "tax.hike");
            pet.status = PetitionStatus.決裁待ち;

            Assert.IsFalse(PersonRingiRules.Adjudicate(other, pet, true)); // 宛先でない＝決裁できない
            Assert.AreEqual(0, PersonDecisionLedger.Count(other.id));
            Assert.AreEqual(0, PersonDecisionLedger.Count(boss.id));
        }

        [Test]
        public void Clear_Empties()
        {
            PersonDecisionLedger.Record(7, 1, "A", true, "", Faction.同盟);
            PersonDecisionLedger.Clear();
            Assert.AreEqual(0, PersonDecisionLedger.Count(7));
        }
    }
}
