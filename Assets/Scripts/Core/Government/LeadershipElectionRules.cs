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
        /// <b>一人1票</b>：同じ人物IDが複数の派閥・同じ派閥に重複して載っていても1票だけ（派閥ID小の派閥で数える＝入力順に依らない）。
        /// 戻り値＝候補id→議員票数。実際の総裁選は所属者ごとの投票（<see cref="PartyLeadershipRules"/>）で数え、この一括集計は見込みに使う。
        /// </summary>
        public static Dictionary<int, int> TallyLegislatorVotesByFaction(IEnumerable<PartyFaction> factions, IDictionary<int, int> endorsements)
        {
            var tally = new Dictionary<int, int>();
            if (factions == null || endorsements == null) return tally;
            var ordered = new List<PartyFaction>();
            foreach (PartyFaction f in factions)
                if (f != null) ordered.Add(f);
            ordered.Sort((a, b) => a.id.CompareTo(b.id));
            var counted = new HashSet<int>();
            for (int i = 0; i < ordered.Count; i++)
            {
                PartyFaction f = ordered[i];
                if (f.memberIds == null || !endorsements.TryGetValue(f.id, out int candidateId)) continue;
                for (int m = 0; m < f.memberIds.Count; m++)
                {
                    int id = f.memberIds[m];
                    if (id < 0 || !counted.Add(id)) continue; // 重複・二重所属は数えない
                    if (!tally.ContainsKey(candidateId)) tally[candidateId] = 0;
                    tally[candidateId]++;
                }
            }
            return tally;
        }

        // ===== 二段階の総裁選の票計算（自民党型の参考モデル・ゲーム用） =====

        /// <summary>有効票の<b>厳密な過半数</b>か（ちょうど50%は過半数でない。有効票0は常に false）。</summary>
        public static bool StrictMajority(int votes, int totalValid)
            => totalValid > 0 && votes > 0 && (long)votes * 2 > totalValid;

        /// <summary>厳密な過半数に要る最小の票数（有効票0なら0）。</summary>
        public static int MajorityNeeded(int totalValid) => totalValid > 0 ? totalValid / 2 + 1 : 0;

        /// <summary>
        /// 生票（党員の実数）を整数の算定票へ正規化する：<paramref name="allotment"/> 票を生票に比例して最大剰余法で配る
        /// （<see cref="SeatAllocationRules.LargestRemainder"/> に委譲＝端数は剰余大→生票大→候補ID小）。生票が全て0なら全員0票。
        /// </summary>
        public static int[] ConvertToAllotment(IList<int> candidateIds, IList<long> rawVotes, int allotment)
        {
            int n = candidateIds == null || rawVotes == null ? 0 : System.Math.Min(candidateIds.Count, rawVotes.Count);
            var ids = new List<int>(n);
            var votes = new List<float>(n);
            for (int i = 0; i < n; i++)
            {
                ids.Add(candidateIds[i]);
                votes.Add(rawVotes[i] > 0 ? (float)rawVotes[i] : 0f);
            }
            return SeatAllocationRules.LargestRemainder(ids, votes, Mathf.Max(0, allotment));
        }

        /// <summary>選挙IDから決定論の種を作る（FNV-1a 32bit。空は0）。</summary>
        public static int SeedOf(string electionId)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (!string.IsNullOrEmpty(electionId))
                    for (int i = 0; i < electionId.Length; i++)
                    {
                        h ^= electionId[i];
                        h *= 16777619u;
                    }
                return (int)h;
            }
        }

        /// <summary>種と2つの整数から [0,1) の決定論の乱数（投票の揺らぎ・同票のくじ）。</summary>
        public static float SeededRoll(int seed, int a, int b)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)a * 2246822519u ^ (uint)b * 3266489917u ^ 0x9E3779B9u;
                h ^= h >> 15; h *= 0x85EBCA6Bu; h ^= h >> 13; h *= 0xC2B2AE35u; h ^= h >> 16;
                return (h & 0xFFFFFFu) / (float)0x1000000;
            }
        }
    }
}
