using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 徽章の保有台帳（#徽章・static）。`BadgeRules.Derive` が状態から導出する階級/兵科/技能章に対し、
    /// ここは<b>明示付与の徽章（部隊章 unit 等）</b>を人物ごとに保持する。導出＋付与を合わせた一覧も返す。
    /// 勲章台帳 `MedalRegistry`#2263 とは別物（あちらは merit、こちらは identity/所属）。
    /// </summary>
    public static class BadgeRegistry
    {
        private static readonly Dictionary<int, List<Badge>> granted = new Dictionary<int, List<Badge>>();

        /// <summary>明示的に徽章を付与する（部隊章など導出されないもの）。</summary>
        public static void Grant(int personId, Badge badge)
        {
            if (!granted.TryGetValue(personId, out List<Badge> list))
            {
                list = new List<Badge>();
                granted[personId] = list;
            }
            list.Add(badge);
        }

        /// <summary>部隊章を付与する簡易窓口。</summary>
        public static Badge GrantUnitBadge(int personId, string unitName)
        {
            Badge b = new Badge(BadgeKind.部隊章, unitName);
            Grant(personId, b);
            return b;
        }

        /// <summary>明示付与ぶんの徽章（無ければ空）。</summary>
        public static int GrantedCount(int personId)
            => granted.TryGetValue(personId, out List<Badge> list) ? list.Count : 0;

        /// <summary>導出（階級/兵科/技能）＋明示付与（部隊章等）を合わせた着用徽章一覧。</summary>
        public static List<Badge> AllBadges(int personId, int rankTier, PersonRole role, bool isSpecialForces, bool isStaff)
        {
            List<Badge> all = BadgeRules.Derive(rankTier, role, isSpecialForces, isStaff);
            if (granted.TryGetValue(personId, out List<Badge> list)) all.AddRange(list);
            return all;
        }

        /// <summary>台帳を空にする（戦役の作り直し）。</summary>
        public static void Clear() => granted.Clear();
    }
}
