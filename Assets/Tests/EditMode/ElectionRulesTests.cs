using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 民主政治の選挙を固定する（選挙システム基盤）：単純多数/過半・決選投票の集計と、領域三層（惑星→星系→勢力）の集約。
    /// 候補の地域得票は民意（<see cref="PoliticianRules.RegionVotes"/>）から組む＝地盤の強者は地元を取るが、広い民意の候補が国政を制す。
    /// 党内選挙（総裁選）は既存 <see cref="LeadershipElectionRules"/> が担い、ここでは扱わない。
    /// </summary>
    public class ElectionRulesTests
    {
        [Test]
        public void WinnerByPlurality_MostVotes()
        {
            var t = new List<VoteTally>
            {
                new VoteTally(1, 100f),
                new VoteTally(2, 150f),
                new VoteTally(3, 50f),
            };
            int w = ElectionRules.WinnerByPlurality(t, out float votes);
            Assert.AreEqual(2, w);
            Assert.AreEqual(150f, votes, 1e-4f);
            Assert.AreEqual(0.5f, ElectionRules.VoteShare(t, 2), 1e-4f);
        }

        [Test]
        public void MajorityWinner_NeedsRunoff_WhenNoMajority()
        {
            // 過半の当選者
            var clear = new List<VoteTally> { new VoteTally(1, 60f), new VoteTally(2, 40f) };
            Assert.AreEqual(1, ElectionRules.MajorityWinner(clear, out float share));
            Assert.AreEqual(0.6f, share, 1e-4f);
            Assert.IsFalse(ElectionRules.NeedsRunoff(clear));

            // 過半なし＝決選が要る／上位2が決選の組
            var split = new List<VoteTally> { new VoteTally(1, 40f), new VoteTally(2, 35f), new VoteTally(3, 25f) };
            Assert.AreEqual(-1, ElectionRules.MajorityWinner(split, out _));
            Assert.IsTrue(ElectionRules.NeedsRunoff(split));
            ElectionRules.TopTwo(split, out int first, out int second);
            Assert.AreEqual(1, first);
            Assert.AreEqual(2, second);
        }

        [Test]
        public void Aggregate_SumsByCandidate_DeterministicOrder()
        {
            var regionA = new List<VoteTally> { new VoteTally(1, 10f), new VoteTally(2, 5f) };
            var regionB = new List<VoteTally> { new VoteTally(2, 7f), new VoteTally(3, 3f) };
            var rolled = ElectionRules.Aggregate(new[] { regionA, regionB });

            // 候補id昇順、得票は合算（1=10, 2=12, 3=3）
            Assert.AreEqual(3, rolled.Count);
            Assert.AreEqual(1, rolled[0].candidateId); Assert.AreEqual(10f, rolled[0].votes, 1e-4f);
            Assert.AreEqual(2, rolled[1].candidateId); Assert.AreEqual(12f, rolled[1].votes, 1e-4f);
            Assert.AreEqual(3, rolled[2].candidateId); Assert.AreEqual(3f, rolled[2].votes, 1e-4f);
            Assert.AreEqual(2, ElectionRules.WinnerByPlurality(rolled, out _));
        }

        [Test]
        public void MultiTier_LocalStrongmanWinsHomePlanet_NationalFigureWinsFaction()
        {
            // A＝地盤(P1)の強者、B＝全国区で広く支持される候補
            var a = new PoliticianProfile(1) { homeRegionKey = "P1", popularity = 0.7f, oratory = 50, integrity = 50 };
            var b = new PoliticianProfile(2) { homeRegionKey = "", popularity = 0.75f, oratory = 70, integrity = 50 };
            const float electorate = 100f;

            // 惑星ごとの得票（民意×有権者×地盤割増）を組む
            List<VoteTally> Planet(string region) => new List<VoteTally>
            {
                new VoteTally(a.personId, PoliticianRules.RegionVotes(a, electorate, region)),
                new VoteTally(b.personId, PoliticianRules.RegionVotes(b, electorate, region)),
            };

            var p1 = Planet("P1"); var p2 = Planet("P2"); var p3 = Planet("P3"); // 星系S1
            var p4 = Planet("P4"); var p5 = Planet("P5");                         // 星系S2

            // 惑星選挙：自分の地盤 P1 は A が勝つ（票が固い）
            Assert.AreEqual(a.personId, ElectionRules.WinnerByPlurality(p1, out _), "地盤で強者が勝たない");
            // 他の惑星は広い民意の B が勝つ
            Assert.AreEqual(b.personId, ElectionRules.WinnerByPlurality(p2, out _));

            // 星系選挙：惑星を集約（S1=P1+P2+P3）→ 広い B が星系を制す
            var s1 = ElectionRules.Aggregate(new[] { p1, p2, p3 });
            var s2 = ElectionRules.Aggregate(new[] { p4, p5 });
            Assert.AreEqual(b.personId, ElectionRules.WinnerByPlurality(s1, out _));

            // 勢力選挙：星系を集約（faction=S1+S2）→ 全国区の B が国政を制す（A は地元 P1 を取っても全体で及ばない）
            var faction = ElectionRules.Aggregate(new[] { s1, s2 });
            Assert.AreEqual(b.personId, ElectionRules.WinnerByPlurality(faction, out _), "全国区候補が勢力選挙を制さない");
        }

        [Test]
        public void NullAndEmptySafe()
        {
            Assert.AreEqual(-1, ElectionRules.WinnerByPlurality(null, out _));
            Assert.AreEqual(0f, ElectionRules.TotalVotes(null));
            Assert.IsFalse(ElectionRules.NeedsRunoff(null));
            Assert.AreEqual(0, ElectionRules.Aggregate(null).Count);
            // 全員0票は不成立
            var zeros = new List<VoteTally> { new VoteTally(1, 0f), new VoteTally(2, 0f) };
            Assert.AreEqual(-1, ElectionRules.WinnerByPlurality(zeros, out _));
            // 地盤外の RegionVotes は割増なし、null は0
            Assert.AreEqual(0f, PoliticianRules.RegionVotes(null, 100f, "X"));
        }
    }
}
