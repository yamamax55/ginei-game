using System.Collections.Generic;
using NUnit.Framework;
using Ginei;
using Cand = Ginei.LeadershipElectionRules.Candidate;
using VP = Ginei.LeadershipElectionRules.VoteParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 党首選出＝総裁選（GOV-7 #165・自民党型）を固定する：党員票×議員票の加重和で当選、比重次第のねじれ逆転、
    /// 党員票トップと議員票トップの乖離検出、派閥が議員票を束ねる集計。
    /// </summary>
    public class LeadershipElectionRulesTests
    {
        [Test]
        public void Elect_PicksHighestWeightedScore()
        {
            // A=党員票60/議員票40、B=党員票40/議員票60。同等比重ならスコア同点→先勝ち（A）
            var cands = new List<Cand> { new Cand(1, 60, 40), new Cand(2, 40, 60) };
            int winner = LeadershipElectionRules.Elect(cands, out float score);
            Assert.AreEqual(1, winner);
            Assert.AreEqual(50f, score, 1e-4f);
        }

        [Test]
        public void LegislatorWeight_FlipsResult_Twist()
        {
            // 党員に人気のA(党員80/議員20) vs 議員に推されるB(党員20/議員80)
            var cands = new List<Cand> { new Cand(1, 80, 20), new Cand(2, 20, 80) };

            // 議員票比重を高くするとB（議員に推される）が勝つ＝ねじれの逆転
            var legHeavy = new VP(0.2f, 0.8f);
            Assert.AreEqual(2, LeadershipElectionRules.Elect(cands, legHeavy, out _));

            // 党員票比重を高くするとA（党員人気）が勝つ
            var memHeavy = new VP(0.8f, 0.2f);
            Assert.AreEqual(1, LeadershipElectionRules.Elect(cands, memHeavy, out _));
        }

        [Test]
        public void HasTwist_DetectsMemberVsLegislatorDivergence()
        {
            var twisted = new List<Cand> { new Cand(1, 80, 20), new Cand(2, 20, 80) };
            Assert.IsTrue(LeadershipElectionRules.HasTwist(twisted)); // 党員トップ=A・議員トップ=B

            var aligned = new List<Cand> { new Cand(1, 80, 70), new Cand(2, 20, 30) };
            Assert.IsFalse(LeadershipElectionRules.HasTwist(aligned)); // 両方ともA
        }

        [Test]
        public void TallyByFaction_BundlesLegislatorVotes()
        {
            // 主流派(5人)→候補1、反主流派(3人)→候補2、中間派(2人)→候補1
            var factions = new List<PartyFaction>
            {
                new PartyFaction(1, "主流派") { memberIds = new List<int> { 1, 2, 3, 4, 5 } },
                new PartyFaction(2, "反主流派") { memberIds = new List<int> { 6, 7, 8 } },
                new PartyFaction(3, "中間派") { memberIds = new List<int> { 9, 10 } },
            };
            var endorsements = new Dictionary<int, int> { { 1, 100 }, { 2, 200 }, { 3, 100 } };

            var tally = LeadershipElectionRules.TallyLegislatorVotesByFaction(factions, endorsements);
            Assert.AreEqual(7, tally[100]); // 5 + 2（領袖が議員票を束ねる＝談合）
            Assert.AreEqual(3, tally[200]);
        }

        [Test]
        public void Elect_EmptyCandidates_ReturnsMinusOne()
        {
            int winner = LeadershipElectionRules.Elect(new List<Cand>(), out float score);
            Assert.AreEqual(-1, winner);
            Assert.AreEqual(0f, score, 1e-4f);
        }
    }
}
