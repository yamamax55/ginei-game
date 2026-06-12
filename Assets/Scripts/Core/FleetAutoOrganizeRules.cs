using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>自動編成に渡す指揮官候補（#146/#147 拡張）。id と階級 tier だけを持つ軽量スロット。</summary>
    public struct CommanderSlot
    {
        public int id;
        public int rankTier;
        public CommanderSlot(int id, int rankTier) { this.id = id; this.rankTier = rankTier; }
    }

    /// <summary>1艦隊ぶんの自動編成案（兵力＋配属司令id。-1＝適任なしで空席）。</summary>
    public struct FleetPlan
    {
        public int strength;
        public int commanderId;
    }

    /// <summary>
    /// 艦隊の自動編成の純ロジック基盤（#146/#147/#148 拡張・test-first）。
    /// 勢力の総艦艇プールを標準艦隊規模で分割し、指揮可能規模（<see cref="CommandCapacityRules"/>）を満たす範囲で
    /// <b>上位階級から大艦隊へ</b>司令を貪欲に割り付ける。手動編成（`FleetOrganizationPanel`）の自動化版＝UIはこの窓口を呼ぶだけ。
    /// 数値（指揮限界）は `CommandCapacityRules` へ委譲し二重実装しない。集約・後方互換。
    /// </summary>
    public static class FleetAutoOrganizeRules
    {
        /// <summary>推奨艦隊数＝ceil(総プール/標準規模)。プール0は0・標準0以下は1。</summary>
        public static int RecommendFleetCount(int totalPool, int standardFleetSize)
        {
            if (standardFleetSize <= 0) return 1;
            if (totalPool <= 0) return 0;
            return Mathf.Max(1, Mathf.CeilToInt((float)totalPool / standardFleetSize));
        }

        /// <summary>総プールを艦隊数へ均等配分（余りは先頭の艦隊へ1ずつ）。</summary>
        public static int[] AllocateStrength(int totalPool, int fleetCount)
        {
            if (fleetCount <= 0) return new int[0];
            totalPool = Mathf.Max(0, totalPool);
            var result = new int[fleetCount];
            int baseShare = totalPool / fleetCount;
            int remainder = totalPool % fleetCount;
            for (int i = 0; i < fleetCount; i++) result[i] = baseShare + (i < remainder ? 1 : 0);
            return result;
        }

        /// <summary>
        /// 各艦隊へ司令を割り付ける（艦隊index揃いの司令id配列を返す・-1＝空席）。
        /// 大きい艦隊から、指揮可能な範囲で最上位の未配属司令を割り当てる（過大兵力には下位階級を付けない）。
        /// </summary>
        public static int[] AssignCommanders(IReadOnlyList<int> fleetStrengths, IReadOnlyList<CommanderSlot> commanders)
        {
            int n = fleetStrengths?.Count ?? 0;
            var result = new int[n];
            for (int i = 0; i < n; i++) result[i] = -1;
            if (n == 0 || commanders == null || commanders.Count == 0) return result;

            // 艦隊を兵力降順で処理。
            var order = new List<int>(n);
            for (int i = 0; i < n; i++) order.Add(i);
            order.Sort((x, y) => fleetStrengths[y].CompareTo(fleetStrengths[x]));

            // 司令を階級降順に並べ、使用済みを管理。
            var pool = new List<CommanderSlot>(commanders);
            pool.Sort((x, y) => y.rankTier.CompareTo(x.rankTier));
            var used = new bool[pool.Count];

            for (int oi = 0; oi < order.Count; oi++)
            {
                int fi = order[oi];
                int strength = fleetStrengths[fi];
                for (int ci = 0; ci < pool.Count; ci++)
                {
                    if (used[ci]) continue;
                    if (CommandCapacityRules.CanCommand(pool[ci].rankTier, strength))
                    {
                        result[fi] = pool[ci].id;
                        used[ci] = true;
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>総プール＋標準規模＋司令候補から、艦隊編成案（兵力＋司令）を一括生成。</summary>
        public static List<FleetPlan> AutoOrganize(int totalPool, int standardFleetSize, IReadOnlyList<CommanderSlot> commanders)
        {
            var plans = new List<FleetPlan>();
            int count = RecommendFleetCount(totalPool, standardFleetSize);
            if (count <= 0) return plans;
            int[] strengths = AllocateStrength(totalPool, count);
            int[] cmds = AssignCommanders(strengths, commanders);
            for (int i = 0; i < count; i++)
                plans.Add(new FleetPlan { strength = strengths[i], commanderId = cmds[i] });
            return plans;
        }
    }
}
