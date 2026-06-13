using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 官僚制の永続化（位階・考課の往復／朝廷の権威の既定・後方互換）を固定する。
    /// </summary>
    public class BureaucracyPersistenceTests
    {
        [Test]
        public void PersonSave_RoundTrips_CourtRankAndMerit()
        {
            var p = new Person(7, "宰相", Faction.帝国, PersonRole.文民)
            { operation = 70, intelligence = 60, courtRank = CourtRank.従五位下, merit = new OfficialMerit(7, 0.42f) };
            MeritEvaluationRules.Record(p.merit, MeritRating.上上);
            MeritEvaluationRules.Record(p.merit, MeritRating.上中);

            PersonSave save = CampaignSerializer.PersonToSave(p);
            Assert.IsTrue(save.hasMerit);
            Assert.AreEqual((int)CourtRank.従五位下, save.courtRank);

            Person r = CampaignSerializer.PersonFromSave(save);
            Assert.AreEqual(CourtRank.従五位下, r.courtRank);
            Assert.IsNotNull(r.merit);
            Assert.AreEqual(2, r.merit.evaluations);
            Assert.AreEqual(p.merit.cumulativeScore, r.merit.cumulativeScore, 1e-4f);
            Assert.AreEqual(2, r.merit.consecutiveTop);
            Assert.AreEqual(0, r.merit.consecutivePoor);
            Assert.AreEqual(0.42f, r.merit.integrity, 1e-4f);
            Assert.AreEqual(MeritRating.上中, r.merit.lastRating);
        }

        [Test]
        public void PersonSave_NoMerit_RestoresNull()
        {
            var p = new Person(1, "未叙位", Faction.同盟, PersonRole.文民) { courtRank = CourtRank.正七位上 };
            PersonSave save = CampaignSerializer.PersonToSave(p);
            Assert.IsFalse(save.hasMerit);
            Person r = CampaignSerializer.PersonFromSave(save);
            Assert.AreEqual(CourtRank.正七位上, r.courtRank);
            Assert.IsNull(r.merit);
        }

        [Test]
        public void OldSave_MissingBureaucracyFields_DefaultsToMuiAndNull()
        {
            // 旧セーブ＝官僚制フィールドが無い（初期化値のまま）→ 無位/未評定で復元（0=正一位の誤復元を防ぐ）。
            var legacy = new PersonSave { id = 3, name = "旧", faction = (int)Faction.帝国, role = (int)PersonRole.文民 };
            Assert.AreEqual((int)CourtRank.無位, legacy.courtRank);
            Assert.IsFalse(legacy.hasMerit);
            Person r = CampaignSerializer.PersonFromSave(legacy);
            Assert.AreEqual(CourtRank.無位, r.courtRank);
            Assert.IsNull(r.merit);
        }

        [Test]
        public void CampaignSaveData_CourtAuthority_DefaultsToFeudalLevel()
        {
            // 旧セーブに朝廷の権威が無くても武家政権相当（0.35）で復元（後方互換）。
            Assert.AreEqual(0.35f, new CampaignSaveData().courtAuthority, 1e-4f);
        }
    }
}
