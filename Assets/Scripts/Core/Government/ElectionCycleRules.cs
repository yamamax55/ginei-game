using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>国政選挙の調整値。</summary>
    public readonly struct ElectionCycleParams
    {
        /// <summary>下院の定数（全議席改選）。</summary>
        public readonly int lowerHouseSeats;
        /// <summary>上院の定数（区分 A/B に分けて半数改選。奇数なら A が1多い）。</summary>
        public readonly int upperHouseSeats;
        /// <summary>保持する国政選挙の開票記録の上限（古いものから捨てる）。</summary>
        public readonly int maxNationalRecords;
        /// <summary>地域の安定度が与党の得票を増減させる幅（安定度1で与党×(1+幅)・0で×(1−幅)）。</summary>
        public readonly float rulingStabilitySwing;

        public ElectionCycleParams(int lowerHouseSeats, int upperHouseSeats, int maxNationalRecords, float rulingStabilitySwing)
        {
            this.lowerHouseSeats = Mathf.Max(1, lowerHouseSeats);
            this.upperHouseSeats = Mathf.Max(2, upperHouseSeats);
            this.maxNationalRecords = Mathf.Max(1, maxNationalRecords);
            this.rulingStabilitySwing = Mathf.Clamp(rulingStabilitySwing, 0f, 0.5f);
        }

        /// <summary>既定＝下院300・上院120（半数60）・記録8件・与党の安定度補正±0.2。</summary>
        public static ElectionCycleParams Default => new ElectionCycleParams(300, 120, 8, 0.2f);
    }

    /// <summary>国政の1年で起きたこと（通知・政府役職の反映に使う）。</summary>
    public struct NationalYearOutcome
    {
        /// <summary>議院を初めて構成した（全議席の初回選挙）。</summary>
        public bool inaugural;
        /// <summary>下院選挙の開票記録（無ければ null）。</summary>
        public NationalElectionRecord lowerRecord;
        /// <summary>上院選挙の開票記録（無ければ null）。</summary>
        public NationalElectionRecord upperRecord;
        /// <summary>下院選挙を受けて組閣した（首相が同じ続投も含む）。</summary>
        public bool governmentFormed;
        /// <summary>首相または組閣状態が変わった。</summary>
        public bool governmentChanged;
        /// <summary>この年の処理前の首相（-1=空席）。</summary>
        public int previousPremierId;
        /// <summary>党員・党首の入れ替え件数。</summary>
        public int partyChanges;
    }

    /// <summary>
    /// 国政選挙の純ロジック（下院の総選挙・上院の半数改選・開票→確定議席→組閣）。
    /// 日程は <see cref="ElectionScheduleRules"/>、得票の集計は <see cref="ElectionRules"/>、議席の配分は <see cref="SeatAllocationRules"/>、
    /// 党首選は <see cref="LeadershipElectionRules"/>、過半数の判定は <see cref="CoalitionRules"/> に任せる（二重実装しない）。
    /// 政府役職（<see cref="GovernmentRegistry"/>）への反映は呼び出し側が行う。決定論・同じ年の再処理で二重に更新しない。
    /// </summary>
    public static class ElectionCycleRules
    {
        /// <summary>改選区分の指定＝全区分（下院の総選挙・議院の初回選挙）。</summary>
        public const int AllClasses = -1;

        /// <summary>地盤の星系キーの接頭辞。</summary>
        public const string RegionKeyPrefix = "星系:";

        // 人物から政治家の素養を推定するときの係数（民望・悪名は 0..MaxRenown で頭打ち）。
        private const float BasePopularity = 0.3f;
        private const float CharismaPopularityDivisor = 250f;
        private const float RenownPopularityDivisor = 500f;
        private const int MaxRenown = 100;
        private const int BaseIntegrity = 50;
        private const float DefaultPartyStanding = 0.5f;

        /// <summary>国政選挙の ID（勢力:議院:年:区分）。</summary>
        public static string NationalElectionId(Faction f, LegislativeChamber chamber, int year, int classUp)
            => (int)f + ":" + chamber + ":" + year + ":" + (classUp < 0 ? "全" : (classUp == 0 ? "A" : "B"));

        /// <summary>星系IDを地盤キーにする。</summary>
        public static string RegionKey(int systemId) => RegionKeyPrefix + systemId;

        /// <summary>両院とも構成済みか（初回選挙が済んでいるか）。</summary>
        public static bool IsSeated(PoliticsState pol)
            => pol != null && pol.lowerSeats != null && pol.lowerSeats.seated
               && pol.upperSeats != null && pol.upperSeats.seated;

        /// <summary>議席の器が無ければ作る（定数は既定値）。</summary>
        public static void EnsureSeats(PoliticsState pol, ElectionCycleParams prm)
        {
            if (pol == null) return;
            if (pol.lowerSeats == null)
                pol.lowerSeats = new ChamberSeats(LegislativeChamber.下院, prm.lowerHouseSeats, 0);
            if (pol.upperSeats == null)
            {
                int a = (prm.upperHouseSeats + 1) / 2;
                pol.upperSeats = new ChamberSeats(LegislativeChamber.上院, a, prm.upperHouseSeats - a);
            }
        }

        /// <summary>
        /// 選挙で選ばれる職（首相・知事）に就ける政治家か＝生存・自由・在野でない・同勢力・政治家・文民。
        /// 職種や官位は見ない（政治任用に貴族の官位ゲートを課さない）。人物の属性は書き換えない。
        /// </summary>
        public static bool IsEligiblePolitician(Person p, Faction f)
            => p != null && p.isPolitician && p.IsAvailable && !p.isFreeAgent
               && p.faction == f && p.role == PersonRole.文民;

        /// <summary>党員として残れるか（死亡・他勢力・在野・政治家でない者は離党。拘束中は党籍を保つ）。</summary>
        public static bool IsValidPartyMember(Person p, Faction f)
            => p != null && p.isPolitician && !p.IsDeceased && !p.isFreeAgent && p.faction == f;

        /// <summary>名簿から人物を ID で引く（無ければ null）。</summary>
        public static Person FindPerson(IList<Person> roster, int id)
        {
            if (roster == null || id < 0) return null;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i] != null && roster[i].id == id) return roster[i];
            return null;
        }

        /// <summary>政党を ID で引く。</summary>
        public static Party FindParty(IList<Party> parties, int partyId)
        {
            if (parties == null) return null;
            for (int i = 0; i < parties.Count; i++)
                if (parties[i] != null && parties[i].id == partyId) return parties[i];
            return null;
        }

        /// <summary>その人物が属する政党（無所属は null）。</summary>
        public static Party PartyOf(IList<Party> parties, int personId)
        {
            if (parties == null || personId < 0) return null;
            for (int i = 0; i < parties.Count; i++)
                if (parties[i] != null && parties[i].memberIds.Contains(personId)) return parties[i];
            return null;
        }

        /// <summary>議院の中のある政党の議席（区分合計）。</summary>
        public static int SeatsOf(ChamberSeats cs, int partyId)
        {
            if (cs == null || cs.parties == null) return 0;
            for (int i = 0; i < cs.parties.Count; i++)
                if (cs.parties[i] != null && cs.parties[i].partyId == partyId) return cs.parties[i].Total;
            return 0;
        }

        /// <summary>過半数に要る議席（定数の半分＋1）。</summary>
        public static int MajorityLine(int totalSeats) => totalSeats / 2 + 1;

        /// <summary>
        /// 人物から政治家の素養（<see cref="PoliticianProfile"/>）をその場で推定する（保存しない・人物は変えない）。
        /// 人気＝人望と民望、弁舌＝情報、清廉さ＝悪名の少なさ、地盤＝出身星系。
        /// </summary>
        public static PoliticianProfile ProfileOf(Person p, float partyStanding)
        {
            if (p == null) return new PoliticianProfile(-1);
            int renown = System.Math.Max(0, System.Math.Min(MaxRenown, p.popularRenown));
            int infamy = System.Math.Max(0, System.Math.Min(MaxRenown, p.infamy));
            return new PoliticianProfile(p.id)
            {
                popularity = Mathf.Clamp01(BasePopularity + p.charisma / CharismaPopularityDivisor
                                           + renown / RenownPopularityDivisor - infamy / RenownPopularityDivisor),
                oratory = System.Math.Max(0, System.Math.Min(MaxRenown, p.intelligence)),
                partyStanding = Mathf.Clamp01(partyStanding),
                integrity = System.Math.Max(0, System.Math.Min(MaxRenown, BaseIntegrity - infamy / 2)),
                homeRegionKey = p.birthSystemId >= 0 ? RegionKey(p.birthSystemId) : "",
            };
        }

        /// <summary>
        /// 政党の党員と党首を整える（所属は <see cref="PartyMembershipRules"/>、個々の党は <see cref="PartyOrganizationRules"/> を使う）：
        /// ①資格を失った党員の離党・重複IDと重複所属の整理・役職と派閥の整合（<see cref="PartyMembershipRules.Normalize"/>）、
        /// ②無所属の政治家だけを理由つきで入党（<see cref="PartyMembershipRules.AssignUnaffiliated"/>＝既に所属する人は移籍させない）、
        /// ③党首が就けない党は党員から党首選（<see cref="LeadershipElectionRules"/>）で選ぶ（総裁選の管理下の党＝<see cref="PartyLeadershipState.managed"/> は除く）。人物の職種・官位は変えない。
        /// 返り値は変更件数。重複所属の整理で議席の党を優先したいときは <see cref="PoliticsState"/> 版を使う。
        /// </summary>
        public static int OrganizeParties(IList<Party> parties, IList<Person> roster, Faction f)
            => OrganizeParties(parties, null, roster, f);

        /// <summary>勢力の政治状態の政党を整える（重複所属は議席の帰属する党を残す）。</summary>
        public static int OrganizeParties(PoliticsState pol, IList<Person> roster, Faction f)
            => pol != null ? OrganizeParties(pol.parties, pol, roster, f) : 0;

        private static int OrganizeParties(IList<Party> parties, PoliticsState pol, IList<Person> roster, Faction f)
        {
            if (parties == null || parties.Count == 0) return 0;

            // ① 資格・重複・役職と派閥
            int changes = PartyMembershipRules.Normalize(parties, pol, f, roster);

            // ② 無所属の政治家を入党（ID 昇順）
            changes += PartyMembershipRules.AssignUnaffiliated(parties, f, roster, pol).Count;

            // ③ 党首の補充（旧来の簡易党首選）。総裁選の管理下の党（PartyLeadershipRules）は任期・欠缺の総裁選で選ぶため補充しない
            for (int i = 0; i < parties.Count; i++)
            {
                Party p = parties[i];
                if (p == null) continue;
                if (p.leadership != null && p.leadership.managed) continue;
                if (p.leaderId >= 0 && IsEligiblePolitician(FindPerson(roster, p.leaderId), f)) continue;

                var ids = new List<int>(p.memberIds);
                ids.Sort();
                var candidates = new List<LeadershipElectionRules.Candidate>();
                for (int m = 0; m < ids.Count; m++)
                {
                    Person member = FindPerson(roster, ids[m]);
                    if (IsEligiblePolitician(member, f))
                        candidates.Add(PoliticianRules.ToCandidate(ProfileOf(member, DefaultPartyStanding)));
                }
                if (candidates.Count == 0) continue; // 党首候補なし＝空席のまま（捏造しない）

                int winner = LeadershipElectionRules.Elect(candidates, out _);
                if (winner >= 0 && winner != p.leaderId && PartyOrganizationRules.AppointPost(p, PartyPost.党首, winner))
                    changes++;
            }
            return changes;
        }

        /// <summary>
        /// 国政選挙の政党票＝星系ごとに「人口×党の支持率×地域補正」を積み、<see cref="ElectionRules.Aggregate"/> で合算する。
        /// 地域補正＝安定した星系ほど与党に、荒れた星系ほど野党に票が流れる。地域が無ければ支持率そのもの。返り値は党ID昇順。
        /// </summary>
        public static List<VoteTally> NationalVotes(IList<Party> parties, IList<RegionalElectorate> regions, int rulingPartyId, float swing)
        {
            if (parties == null) return new List<VoteTally>();
            float s = Mathf.Clamp(swing, 0f, 0.5f);
            bool hasRegions = regions != null && regions.Count > 0;
            int count = hasRegions ? regions.Count : 1;

            var all = new List<IEnumerable<VoteTally>>(count);
            for (int r = 0; r < count; r++)
            {
                float pop = hasRegions ? Mathf.Max(0f, regions[r].population) : 1f;
                float mood = (hasRegions ? Mathf.Clamp01(regions[r].stability01) : 0.5f) * 2f - 1f;
                var tallies = new List<VoteTally>(parties.Count);
                for (int i = 0; i < parties.Count; i++)
                {
                    Party p = parties[i];
                    if (p == null) continue;
                    float mod = rulingPartyId < 0 ? 1f : (p.id == rulingPartyId ? 1f + s * mood : 1f - s * mood);
                    tallies.Add(new VoteTally(p.id, pop * Mathf.Max(0f, p.support) * mod));
                }
                all.Add(tallies);
            }
            List<VoteTally> result = ElectionRules.Aggregate(all);
            if (hasRegions && ElectionRules.TotalVotes(result) <= 0f)
                return NationalVotes(parties, null, rulingPartyId, swing); // 有権者のいる星系が無い＝支持率だけで数える
            return result;
        }

        /// <summary>
        /// 一議院の選挙を開票して議席を確定する。<paramref name="classUp"/>＝<see cref="AllClasses"/> で全区分、0/1 で上院の片区分。
        /// 改選しない区分の議席は残す。同じ年に同じ区分を開票済みなら何もせず null。票が無ければ null（議席は変えない）。
        /// </summary>
        public static NationalElectionRecord RunChamberElection(PoliticsState pol, Faction f, LegislativeChamber chamber, int year,
            int classUp, bool inaugural, IList<RegionalElectorate> regions, ElectionCycleParams prm)
        {
            if (pol == null || pol.parties == null || pol.parties.Count == 0) return null;
            EnsureSeats(pol, prm);
            bool lower = chamber == LegislativeChamber.下院;
            ChamberSeats cs = lower ? pol.lowerSeats : pol.upperSeats;
            if (cs.parties == null) cs.parties = new List<PartySeatCount>();

            bool upA = lower || classUp == AllClasses || classUp == 0;
            bool upB = !lower && (classUp == AllClasses || classUp == 1);
            if (upA && cs.lastElectionYearA >= year) return null; // 同じ年の二重開票を防ぐ
            if (upB && cs.lastElectionYearB >= year) return null;

            int ruling = pol.government != null ? pol.government.partyId : -1;
            List<VoteTally> tallies = NationalVotes(pol.parties, regions, ruling, prm.rulingStabilitySwing);
            float total = ElectionRules.TotalVotes(tallies);
            if (total <= 0f) return null;

            var ids = new List<int>(tallies.Count);
            var votes = new List<float>(tallies.Count);
            for (int i = 0; i < tallies.Count; i++) { ids.Add(tallies[i].candidateId); votes.Add(tallies[i].votes); }

            int[] wonA = upA ? SeatAllocationRules.LargestRemainder(ids, votes, cs.classSeatsA) : null;
            int[] wonB = upB ? SeatAllocationRules.LargestRemainder(ids, votes, cs.classSeatsB) : null;

            // 改選区分だけ置き換える（非改選の議席は保持）
            for (int i = 0; i < cs.parties.Count; i++)
            {
                if (cs.parties[i] == null) continue;
                if (upA) cs.parties[i].classA = 0;
                if (upB) cs.parties[i].classB = 0;
            }
            var result = new NationalElectionRecord
            {
                electionId = NationalElectionId(f, chamber, year, lower ? AllClasses : classUp),
                year = year,
                chamber = chamber,
                classUp = lower ? AllClasses : classUp,
                seatsUp = (upA ? cs.classSeatsA : 0) + (upB ? cs.classSeatsB : 0),
                totalSeats = cs.TotalSeats,
                inaugural = inaugural,
            };
            for (int i = 0; i < ids.Count; i++)
            {
                PartySeatCount entry = SeatEntry(cs, ids[i]);
                if (upA) entry.classA = wonA[i];
                if (upB) entry.classB = wonB[i];
            }
            for (int i = 0; i < ids.Count; i++)
            {
                Party party = FindParty(pol.parties, ids[i]);
                result.results.Add(new PartyVoteResult
                {
                    partyId = ids[i],
                    partyName = party != null ? party.partyName : "",
                    voteShare = votes[i] / total,
                    seatsWon = (upA ? wonA[i] : 0) + (upB ? wonB[i] : 0),
                    seatsAfter = SeatsOf(cs, ids[i]),
                });
            }

            if (upA) cs.lastElectionYearA = year;
            if (upB) cs.lastElectionYearB = year;
            if (cs.lastElectionYearA > 0 && (lower || cs.lastElectionYearB > 0)) cs.seated = true;

            if (pol.recentResults == null) pol.recentResults = new List<NationalElectionRecord>();
            pol.recentResults.Add(result);
            while (pol.recentResults.Count > prm.maxNationalRecords) pol.recentResults.RemoveAt(0);
            return result;
        }

        /// <summary>
        /// 下院の確定議席から組閣する：第一党（議席最多・同数は党ID小）の党首が適格なら首相。
        /// 過半数（<see cref="CoalitionRules.NeedsCoalition"/>）が無ければ少数政権と明示し、党首が就けなければ組閣未成立＝首相は空席。
        /// </summary>
        public static GovernmentFormation FormGovernment(PoliticsState pol, Faction f, int year, string sourceElectionId, IList<Person> roster)
        {
            var g = new GovernmentFormation { formedYear = year, sourceElectionId = sourceElectionId ?? "" };
            if (pol == null) return g;

            ChamberSeats cs = pol.lowerSeats;
            if (cs == null || !cs.seated)
            {
                g.status = CabinetStatus.組閣未成立;
                g.reason = "下院がまだ構成されていない";
                pol.government = g;
                return g;
            }
            g.totalSeats = cs.TotalSeats;

            int bestId = -1, bestSeats = 0;
            for (int i = 0; i < cs.parties.Count; i++)
            {
                PartySeatCount e = cs.parties[i];
                if (e == null) continue;
                int t = e.Total;
                if (t > bestSeats || (t == bestSeats && t > 0 && e.partyId < bestId))
                {
                    bestSeats = t;
                    bestId = e.partyId;
                }
            }
            if (bestId < 0 || bestSeats <= 0)
            {
                g.status = CabinetStatus.組閣未成立;
                g.reason = "下院に議席を持つ政党がない";
                pol.government = g;
                return g;
            }

            g.partyId = bestId;
            g.partySeats = bestSeats;
            Party party = FindParty(pol.parties, bestId);
            string partyName = party != null ? party.partyName : "党#" + bestId;
            Person leader = party != null ? FindPerson(roster, party.leaderId) : null;
            if (!IsEligiblePolitician(leader, f))
            {
                g.status = CabinetStatus.組閣未成立;
                g.reason = "第一党 " + partyName + " に首相に就ける党首がいない（生存・同勢力の文民政治家が必要）";
                pol.government = g;
                return g;
            }

            g.premierPersonId = leader.id;
            bool minority = CoalitionRules.NeedsCoalition(g.totalSeats > 0 ? (float)bestSeats / g.totalSeats : 0f);
            g.status = minority ? CabinetStatus.少数政権 : CabinetStatus.単独過半;
            g.reason = minority
                ? "過半数 " + MajorityLine(g.totalSeats) + " 議席に届かない（連立は未実装）＝第一党の少数政権"
                : "";
            pol.government = g;
            return g;
        }

        /// <summary>
        /// 選挙の無い年の政府の維持：首相が職務を続けられない（死亡・拘束・離反等）か未組閣なら、
        /// 党首を整えてから現在の議席で組み直す。首相か組閣状態が変わったら true。首相が健在なら何もしない。
        /// </summary>
        public static bool MaintainGovernment(PoliticsState pol, Faction f, int year, IList<Person> roster, out int previousPremierId)
        {
            previousPremierId = pol != null && pol.government != null ? pol.government.premierPersonId : -1;
            if (!IsSeated(pol)) return false;

            GovernmentFormation old = pol.government;
            if (old != null && old.premierPersonId >= 0 && IsEligiblePolitician(FindPerson(roster, old.premierPersonId), f))
                return false;

            OrganizeParties(pol, roster, f);
            CabinetStatus oldStatus = old != null ? old.status : CabinetStatus.未実施;
            GovernmentFormation g = FormGovernment(pol, f, year, old != null ? old.sourceElectionId : "", roster);

            bool changed = g.premierPersonId != previousPremierId || g.status != oldStatus;
            if (!changed && old != null)
            {
                pol.government = old; // 同じ結果なら元の記録（組閣年・理由）を残す
                return false;
            }
            if (previousPremierId >= 0)
                g.reason = "前首相が職務を続けられないため現議席で再組閣。" + g.reason;
            return true;
        }

        /// <summary>
        /// 民主政の勢力の国政の1年：党を整え、未構成なら両院の全議席で初回選挙（日程もその年から組み直す）、
        /// 構成済みなら日程（<paramref name="tick"/>）どおりに下院総選挙・上院半数改選を開票し、下院選挙の後に組閣する。
        /// 選挙の無い年は <see cref="MaintainGovernment"/>。
        /// </summary>
        public static NationalYearOutcome RunNationalYear(FactionState s, int year, PoliticsTickRules.PoliticsTickResult tick,
            IList<RegionalElectorate> regions, IList<Person> roster, ElectionCycleParams prm)
        {
            var o = new NationalYearOutcome { previousPremierId = -1 };
            if (s == null || s.politics == null) return o;
            PoliticsState pol = s.politics;
            Faction f = s.faction;
            o.previousPremierId = pol.government != null ? pol.government.premierPersonId : -1;
            o.partyChanges = OrganizeParties(pol, roster, f);
            EnsureSeats(pol, prm);

            if (!IsSeated(pol))
            {
                o.inaugural = true;
                pol.lowerHouse = ElectionScheduleRules.Found(LegislativeChamber.下院, year); // 任期はこの年から数える
                pol.upperHouse = ElectionScheduleRules.Found(LegislativeChamber.上院, year);
                o.lowerRecord = RunChamberElection(pol, f, LegislativeChamber.下院, year, AllClasses, true, regions, prm);
                o.upperRecord = RunChamberElection(pol, f, LegislativeChamber.上院, year, AllClasses, true, regions, prm);
            }
            else
            {
                if (tick.lowerHouseElection)
                    o.lowerRecord = RunChamberElection(pol, f, LegislativeChamber.下院, year, AllClasses, false, regions, prm);
                if (tick.upperHouseElection && tick.upperClassUp >= 0)
                    o.upperRecord = RunChamberElection(pol, f, LegislativeChamber.上院, year, tick.upperClassUp, false, regions, prm);
            }

            if (o.lowerRecord != null)
            {
                CabinetStatus oldStatus = pol.government != null ? pol.government.status : CabinetStatus.未実施;
                GovernmentFormation g = FormGovernment(pol, f, year, o.lowerRecord.electionId, roster);
                o.governmentFormed = true;
                o.governmentChanged = g.premierPersonId != o.previousPremierId || g.status != oldStatus;
            }
            else
            {
                o.governmentChanged = MaintainGovernment(pol, f, year, roster, out _);
            }
            return o;
        }

        /// <summary>
        /// 非民主の政体へ移った勢力の国政を止める（首相を選挙で選ばない＝政府は対象外）。
        /// 一度でも選挙をしていて、まだ止めていなければ true。議席と記録は履歴として残す。
        /// </summary>
        public static bool SuspendNational(PoliticsState pol, int year, string reason, out int previousPremierId)
        {
            previousPremierId = -1;
            if (pol == null) return false;
            GovernmentFormation g = pol.government;
            if (g != null && g.status == CabinetStatus.対象外) return false;
            if (g == null && !IsSeated(pol)) return false;
            previousPremierId = g != null ? g.premierPersonId : -1;
            pol.government = new GovernmentFormation
            {
                status = CabinetStatus.対象外,
                formedYear = year,
                sourceElectionId = g != null ? g.sourceElectionId : "",
                partyId = -1,
                reason = reason ?? "",
            };
            return true;
        }

        /// <summary>
        /// セーブから読んだ政治状態の穴埋め（読込だけで選挙はしない）。JsonUtility は空のクラスを既定値で作るため、
        /// 年が0の日程・定数0の議院・中身の無い政府を「未設定（null）」に戻し、null のリストを空にする。
        /// </summary>
        public static void NormalizeLoaded(PoliticsState pol)
        {
            if (pol == null) return;
            if (pol.parties == null) pol.parties = new List<Party>();
            for (int i = pol.parties.Count - 1; i >= 0; i--)
            {
                Party p = pol.parties[i];
                if (p == null) { pol.parties.RemoveAt(i); continue; }
                if (p.memberIds == null) p.memberIds = new List<int>();
                if (p.factions == null) p.factions = new List<PartyFaction>();
                if (p.posts == null) p.posts = new List<PartyAppointment>();
                if (p.partyName == null) p.partyName = "";
                if (p.platform == null) p.platform = "";
                if (p.classBase == null) p.classBase = "";
            }

            if (pol.lowerHouse != null && pol.lowerHouse.nextElectionYear <= 0) pol.lowerHouse = null;
            if (pol.upperHouse != null && pol.upperHouse.nextElectionYear <= 0) pol.upperHouse = null;
            if (pol.lowerHouse != null) pol.lowerHouse.chamber = LegislativeChamber.下院;
            if (pol.upperHouse != null) pol.upperHouse.chamber = LegislativeChamber.上院;

            pol.lowerSeats = NormalizeSeats(pol.lowerSeats, LegislativeChamber.下院);
            pol.upperSeats = NormalizeSeats(pol.upperSeats, LegislativeChamber.上院);

            GovernmentFormation g = pol.government;
            if (g != null)
            {
                if (g.sourceElectionId == null) g.sourceElectionId = "";
                if (g.reason == null) g.reason = "";
                if (g.status == CabinetStatus.未実施 && g.premierPersonId < 0 && g.formedYear == 0 && g.sourceElectionId.Length == 0)
                    pol.government = null;
            }

            if (pol.recentResults == null) pol.recentResults = new List<NationalElectionRecord>();
            for (int i = pol.recentResults.Count - 1; i >= 0; i--)
            {
                NationalElectionRecord r = pol.recentResults[i];
                if (r == null || r.year <= 0) { pol.recentResults.RemoveAt(i); continue; }
                if (r.results == null) r.results = new List<PartyVoteResult>();
                if (r.electionId == null) r.electionId = "";
            }

            if (pol.locals == null) pol.locals = new List<LocalElectionState>();
            for (int i = pol.locals.Count - 1; i >= 0; i--)
            {
                LocalElectionState l = pol.locals[i];
                if (l == null) { pol.locals.RemoveAt(i); continue; }
                if (l.lastResults == null) l.lastResults = new List<LocalCandidateResult>();
                if (l.reason == null) l.reason = "";
                if (l.lastElectionId == null) l.lastElectionId = "";
            }

            LegislatorRosterRules.NormalizeLoaded(pol); // 議員名簿（旧セーブは空・議席総数を超えない）
            PartyMembershipRules.NormalizeLoaded(pol);  // 一般党員の集計（旧セーブは不明）・一人一党（入党はさせない）
            PartyLeadershipRules.NormalizeLoaded(pol);  // 総裁選の記録と派閥の整理（旧セーブは未管理のまま・総裁選は起こさない）
            PartyExecutiveRules.NormalizeLoaded(pol);   // 党三役の履歴の穴埋め（読込だけでは任命しない）
            CabinetAppointmentRules.NormalizeLoaded(pol); // 内閣の職・履歴の穴埋め（旧セーブは空＝次の年次で組閣）
        }

        private static ChamberSeats NormalizeSeats(ChamberSeats cs, LegislativeChamber chamber)
        {
            if (cs == null || cs.TotalSeats <= 0) return null;
            cs.chamber = chamber;
            if (cs.parties == null) cs.parties = new List<PartySeatCount>();
            for (int i = cs.parties.Count - 1; i >= 0; i--)
                if (cs.parties[i] == null) cs.parties.RemoveAt(i);
            return cs;
        }

        private static PartySeatCount SeatEntry(ChamberSeats cs, int partyId)
        {
            for (int i = 0; i < cs.parties.Count; i++)
                if (cs.parties[i] != null && cs.parties[i].partyId == partyId) return cs.parties[i];
            var e = new PartySeatCount(partyId);
            cs.parties.Add(e);
            return e;
        }

        /// <summary>条件に合う人物を ID 昇順で集める（LINQ 不使用）。</summary>
        internal static List<Person> SortedById(IList<Person> roster, System.Predicate<Person> match)
        {
            var list = new List<Person>();
            if (roster == null) return list;
            for (int i = 0; i < roster.Count; i++)
            {
                Person p = roster[i];
                if (p != null && (match == null || match(p))) list.Add(p);
            }
            list.Sort((a, b) => a.id.CompareTo(b.id));
            return list;
        }
    }
}
