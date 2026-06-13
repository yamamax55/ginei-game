using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 文官の省庁・要職への<b>銓衡配属</b>の年次オーケストレータ（日本の律令制・官僚制基盤・配線ロジック）。
    /// 叙位された文官（<see cref="Person.courtRank"/>）を<b>官位相当</b>（位階が役職の要求位階以上）で資格判定し、
    /// 考課（<see cref="OfficialMerit"/>）と位階で最適者を<b>銓衡</b>（<see cref="CivilServiceRules"/>）して
    /// <see cref="GovernmentRegistry"/> へ任命する。純席次で継ぐ <see cref="VacancyRules"/>（武官の要職に使用）に対し、
    /// 本ルールは<b>位階＋実績で文官を選ぶ</b>＝律令の式部省の選叙。役職の必要位階は <see cref="Office.requiredTier"/> でなく
    /// <see cref="CourtRank"/> で受ける（位階軸と登用等級軸の混同を避ける＝Office.requiredTier は0前提で civilianOnly のみ効かせる）。
    /// 純ロジック（非 MonoBehaviour・test-first）・状態は <see cref="GovernmentRegistry"/> のみ更新（基準値非破壊）。
    /// </summary>
    public static class CivilAppointmentRules
    {
        /// <summary>官位相当の資格＝文民・在任可能・武官専用でない・<b>位階が要求位階以上</b>・OfficeRules の文民制約を満たす。</summary>
        public static bool IsQualified(Person p, Office office, CourtRank requiredRank)
        {
            if (p == null || office == null) return false;
            if (!p.IsAvailable || p.role != PersonRole.文民) return false;
            if (office.militaryOnly) return false;
            if (JapaneseCourtRankRules.Compare(p.courtRank, requiredRank) < 0) return false; // 官位相当（位階が足りない）
            return OfficeRules.CanHold(p, office); // civilianOnly/政治任用などの最終ゲート（requiredTier=0前提）
        }

        /// <summary>
        /// 銓衡＝有資格の文官から、位階（<see cref="JapaneseCourtRankRules.Tier"/>）と考課で最適者を選ぶ。
        /// 同点は若い順（後進に道を開く）。適任者がいなければ null（空席のまま＝機能低下を許容）。
        /// </summary>
        public static Person SelectFor(Office office, CourtRank requiredRank, IEnumerable<Person> roster,
                                       CivilServiceRules.AppointmentParams prm)
        {
            if (roster == null || office == null) return null;
            Person best = null;
            float bestScore = float.NegativeInfinity;
            foreach (var p in roster)
            {
                if (!IsQualified(p, office, requiredRank)) continue;
                float score = CivilServiceRules.CandidateScore(JapaneseCourtRankRules.Tier(p.courtRank), p.merit, prm);
                if (score > bestScore || (Mathf.Approximately(score, bestScore) && best != null && p.birthYear > best.birthYear))
                {
                    bestScore = score;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>
        /// 空席を銓衡で埋める。在任の文民がいればそのまま（再任）。空席なら最適者を <see cref="GovernmentRegistry"/> へ任命し、
        /// 就任した <see cref="Person"/> を返す（適任不在は null＝空席）。在任者の解任（死亡/位階喪失等）は呼び出し側の責務。
        /// </summary>
        public static Person FillVacancy(Faction faction, Office office, CourtRank requiredRank,
                                         IEnumerable<Person> roster, CivilServiceRules.AppointmentParams prm, int scopeKey = 0)
        {
            if (office == null) return null;
            var current = GovernmentRegistry.GetHolder(office, scopeKey) as Person;
            if (current != null && current.IsAvailable && current.role == PersonRole.文民) return current; // 再任

            Person pick = SelectFor(office, requiredRank, roster, prm);
            if (pick == null) return null;
            return GovernmentRegistry.TryAppoint(faction, office, pick, scopeKey) ? pick : null;
        }
    }
}
