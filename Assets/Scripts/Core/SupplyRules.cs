using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 補給線の純ロジック（L-2 #94・唯一の窓口）。後方の生産星系（補給源）から前線へ、<b>所有回廊の連なり</b>で資源が流れる。
    /// 補給線は<b>敵ZOC（#81）を通せない</b>＝そこを断たれると前線が補給切れ＝枯れる。<see cref="GalaxyMap"/>（回廊グラフ）上で動く。
    /// 版図の一体化 <see cref="LogisticsRules"/> とは別（あれは国力割引・こちらは補給到達＋ZOC遮断）。test-first。
    /// </summary>
    public static class SupplyRules
    {
        /// <summary>
        /// 補給源 <paramref name="sources"/> から、<paramref name="faction"/> 所有星系のみを辿って到達できる星系集合。
        /// <paramref name="blocked"/>（敵ZOC下の星系）は通行不可＝そこで補給線が断たれる。
        /// </summary>
        public static HashSet<int> SuppliedSystems(GalaxyMap map, Faction faction, IEnumerable<int> sources, ISet<int> blocked = null)
        {
            var reached = new HashSet<int>();
            if (map == null || sources == null) return reached;

            var queue = new Queue<int>();
            foreach (int src in sources)
            {
                StarSystem s = map.GetSystem(src);
                if (s == null || s.owner != faction) continue;       // 補給源は自勢力所有
                if (blocked != null && blocked.Contains(src)) continue; // 断たれた源は使えない
                if (reached.Add(src)) queue.Enqueue(src);
            }

            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                foreach (int nb in map.Neighbors(cur))
                {
                    if (reached.Contains(nb)) continue;
                    StarSystem ns = map.GetSystem(nb);
                    if (ns == null || ns.owner != faction) continue;     // 所有回廊の連なりのみ
                    if (blocked != null && blocked.Contains(nb)) continue; // 敵ZOC下は通れない
                    reached.Add(nb);
                    queue.Enqueue(nb);
                }
            }
            return reached;
        }

        /// <summary>前線 <paramref name="target"/> が補給源から補給線で繋がっているか（ZOC遮断を考慮）。</summary>
        public static bool IsSupplied(GalaxyMap map, Faction faction, IEnumerable<int> sources, int target, ISet<int> blocked = null)
            => SuppliedSystems(map, faction, sources, blocked).Contains(target);

        /// <summary>
        /// 前線備蓄の1ターン更新（L-2）。補給線が通っていれば補給で増え、断たれていれば消費で枯れる（滅びの時計）。
        /// 補給/消費は全資源一律で簡約（物資/弾薬/燃料）。<see cref="ResourceStockpile.IsDepleted"/> で補給切れを判定する。
        /// </summary>
        public static void TickFront(ResourceStockpile frontStock, bool supplied, float resupplyRate, float consumeRate, float dt)
        {
            if (frontStock == null || dt <= 0f) return;
            float delta = supplied ? Mathf.Max(0f, resupplyRate) : -Mathf.Max(0f, consumeRate);
            frontStock.AddAll(delta * dt);
        }
    }
}
