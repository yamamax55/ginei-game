using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 党首選出（総裁選）の純ロジック（GOV-7 #165・自民党型）。<b>党員票</b>（広い支持基盤＝階級#110/支持#113）と
    /// <b>議員票</b>（党所属の政治家）の<b>加重和</b>で党首が決まる。比重次第で「党員に人気だが議員に嫌われる」等の
    /// <b>ねじれ</b>が起きる。派閥（<see cref="PartyFaction"/>）は領袖が議員票を束ねる＝派閥推薦の集計を提供。
    /// 党首交代はイベント #116 で処理する想定（ここは票の解決のみ）。test-first。
    /// </summary>
    public static class LeadershipElectionRules
    {
        /// <summary>党員票/議員票の比重。</summary>
        public readonly struct VoteParams
        {
            /// <summary>党員票の比重。</summary>
            public readonly float memberWeight;
            /// <summary>議員票の比重。</summary>
            public readonly float legislatorWeight;

            public VoteParams(float memberWeight, float legislatorWeight)
            {
                this.memberWeight = Mathf.Max(0f, memberWeight);
                this.legislatorWeight = Mathf.Max(0f, legislatorWeight);
            }

            /// <summary>既定＝党員票・議員票を同等（0.5/0.5）。</summary>
            public static VoteParams Default => new VoteParams(0.5f, 0.5f);
        }

        /// <summary>1候補の得票（党員票・議員票）。</summary>
        public struct Candidate
        {
            public int id;
            public float memberVotes;     // 党員票（票数 or シェア）
            public float legislatorVotes; // 議員票

            public Candidate(int id, float memberVotes, float legislatorVotes)
            {
                this.id = id;
                this.memberVotes = memberVotes;
                this.legislatorVotes = legislatorVotes;
            }
        }

        /// <summary>加重得点＝党員票×比重 ＋ 議員票×比重。</summary>
        public static float Score(Candidate c, VoteParams p)
            => c.memberVotes * p.memberWeight + c.legislatorVotes * p.legislatorWeight;

        /// <summary>加重得点最大の候補 id を返す（候補なしは -1）。winScore に勝者得点。</summary>
        public static int Elect(IList<Candidate> candidates, VoteParams p, out float winScore)
        {
            winScore = float.NegativeInfinity;
            int winner = -1;
            if (candidates == null) return -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                float s = Score(candidates[i], p);
                if (s > winScore) { winScore = s; winner = candidates[i].id; }
            }
            if (winner == -1) winScore = 0f;
            return winner;
        }

        /// <summary>既定比重版。</summary>
        public static int Elect(IList<Candidate> candidates, out float winScore)
            => Elect(candidates, VoteParams.Default, out winScore);

        /// <summary>ねじれ＝党員票トップと議員票トップが別人か（人気と党内基盤の乖離）。</summary>
        public static bool HasTwist(IList<Candidate> candidates)
        {
            if (candidates == null || candidates.Count < 2) return false;
            int memberTop = -1, legTop = -1;
            float memBest = float.NegativeInfinity, legBest = float.NegativeInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].memberVotes > memBest) { memBest = candidates[i].memberVotes; memberTop = candidates[i].id; }
                if (candidates[i].legislatorVotes > legBest) { legBest = candidates[i].legislatorVotes; legTop = candidates[i].id; }
            }
            return memberTop != legTop;
        }

        /// <summary>
        /// 派閥が議員票を束ねる：各派閥の推薦先（<paramref name="endorsements"/>＝派閥id→候補id）に、その派閥の
        /// 所属議員数を議員票として加算する。領袖の談合・主流派/反主流派の動きを票の集計として表す。
        /// 戻り値＝候補id→議員票数。
        /// </summary>
        public static Dictionary<int, int> TallyLegislatorVotesByFaction(IEnumerable<PartyFaction> factions, IDictionary<int, int> endorsements)
        {
            var tally = new Dictionary<int, int>();
            if (factions == null || endorsements == null) return tally;
            foreach (PartyFaction f in factions)
            {
                if (f == null) continue;
                if (!endorsements.TryGetValue(f.id, out int candidateId)) continue;
                if (!tally.ContainsKey(candidateId)) tally[candidateId] = 0;
                tally[candidateId] += f.Weight; // 派閥サイズ＝束ねる議員票
            }
            return tally;
        }
    }
}
