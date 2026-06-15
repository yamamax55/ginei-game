using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>一候補の得票（選挙の集計単位）。<see cref="votes"/> は実数票（or 重み付き票）。</summary>
    public struct VoteTally
    {
        public int candidateId;
        public float votes;

        public VoteTally(int candidateId, float votes)
        {
            this.candidateId = candidateId;
            this.votes = Mathf.Max(0f, votes);
        }
    }

    /// <summary>
    /// 民主政治の<b>選挙</b>の純ロジック（選挙システム基盤・<see cref="ElectoralSystemRules.IsElectoral"/> な政体）。
    /// 得票の集計（多数決/過半・決選投票）と<b>領域三層の集約</b>（惑星→星系→勢力＝下位の得票を上位スコープへ合算する直接選挙）を担う。
    /// 党内選挙（総裁選）は組織の選挙ゆえ既存 <see cref="LeadershipElectionRules"/> を流用（ここでは扱わない＝二重実装しない）。
    /// 候補の地域得票は <see cref="PoliticianRules.RegionVotes"/> から組む（民意が票になる）。決定論・test-first。
    /// </summary>
    public static class ElectionRules
    {
        /// <summary>当選に要する得票率の既定＝過半（小選挙区の単純多数とは別に、過半判定/決選に使う）。</summary>
        public const float DefaultMajorityThreshold = 0.5f;

        /// <summary>総得票。</summary>
        public static float TotalVotes(IEnumerable<VoteTally> tallies)
        {
            if (tallies == null) return 0f;
            float t = 0f;
            foreach (var x in tallies) t += Mathf.Max(0f, x.votes);
            return t;
        }

        /// <summary>ある候補の得票（同一候補が複数行あれば合算）。</summary>
        public static float VotesFor(IEnumerable<VoteTally> tallies, int candidateId)
        {
            if (tallies == null) return 0f;
            float t = 0f;
            foreach (var x in tallies)
                if (x.candidateId == candidateId) t += Mathf.Max(0f, x.votes);
            return t;
        }

        /// <summary>ある候補の得票率（0..1・総得票0は0）。</summary>
        public static float VoteShare(IEnumerable<VoteTally> tallies, int candidateId)
        {
            float total = TotalVotes(tallies);
            if (total <= 0f) return 0f;
            return Mathf.Clamp01(VotesFor(tallies, candidateId) / total);
        }

        /// <summary>単純多数の当選者（得票最大・同点は候補id小）。<paramref name="winnerVotes"/> に得票。票が無ければ -1/0。</summary>
        public static int WinnerByPlurality(IEnumerable<VoteTally> tallies, out float winnerVotes)
        {
            winnerVotes = 0f;
            if (tallies == null) return -1;

            var agg = new Dictionary<int, float>();
            foreach (var x in tallies)
            {
                float v = Mathf.Max(0f, x.votes);
                agg.TryGetValue(x.candidateId, out float cur);
                agg[x.candidateId] = cur + v;
            }
            if (agg.Count == 0) return -1;

            int winner = -1;
            float best = float.NegativeInfinity;
            foreach (var kv in agg)
            {
                if (kv.Value > best || (kv.Value == best && kv.Key < winner))
                {
                    best = kv.Value;
                    winner = kv.Key;
                }
            }
            if (best <= 0f) return -1; // 全員0票＝不成立
            winnerVotes = best;
            return winner;
        }

        /// <summary>ある候補が過半（得票率 ≥ 閾値）を取っているか。</summary>
        public static bool HasMajority(IEnumerable<VoteTally> tallies, int candidateId, float threshold)
            => VoteShare(tallies, candidateId) >= threshold;

        public static bool HasMajority(IEnumerable<VoteTally> tallies, int candidateId)
            => HasMajority(tallies, candidateId, DefaultMajorityThreshold);

        /// <summary>過半を取った当選者（最多得票者が過半なら id、無ければ -1＝決選が要る）。<paramref name="share"/> に最多得票者の得票率。</summary>
        public static int MajorityWinner(IEnumerable<VoteTally> tallies, float threshold, out float share)
        {
            int top = WinnerByPlurality(tallies, out _);
            share = top < 0 ? 0f : VoteShare(tallies, top);
            if (top < 0) return -1;
            return share >= threshold ? top : -1;
        }

        public static int MajorityWinner(IEnumerable<VoteTally> tallies, out float share)
            => MajorityWinner(tallies, DefaultMajorityThreshold, out share);

        /// <summary>決選投票が要るか＝候補が2人以上で、最多得票者が過半に届かない。</summary>
        public static bool NeedsRunoff(IEnumerable<VoteTally> tallies, float threshold)
        {
            if (tallies == null) return false;
            var ids = new HashSet<int>();
            foreach (var x in tallies)
                if (x.votes > 0f) ids.Add(x.candidateId);
            if (ids.Count < 2) return false;
            return MajorityWinner(tallies, threshold, out _) < 0;
        }

        public static bool NeedsRunoff(IEnumerable<VoteTally> tallies)
            => NeedsRunoff(tallies, DefaultMajorityThreshold);

        /// <summary>得票上位2候補（決選投票の組）。<paramref name="first"/>=最多、<paramref name="second"/>=次点（同点は id 小）。不足は -1。</summary>
        public static void TopTwo(IEnumerable<VoteTally> tallies, out int first, out int second)
        {
            first = WinnerByPlurality(tallies, out float firstVotes);
            second = -1;
            if (first < 0) return;

            float bestSecond = float.NegativeInfinity;
            var agg = new Dictionary<int, float>();
            foreach (var x in tallies)
            {
                if (x.candidateId == first) continue;
                float v = Mathf.Max(0f, x.votes);
                agg.TryGetValue(x.candidateId, out float cur);
                agg[x.candidateId] = cur + v;
            }
            foreach (var kv in agg)
            {
                if (kv.Value <= 0f) continue;
                if (kv.Value > bestSecond || (kv.Value == bestSecond && kv.Key < second))
                {
                    bestSecond = kv.Value;
                    second = kv.Key;
                }
            }
        }

        /// <summary>
        /// 領域三層の集約（惑星→星系→勢力）＝地域別の得票を候補ごとに合算して上位スコープの得票を得る（直接選挙＝得票の総和）。
        /// 惑星の <see cref="VoteTally"/> 群を星系へ、星系を勢力へ、と段階的に重ねられる。返り値は候補id昇順（決定論）。
        /// </summary>
        public static List<VoteTally> Aggregate(IEnumerable<IEnumerable<VoteTally>> regions)
        {
            var agg = new SortedDictionary<int, float>();
            if (regions != null)
            {
                foreach (var region in regions)
                {
                    if (region == null) continue;
                    foreach (var x in region)
                    {
                        agg.TryGetValue(x.candidateId, out float cur);
                        agg[x.candidateId] = cur + Mathf.Max(0f, x.votes);
                    }
                }
            }
            var result = new List<VoteTally>(agg.Count);
            foreach (var kv in agg) result.Add(new VoteTally(kv.Key, kv.Value));
            return result;
        }
    }
}
