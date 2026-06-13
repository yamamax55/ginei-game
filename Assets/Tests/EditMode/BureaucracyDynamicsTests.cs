using NUnit.Framework;
using Ginei;
using IP = Ginei.OfficialIntegrityRules.IntegrityParams;
using CAP = Ginei.CourtAuthorityRules.AuthorityParams;
using AP = Ginei.CivilServiceRules.AppointmentParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 官僚システムの動態（清廉度の汚職ドリフト／門閥人事＝権威で選抜が腐る／朝廷の権威が戦乱で動く）を固定する。
    /// </summary>
    public class BureaucracyDynamicsTests
    {
        // ---- 清廉度の動態（OfficialIntegrityRules） ----

        [Test]
        public void Integrity_TargetTracksAuthority_AndDrifts()
        {
            var p = IP.Default; // 権威1→0.85 / 権威0→0.30
            Assert.AreEqual(0.85f, OfficialIntegrityRules.TargetIntegrity(1f, p), 1e-4f);
            Assert.AreEqual(0.30f, OfficialIntegrityRules.TargetIntegrity(0f, p), 1e-4f);
            // 監督が弱い（権威0）と清廉が崩れる：0.85 → 0.75（年0.10ドリフト）
            Assert.AreEqual(0.75f, OfficialIntegrityRules.Tick(0.85f, 0f, p), 1e-4f);
            // 監督が効く（権威1）と清廉が回復：0.30 → 0.40
            Assert.AreEqual(0.40f, OfficialIntegrityRules.Tick(0.30f, 1f, p), 1e-4f);
        }

        // ---- 朝廷の権威の動態（CourtAuthorityRules） ----

        [Test]
        public void CourtAuthority_WarLowersTarget_PeaceRecovers()
        {
            var p = CAP.Default; // 平時0.6 / 戦乱最大0.1
            Assert.AreEqual(0.6f, CourtAuthorityRules.Target(0f, p), 1e-4f);
            Assert.AreEqual(0.1f, CourtAuthorityRules.Target(1f, p), 1e-4f);
            Assert.Greater(CourtAuthorityRules.Target(0.2f, p), CourtAuthorityRules.Target(0.9f, p));

            // 平時は回復（0.35→0.40）、戦乱は低下（0.35→0.30）
            var peace = new CourtAuthority(0.35f);
            CourtAuthorityRules.TickYear(peace, 0f, p);
            Assert.AreEqual(0.40f, peace.authority, 1e-4f);
            var war = new CourtAuthority(0.35f);
            CourtAuthorityRules.TickYear(war, 1f, p);
            Assert.AreEqual(0.30f, war.authority, 1e-4f);
            Assert.DoesNotThrow(() => CourtAuthorityRules.TickYear(null, 0.5f, p));
        }

        // ---- 門閥人事（CivilServiceRules.ParamsForAuthority） ----

        [Test]
        public void ParamsForAuthority_LowAuthorityShiftsMeritToBirth()
        {
            var basep = AP.Default; // tier0.6 / merit0.4
            var high = CivilServiceRules.ParamsForAuthority(1f, basep);
            var low = CivilServiceRules.ParamsForAuthority(0f, basep);
            var mid = CivilServiceRules.ParamsForAuthority(0.5f, basep);

            // 権威満点＝基準と同じ（実力本位）
            Assert.AreEqual(basep.meritWeight, high.meritWeight, 1e-4f);
            Assert.AreEqual(basep.tierWeight, high.tierWeight, 1e-4f);
            // 権威0＝実績の重みが消え、すべて位階（門閥）へ
            Assert.AreEqual(0f, low.meritWeight, 1e-4f);
            Assert.AreEqual(basep.tierWeight + basep.meritWeight, low.tierWeight, 1e-4f);
            // 権威が下がるほど実績の重みは単調に減る
            Assert.Greater(high.meritWeight, mid.meritWeight);
            Assert.Greater(mid.meritWeight, low.meritWeight);
            // 総和は保存（実績ぶんが位階ぶんへ移るだけ）
            Assert.AreEqual(basep.tierWeight + basep.meritWeight, low.tierWeight + low.meritWeight, 1e-4f);
        }

        [Test]
        public void MeritocracyVsBirth_SelectionFlipsWithAuthority()
        {
            // 高位階・低考課（門閥の凡才＝蔭位で五位）vs 低位階・高考課（実力の俊英＝六位の進士級）
            var birth = new Person(1, "門閥", Faction.帝国, PersonRole.文民)
            { courtRank = CourtRank.従五位下, merit = new OfficialMerit(1) };
            MeritEvaluationRules.Record(birth.merit, MeritRating.下下);
            var talent = new Person(2, "俊英", Faction.帝国, PersonRole.文民)
            { courtRank = CourtRank.正六位上, merit = new OfficialMerit(2) };
            for (int i = 0; i < 3; i++) MeritEvaluationRules.Record(talent.merit, MeritRating.上上);

            int birthTier = JapaneseCourtRankRules.Tier(birth.courtRank);
            int talentTier = JapaneseCourtRankRules.Tier(talent.courtRank);

            // 権威が高い世＝実力が家柄を追い越す
            var hi = CivilServiceRules.ParamsForAuthority(1f, AP.Default);
            Assert.Greater(CivilServiceRules.CandidateScore(talentTier, talent.merit, hi),
                           CivilServiceRules.CandidateScore(birthTier, birth.merit, hi));
            // 権威が低い世＝門閥（高位階）が実力を押さえる
            var lo = CivilServiceRules.ParamsForAuthority(0f, AP.Default);
            Assert.Greater(CivilServiceRules.CandidateScore(birthTier, birth.merit, lo),
                           CivilServiceRules.CandidateScore(talentTier, talent.merit, lo));
        }
    }
}
