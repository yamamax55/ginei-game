using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>合議に参加する一人の寡頭の票（選挙システム基盤・寡頭制）。実権の重み付き。</summary>
    public struct CouncilVote
    {
        /// <summary>合議に参加する寡頭（<see cref="Person.id"/>）。</summary>
        public int oligarchId;
        /// <summary>その寡頭が推す候補（<see cref="Person.id"/>）。</summary>
        public int candidateId;
        /// <summary>実権の重み（軍/経済/派閥の力＝0 以上。1=平等な一票）。</summary>
        public float weight;

        public CouncilVote(int oligarchId, int candidateId, float weight = 1f)
        {
            this.oligarchId = oligarchId;
            this.candidateId = candidateId;
            this.weight = Mathf.Max(0f, weight);
        }
    }

    /// <summary>
    /// 寡頭制の<b>少数による合議</b>の純ロジック（選挙システム基盤・<see cref="ElectoralSystemRules.IsOligarchic"/> な政体）。
    /// 共産の集団指導や首長制の長老会のように、<b>少数の実力者が実権の重みで決める</b>＝一人一票でなく力の大きい者の声が通る。
    /// 合意に要する閾値（過半/特別多数）を満たさなければ<b>膠着</b>（割れて決まらない）。決定論・基準値非破壊。test-first。
    /// </summary>
    public static class CouncilRules
    {
        /// <summary>合意の既定しきい値＝過半（実権の重みの半分超）。特別多数（全会一致に近い）は呼び出し側で上げる。</summary>
        public const float DefaultConsensusThreshold = 0.5f;

        /// <summary>合議全体の実権の総和。</summary>
        public static float TotalWeight(IEnumerable<CouncilVote> votes)
        {
            if (votes == null) return 0f;
            float t = 0f;
            foreach (var v in votes) t += Mathf.Max(0f, v.weight);
            return t;
        }

        /// <summary>ある候補を推す実権の合計。</summary>
        public static float WeightFor(IEnumerable<CouncilVote> votes, int candidateId)
        {
            if (votes == null) return 0f;
            float t = 0f;
            foreach (var v in votes)
                if (v.candidateId == candidateId) t += Mathf.Max(0f, v.weight);
            return t;
        }

        /// <summary>ある候補の支持率（実権シェア 0..1）。総和0は0。</summary>
        public static float Support(IEnumerable<CouncilVote> votes, int candidateId)
        {
            float total = TotalWeight(votes);
            if (total <= 0f) return 0f;
            return Mathf.Clamp01(WeightFor(votes, candidateId) / total);
        }

        /// <summary>
        /// 実権の重みで最有力の候補（重み最大・同点は <see cref="Person.id"/> 小）。<paramref name="winningSupport"/> に支持率（0..1）。
        /// 票が無ければ -1/0。合意の閾値は問わない（最有力を返すだけ＝閾値判定は <see cref="Decide"/>）。
        /// </summary>
        public static int ResolveByWeight(IEnumerable<CouncilVote> votes, out float winningSupport)
        {
            winningSupport = 0f;
            if (votes == null) return -1;

            var agg = new Dictionary<int, float>();
            float total = 0f;
            foreach (var v in votes)
            {
                float w = Mathf.Max(0f, v.weight);
                total += w;
                agg.TryGetValue(v.candidateId, out float cur);
                agg[v.candidateId] = cur + w;
            }
            if (agg.Count == 0 || total <= 0f) return -1;

            int winner = -1;
            float best = float.NegativeInfinity;
            foreach (var kv in agg)
            {
                // 重み最大、同点は候補id小（決定論）
                if (kv.Value > best || (kv.Value == best && kv.Key < winner))
                {
                    best = kv.Value;
                    winner = kv.Key;
                }
            }
            winningSupport = Mathf.Clamp01(best / total);
            return winner;
        }

        /// <summary>ある候補が合意の閾値に達しているか（実権シェア ≥ 閾値）。</summary>
        public static bool HasConsensus(IEnumerable<CouncilVote> votes, int candidateId, float threshold)
            => Support(votes, candidateId) >= threshold;

        public static bool HasConsensus(IEnumerable<CouncilVote> votes, int candidateId)
            => HasConsensus(votes, candidateId, DefaultConsensusThreshold);

        /// <summary>
        /// 合議の決定：最有力候補が合意の閾値を満たせばその id、満たさなければ -1（膠着＝割れて決まらない）。
        /// <paramref name="winnerSupport"/> に最有力候補の支持率（0..1）。
        /// </summary>
        public static int Decide(IEnumerable<CouncilVote> votes, float threshold, out float winnerSupport)
        {
            int top = ResolveByWeight(votes, out winnerSupport);
            if (top < 0) return -1;
            return winnerSupport >= threshold ? top : -1;
        }

        public static int Decide(IEnumerable<CouncilVote> votes, out float winnerSupport)
            => Decide(votes, DefaultConsensusThreshold, out winnerSupport);

        /// <summary>膠着か＝票はあるが最有力候補が合意の閾値に届かない（合議が割れて決まらない）。</summary>
        public static bool IsDeadlocked(IEnumerable<CouncilVote> votes, float threshold)
        {
            int top = ResolveByWeight(votes, out _);
            if (top < 0) return false; // 票が無いのは膠着でなく不成立
            return Decide(votes, threshold, out _) < 0;
        }

        public static bool IsDeadlocked(IEnumerable<CouncilVote> votes)
            => IsDeadlocked(votes, DefaultConsensusThreshold);
    }
}
