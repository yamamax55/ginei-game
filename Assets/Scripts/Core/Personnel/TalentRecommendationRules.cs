using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 提督の能力に見合う特技を決定論で推薦する（本作オリジナル）。手置き（<c>SampleScenarioCreator</c>）でなく
    /// 自動生成・ランダム提督にも「得意分野に合った特技」を一括付与するための唯一の窓口。
    /// 数式は持たず <see cref="TalentRules"/>（素養ゲート/格）と <see cref="TalentCatalog"/> へ委譲＝二重実装しない。
    /// 「得意な素養ほど高い格の代表特技を持つ」＝有能な提督は強い特技を冴えさせる。決定論（roll不要）・test-first。
    /// </summary>
    public static class TalentRecommendationRules
    {
        /// <summary>素養（実効能力）で扱える最高の格。閾値は <see cref="TalentRules.RequiredStat"/> と一致。</summary>
        public static TalentGrade BestGrade(float stat)
        {
            if (stat >= TalentRules.RequiredStat(TalentGrade.神)) return TalentGrade.神; // 90
            if (stat >= TalentRules.RequiredStat(TalentGrade.特)) return TalentGrade.特; // 75
            if (stat >= TalentRules.RequiredStat(TalentGrade.上)) return TalentGrade.上; // 55
            if (stat >= TalentRules.RequiredStat(TalentGrade.中)) return TalentGrade.中; // 35
            return TalentGrade.初;
        }

        // 素養の列挙順（得意度が同点のときの決定論タイブレーク）。
        private static readonly TalentAspect[] AspectOrder =
            { TalentAspect.武勇, TalentAspect.知略, TalentAspect.統率, TalentAspect.政務 };

        /// <summary>
        /// その素養を司る代表特技を1つ選ぶ（決定論）。優先＝特性＞戦法、常時＞条件付き、その後 id 昇順。
        /// <paramref name="exclude"/> に含まれる defId は飛ばす（重複回避）。該当なしは null。
        /// </summary>
        public static TalentDef BestTalentForAspect(TalentAspect aspect, ICollection<string> exclude)
        {
            TalentDef best = null;
            int bestScore = int.MaxValue;
            var all = TalentCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                TalentDef d = all[i];
                if (d == null || d.aspect != aspect) continue;
                if (exclude != null && exclude.Contains(d.id)) continue;
                int score = (d.kind == TalentKind.特性 ? 0 : 100)
                          + (d.condition == SkillCondition.常時 ? 0 : 10);
                if (best == null || score < bestScore
                    || (score == bestScore && string.CompareOrdinal(d.id, best.id) < 0))
                {
                    best = d; bestScore = score;
                }
            }
            return best;
        }

        /// <summary>
        /// 提督に見合う特技を最大 <paramref name="max"/> 件推薦する。得意な素養（実効能力が高い順・同点は
        /// 武勇＞知略＞統率＞政務）から代表特技を1つずつ拾い、格は素養で扱える最高（<see cref="BestGrade"/>）。
        /// defId は重複させない。null/max≤0 は空。
        /// </summary>
        public static List<Talent> Recommend(AdmiralData admiral, int max = 2)
        {
            var result = new List<Talent>();
            if (admiral == null || max <= 0) return result;

            // 素養を実効能力で降順に並べる（決定論：同点は AspectOrder＝挿入安定で前優先）。
            var ranked = new List<TalentAspect>(AspectOrder);
            ranked.Sort((a, b) =>
            {
                float sa = TalentRules.AspectStat(admiral, a);
                float sb = TalentRules.AspectStat(admiral, b);
                if (sb > sa) return 1;
                if (sb < sa) return -1;
                return System.Array.IndexOf(AspectOrder, a) - System.Array.IndexOf(AspectOrder, b);
            });

            var chosen = new HashSet<string>();
            for (int i = 0; i < ranked.Count && result.Count < max; i++)
            {
                TalentAspect aspect = ranked[i];
                float stat = TalentRules.AspectStat(admiral, aspect);
                TalentDef def = BestTalentForAspect(aspect, chosen);
                if (def == null) continue;
                result.Add(new Talent(def.id, BestGrade(stat)));
                chosen.Add(def.id);
            }
            return result;
        }
    }
}
