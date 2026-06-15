using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 物流・版図の一体化の純ロジック（地政学 GEO-3 #844）。所有星系どうしが回廊で繋がっているほど
    /// 一体化度が高い。敵に分断された／散在する版図（島嶼国家・帝国の縮図 #839）は一体化度が下がり、
    /// 国力を出し切れない。既存の <see cref="GalaxyMap"/>（回廊グラフ）上で動く。test-first。
    /// </summary>
    public static class LogisticsRules
    {
        /// <summary>owner が所有する星系IDを集める（StarSystem.owner）。</summary>
        public static List<int> OwnedSystemIds(GalaxyMap map, Faction owner)
        {
            var result = new List<int>();
            if (map == null) return result;
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s != null && s.owner == owner) result.Add(s.id);
            }
            return result;
        }

        /// <summary>
        /// 所有星系のうち最大の連結成分のサイズ（<b>所有星系のみを通って</b>繋がるものを数える）。
        /// 敵星系を挟むと分断される＝版図の内部結合を測る。
        /// </summary>
        public static int LargestConnectedComponent(GalaxyMap map, ICollection<int> ownerIds)
        {
            if (map == null || ownerIds == null || ownerIds.Count == 0) return 0;
            var owned = new HashSet<int>(ownerIds);
            var visited = new HashSet<int>();
            int best = 0;
            foreach (int start in owned)
            {
                if (visited.Contains(start)) continue;
                int size = 0;
                var queue = new Queue<int>();
                queue.Enqueue(start);
                visited.Add(start);
                while (queue.Count > 0)
                {
                    int cur = queue.Dequeue();
                    size++;
                    foreach (int nb in map.Neighbors(cur))
                    {
                        if (owned.Contains(nb) && !visited.Contains(nb))
                        {
                            visited.Add(nb);
                            queue.Enqueue(nb);
                        }
                    }
                }
                if (size > best) best = size;
            }
            return best;
        }

        /// <summary>版図の一体化度 0..1＝最大連結成分/所有数。1=完全連結、低い=散在（分断）。</summary>
        public static float CohesionFactor(GalaxyMap map, ICollection<int> ownerIds)
        {
            if (ownerIds == null || ownerIds.Count == 0) return 0f;
            return (float)LargestConnectedComponent(map, ownerIds) / ownerIds.Count;
        }

        /// <summary>owner の版図の一体化度 0..1（StarSystem.owner から集計）。</summary>
        public static float CohesionFactor(GalaxyMap map, Faction owner)
            => CohesionFactor(map, OwnedSystemIds(map, owner));
    }
}
