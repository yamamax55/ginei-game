using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>国政議員の名簿の調整値。</summary>
    public readonly struct LegislatorRosterParams
    {
        /// <summary>党首の党内基盤（候補の評価に使う）。</summary>
        public readonly float leaderStanding;
        /// <summary>党首以外の党内基盤。</summary>
        public readonly float memberStanding;
        /// <summary>名簿へ反映済みの選挙IDを覚えておく件数（古いものから捨てる）。</summary>
        public readonly int maxAssignedLog;

        public LegislatorRosterParams(float leaderStanding, float memberStanding, int maxAssignedLog)
        {
            this.leaderStanding = Mathf.Clamp01(leaderStanding);
            this.memberStanding = Mathf.Clamp01(memberStanding);
            this.maxAssignedLog = Mathf.Max(1, maxAssignedLog);
        }

        /// <summary>既定＝党首基盤0.8・党員0.5・反映ログ16件。</summary>
        public static LegislatorRosterParams Default => new LegislatorRosterParams(0.8f, 0.5f, 16);
    }

    /// <summary>一回の開票を名簿へ反映した結果。</summary>
    public struct LegislatorAssignment
    {
        /// <summary>同じ選挙IDを既に反映済みだった（何も変えていない）。</summary>
        public bool alreadyAssigned;
        /// <summary>改選議席のうち実在の人物を充てた数。</summary>
        public int named;
        /// <summary>改選議席のうち人物のいない集計議席の数。</summary>
        public int aggregate;
        public int newlyElected;
        public int reelected;
        /// <summary>改選で議席を失った現職。</summary>
        public int defeated;
    }

    /// <summary>議員資格を失った一件（通知用）。</summary>
    public struct LegislatorVacancy
    {
        public int personId;
        public LegislativeChamber chamber;
        public string reason;
    }

    /// <summary>
    /// 国政議員の名簿と個人の当選履歴の純ロジック（#2768 / #159 / #165）。
    /// 党別の整数議席（<see cref="ElectionCycleRules.RunChamberElection"/> が確定）を変えずに、その議席数以内で
    /// 実在の生存・同勢力・適格な政治家を当選者として割り当て、当選回数を開票イベントごとに1回だけ数える。
    /// 候補が足りない議席は集計議席のまま（人物を生成しない）。
    /// <para>候補の配分優先順位（党ごと・決定論）：①党首 ②改選される議席の現職 ③選挙力
    /// （<see cref="PoliticianRules.ElectoralStrength"/>＝人望・民望・弁舌・党内基盤）×地盤（出身星系が勢力の選挙区なら
    /// <see cref="PoliticianRules.HomeTurnoutBonus"/>）の高い順 ④人物ID小。上院の全区分改選では区分A→Bの順に充てる。
    /// 同じ年に両院の選挙があれば呼び出し側が下院→上院の順に反映する。</para>
    /// <para>候補から外す人：もう一方の議院の議員／改選されない区分の議員／星系知事。首相は下院議員を兼ねてよい。</para>
    /// </summary>
    public static class LegislatorRosterRules
    {
        /// <summary>人物の記録を引く（無ければ null）。</summary>
        public static LegislatorRecord Find(PoliticsState pol, int personId)
        {
            if (pol == null || pol.legislators == null || personId < 0) return null;
            for (int i = 0; i < pol.legislators.Count; i++)
                if (pol.legislators[i] != null && pol.legislators[i].personId == personId) return pol.legislators[i];
            return null;
        }

        /// <summary>現在どちらかの議院の議員か。</summary>
        public static bool IsSeated(PoliticsState pol, int personId)
        {
            LegislatorRecord r = Find(pol, personId);
            return r != null && r.seated;
        }

        /// <summary>国政の累積当選回数（年功の素材。記録が無ければ0＝不明/未当選）。能力や任命権には使わない。</summary>
        public static int TotalWins(PoliticsState pol, int personId)
        {
            LegislatorRecord r = Find(pol, personId);
            return r != null ? r.TotalWins : 0;
        }

        /// <summary>ある議院のある政党の、実在の議員の数（<paramref name="seatClass"/>＝-2 で全区分）。</summary>
        public static int NamedSeats(PoliticsState pol, LegislativeChamber chamber, int partyId, int seatClass = -2)
        {
            if (pol == null || pol.legislators == null) return 0;
            int n = 0;
            for (int i = 0; i < pol.legislators.Count; i++)
            {
                LegislatorRecord r = pol.legislators[i];
                if (r == null || !r.seated || r.seatChamber != chamber || r.seatPartyId != partyId) continue;
                if (seatClass != -2 && r.seatClass != seatClass) continue;
                n++;
            }
            return n;
        }

        /// <summary>ある議院のある政党の集計議席（確定議席−実在の議員。負にしない）。</summary>
        public static int AggregateSeats(PoliticsState pol, LegislativeChamber chamber, int partyId)
        {
            if (pol == null) return 0;
            ChamberSeats cs = chamber == LegislativeChamber.下院 ? pol.lowerSeats : pol.upperSeats;
            return Mathf.Max(0, ElectionCycleRules.SeatsOf(cs, partyId) - NamedSeats(pol, chamber, partyId));
        }

        /// <summary>ある議院の現職議員（区分→政党ID→人物ID の順）。</summary>
        public static List<LegislatorRecord> SeatedMembers(PoliticsState pol, LegislativeChamber chamber)
        {
            var list = new List<LegislatorRecord>();
            if (pol == null || pol.legislators == null) return list;
            for (int i = 0; i < pol.legislators.Count; i++)
            {
                LegislatorRecord r = pol.legislators[i];
                if (r != null && r.seated && r.seatChamber == chamber) list.Add(r);
            }
            list.Sort((a, b) => a.seatClass != b.seatClass ? a.seatClass.CompareTo(b.seatClass)
                : a.seatPartyId != b.seatPartyId ? a.seatPartyId.CompareTo(b.seatPartyId)
                : a.personId.CompareTo(b.personId));
            return list;
        }

        /// <summary>
        /// 開票結果（<paramref name="rec"/>）を名簿へ反映する：改選される議席の現職をいったん外し、党ごとに確定議席の数まで
        /// 優先順位どおりに実在の候補を充て、当選回数を数える（同じ選挙IDでは数えない）。再選されなかった現職は落選（累積は残す）。
        /// 党別の確定議席は変えない。
        /// </summary>
        public static LegislatorAssignment AssignElection(PoliticsState pol, Faction f, NationalElectionRecord rec,
            IList<Person> roster, IList<RegionalElectorate> regions, LegislatorRosterParams p)
        {
            var result = new LegislatorAssignment();
            if (pol == null || rec == null || string.IsNullOrEmpty(rec.electionId) || rec.year <= 0) return result;
            EnsureLists(pol);
            if (pol.legislatorAssignedElectionIds.Contains(rec.electionId))
            {
                result.alreadyAssigned = true;
                return result;
            }

            bool lower = rec.chamber == LegislativeChamber.下院;
            ChamberSeats cs = lower ? pol.lowerSeats : pol.upperSeats;
            if (cs == null || cs.parties == null) return result;
            bool upA = lower || rec.classUp == ElectionCycleRules.AllClasses || rec.classUp == 0;
            bool upB = !lower && (rec.classUp == ElectionCycleRules.AllClasses || rec.classUp == 1);
            if (pol.legislatorHistorySinceYear <= 0) pol.legislatorHistorySinceYear = rec.year;

            // 1) 改選される議席の現職をいったん外す（再選されれば戻る）
            var upIncumbents = new List<int>();
            for (int i = 0; i < pol.legislators.Count; i++)
            {
                LegislatorRecord r = pol.legislators[i];
                if (r == null || !r.seated || r.seatChamber != rec.chamber) continue;
                bool up = lower || (r.seatClass == 0 && upA) || (r.seatClass == 1 && upB);
                if (!up) continue;
                upIncumbents.Add(r.personId);
                r.seated = false;
            }

            // 2) 党ごと（党ID昇順）に確定議席の数まで充てる
            var partyEntries = new List<PartySeatCount>();
            for (int i = 0; i < cs.parties.Count; i++) if (cs.parties[i] != null) partyEntries.Add(cs.parties[i]);
            partyEntries.Sort((a, b) => a.partyId.CompareTo(b.partyId));

            for (int e = 0; e < partyEntries.Count; e++)
            {
                PartySeatCount entry = partyEntries[e];
                int needA = upA ? Mathf.Max(0, entry.classA) : 0;
                int needB = upB ? Mathf.Max(0, entry.classB) : 0;
                if (needA + needB <= 0) continue;

                Party party = ElectionCycleRules.FindParty(pol.parties, entry.partyId);
                List<Person> candidates = party != null
                    ? RankedCandidates(pol, f, party, roster, regions, upIncumbents, p)
                    : new List<Person>();

                int idx = 0;
                int filledA = 0, filledB = 0;
                for (; filledA < needA && idx < candidates.Count; filledA++, idx++)
                    Seat(pol, candidates[idx].id, rec, lower ? -1 : 0, entry.partyId, upIncumbents, ref result);
                for (; filledB < needB && idx < candidates.Count; filledB++, idx++)
                    Seat(pol, candidates[idx].id, rec, 1, entry.partyId, upIncumbents, ref result);
                result.named += filledA + filledB;
                result.aggregate += (needA - filledA) + (needB - filledB);
            }

            // 3) 再選されなかった現職＝落選（累積は残し、連続当選は途切れる）
            for (int i = 0; i < upIncumbents.Count; i++)
            {
                LegislatorRecord r = Find(pol, upIncumbents[i]);
                if (r == null || r.seated) continue;
                r.consecutiveWins = 0;
                ClearSeat(r, "SE" + rec.year + " の" + rec.chamber + "改選で議席を得られなかった");
                result.defeated++;
            }

            pol.legislatorAssignedElectionIds.Add(rec.electionId);
            while (pol.legislatorAssignedElectionIds.Count > p.maxAssignedLog) pol.legislatorAssignedElectionIds.RemoveAt(0);
            return result;
        }

        /// <summary>
        /// 選挙の無い時点で議員資格を現況へ合わせる（当選回数は変えない）：死亡・名簿不在・離反/在野・政治家でない・
        /// 当選した党を離れた（議席は党に帰属）・星系知事に就いた人の議席を外し、集計議席へ戻す。拘束中は議席を保つ。
        /// 外した人の一覧を返す。
        /// </summary>
        public static List<LegislatorVacancy> Reconcile(PoliticsState pol, Faction f, IList<Person> roster, int year)
        {
            var list = new List<LegislatorVacancy>();
            if (pol == null || pol.legislators == null) return list;
            for (int i = 0; i < pol.legislators.Count; i++)
            {
                LegislatorRecord r = pol.legislators[i];
                if (r == null || !r.seated) continue;
                string why = VacancyReason(pol, f, r, ElectionCycleRules.FindPerson(roster, r.personId));
                if (why == null) continue;
                LegislativeChamber chamber = r.seatChamber;
                r.consecutiveWins = 0;
                ClearSeat(r, (year > 0 ? "SE" + year + " " : "") + why);
                list.Add(new LegislatorVacancy { personId = r.personId, chamber = chamber, reason = why });
            }
            return list;
        }

        /// <summary>
        /// 一人の議員資格を外す（離党・移籍で議席の帰属する党を離れたとき。当選回数は変えず、議席は集計議席へ戻る）。
        /// 議員でなければ false。<paramref name="onlyIfPartyId"/> が0以上なら、その党の議席のときだけ外す。
        /// </summary>
        public static bool VacateSeat(PoliticsState pol, int personId, string reason, int onlyIfPartyId = -1)
        {
            LegislatorRecord r = Find(pol, personId);
            if (r == null || !r.seated) return false;
            if (onlyIfPartyId >= 0 && r.seatPartyId != onlyIfPartyId) return false;
            r.consecutiveWins = 0;
            ClearSeat(r, reason);
            return true;
        }

        /// <summary>非民主へ移った勢力の議員資格をすべて外す（履歴は残す）。外した人数を返す。</summary>
        public static int SuspendAll(PoliticsState pol, string reason)
        {
            if (pol == null || pol.legislators == null) return 0;
            int n = 0;
            for (int i = 0; i < pol.legislators.Count; i++)
            {
                LegislatorRecord r = pol.legislators[i];
                if (r == null || !r.seated) continue;
                r.consecutiveWins = 0;
                ClearSeat(r, reason ?? "");
                n++;
            }
            return n;
        }

        /// <summary>
        /// シナリオが明示した開始前の国政当選歴を記録する（明示値がある場合だけ呼ぶ。議席は与えない）。
        /// 年は0で不明のまま。負数は0に丸める。
        /// </summary>
        public static LegislatorRecord SeedPriorHistory(PoliticsState pol, int personId, int priorLowerWins, int priorUpperWins,
            int firstWinYear, int lastWinYear)
        {
            if (pol == null || personId < 0) return null;
            EnsureLists(pol);
            LegislatorRecord r = Find(pol, personId);
            if (r == null)
            {
                r = new LegislatorRecord(personId);
                pol.legislators.Add(r);
            }
            r.priorKnown = true;
            r.priorLowerWins = Mathf.Max(0, priorLowerWins);
            r.priorUpperWins = Mathf.Max(0, priorUpperWins);
            if (firstWinYear > 0 && (r.firstWinYear <= 0 || firstWinYear < r.firstWinYear)) r.firstWinYear = firstWinYear;
            if (lastWinYear > r.lastWinYear) r.lastWinYear = lastWinYear;
            return r;
        }

        /// <summary>
        /// セーブから読んだ名簿の穴埋め（読込だけで当選は数えない）：null のリスト・文字列を空にし、壊れた記録・重複を除き、
        /// 構成されていない議院の議席を外し、党の確定議席を超える実在議員を人物ID大の方から外す（議席総数を超えない）。
        /// </summary>
        public static void NormalizeLoaded(PoliticsState pol)
        {
            if (pol == null) return;
            EnsureLists(pol);
            var seen = new List<int>();
            for (int i = 0; i < pol.legislators.Count; i++)
            {
                LegislatorRecord r = pol.legislators[i];
                if (r == null || r.personId < 0 || seen.Contains(r.personId))
                {
                    pol.legislators.RemoveAt(i);
                    i--;
                    continue;
                }
                seen.Add(r.personId);
                if (r.lastLowerElectionId == null) r.lastLowerElectionId = "";
                if (r.lastUpperElectionId == null) r.lastUpperElectionId = "";
                if (r.seatElectionId == null) r.seatElectionId = "";
                if (r.statusReason == null) r.statusReason = "";
                if (!r.seated) continue;
                ChamberSeats cs = r.seatChamber == LegislativeChamber.下院 ? pol.lowerSeats : pol.upperSeats;
                if (cs == null || !cs.seated) ClearSeat(r, "議院が未構成のため議席なし");
                else if (r.seatChamber == LegislativeChamber.下院) r.seatClass = -1;
                else if (r.seatClass != 0 && r.seatClass != 1) ClearSeat(r, "改選区分が不正なため議席なし");
            }
            for (int i = pol.legislatorAssignedElectionIds.Count - 1; i >= 0; i--)
                if (string.IsNullOrEmpty(pol.legislatorAssignedElectionIds[i])) pol.legislatorAssignedElectionIds.RemoveAt(i);

            TrimOverfilled(pol, LegislativeChamber.下院);
            TrimOverfilled(pol, LegislativeChamber.上院);
        }

        // ===== 内部 =====

        private static void EnsureLists(PoliticsState pol)
        {
            if (pol.legislators == null) pol.legislators = new List<LegislatorRecord>();
            if (pol.legislatorAssignedElectionIds == null) pol.legislatorAssignedElectionIds = new List<string>();
        }

        /// <summary>党の候補を優先順位どおりに並べる（資格のない人・他院/非改選区分の議員・知事は除く）。</summary>
        private static List<Person> RankedCandidates(PoliticsState pol, Faction f, Party party, IList<Person> roster,
            IList<RegionalElectorate> regions, List<int> upIncumbents, LegislatorRosterParams p)
        {
            List<Person> list = ElectionCycleRules.SortedById(roster, x =>
                ElectionCycleRules.IsEligiblePolitician(x, f)
                && party.memberIds != null && party.memberIds.Contains(x.id)
                && !IsSeated(pol, x.id)
                && LocalElectionRules.GovernedSystemOf(pol, x.id, -1) < 0);

            var scores = new Dictionary<int, float>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                Person x = list[i];
                bool leader = party.leaderId == x.id;
                float s = PoliticianRules.ElectoralStrength(ElectionCycleRules.ProfileOf(x, leader ? p.leaderStanding : p.memberStanding));
                if (IsHomeRegion(regions, x.birthSystemId)) s *= PoliticianRules.HomeTurnoutBonus;
                scores[x.id] = s;
            }
            list.Sort((a, b) =>
            {
                bool la = party.leaderId == a.id, lb = party.leaderId == b.id;
                if (la != lb) return la ? -1 : 1;
                bool ia = upIncumbents.Contains(a.id), ib = upIncumbents.Contains(b.id);
                if (ia != ib) return ia ? -1 : 1;
                float sa = scores[a.id], sb = scores[b.id];
                if (sa != sb) return sb.CompareTo(sa);
                return a.id.CompareTo(b.id);
            });
            return list;
        }

        private static bool IsHomeRegion(IList<RegionalElectorate> regions, int systemId)
        {
            if (regions == null || systemId < 0) return false;
            for (int i = 0; i < regions.Count; i++)
                if (regions[i].systemId == systemId) return true;
            return false;
        }

        private static void Seat(PoliticsState pol, int personId, NationalElectionRecord rec, int seatClass, int partyId,
            List<int> upIncumbents, ref LegislatorAssignment result)
        {
            LegislatorRecord r = Find(pol, personId);
            if (r == null)
            {
                r = new LegislatorRecord(personId) { recordStartYear = rec.year };
                pol.legislators.Add(r);
            }
            if (r.recordStartYear <= 0) r.recordStartYear = rec.year;

            bool lower = rec.chamber == LegislativeChamber.下院;
            bool incumbent = upIncumbents.Contains(personId);
            string lastId = lower ? r.lastLowerElectionId : r.lastUpperElectionId;
            if (lastId != rec.electionId)
            {
                if (lower) { r.lowerWins++; r.lastLowerElectionId = rec.electionId; }
                else { r.upperWins++; r.lastUpperElectionId = rec.electionId; }
                r.consecutiveWins = incumbent ? r.consecutiveWins + 1 : 1;
                if (r.firstWinYear <= 0) r.firstWinYear = rec.year;
                if (rec.year > r.lastWinYear) r.lastWinYear = rec.year;
                if (incumbent) result.reelected++; else result.newlyElected++;
            }

            r.seated = true;
            r.seatChamber = rec.chamber;
            r.seatClass = lower ? -1 : seatClass;
            r.seatPartyId = partyId;
            r.seatElectedYear = rec.year;
            r.seatElectionId = rec.electionId;
            r.statusReason = "";
        }

        private static void ClearSeat(LegislatorRecord r, string reason)
        {
            r.seated = false;
            r.seatPartyId = -1;
            r.seatElectionId = "";
            r.statusReason = reason ?? "";
        }

        private static string VacancyReason(PoliticsState pol, Faction f, LegislatorRecord r, Person person)
        {
            if (person == null) return "名簿に居ないため議席を失った";
            if (person.IsDeceased) return "死去により議席を失った";
            if (person.faction != f || person.isFreeAgent) return "離反・在野により議席を失った";
            if (!person.isPolitician || person.role != PersonRole.文民) return "政治家でなくなったため議席を失った";
            Party party = ElectionCycleRules.FindParty(pol.parties, r.seatPartyId);
            if (party == null || party.memberIds == null || !party.memberIds.Contains(r.personId))
                return "当選した党を離れたため議席を失った（議席は党に帰属）";
            if (LocalElectionRules.GovernedSystemOf(pol, r.personId, -1) >= 0) return "星系知事に就いたため議員を辞職";
            return null;
        }

        /// <summary>党の確定議席を超える実在議員を人物ID大の方から外す（読込データの食い違い対策）。</summary>
        private static void TrimOverfilled(PoliticsState pol, LegislativeChamber chamber)
        {
            ChamberSeats cs = chamber == LegislativeChamber.下院 ? pol.lowerSeats : pol.upperSeats;
            if (cs == null || cs.parties == null) return;
            List<LegislatorRecord> members = SeatedMembers(pol, chamber);
            members.Sort((a, b) => b.personId.CompareTo(a.personId));
            for (int i = 0; i < members.Count; i++)
            {
                LegislatorRecord r = members[i];
                PartySeatCount entry = null;
                for (int k = 0; k < cs.parties.Count; k++)
                    if (cs.parties[k] != null && cs.parties[k].partyId == r.seatPartyId) { entry = cs.parties[k]; break; }
                int limit = entry == null ? 0
                    : chamber == LegislativeChamber.下院 ? entry.Total
                    : (r.seatClass == 0 ? entry.classA : entry.classB);
                int named = chamber == LegislativeChamber.下院
                    ? NamedSeats(pol, chamber, r.seatPartyId)
                    : NamedSeats(pol, chamber, r.seatPartyId, r.seatClass);
                if (named > limit) ClearSeat(r, "党の確定議席を超えていたため議席なし");
            }
        }
    }
}
