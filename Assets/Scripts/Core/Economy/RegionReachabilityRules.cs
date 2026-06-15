using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 連結領域の判定（DIST-2・#2112・純ロジック）。供給プールを共有できる惑星群＝所有星系のうち回廊で繋がった連結成分。
    /// 遮断星系（通商破壊#95 で中継不能）は分断される。`LogisticsRules.OwnedSystemIds`#844 を流用。test-first。
    /// </summary>
    public static class RegionReachabilityRules
    {
        /// <summary>start から所有・非遮断の星系を回廊で BFS して連結成分を返す。</summary>
        public static HashSet<int> ConnectedComponent(GalaxyMap map, Faction owner, int startId, ISet<int> blocked)
        {
            var visited = new HashSet<int>();
            if (map == null) return visited;
            var start = map.GetSystem(startId);
            if (start == null || start.owner != owner) return visited;
            if (blocked != null && blocked.Contains(startId)) return visited;

            var queue = new Queue<int>();
            queue.Enqueue(startId);
            visited.Add(startId);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                var neighbors = map.Neighbors(cur);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int nb = neighbors[i];
                    if (visited.Contains(nb)) continue;
                    var sys = map.GetSystem(nb);
                    if (sys == null || sys.owner != owner) continue;
                    if (blocked != null && blocked.Contains(nb)) continue;
                    visited.Add(nb);
                    queue.Enqueue(nb);
                }
            }
            return visited;
        }

        /// <summary>所有星系の全連結成分（遮断で分断される）。</summary>
        public static List<HashSet<int>> Components(GalaxyMap map, Faction owner, ISet<int> blocked)
        {
            var result = new List<HashSet<int>>();
            if (map == null) return result;
            var owned = LogisticsRules.OwnedSystemIds(map, owner);
            var seen = new HashSet<int>();
            for (int i = 0; i < owned.Count; i++)
            {
                int id = owned[i];
                if (seen.Contains(id)) continue;
                if (blocked != null && blocked.Contains(id)) { seen.Add(id); continue; }
                var comp = ConnectedComponent(map, owner, id, blocked);
                foreach (var c in comp) seen.Add(c);
                if (comp.Count > 0) result.Add(comp);
            }
            return result;
        }
    }
}
