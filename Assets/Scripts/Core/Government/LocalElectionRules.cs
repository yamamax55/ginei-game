using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>星系知事選の調整値。</summary>
    public readonly struct LocalElectionParams
    {
        /// <summary>知事の任期（年）。</summary>
        public readonly int governorTermYears;
        /// <summary>不成立のとき再実施までの年数。</summary>
        public readonly int retryYears;
        /// <summary>新たに編入した星系・民主化した星系の初回知事選までの猶予（年）。</summary>
        public readonly int acquiredGraceYears;
        /// <summary>地域の安定度が与党候補の票を増減させる幅（国政より大きい＝地方の民意は政権評価に敏感）。</summary>
        public readonly float rulingSwing;
        /// <summary>現職の強さの幅（安定した星系ほど現職有利・荒れた星系では現職不利）。</summary>
        public readonly float incumbencyMax;
        /// <summary>党の綱領が星系の土着思想と一致するときの上乗せ。</summary>
        public readonly float ideologyBonus;
        /// <summary>党首の党内基盤（党首以外は <see cref="memberStanding"/>）。</summary>
        public readonly float leaderStanding;
        public readonly float memberStanding;

        public LocalElectionParams(int governorTermYears, int retryYears, int acquiredGraceYears,
                                   float rulingSwing, float incumbencyMax, float ideologyBonus,
                                   float leaderStanding, float memberStanding)
        {
            this.governorTermYears = Mathf.Max(1, governorTermYears);
            this.retryYears = Mathf.Max(1, retryYears);
            this.acquiredGraceYears = Mathf.Max(0, acquiredGraceYears);
            this.rulingSwing = Mathf.Clamp(rulingSwing, 0f, 0.9f);
            this.incumbencyMax = Mathf.Clamp(incumbencyMax, 0f, 0.9f);
            this.ideologyBonus = Mathf.Max(0f, ideologyBonus);
            this.leaderStanding = Mathf.Clamp01(leaderStanding);
            this.memberStanding = Mathf.Clamp01(memberStanding);
        }

        /// <summary>既定＝任期4年・再実施1年後・編入猶予1年・与党補正±0.3・現職補正±0.25・思想一致+0.2・党首基盤0.8/党員0.5。</summary>
        public static LocalElectionParams Default => new LocalElectionParams(4, 1, 1, 0.3f, 0.25f, 0.2f, 0.8f, 0.5f);
    }

    /// <summary>知事選で起きたことの種類。</summary>
    public enum LocalElectionEventKind { 当選, 再選, 不成立, 失職 }

    /// <summary>知事選の出来事（通知・政府役職の反映に使う）。</summary>
    public struct LocalElectionEvent
    {
        public int systemId;
        public LocalElectionEventKind kind;
        /// <summary>当選者（当選/再選）。</summary>
        public int personId;
        /// <summary>前の知事（-1=いない）。失職ではこの人物の権限を外す。</summary>
        public int previousPersonId;
        public int partyId;
        public int nextElectionYear;
        public string reason;
    }

    /// <summary>
    /// 星系知事選の純ロジック（地方選挙・任期4年）。候補は実在の生存・同勢力の文民政治家だけ（捏造しない）。
    /// 票は現地の人口・安定度・土着思想・現職かどうか・候補の人望で決まり、国政の結果をそのまま写さない。
    /// 当選者の決定は <see cref="ElectionRules.WinnerByPlurality"/>（同票は人物ID小）、候補の票の素は <see cref="PoliticianRules.RegionVotes"/>。
    /// 首相と知事、複数星系の知事は兼ねられない。政府役職への反映は呼び出し側。決定論・同じ年の再処理で二重に更新しない。
    /// </summary>
    public static class LocalElectionRules
    {
        /// <summary>知事選の ID（勢力:知事:星系:年）。</summary>
        public static string LocalElectionId(Faction f, int systemId, int year)
            => (int)f + ":知事:" + systemId + ":" + year;

        /// <summary>不成立の理由（候補者不在）。</summary>
        public const string NoCandidateReason = "適格な候補者がいない（生存・同勢力の文民政治家が必要。首相と他星系の知事は兼任できない）";

        /// <summary>星系の知事選の状態を引く。</summary>
        public static LocalElectionState Find(PoliticsState pol, int systemId)
        {
            if (pol == null || pol.locals == null) return null;
            for (int i = 0; i < pol.locals.Count; i++)
                if (pol.locals[i] != null && pol.locals[i].systemId == systemId) return pol.locals[i];
            return null;
        }

        /// <summary>その人物が知事を務める星系（<paramref name="exceptSystemId"/> を除く。無ければ -1）。</summary>
        public static int GovernedSystemOf(PoliticsState pol, int personId, int exceptSystemId)
        {
            if (pol == null || pol.locals == null || personId < 0) return -1;
            for (int i = 0; i < pol.locals.Count; i++)
            {
                LocalElectionState l = pol.locals[i];
                if (l != null && l.systemId != exceptSystemId && l.governorPersonId == personId) return l.systemId;
            }
            return -1;
        }

        /// <summary>
        /// 一候補の得票＝<see cref="PoliticianRules.RegionVotes"/>（人口×人気×地盤）×党の地力（0.5＋支持率）
        /// ×政権評価（安定なら与党に・荒れれば野党に）×現職の強さ（安定なら有利・荒れれば不利）×思想一致。無所属は党補正なし。
        /// </summary>
        public static float CandidateVotes(PoliticianProfile profile, bool hasParty, float partySupport, bool isRulingParty,
            bool isIncumbent, bool platformMatches, LocalConstituency k, LocalElectionParams p)
        {
            float baseVotes = PoliticianRules.RegionVotes(profile, k.population, ElectionCycleRules.RegionKey(k.systemId));
            if (baseVotes <= 0f) return 0f;
            float mood = Mathf.Clamp01(k.stability01) * 2f - 1f; // -1 荒廃 .. +1 安定
            float partyBase = hasParty ? 0.5f + Mathf.Clamp01(partySupport) : 1f;
            float ruling = !hasParty ? 1f : (isRulingParty ? 1f + p.rulingSwing * mood : 1f - p.rulingSwing * mood);
            float incumbency = isIncumbent ? 1f + p.incumbencyMax * mood : 1f;
            float ideology = platformMatches ? 1f + p.ideologyBonus : 1f;
            return Mathf.Max(0f, baseVotes * partyBase * ruling * incumbency * ideology);
        }

        /// <summary>
        /// 所有星系と知事選の台帳を突き合わせる：手放した星系（占領/離反/割譲）は知事を失職させて台帳から外し、
        /// 新しい星系は日程を組む（初回の編成はその年、以後の編入・民主化は猶予つき）。台帳は星系ID昇順に並べる。
        /// </summary>
        public static List<LocalElectionEvent> Reconcile(PoliticsState pol, IList<LocalConstituency> owned, int year, LocalElectionParams p)
        {
            var events = new List<LocalElectionEvent>();
            if (pol == null) return events;
            if (pol.locals == null) pol.locals = new List<LocalElectionState>();

            var ownedIds = new List<int>();
            if (owned != null)
                for (int i = 0; i < owned.Count; i++)
                    if (!ownedIds.Contains(owned[i].systemId)) ownedIds.Add(owned[i].systemId);
            ownedIds.Sort();

            for (int i = pol.locals.Count - 1; i >= 0; i--)
            {
                LocalElectionState rec = pol.locals[i];
                if (rec == null) { pol.locals.RemoveAt(i); continue; }
                if (ownedIds.Contains(rec.systemId)) continue;
                if (rec.governorPersonId >= 0)
                    events.Add(new LocalElectionEvent
                    {
                        systemId = rec.systemId, kind = LocalElectionEventKind.失職,
                        personId = -1, previousPersonId = rec.governorPersonId, partyId = rec.governorPartyId,
                        reason = "星系が勢力を離れた（占領・離反・割譲）ため知事の権限を失った",
                    });
                pol.locals.RemoveAt(i);
            }

            bool first = !pol.localsSeeded;
            for (int i = 0; i < ownedIds.Count; i++)
            {
                LocalElectionState rec = Find(pol, ownedIds[i]);
                if (rec == null)
                {
                    rec = new LocalElectionState(ownedIds[i]);
                    rec.nextElectionYear = first ? year : year + p.acquiredGraceYears;
                    rec.reason = first ? "" : "新規編入：SE" + rec.nextElectionYear + " に知事選";
                    pol.locals.Add(rec);
                }
                else if (rec.status == LocalElectionStatus.対象外)
                {
                    rec.status = LocalElectionStatus.未実施;
                    rec.nextElectionYear = year + p.acquiredGraceYears;
                    rec.reason = "民主政へ移行：SE" + rec.nextElectionYear + " に知事選";
                }
            }
            pol.locals.Sort((a, b) => a.systemId.CompareTo(b.systemId));
            pol.localsSeeded = true;
            return events;
        }

        /// <summary>
        /// 期日の来た知事選を行う。先に現職の資格を確かめ（死亡・拘束・離反・首相就任なら失職→その年に補欠選挙）、
        /// 次に期日の星系ごとに候補を集めて開票する。候補ゼロは不成立（理由と再実施年を記録・当選者は作らない）。
        /// </summary>
        public static List<LocalElectionEvent> RunDue(PoliticsState pol, Faction f, int year, IList<LocalConstituency> owned,
            IList<Person> roster, LocalElectionParams p)
        {
            var events = new List<LocalElectionEvent>();
            if (pol == null || pol.locals == null) return events;
            int premierId = pol.government != null ? pol.government.premierPersonId : -1;
            int rulingPartyId = pol.government != null ? pol.government.partyId : -1;

            // 1) 現職の資格
            for (int i = 0; i < pol.locals.Count; i++)
            {
                LocalElectionState rec = pol.locals[i];
                if (rec == null || rec.governorPersonId < 0) continue;
                string why = null;
                if (!ElectionCycleRules.IsEligiblePolitician(ElectionCycleRules.FindPerson(roster, rec.governorPersonId), f))
                    why = "知事が死亡・拘束・離反などで職務を続けられない";
                else if (rec.governorPersonId == premierId)
                    why = "首相就任により知事を辞職";
                if (why == null) continue;

                events.Add(new LocalElectionEvent
                {
                    systemId = rec.systemId, kind = LocalElectionEventKind.失職,
                    personId = -1, previousPersonId = rec.governorPersonId, partyId = rec.governorPartyId,
                    nextElectionYear = year, reason = why,
                });
                rec.governorPersonId = -1;
                rec.governorPartyId = -1;
                rec.termEndYear = 0;
                rec.status = LocalElectionStatus.失職;
                rec.reason = why + "（補欠選挙）";
                rec.nextElectionYear = year;
            }

            // 2) 期日の来た選挙（星系ID昇順）
            for (int i = 0; i < pol.locals.Count; i++)
            {
                LocalElectionState rec = pol.locals[i];
                if (rec == null || rec.status == LocalElectionStatus.対象外) continue;
                if (rec.nextElectionYear <= 0 || rec.nextElectionYear > year) continue;
                if (rec.lastAttemptYear >= year) continue; // 同じ年の再処理を防ぐ
                if (!TryGetConstituency(owned, rec.systemId, out LocalConstituency k)) continue;

                rec.lastAttemptYear = year;
                rec.lastElectionId = LocalElectionId(f, rec.systemId, year);

                List<Person> candidates = ElectionCycleRules.SortedById(roster, x =>
                    ElectionCycleRules.IsEligiblePolitician(x, f) && x.id != premierId
                    && GovernedSystemOf(pol, x.id, rec.systemId) < 0);

                var tallies = new List<VoteTally>(candidates.Count);
                var results = new List<LocalCandidateResult>(candidates.Count);
                for (int c = 0; c < candidates.Count; c++)
                {
                    Person cand = candidates[c];
                    Party party = ElectionCycleRules.PartyOf(pol.parties, cand.id);
                    float standing = party != null && party.leaderId == cand.id ? p.leaderStanding : p.memberStanding;
                    bool platformMatches = party != null && !string.IsNullOrEmpty(party.platform) && party.platform == k.nativeIdeology;
                    float v = CandidateVotes(ElectionCycleRules.ProfileOf(cand, standing), party != null,
                        party != null ? party.support : 0f, party != null && party.id == rulingPartyId,
                        cand.id == rec.governorPersonId, platformMatches, k, p);
                    tallies.Add(new VoteTally(cand.id, v));
                    results.Add(new LocalCandidateResult { personId = cand.id, partyId = party != null ? party.id : -1, votes = v });
                }

                int winner = ElectionRules.WinnerByPlurality(tallies, out _);
                if (winner < 0)
                {
                    string why = candidates.Count == 0 ? NoCandidateReason : "有効票がない（有権者がいない）";
                    rec.nextElectionYear = year + p.retryYears;
                    if (rec.governorPersonId < 0) rec.status = LocalElectionStatus.不成立;
                    rec.reason = why;
                    rec.lastResults.Clear();
                    events.Add(new LocalElectionEvent
                    {
                        systemId = rec.systemId, kind = LocalElectionEventKind.不成立,
                        personId = -1, previousPersonId = rec.governorPersonId, partyId = -1,
                        nextElectionYear = rec.nextElectionYear, reason = why,
                    });
                    continue;
                }

                float total = ElectionRules.TotalVotes(tallies);
                for (int c = 0; c < results.Count; c++)
                    results[c].voteShare = total > 0f ? results[c].votes / total : 0f;
                results.Sort((a, b) => a.votes != b.votes ? b.votes.CompareTo(a.votes) : a.personId.CompareTo(b.personId));

                int previous = rec.governorPersonId;
                Party winnerParty = ElectionCycleRules.PartyOf(pol.parties, winner);
                rec.governorPersonId = winner;
                rec.governorPartyId = winnerParty != null ? winnerParty.id : -1;
                rec.termEndYear = year + p.governorTermYears;
                rec.nextElectionYear = year + p.governorTermYears;
                rec.lastElectionYear = year;
                rec.status = LocalElectionStatus.当選;
                rec.reason = "";
                rec.lastResults = results;
                events.Add(new LocalElectionEvent
                {
                    systemId = rec.systemId,
                    kind = previous == winner ? LocalElectionEventKind.再選 : LocalElectionEventKind.当選,
                    personId = winner, previousPersonId = previous, partyId = rec.governorPartyId,
                    nextElectionYear = rec.nextElectionYear, reason = "",
                });
            }
            return events;
        }

        /// <summary>
        /// 非民主の政体へ移った勢力の知事選を止める（選出知事は失職・台帳は対象外として残す）。止めた星系ぶんの出来事を返す。
        /// </summary>
        public static List<LocalElectionEvent> Suspend(PoliticsState pol, string reason)
        {
            var events = new List<LocalElectionEvent>();
            if (pol == null || pol.locals == null) return events;
            for (int i = 0; i < pol.locals.Count; i++)
            {
                LocalElectionState rec = pol.locals[i];
                if (rec == null || rec.status == LocalElectionStatus.対象外) continue;
                if (rec.governorPersonId >= 0)
                    events.Add(new LocalElectionEvent
                    {
                        systemId = rec.systemId, kind = LocalElectionEventKind.失職,
                        personId = -1, previousPersonId = rec.governorPersonId, partyId = rec.governorPartyId,
                        reason = reason ?? "",
                    });
                rec.governorPersonId = -1;
                rec.governorPartyId = -1;
                rec.termEndYear = 0;
                rec.nextElectionYear = 0;
                rec.status = LocalElectionStatus.対象外;
                rec.reason = reason ?? "";
            }
            return events;
        }

        private static bool TryGetConstituency(IList<LocalConstituency> owned, int systemId, out LocalConstituency k)
        {
            k = default(LocalConstituency);
            if (owned == null) return false;
            for (int i = 0; i < owned.Count; i++)
                if (owned[i].systemId == systemId) { k = owned[i]; return true; }
            return false;
        }
    }
}
