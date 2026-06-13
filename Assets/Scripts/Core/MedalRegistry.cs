using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 勲章の保有台帳（#2263・static）。人物id→保有勲章リスト。叙勲（<see cref="MedalRules.Award"/>）の保存先で、
    /// 恩給・名誉の算定（<see cref="MedalRules"/>）が読む唯一の窓口。`NamedAssetRegistry`#2063 と同型の有界台帳。
    /// </summary>
    public static class MedalRegistry
    {
        private static readonly Dictionary<int, List<Decoration>> byPerson = new Dictionary<int, List<Decoration>>();
        private static readonly List<Decoration> Empty = new List<Decoration>();

        /// <summary>人物に勲章を叙勲（保存）する。</summary>
        public static void Award(int personId, Decoration decoration)
        {
            if (!byPerson.TryGetValue(personId, out List<Decoration> list))
            {
                list = new List<Decoration>();
                byPerson[personId] = list;
            }
            list.Add(decoration);
        }

        /// <summary>戦功と種別から叙勲して保存する簡易窓口。</summary>
        public static Decoration Award(int personId, MedalKind kind, float meritScore, int year = 0, string citation = "")
        {
            Decoration d = MedalRules.Award(kind, meritScore, year, citation);
            Award(personId, d);
            return d;
        }

        /// <summary>人物の保有勲章（無ければ空リスト）。</summary>
        public static IReadOnlyList<Decoration> Decorations(int personId)
            => byPerson.TryGetValue(personId, out List<Decoration> list) ? list : Empty;

        /// <summary>人物の保有勲章数。</summary>
        public static int Count(int personId)
            => byPerson.TryGetValue(personId, out List<Decoration> list) ? list.Count : 0;

        /// <summary>人物の恩給倍率（保有勲章から・<see cref="MedalRules.PensionFactor"/>）。</summary>
        public static float PensionFactor(int personId) => MedalRules.PensionFactor(Decorations(personId));

        /// <summary>人物の名誉点（保有勲章から・<see cref="MedalRules.Prestige"/>）。</summary>
        public static float Prestige(int personId) => MedalRules.Prestige(Decorations(personId));

        /// <summary>台帳を空にする（戦役の作り直し）。</summary>
        public static void Clear() => byPerson.Clear();
    }
}
