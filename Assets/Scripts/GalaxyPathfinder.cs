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
        /// startId から goalId への最短経路（回廊 length 合計が最小）を星系ID列で返す。
        /// 先頭=start・末尾=goal を含む。start==goal は [start]。到達不能/未知ノードは空リスト。
        /// </summary>
        public static List<int> FindPath(GalaxyMap map, int startId, int goalId)
        {
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
