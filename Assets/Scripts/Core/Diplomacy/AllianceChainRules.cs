using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 同盟連鎖（連合形成・SARA-2／DIPLO・#2119）。「A と B が同盟、B と C が同盟」なら、共通の同盟相手を持つ
    /// A↔C の関係値を少しずつ引き上げる＝連合（ブロック）が緩やかに収束する。実体は <see cref="DiplomacyRules.AdjustOpinion"/>
    /// へ委譲（並行系を作らない・additive）。既に同盟済み/関係が既に良好なペアは触らない。決定論・test-first。
    /// </summary>
    public static class AllianceChainRules
    {
        /// <summary>この値以上の関係値なら既に十分良好としてドリフトしない。</summary>
        public const float GoodEnoughOpinion = 50f;

        /// <summary>
        /// 同盟の推移的な引力を1回ぶん適用する。共通の同盟相手 B を持つ非同盟ペア (A,C) の関係値を
        /// <paramref name="drift"/> ぶん引き上げる（関係値が <see cref="GoodEnoughOpinion"/> 未満のときだけ）。
        /// 返り値＝ドリフトを適用したペア数。
        /// </summary>
        public static int FormTransitiveAlliances(DiplomacyState state, float drift)
        {
            if (state == null || drift == 0f) return 0;

            // 各勢力の同盟相手集合を構築。
            var allies = new Dictionary<string, HashSet<string>>();
            for (int i = 0; i < state.entries.Count; i++)
            {
                DiplomacyState.Entry e = state.entries[i];
                if (e == null || e.status != DiplomacyState.DiplomaticStatus.同盟) continue;
                AddAlly(allies, e.factionA, e.factionB);
                AddAlly(allies, e.factionB, e.factionA);
            }

            int changed = 0;
            var seen = new HashSet<string>();
            foreach (KeyValuePair<string, HashSet<string>> kv in allies)
            {
                var list = new List<string>(kv.Value); // B の同盟相手たち＝互いに「共通の同盟相手 B」を持つ
                for (int i = 0; i < list.Count; i++)
                {
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        string a = list[i], c = list[j];
                        if (a == c) continue;
                        string key = PairKey(a, c);
                        if (seen.Contains(key)) continue;
                        seen.Add(key);
                        if (state.Status(a, c) == DiplomacyState.DiplomaticStatus.同盟) continue; // 既に同盟
                        if (state.Opinion(a, c) >= GoodEnoughOpinion) continue;                   // 既に良好
                        DiplomacyRules.AdjustOpinion(state, a, c, drift);
                        changed++;
                    }
                }
            }
            return changed;
        }

        private static void AddAlly(Dictionary<string, HashSet<string>> map, string from, string to)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;
            if (!map.TryGetValue(from, out HashSet<string> set)) { set = new HashSet<string>(); map[from] = set; }
            set.Add(to);
        }

        // 順不同のペアキー（区切り文字で連結し衝突を避ける）。
        private static string PairKey(string a, string b)
            => string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
    }
}
