using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 銀河グラフの最短経路探索（C-1 #34）。回廊 length を重みとした Dijkstra。
    /// 経路計画（多ホップワープ）の唯一の窓口。純ロジック・シーン非依存。
    /// </summary>
    public static class GalaxyPathfinder
    {
        /// <summary>
        /// 経路探索の条件（#40）。既定＝どの回廊も通れる従来どおりの最短経路。
        /// <paramref name="avoidFortressBlocked"/> を立てると、<paramref name="viewerFaction"/> にとって
        /// 敵の要塞が封鎖している回廊（<see cref="FortressBlockadeRules.Blocks(Corridor,Faction)"/>）を通らない。
        /// 要塞は迂回不可なので、封鎖回廊しか道が無ければ経路は<b>見つからない</b>＝呼び手は
        /// 「回り道は無い＝制圧しに行くしかない」と判断できる。
        /// </summary>
        public readonly struct PathQuery
        {
            /// <summary>前線回廊（両端が敵対所有＝FTL不可）を避けるか。</summary>
            public readonly bool avoidFtlBlocked;
            /// <summary>敵要塞に封鎖された回廊を避けるか。</summary>
            public readonly bool avoidFortressBlocked;
            /// <summary>誰から見た封鎖か（要塞所有者と非敵対なら封鎖されない）。</summary>
            public readonly Faction viewerFaction;

            public PathQuery(bool avoidFtlBlocked, bool avoidFortressBlocked, Faction viewerFaction)
            {
                this.avoidFtlBlocked = avoidFtlBlocked;
                this.avoidFortressBlocked = avoidFortressBlocked;
                this.viewerFaction = viewerFaction;
            }

            /// <summary>従来どおり（どの回廊も通れる）。</summary>
            public static PathQuery Default => new PathQuery(false, false, Faction.帝国);

            /// <summary>敵要塞の封鎖を避ける（迂回路があるかを調べたいとき）。</summary>
            public static PathQuery AvoidingFortresses(Faction viewer) => new PathQuery(false, true, viewer);
        }

        /// <summary>
        /// startId から goalId への最短経路（回廊 length 合計が最小）を星系ID列で返す。
        /// 先頭=start・末尾=goal を含む。start==goal は [start]。到達不能/未知ノードは空リスト。
        /// avoidFtlBlocked=true なら前線回廊（StrategyRules.IsFtlBlocked）を通らない経路を返す。
        /// </summary>
        public static List<int> FindPath(GalaxyMap map, int startId, int goalId, bool avoidFtlBlocked = false)
            => FindPath(map, startId, goalId, new PathQuery(avoidFtlBlocked, false, Faction.帝国));

        /// <summary><inheritdoc cref="FindPath(GalaxyMap,int,int,bool)"/> 条件は <see cref="PathQuery"/> で与える（#40）。</summary>
        public static List<int> FindPath(GalaxyMap map, int startId, int goalId, PathQuery query)
        {
            bool avoidFtlBlocked = query.avoidFtlBlocked;
            var result = new List<int>();
            if (map == null) return result;
            if (map.GetSystem(startId) == null || map.GetSystem(goalId) == null) return result;
            if (startId == goalId) { result.Add(startId); return result; }

            var dist = new Dictionary<int, float>();
            var prev = new Dictionary<int, int>();
            var visited = new HashSet<int>();
            foreach (var s in map.systems) if (s != null) dist[s.id] = float.PositiveInfinity;
            if (!dist.ContainsKey(startId)) return result;
            dist[startId] = 0f;

            while (true)
            {
                // 未訪問で最小距離のノードを選ぶ
                int u = -1;
                float best = float.PositiveInfinity;
                foreach (var kv in dist)
                {
                    if (visited.Contains(kv.Key)) continue;
                    if (kv.Value < best) { best = kv.Value; u = kv.Key; }
                }
                if (u == -1 || float.IsPositiveInfinity(best)) break; // これ以上到達できない
                if (u == goalId) break;
                visited.Add(u);

                foreach (int v in map.Neighbors(u))
                {
                    if (visited.Contains(v)) continue;
                    Corridor c = map.GetCorridor(u, v);
                    if (c == null) continue;
                    if (avoidFtlBlocked && StrategyRules.IsFtlBlocked(map, c)) continue; // 前線はFTL不可
                    // #40：敵の要塞が扼する回廊は、制圧するまで通り抜けられない（迂回不可＝ここで枝を切る）。
                    if (query.avoidFortressBlocked
                        && FortressBlockadeRules.Blocks(c, query.viewerFaction)) continue;
                    float nd = dist[u] + Mathf.Max(0f, c.length);
                    if (!dist.ContainsKey(v) || nd < dist[v]) { dist[v] = nd; prev[v] = u; }
                }
            }

            // 経路復元（goal から prev を辿る。途切れたら到達不能＝空）
            var rev = new List<int> { goalId };
            int cur = goalId;
            while (cur != startId)
            {
                if (!prev.ContainsKey(cur)) return new List<int>();
                cur = prev[cur];
                rev.Add(cur);
            }
            rev.Reverse();
            return rev;
        }

        /// <summary>経路（星系ID列）の総コスト（回廊 length 合計）。隣接が欠ける場合は -1、要素1以下は0。</summary>
        public static float PathCost(GalaxyMap map, List<int> route)
        {
            if (map == null || route == null || route.Count < 2) return 0f;
            float total = 0f;
            for (int i = 0; i + 1 < route.Count; i++)
            {
                Corridor c = map.GetCorridor(route[i], route[i + 1]);
                if (c == null) return -1f;
                total += c.length;
            }
            return total;
        }
    }
}
