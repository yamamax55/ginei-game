using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 総裁選（党首選）と党内派閥の投票行動の調整値（党規則。自民党総裁選を参考にしたゲーム用の値で、現実の人数を固定しない）。
    /// </summary>
    public readonly struct PartyLeadershipParams
    {
        /// <summary>党首の任期（年）。</summary>
        public readonly int termYears;
        /// <summary>連続して務められる任期数（到達した現党首は次の総裁選に立てない＝無限再選の防止）。</summary>
        public readonly int maxConsecutiveTerms;
        /// <summary>暫定続投・暫定選出の任期（年）。</summary>
        public readonly int provisionalTermYears;

        /// <summary>推薦人の割合（投票者数×割合を切り捨て）。</summary>
        public readonly float endorsementRatio;
        /// <summary>推薦人の下限（ただし投票者−1を超えない＝小党でも立候補できる）。</summary>
        public readonly int minEndorsers;
        /// <summary>推薦人の上限。</summary>
        public readonly int maxEndorsers;
        /// <summary>自動の立候補者の上限。</summary>
        public readonly int maxCandidates;

        /// <summary>集計議席（人物のいない議席）の匿名票を議員票に含めるか。</summary>
        public readonly bool useAggregateSeatVotes;
        /// <summary>国政議員（実在・集計とも）がいない小党では、ネームド党員が一人1票で議員票を代わりに投じる例外を使うか。</summary>
        public readonly bool smallPartyNamedMemberVotes;
        /// <summary>議員票が0の党で、党員票を正規化する算定票の総数。</summary>
        public readonly int noLegislatorMemberAllotment;

        /// <summary>候補評価での能力・実績（人気・弁舌・行政）の重み。</summary>
        public readonly float abilityWeight;
        /// <summary>候補評価での年功（当選回数・逓減つき）の重み。</summary>
        public readonly float seniorityWeight;
        /// <summary>党文化（0＝実績重視で年功を見ない … 1＝既定 … 2＝年功重視）。年功の重みに掛ける。</summary>
        public readonly float seniorityCulture;
        /// <summary>投票者と候補の信条が同じときの加点。</summary>
        public readonly float policyWeight;
        /// <summary>人間関係（忠誠の対象・同期・同郷）の加点の重み。</summary>
        public readonly float relationWeight;
        /// <summary>所属派閥の推薦の加点の重み（結束を掛ける）。</summary>
        public readonly float factionWeight;
        /// <summary>投票の揺らぎ（seed 由来・0で揺らぎなし）。</summary>
        public readonly float noiseWeight;

        /// <summary>党ごとに保持する総裁選の記録の上限。</summary>
        public readonly int maxRecords;
        public readonly PartySeniorityParams seniority;

        public PartyLeadershipParams(int termYears, int maxConsecutiveTerms, int provisionalTermYears,
            float endorsementRatio, int minEndorsers, int maxEndorsers, int maxCandidates,
            bool useAggregateSeatVotes, bool smallPartyNamedMemberVotes, int noLegislatorMemberAllotment,
            float abilityWeight, float seniorityWeight, float seniorityCulture, float policyWeight,
            float relationWeight, float factionWeight, float noiseWeight, int maxRecords, PartySeniorityParams seniority)
        {
            this.termYears = Mathf.Max(1, termYears);
            this.maxConsecutiveTerms = Mathf.Max(1, maxConsecutiveTerms);
            this.provisionalTermYears = Mathf.Max(1, provisionalTermYears);
            this.endorsementRatio = Mathf.Clamp01(endorsementRatio);
            this.minEndorsers = Mathf.Max(0, minEndorsers);
            this.maxEndorsers = Mathf.Max(this.minEndorsers, maxEndorsers);
            this.maxCandidates = Mathf.Max(1, maxCandidates);
            this.useAggregateSeatVotes = useAggregateSeatVotes;
            this.smallPartyNamedMemberVotes = smallPartyNamedMemberVotes;
            this.noLegislatorMemberAllotment = Mathf.Max(1, noLegislatorMemberAllotment);
            this.abilityWeight = Mathf.Max(0f, abilityWeight);
            this.seniorityWeight = Mathf.Max(0f, seniorityWeight);
            this.seniorityCulture = Mathf.Clamp(seniorityCulture, 0f, 2f);
            this.policyWeight = Mathf.Max(0f, policyWeight);
            this.relationWeight = Mathf.Max(0f, relationWeight);
            this.factionWeight = Mathf.Max(0f, factionWeight);
            this.noiseWeight = Mathf.Max(0f, noiseWeight);
            this.maxRecords = Mathf.Max(1, maxRecords);
            this.seniority = seniority;
        }

        /// <summary>
        /// 既定＝任期3年・連続3期まで・暫定1年／推薦人 投票者×7%（1〜20人）・候補3人まで／集計議席の匿名票あり・小党例外あり・議員票0の党の算定票100／
        /// 評価 能力0.6・年功0.3（党文化1.0）／投票 政策0.15・関係0.2・派閥0.35・揺らぎ0.1／記録4件。
        /// </summary>
        public static PartyLeadershipParams Default => new PartyLeadershipParams(
            3, 3, 1, 0.07f, 1, 20, 3, true, true, 100,
            0.6f, 0.3f, 1f, 0.15f, 0.2f, 0.35f, 0.1f, 4, PartySeniorityParams.Default);

        /// <summary>一部だけ変えた調整値（試験・党文化の切替用）。</summary>
        public PartyLeadershipParams With(int? maxConsecutiveTerms = null, int? minEndorsers = null, int? maxCandidates = null,
            bool? useAggregateSeatVotes = null, bool? smallPartyNamedMemberVotes = null, float? seniorityCulture = null,
            float? policyWeight = null, float? relationWeight = null, float? factionWeight = null, float? noiseWeight = null,
            float? endorsementRatio = null)
            => new PartyLeadershipParams(termYears, maxConsecutiveTerms ?? this.maxConsecutiveTerms, provisionalTermYears,
                endorsementRatio ?? this.endorsementRatio, minEndorsers ?? this.minEndorsers, maxEndorsers, maxCandidates ?? this.maxCandidates,
                useAggregateSeatVotes ?? this.useAggregateSeatVotes, smallPartyNamedMemberVotes ?? this.smallPartyNamedMemberVotes,
                noLegislatorMemberAllotment, abilityWeight, seniorityWeight, seniorityCulture ?? this.seniorityCulture,
                policyWeight ?? this.policyWeight, relationWeight ?? this.relationWeight, factionWeight ?? this.factionWeight,
                noiseWeight ?? this.noiseWeight, maxRecords, seniority);
    }

    /// <summary>届け出た立候補（候補と推薦人の一覧）。推薦人の重複は選挙規則で一人一候補に整理する。</summary>
    public struct LeadershipCandidacy
    {
        public int candidateId;
        public List<int> endorserIds;

        public LeadershipCandidacy(int candidateId, List<int> endorserIds)
        {
            this.candidateId = candidateId;
            this.endorserIds = endorserIds;
        }
    }

    /// <summary>
    /// 総裁選（党首選）と党内派閥の純ロジック（#165 GOV-7 / #159 / #2768）。自民党総裁選を参考にしたゲーム仕様：
    /// <para>①告示：党首の任期満了（既定3年）・死亡/離反などの欠缺で実施（国政選挙とは別の日程とID）。
    /// ②立候補：資格＝党員の適格な政治家、推薦人数は党の規模から（<see cref="RequiredEndorsers"/>）、推薦者は一人一候補、足りない候補は撤回。
    /// ③第1回：議員票（ネームド議員が一人1票＋集計議席の匿名票）＋党員票（一般党員の生票を議員票と同数の整数の算定票へ最大剰余法で正規化）。
    /// 有効票の厳密な過半数で当選。④決選：上位2人に議員票＋地方票（一般党員の集計がある星系ごと1票＝第1回の生票の多い方）。</para>
    /// <para>派閥（<see cref="PartyFaction"/>）は領袖が推薦し、結束・政策・人間関係・候補評価・seed の揺らぎから所属者ごとに投票先が決まる（全員を強制しない）。
    /// 票は一人1票で数え、派閥票と個人票を二重に数えない。当選回数は候補評価の一要素（<see cref="PartySeniorityRules"/>）。</para>
    /// <para>勝者は <see cref="Party.leaderId"/> になるだけで、首相・閣僚・国政議席・軍の指揮権は変えない（組閣は <see cref="ElectionCycleRules"/>）。
    /// 同じ党・同じ年の総裁選は一度だけ（再処理で結果・任期を更新しない）。決定論・test-first。</para>
    /// </summary>
    public static class PartyLeadershipRules
    {
        /// <summary>候補が自分に投じるときの選好（ほかのどの加点より大きい）。</summary>
        private const float SelfVotePreference = 1000f;
        // 候補評価の能力の内訳（人気・弁舌の訴求と行政の素養を半々）。
        private const float AppealShare = 0.5f;
        private const float CivilShare = 0.5f;
        private const float CivilAptitudeScale = 100f;
        private const float StandingForAppeal = 0.5f;
        // 人間関係の強さ（忠誠の対象＞同期の同窓＞同郷）。
        private const float LoyaltyRelation = 1f;
        private const float ClassmateRelation = 0.5f;
        private const float HometownRelation = 0.3f;
        // seed の揺らぎ・くじの系列番号。
        private const int NoiseRoundOffset = 97;
        private const int LotteryStream = 7777;
        private const int Round1TieStream = 1;

        public const string VoterBasisLegislators = "所属国政議員（一人1票）";
        public const string VoterBasisSmallParty = "小党例外：国政議員のいない党のネームド党員（一人1票）";
        public const string VoterBasisAggregateOnly = "所属議員はすべて集計議席（匿名）＝推薦人なし";
        public const string VoterBasisNone = "投票できる議員がいない";

        /// <summary>総裁選の ID（勢力:党:総裁選:年）。国政選挙の ID と重ならない。</summary>
        public static string ElectionId(Faction f, int partyId, int year) => (int)f + ":党" + partyId + ":総裁選:" + year;

        /// <summary>党首を総裁選で選ぶ管理下に置く（旧来の自動補充を止める）。状態が無ければ作る。</summary>
        public static void Adopt(PoliticsState pol)
        {
            if (pol == null || pol.parties == null) return;
            for (int i = 0; i < pol.parties.Count; i++)
            {
                Party p = pol.parties[i];
                if (p == null) continue;
                if (p.leadership == null) p.leadership = new PartyLeadershipState();
                p.leadership.managed = true;
            }
        }

        /// <summary>推薦人数＝clamp(floor(投票者×割合), 下限, 上限) を「投票者−1」以下に抑える（候補本人を除いても集まる数）。</summary>
        public static int RequiredEndorsers(int voterCount, PartyLeadershipParams p)
        {
            if (voterCount <= 0) return 0;
            int configured = Mathf.Clamp(Mathf.FloorToInt(voterCount * p.endorsementRatio), p.minEndorsers, p.maxEndorsers);
            return Mathf.Min(configured, voterCount - 1);
        }

        /// <summary>
        /// 候補評価（0..）＝能力・実績の重み×（人気と弁舌の訴求・行政の素養）＋年功の重み×党文化×年功（逓減・頭打ち）。
        /// 年功だけでは決まらない（能力の高い若手が勝てる）。状態は変えない。
        /// </summary>
        public static float CandidateStrength(PoliticsState pol, Person p, PartyLeadershipParams prm)
        {
            if (p == null) return 0f;
            float appeal = PoliticianRules.MemberVoteAppeal(ElectionCycleRules.ProfileOf(p, StandingForAppeal));
            float civil = Mathf.Clamp01(p.CivilAptitude / CivilAptitudeScale);
            float ability = AppealShare * appeal + CivilShare * civil;
            float standing = PartySeniorityRules.Standing(LegislatorRosterRules.TotalWins(pol, p.id), prm.seniority);
            return prm.abilityWeight * ability + prm.seniorityWeight * prm.seniorityCulture * standing;
        }

        /// <summary>人間関係の近さ（0..1）：忠誠の対象＝1、同じ学校の同期＝0.5、同郷＝0.3（大きい方）。</summary>
        public static float Relation(Person voter, Person candidate)
        {
            if (voter == null || candidate == null || voter.id == candidate.id) return 0f;
            float r = 0f;
            if (voter.loyaltyTargetId >= 0 && voter.loyaltyTargetId == candidate.id) r = LoyaltyRelation;
            if (voter.schoolId > 0 && voter.schoolId == candidate.schoolId && voter.graduationYear > 0
                && voter.graduationYear == candidate.graduationYear) r = Mathf.Max(r, ClassmateRelation);
            if (voter.birthSystemId >= 0 && voter.birthSystemId == candidate.birthSystemId) r = Mathf.Max(r, HometownRelation);
            return r;
        }

        // ===== 年次 =====

        /// <summary>
        /// 勢力の政党の総裁選を年次で回す（党ID昇順）：管理下に置き、派閥を整え、任期満了・党首の欠缺・暫定任期の満了なら実施する。
        /// 就任年の分からない現党首は、その年から任期を起算するだけ（総裁選はしない）。同じ年に処理済みの党は何もしない。
        /// 新しく記録した総裁選の一覧を返す。<paramref name="ownedSystemIds"/> があれば、地方支部はその星系に限る。
        /// </summary>
        public static List<LeadershipElectionRecord> TickYear(PoliticsState pol, Faction f, int year, IList<Person> roster,
            ICollection<int> ownedSystemIds, PartyLeadershipParams prm)
        {
            var list = new List<LeadershipElectionRecord>();
            if (pol == null || pol.parties == null || year <= 0) return list;
            var parties = new List<Party>();
            for (int i = 0; i < pol.parties.Count; i++) if (pol.parties[i] != null) parties.Add(pol.parties[i]);
            parties.Sort((a, b) => a.id.CompareTo(b.id));

            for (int i = 0; i < parties.Count; i++)
            {
                Party party = parties[i];
                if (party.faction != f) continue;
                if (party.leadership == null) party.leadership = new PartyLeadershipState();
                PartyLeadershipState st = party.leadership;
                st.managed = true;
                NormalizeFactions(party);
                if (st.lastElectionYear >= year) continue; // 同じ年の再処理（読込後を含む）はしない

                int previous = party.leaderId;
                string trigger = TriggerOf(party, f, year, roster, prm);
                if (trigger == null) continue;

                // 適格な党員がいない党は、既に選出不能と記録していれば記録を増やさない（毎年の空記録を防ぐ）
                if (party.leaderId < 0 && EligibleMembers(party, f, roster).Count == 0
                    && st.Latest != null && st.Latest.outcome == LeadershipOutcome.選出不能)
                {
                    st.lastElectionYear = year;
                    st.nextElectionYear = year + prm.provisionalTermYears;
                    continue;
                }

                LeadershipElectionRecord rec = Conduct(pol, f, party, year, roster, ownedSystemIds, prm, trigger, previous, null);
                if (rec != null) list.Add(rec);
            }
            return list;
        }

        /// <summary>
        /// 届け出た立候補で総裁選を実施する（プレイヤー・イベント用の入口。年次と同じ規則）。<paramref name="declared"/> が null なら自動の立候補。
        /// 同じ党で同じ年に実施済みなら何もせず null。
        /// </summary>
        public static LeadershipElectionRecord RunElection(PoliticsState pol, Faction f, Party party, int year, IList<Person> roster,
            ICollection<int> ownedSystemIds, PartyLeadershipParams prm, string trigger, IList<LeadershipCandidacy> declared)
        {
            if (pol == null || party == null || year <= 0) return null;
            if (party.leadership == null) party.leadership = new PartyLeadershipState();
            party.leadership.managed = true;
            NormalizeFactions(party);
            return Conduct(pol, f, party, year, roster, ownedSystemIds, prm, trigger ?? "臨時の総裁選", party.leaderId, declared);
        }

        /// <summary>党首の欠缺の理由（適格なら null）。</summary>
        public static string VacancyCause(Person leader, Faction f)
        {
            if (ElectionCycleRules.IsEligiblePolitician(leader, f)) return null;
            if (leader == null) return "名簿に居ない";
            if (leader.IsDeceased) return "死去";
            if (leader.faction != f || leader.isFreeAgent) return "離反・在野";
            if (!leader.IsAvailable) return "拘束・行方不明";
            return "政治家でなくなった";
        }

        // ===== 派閥の整理 =====

        /// <summary>
        /// 派閥を整える（人物名簿なしで行える構造の整理）：null・重複した派閥IDを除き、政策傾向の null を空に・結束を0..1に、
        /// 党員でない領袖・複数派閥の領袖（派閥ID小だけ残す）を外し、領袖を自派閥の名簿へ移し、党員でない人・負のID・
        /// 複数派閥への重複（派閥ID小に残す）を名簿から除く＝一人一派閥。変更件数を返す。
        /// </summary>
        public static int NormalizeFactions(Party p)
        {
            if (p == null) return 0;
            int changes = 0;
            if (p.memberIds == null) p.memberIds = new List<int>();
            if (p.factions == null) { p.factions = new List<PartyFaction>(); return 0; }

            var ids = new HashSet<int>();
            for (int i = 0; i < p.factions.Count; i++)
            {
                PartyFaction pf = p.factions[i];
                if (pf == null || !ids.Add(pf.id)) { p.factions.RemoveAt(i); i--; changes++; continue; }
                if (pf.memberIds == null) pf.memberIds = new List<int>();
                if (pf.name == null) pf.name = "";
                if (pf.policyStance == null) pf.policyStance = "";
                float c = float.IsNaN(pf.cohesion) ? 0f : Mathf.Clamp01(pf.cohesion);
                if (c != pf.cohesion) { pf.cohesion = c; changes++; }
            }

            var order = new List<PartyFaction>(p.factions);
            order.Sort((a, b) => a.id.CompareTo(b.id));

            var bosses = new HashSet<int>();
            for (int i = 0; i < order.Count; i++)
            {
                PartyFaction pf = order[i];
                if (pf.bossId < 0) continue;
                if (!p.memberIds.Contains(pf.bossId) || !bosses.Add(pf.bossId)) { pf.bossId = -1; changes++; }
            }
            for (int i = 0; i < order.Count; i++)
            {
                PartyFaction pf = order[i];
                if (pf.bossId < 0) continue;
                for (int k = 0; k < order.Count; k++)
                {
                    if (k == i) continue;
                    int removed = order[k].memberIds.RemoveAll(x => x == pf.bossId);
                    changes += removed;
                }
                if (!pf.memberIds.Contains(pf.bossId)) { pf.memberIds.Add(pf.bossId); changes++; }
            }

            var placed = new HashSet<int>();
            for (int i = 0; i < order.Count; i++)
            {
                List<int> m = order[i].memberIds;
                for (int k = 0; k < m.Count; k++)
                {
                    int id = m[k];
                    if (id < 0 || !p.memberIds.Contains(id) || !placed.Add(id)) { m.RemoveAt(k); k--; changes++; }
                }
            }
            return changes;
        }

        /// <summary>その人物の派閥（無派閥は null）。</summary>
        public static PartyFaction FactionOf(Party p, int personId)
        {
            if (p == null || p.factions == null || personId < 0) return null;
            PartyFaction best = null;
            for (int i = 0; i < p.factions.Count; i++)
            {
                PartyFaction pf = p.factions[i];
                if (pf == null || pf.memberIds == null || !pf.memberIds.Contains(personId)) continue;
                if (best == null || pf.id < best.id) best = pf;
            }
            return best;
        }

        /// <summary>無派閥の党員の数（重複IDを数えない）。</summary>
        public static int UnaffiliatedCount(Party p)
        {
            if (p == null || p.memberIds == null) return 0;
            var seen = new HashSet<int>();
            int n = 0;
            for (int i = 0; i < p.memberIds.Count; i++)
            {
                int id = p.memberIds[i];
                if (id < 0 || !seen.Add(id)) continue;
                if (FactionOf(p, id) == null) n++;
            }
            return n;
        }

        // ===== 読込 =====

        /// <summary>
        /// セーブから読んだ総裁選の状態の穴埋め（読込だけで総裁選・当選回数を起こさない）：null の状態・記録・リスト・文字列を埋め、
        /// 壊れた記録を除き、上限を超えた古い記録を捨て、派閥を整える。旧セーブは未管理のまま。
        /// </summary>
        public static void NormalizeLoaded(PoliticsState pol)
        {
            if (pol == null || pol.parties == null) return;
            int max = PartyLeadershipParams.Default.maxRecords;
            for (int i = 0; i < pol.parties.Count; i++)
            {
                Party p = pol.parties[i];
                if (p == null) continue;
                if (p.leadership == null) p.leadership = new PartyLeadershipState();
                PartyLeadershipState st = p.leadership;
                if (st.termNote == null) st.termNote = "";
                if (st.pendingReason == null) st.pendingReason = "";
                if (st.termStartYear < 0) st.termStartYear = 0;
                if (st.termEndYear < 0) st.termEndYear = 0;
                if (st.nextElectionYear < 0) st.nextElectionYear = 0;
                if (st.lastElectionYear < 0) st.lastElectionYear = 0;
                if (st.consecutiveTerms < 0) st.consecutiveTerms = 0;
                if (st.records == null) st.records = new List<LeadershipElectionRecord>();
                if (st.process == null) st.process = new LeadershipElectionProcess();
                if (st.process.trigger == null) st.process.trigger = "";
                if (st.process.candidacies == null) st.process.candidacies = new List<LeadershipCandidacyData>();
                for (int k = 0; k < st.process.candidacies.Count; k++)
                    if (st.process.candidacies[k] == null || st.process.candidacies[k].candidateId < 0)
                    { st.process.candidacies.RemoveAt(k); k--; }
                for (int k = 0; k < st.records.Count; k++)
                {
                    LeadershipElectionRecord r = st.records[k];
                    if (r == null || r.year <= 0 || string.IsNullOrEmpty(r.electionId)) { st.records.RemoveAt(k); k--; continue; }
                    NormalizeRecord(r);
                }
                while (st.records.Count > max) st.records.RemoveAt(0);
                NormalizeFactions(p);
            }
        }

        // ===== 内部：実施 =====

        private sealed class Ctx
        {
            public PoliticsState pol;
            public Faction f;
            public Party party;
            public PartyLeadershipParams prm;
            public int seed;
            public readonly Dictionary<int, Person> people = new Dictionary<int, Person>();
            public readonly Dictionary<int, float> strength = new Dictionary<int, float>();

            public Person Get(int id) => people.TryGetValue(id, out Person p) ? p : null;

            public float Strength(int id)
            {
                if (strength.TryGetValue(id, out float s)) return s;
                s = CandidateStrength(pol, Get(id), prm);
                strength[id] = s;
                return s;
            }
        }

        private struct MemberPool
        {
            public int systemId;
            public long members;
        }

        private static LeadershipElectionRecord Conduct(PoliticsState pol, Faction f, Party party, int year, IList<Person> roster,
            ICollection<int> owned, PartyLeadershipParams prm, string trigger, int previousLeaderId, IList<LeadershipCandidacy> declared)
        {
            PartyLeadershipState st = party.leadership;
            if (st.lastElectionYear >= year) return null;

            var c = new Ctx { pol = pol, f = f, party = party, prm = prm };
            if (roster != null)
                for (int i = 0; i < roster.Count; i++)
                    if (roster[i] != null && !c.people.ContainsKey(roster[i].id)) c.people[roster[i].id] = roster[i];

            var rec = new LeadershipElectionRecord
            {
                electionId = ElectionId(f, party.id, year),
                year = year,
                partyId = party.id,
                trigger = trigger ?? "",
                previousLeaderId = previousLeaderId,
            };
            c.seed = rec.seed = LeadershipElectionRules.SeedOf(rec.electionId);

            List<int> eligible = EligibleMembers(party, f, roster);

            // --- 投票者（議員票）と推薦人の母集団 ---
            var named = new List<int>();
            for (int i = 0; i < eligible.Count; i++)
            {
                LegislatorRecord lr = LegislatorRosterRules.Find(pol, eligible[i]);
                if (lr != null && lr.seated && lr.seatPartyId == party.id) named.Add(eligible[i]);
            }
            int aggregateSeats = prm.useAggregateSeatVotes
                ? SeatedAggregate(pol, LegislativeChamber.下院, party.id) + SeatedAggregate(pol, LegislativeChamber.上院, party.id) : 0;
            List<int> voters;
            if (named.Count > 0) { voters = named; rec.voterBasis = VoterBasisLegislators; }
            else if (aggregateSeats > 0) { voters = new List<int>(); rec.voterBasis = VoterBasisAggregateOnly; }
            else if (prm.smallPartyNamedMemberVotes && eligible.Count > 0)
            {
                voters = new List<int>(eligible);
                aggregateSeats = 0;
                rec.voterBasis = VoterBasisSmallParty;
                rec.restrictions.Add("国政議員がいない党の例外＝ネームド党員" + eligible.Count + "名が議員票に代えて一人1票（党規則 smallPartyNamedMemberVotes）");
            }
            else { voters = new List<int>(); rec.voterBasis = VoterBasisNone; }
            if (!prm.useAggregateSeatVotes && (SeatedAggregate(pol, LegislativeChamber.下院, party.id) + SeatedAggregate(pol, LegislativeChamber.上院, party.id)) > 0)
                rec.restrictions.Add("集計議席の匿名票は党規則で使わない");

            // --- 立候補 ---
            int incumbent = previousLeaderId >= 0 && previousLeaderId == party.leaderId && eligible.Contains(previousLeaderId) ? previousLeaderId : -1;
            bool incumbentBarred = incumbent >= 0 && st.consecutiveTerms >= prm.maxConsecutiveTerms;
            if (incumbentBarred)
                rec.restrictions.Add("現党首は連続" + st.consecutiveTerms + "期で上限（" + prm.maxConsecutiveTerms + "期）に達し立候補できない");

            List<int> nominees = declared != null
                ? DeclaredNominees(c, declared, eligible, incumbentBarred ? incumbent : -1, rec)
                : AutoNominees(c, eligible, incumbentBarred ? incumbent : -1);
            for (int i = 0; i < nominees.Count; i++)
            {
                LegislatorRecord lr = LegislatorRosterRules.Find(pol, nominees[i]);
                rec.candidates.Add(new LeadershipCandidateResult(nominees[i])
                {
                    status = LeadershipCandidateStatus.立候補,
                    strength = c.Strength(nominees[i]),
                    nationalWins = lr != null ? lr.TotalWins : 0,
                    historyRegistered = lr != null,
                });
            }

            // --- 推薦（一人一候補・足りない候補は撤回） ---
            rec.requiredEndorsers = RequiredEndorsers(voters.Count, prm);
            List<int> remaining = ResolveEndorsements(c, rec, nominees, voters, declared);

            if (remaining.Count == 0)
                return FinishWithoutVote(c, rec, st, eligible, incumbent, year,
                    nominees.Count == 0 ? "立候補者がいない" : "推薦人の要件（" + rec.requiredEndorsers + "人）を満たす候補がいない", true);

            if (remaining.Count == 1)
            {
                int only = remaining[0];
                Dictionary<int, int> endorse1 = FactionEndorsements(c, remaining, null, rec, false);
                rec.outcome = LeadershipOutcome.無投票当選;
                rec.reason = "推薦人の要件を満たした候補が1人＝無投票当選";
                SetStatus(rec, only, LeadershipCandidateStatus.当選);
                return Finish(c, rec, st, only, previousLeaderId, year, endorse1);
            }

            // --- 第1回 ---
            Dictionary<int, int> endorse = FactionEndorsements(c, remaining, null, rec, false);
            var namedVotes = CastVotes(c, rec, voters, remaining, endorse, 1, true);
            int namedTotal = 0;
            for (int i = 0; i < remaining.Count; i++) namedTotal += namedVotes[remaining[i]];
            rec.namedVoters = namedTotal;

            int[] agg = DistributeAggregate(c, rec, remaining, namedVotes, aggregateSeats, "第1回");
            rec.aggregateVotes = aggregateSeats;
            int legislatorTotal = namedTotal + aggregateSeats;

            List<MemberPool> pools = MemberPools(c, rec, owned);
            var raw = new long[remaining.Count];
            for (int k = 0; k < pools.Count; k++)
            {
                long[] v = RawMemberVotes(c, remaining, pools[k]);
                for (int i = 0; i < remaining.Count; i++) raw[i] += v[i];
            }
            long rawTotal = 0;
            for (int i = 0; i < raw.Length; i++) rawTotal += raw[i];
            rec.memberVotesKnown = rawTotal > 0;
            rec.memberRawTotal = rawTotal;
            if (rec.memberVotesKnown)
            {
                rec.memberAllotment = legislatorTotal > 0 ? legislatorTotal : prm.noLegislatorMemberAllotment;
                if (legislatorTotal <= 0)
                    rec.restrictions.Add("議員票が0の党＝党員票を算定票" + rec.memberAllotment + "票へ正規化（党規則）");
            }
            int[] converted = LeadershipElectionRules.ConvertToAllotment(remaining, raw, rec.memberAllotment);

            int total = 0;
            for (int i = 0; i < remaining.Count; i++)
            {
                LeadershipCandidateResult cr = rec.Candidate(remaining[i]);
                cr.round1Named = namedVotes[remaining[i]];
                cr.round1Aggregate = agg[i];
                cr.round1MemberRaw = raw[i];
                cr.round1MemberConverted = converted[i];
                cr.round1Total = cr.round1Named + cr.round1Aggregate + cr.round1MemberConverted;
                total += cr.round1Total;
            }
            rec.round1Total = total;
            if (total <= 0)
                return FinishWithoutVote(c, rec, st, eligible, incumbent, year, "有効票がない（投票できる議員も一般党員の集計もない）", false);

            List<int> ranked = RankRound1(c, rec, remaining);
            LeadershipCandidateResult top = rec.Candidate(ranked[0]);
            if (LeadershipElectionRules.StrictMajority(top.round1Total, total))
            {
                for (int i = 1; i < ranked.Count; i++) SetStatus(rec, ranked[i], LeadershipCandidateStatus.第1回落選);
                SetStatus(rec, top.personId, LeadershipCandidateStatus.当選);
                rec.outcome = LeadershipOutcome.第1回当選;
                rec.reason = "第1回で有効票" + total + "票の過半数（" + LeadershipElectionRules.MajorityNeeded(total) + "票以上）となる" + top.round1Total + "票を獲得";
                FillFactionVotes(c, rec, voters, remaining, endorse, 1, true);
                return Finish(c, rec, st, top.personId, previousLeaderId, year, endorse);
            }

            // --- 決選 ---
            rec.runoffHeld = true;
            int a = ranked[0], b = ranked[1];
            for (int i = 2; i < ranked.Count; i++) SetStatus(rec, ranked[i], LeadershipCandidateStatus.第1回落選);
            SetStatus(rec, a, LeadershipCandidateStatus.決選進出);
            SetStatus(rec, b, LeadershipCandidateStatus.決選進出);
            var finalists = new List<int> { a, b };
            if (b < a) { finalists[0] = b; finalists[1] = a; }

            Dictionary<int, int> endorse2 = FactionEndorsements(c, finalists, endorse, rec, true);
            var runoffNamed = CastVotes(c, rec, voters, finalists, endorse2, 2, true);
            int[] agg2 = DistributeAggregate(c, rec, finalists, runoffNamed, aggregateSeats, "決選");

            int regionalA = 0, regionalB = 0, tiedRegions = 0;
            int idxA = remaining.IndexOf(a), idxB = remaining.IndexOf(b);
            for (int k = 0; k < pools.Count; k++)
            {
                if (pools[k].systemId < 0) continue; // 星系不明分は地方票にしない
                long[] v = RawMemberVotes(c, remaining, pools[k]);
                var region = new LeadershipRegionResult { systemId = pools[k].systemId, members = pools[k].members, votesFirst = v[idxA], votesSecond = v[idxB] };
                if (v[idxA] > v[idxB]) { region.voteFor = a; regionalA++; }
                else if (v[idxB] > v[idxA]) { region.voteFor = b; regionalB++; }
                else tiedRegions++;
                rec.regions.Add(region);
            }
            if (rec.regions.Count == 0) rec.restrictions.Add("星系ごとの一般党員集計がない＝決選の地方票なし（議員票のみ）");
            if (tiedRegions > 0) rec.restrictions.Add("地方票 " + tiedRegions + "星系は決選の2人が同数のため無効");

            LeadershipCandidateResult ca = rec.Candidate(a), cb = rec.Candidate(b);
            ca.runoffNamed = runoffNamed[a];
            cb.runoffNamed = runoffNamed[b];
            ca.runoffAggregate = agg2[finalists.IndexOf(a)];
            cb.runoffAggregate = agg2[finalists.IndexOf(b)];
            ca.runoffRegional = regionalA;
            cb.runoffRegional = regionalB;
            ca.runoffTotal = ca.runoffNamed + ca.runoffAggregate + ca.runoffRegional;
            cb.runoffTotal = cb.runoffNamed + cb.runoffAggregate + cb.runoffRegional;
            rec.runoffTotal = ca.runoffTotal + cb.runoffTotal;

            int winner;
            if (ca.runoffTotal != cb.runoffTotal)
            {
                winner = ca.runoffTotal > cb.runoffTotal ? a : b;
                LeadershipCandidateResult w = rec.Candidate(winner), l = rec.Candidate(winner == a ? b : a);
                rec.reason = "第1回で過半数なし（最多 " + top.round1Total + "/" + total + "票）＝上位2人の決選で " + w.runoffTotal + "対" + l.runoffTotal +
                             "（議員" + (w.runoffNamed + w.runoffAggregate) + "・地方" + w.runoffRegional + "）";
            }
            else
            {
                float ra = LeadershipElectionRules.SeededRoll(c.seed, a, LotteryStream);
                float rb = LeadershipElectionRules.SeededRoll(c.seed, b, LotteryStream);
                winner = ra != rb ? (ra > rb ? a : b) : Mathf.Min(a, b);
                rec.reason = "決選が " + ca.runoffTotal + "対" + cb.runoffTotal + " の同票＝seed " + c.seed + " のくじで決定";
            }
            SetStatus(rec, winner, LeadershipCandidateStatus.当選);
            SetStatus(rec, winner == a ? b : a, LeadershipCandidateStatus.決選落選);
            rec.outcome = LeadershipOutcome.決選当選;
            FillFactionVotes(c, rec, voters, finalists, endorse2, 2, true);
            return Finish(c, rec, st, winner, previousLeaderId, year, endorse2);
        }

        /// <summary>選出の実施時期の判定（実施しないなら null）。欠缺の党首はここで空席にする。</summary>
        private static string TriggerOf(Party party, Faction f, int year, IList<Person> roster, PartyLeadershipParams prm)
        {
            PartyLeadershipState st = party.leadership;
            if (party.leaderId >= 0)
            {
                string cause = VacancyCause(ElectionCycleRules.FindPerson(roster, party.leaderId), f);
                if (cause != null)
                {
                    int former = party.leaderId;
                    PartyOrganizationRules.DismissPost(party, PartyPost.党首);
                    st.pendingReason = "党首（人物#" + former + "）の欠缺（" + cause + "）＝総裁選で選出";
                    return "党首の欠缺（" + cause + "）";
                }
                if (st.termEndYear <= 0)
                {
                    st.termStartYear = year;
                    st.termEndYear = year + prm.termYears;
                    st.nextElectionYear = st.termEndYear;
                    if (st.consecutiveTerms <= 0) st.consecutiveTerms = 1;
                    st.termNote = "就任年が不明のため SE" + year + " から任期を起算";
                    return null;
                }
                if (year < st.termEndYear) return null;
                return st.pendingReason.Length > 0 ? "暫定任期の満了" : "任期満了（" + prm.termYears + "年）";
            }
            return st.pendingReason.Length > 0 ? st.pendingReason : "党首の空席";
        }

        private static List<int> EligibleMembers(Party party, Faction f, IList<Person> roster)
        {
            var list = new List<int>();
            if (party == null || party.memberIds == null) return list;
            var seen = new HashSet<int>();
            for (int i = 0; i < party.memberIds.Count; i++)
            {
                int id = party.memberIds[i];
                if (id < 0 || !seen.Add(id)) continue;
                if (ElectionCycleRules.IsEligiblePolitician(ElectionCycleRules.FindPerson(roster, id), f)) list.Add(id);
            }
            list.Sort();
            return list;
        }

        private static int SeatedAggregate(PoliticsState pol, LegislativeChamber chamber, int partyId)
        {
            ChamberSeats cs = chamber == LegislativeChamber.下院 ? pol.lowerSeats : pol.upperSeats;
            return cs != null && cs.seated ? LegislatorRosterRules.AggregateSeats(pol, chamber, partyId) : 0;
        }

        private static bool IsValidBoss(Ctx c, PartyFaction pf)
            => pf != null && pf.bossId >= 0 && c.party.memberIds.Contains(pf.bossId)
               && ElectionCycleRules.IsEligiblePolitician(c.Get(pf.bossId), c.f);

        /// <summary>自動の立候補：領袖（候補評価順）→現党首→その他（候補評価順）を上限まで。</summary>
        private static List<int> AutoNominees(Ctx c, List<int> eligible, int barred)
        {
            var bosses = new List<int>();
            var others = new List<int>();
            int incumbent = c.party.leaderId;
            for (int i = 0; i < eligible.Count; i++)
            {
                int id = eligible[i];
                if (id == barred) continue;
                bool boss = false;
                if (c.party.factions != null)
                    for (int k = 0; k < c.party.factions.Count; k++)
                        if (c.party.factions[k] != null && c.party.factions[k].bossId == id && IsValidBoss(c, c.party.factions[k])) boss = true;
                if (boss) bosses.Add(id); else if (id != incumbent) others.Add(id);
            }
            bosses.Sort((x, y) => CompareStrength(c, x, y));
            others.Sort((x, y) => CompareStrength(c, x, y));
            var list = new List<int>();
            for (int i = 0; i < bosses.Count && list.Count < c.prm.maxCandidates; i++) list.Add(bosses[i]);
            if (incumbent >= 0 && incumbent != barred && eligible.Contains(incumbent) && !list.Contains(incumbent) && list.Count < c.prm.maxCandidates)
                list.Add(incumbent);
            for (int i = 0; i < others.Count && list.Count < c.prm.maxCandidates; i++) list.Add(others[i]);
            return list;
        }

        private static List<int> DeclaredNominees(Ctx c, IList<LeadershipCandidacy> declared, List<int> eligible, int barred, LeadershipElectionRecord rec)
        {
            var list = new List<int>();
            for (int i = 0; i < declared.Count; i++)
            {
                int id = declared[i].candidateId;
                if (list.Contains(id)) continue;
                if (!eligible.Contains(id)) { rec.restrictions.Add("人物#" + id + " は立候補の資格がない（党員の適格な政治家でない）"); continue; }
                if (id == barred) { rec.restrictions.Add("人物#" + id + " は連続任期の上限で立候補できない"); continue; }
                list.Add(id);
            }
            list.Sort();
            return list;
        }

        /// <summary>
        /// 推薦を解く：各推薦人（ID昇順）は残る候補のうち最も支持する1人だけを推す（届出がある場合は自分を載せた候補の中から）。
        /// 要件に届かない候補がいれば、推薦の最も少ない候補（同数は候補評価の低い方→ID大）を撤回させて繰り返す。残った候補を返す。
        /// </summary>
        private static List<int> ResolveEndorsements(Ctx c, LeadershipElectionRecord rec, List<int> nominees, List<int> voters,
            IList<LeadershipCandidacy> declared)
        {
            var remaining = new List<int>(nominees);
            Dictionary<int, List<int>> allowed = null;
            if (declared != null)
            {
                allowed = new Dictionary<int, List<int>>();
                int rejected = 0;
                for (int i = 0; i < declared.Count; i++)
                {
                    LeadershipCandidacy d = declared[i];
                    if (d.endorserIds == null || !nominees.Contains(d.candidateId)) continue;
                    var seen = new HashSet<int>();
                    for (int k = 0; k < d.endorserIds.Count; k++)
                    {
                        int e = d.endorserIds[k];
                        if (!seen.Add(e)) continue;
                        if (!voters.Contains(e) || e == d.candidateId || nominees.Contains(e)) { rejected++; continue; }
                        if (!allowed.TryGetValue(e, out List<int> set)) { set = new List<int>(); allowed[e] = set; }
                        if (!set.Contains(d.candidateId)) set.Add(d.candidateId);
                    }
                }
                if (rejected > 0) rec.restrictions.Add("推薦人 " + rejected + "件は無効（投票資格なし・候補本人・他の候補）");
            }

            var sortedVoters = new List<int>(voters);
            sortedVoters.Sort();
            while (true)
            {
                var counts = new Dictionary<int, List<int>>();
                for (int i = 0; i < remaining.Count; i++) counts[remaining[i]] = new List<int>();
                if (remaining.Count > 0)
                {
                    Dictionary<int, int> endorse = FactionEndorsements(c, remaining, null, null, false);
                    for (int i = 0; i < sortedVoters.Count; i++)
                    {
                        int e = sortedVoters[i];
                        if (remaining.Contains(e)) continue;
                        List<int> options = remaining;
                        if (allowed != null)
                        {
                            if (!allowed.TryGetValue(e, out List<int> set)) continue;
                            options = new List<int>();
                            for (int k = 0; k < set.Count; k++) if (remaining.Contains(set[k])) options.Add(set[k]);
                            if (options.Count == 0) continue;
                        }
                        int pick = BestChoice(c, c.Get(e), options, endorse, 0, false);
                        if (pick >= 0) counts[pick].Add(e);
                    }
                }

                int drop = -1;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int id = remaining[i];
                    if (counts[id].Count >= rec.requiredEndorsers) continue;
                    if (drop < 0 || counts[id].Count < counts[drop].Count
                        || (counts[id].Count == counts[drop].Count && CompareStrength(c, id, drop) > 0))
                        drop = id;
                }
                for (int i = 0; i < remaining.Count; i++)
                {
                    LeadershipCandidateResult cr = rec.Candidate(remaining[i]);
                    cr.endorserIds = counts[remaining[i]];
                }
                if (drop < 0) break;
                SetStatus(rec, drop, LeadershipCandidateStatus.推薦人不足で撤回);
                rec.Candidate(drop).endorserIds = counts[drop];
                remaining.Remove(drop);
            }
            return remaining;
        }

        /// <summary>派閥の推薦（派閥ID→候補ID、-1＝自主投票）。<paramref name="previous"/> があれば、推した候補が残っている派閥は据え置く。</summary>
        private static Dictionary<int, int> FactionEndorsements(Ctx c, List<int> candidates, Dictionary<int, int> previous,
            LeadershipElectionRecord rec, bool runoff)
        {
            var map = new Dictionary<int, int>();
            if (c.party.factions == null) return map;
            var order = new List<PartyFaction>();
            for (int i = 0; i < c.party.factions.Count; i++) if (c.party.factions[i] != null) order.Add(c.party.factions[i]);
            order.Sort((x, y) => x.id.CompareTo(y.id));
            for (int i = 0; i < order.Count; i++)
            {
                PartyFaction pf = order[i];
                int pick = -1;
                string why;
                int before = previous != null && previous.TryGetValue(pf.id, out int pv) ? pv : -1;
                if (!IsValidBoss(c, pf)) why = "領袖不在＝自主投票";
                else if (before >= 0 && candidates.Contains(before)) { pick = before; why = "推した候補が決選に残り支持を維持"; }
                else if (!runoff && candidates.Contains(pf.endorsedCandidateId))
                {
                    pick = pf.endorsedCandidateId;
                    why = "派閥の事前決定による支持";
                }
                else
                {
                    pick = BossChoice(c, pf, candidates);
                    if (candidates.Contains(pf.bossId)) why = "領袖が出馬";
                    else if (runoff && before >= 0) why = "推した候補が決選に残らず支持を変更（領袖の判断：政策・関係・候補評価）";
                    else why = "領袖の推薦（政策・関係・候補評価）";
                }
                map[pf.id] = pick;
                if (rec == null) continue;
                LeadershipFactionStance stance = StanceOf(rec, pf);
                if (runoff) stance.endorsedRunoff = pick; else stance.endorsedRound1 = pick;
                stance.reason = runoff ? (stance.reason + "／決選：" + why) : why;
            }
            return map;
        }

        private static int BossChoice(Ctx c, PartyFaction pf, List<int> candidates)
        {
            Person boss = c.Get(pf.bossId);
            int best = -1;
            float bestScore = float.NegativeInfinity;
            var sorted = new List<int>(candidates);
            sorted.Sort();
            for (int i = 0; i < sorted.Count; i++)
            {
                float s = Preference(c, boss, sorted[i], null, 0, false);
                Person cand = c.Get(sorted[i]);
                if (cand != null && pf.policyStance.Length > 0 && cand.creed != Creed.無関心 && pf.policyStance == cand.creed.ToString())
                    s += c.prm.policyWeight;
                if (s > bestScore) { bestScore = s; best = sorted[i]; }
            }
            return best;
        }

        /// <summary>投票者の選好＝候補評価＋政策の一致＋人間関係＋所属派閥の推薦×結束＋seed の揺らぎ（候補本人は自分に）。</summary>
        private static float Preference(Ctx c, Person voter, int candidateId, Dictionary<int, int> endorse, int round, bool noise)
        {
            if (voter == null) return c.Strength(candidateId);
            if (voter.id == candidateId) return SelfVotePreference;
            Person cand = c.Get(candidateId);
            float s = c.Strength(candidateId);
            if (cand != null && voter.creed != Creed.無関心 && voter.creed == cand.creed) s += c.prm.policyWeight;
            s += c.prm.relationWeight * Relation(voter, cand);
            if (endorse != null)
            {
                PartyFaction pf = FactionOf(c.party, voter.id);
                if (pf != null && endorse.TryGetValue(pf.id, out int e) && e == candidateId) s += c.prm.factionWeight * pf.cohesion;
            }
            if (noise && c.prm.noiseWeight > 0f)
                s += c.prm.noiseWeight * (LeadershipElectionRules.SeededRoll(c.seed, voter.id * NoiseRoundOffset + round, candidateId) - 0.5f);
            return s;
        }

        private static int BestChoice(Ctx c, Person voter, List<int> options, Dictionary<int, int> endorse, int round, bool noise)
        {
            int best = -1;
            float bestScore = float.NegativeInfinity;
            var sorted = new List<int>(options);
            sorted.Sort();
            for (int i = 0; i < sorted.Count; i++)
            {
                float s = Preference(c, voter, sorted[i], endorse, round, noise);
                if (s > bestScore) { bestScore = s; best = sorted[i]; }
            }
            return best;
        }

        /// <summary>一人1票の投票（投票者はID昇順・重複IDは1回）。</summary>
        private static Dictionary<int, int> CastVotes(Ctx c, LeadershipElectionRecord rec, List<int> voters, List<int> candidates,
            Dictionary<int, int> endorse, int round, bool noise)
        {
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < candidates.Count; i++) counts[candidates[i]] = 0;
            var seen = new HashSet<int>();
            var sorted = new List<int>(voters);
            sorted.Sort();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (!seen.Add(sorted[i])) continue;
                int pick = BestChoice(c, c.Get(sorted[i]), candidates, endorse, round, noise);
                if (pick >= 0) counts[pick]++;
            }
            return counts;
        }

        /// <summary>派閥ごとの最終回の投票の実績（投票した所属者・推薦どおりに投じた人＝差が造反）と無派閥の投票者数を記録する。</summary>
        private static void FillFactionVotes(Ctx c, LeadershipElectionRecord rec, List<int> voters, List<int> candidates,
            Dictionary<int, int> endorse, int round, bool noise)
        {
            var seen = new HashSet<int>();
            int unaffiliated = 0;
            var sorted = new List<int>(voters);
            sorted.Sort();
            for (int i = 0; i < sorted.Count; i++)
            {
                int v = sorted[i];
                if (!seen.Add(v)) continue;
                PartyFaction pf = FactionOf(c.party, v);
                if (pf == null) { unaffiliated++; continue; }
                LeadershipFactionStance stance = StanceOf(rec, pf);
                stance.membersVoted++;
                int pick = BestChoice(c, c.Get(v), candidates, endorse, round, noise);
                if (endorse.TryGetValue(pf.id, out int e) && e >= 0 && e == pick) stance.membersFollowed++;
            }
            rec.unaffiliatedVoters = unaffiliated;
        }

        private static LeadershipFactionStance StanceOf(LeadershipElectionRecord rec, PartyFaction pf)
        {
            for (int i = 0; i < rec.factions.Count; i++)
                if (rec.factions[i].factionId == pf.id) return rec.factions[i];
            var s = new LeadershipFactionStance { factionId = pf.id, name = pf.name ?? "", bossId = pf.bossId };
            rec.factions.Add(s);
            rec.factions.Sort((x, y) => x.factionId.CompareTo(y.factionId));
            return s;
        }

        /// <summary>
        /// 集計議席の匿名票を候補へ配る：ネームド議員の票の比率で最大剰余法（ネームド票が無ければ候補評価の比率＝推計）。算式を記録に残す。
        /// </summary>
        private static int[] DistributeAggregate(Ctx c, LeadershipElectionRecord rec, List<int> candidates, Dictionary<int, int> namedVotes,
            int aggregateSeats, string roundLabel)
        {
            if (aggregateSeats <= 0) return new int[candidates.Count];
            var weights = new List<float>(candidates.Count);
            int namedTotal = 0;
            for (int i = 0; i < candidates.Count; i++) namedTotal += namedVotes[candidates[i]];
            bool byNamed = namedTotal > 0;
            for (int i = 0; i < candidates.Count; i++)
                weights.Add(byNamed ? namedVotes[candidates[i]] : Mathf.Max(0.0001f, c.Strength(candidates[i])));
            string formula = roundLabel + "：集計議席" + aggregateSeats + "票（人物のいない議席＝匿名）を" +
                             (byNamed ? "ネームド議員の票の比率" : "候補評価の比率（ネームド議員の票が無いための推計）") + "で最大剰余配分";
            rec.aggregateFormula = rec.aggregateFormula.Length == 0 ? formula : rec.aggregateFormula + "／" + formula;
            return SeatAllocationRules.LargestRemainder(candidates, weights, aggregateSeats);
        }

        /// <summary>一般党員の票の母集団：星系の集計（地方支部）を優先し、全国集計が上回る分は「星系不明分」。どちらも無ければ不明＝党員票なし。</summary>
        private static List<MemberPool> MemberPools(Ctx c, LeadershipElectionRecord rec, ICollection<int> owned)
        {
            var pools = new List<MemberPool>();
            long regionalSum = 0;
            int excluded = 0;
            if (c.party.regionalMemberships != null)
                for (int i = 0; i < c.party.regionalMemberships.Count; i++)
                {
                    PartyMembershipTally t = c.party.regionalMemberships[i];
                    if (t == null || !t.known || t.members <= 0 || t.systemId < 0) continue;
                    if (owned != null && !owned.Contains(t.systemId)) { excluded++; continue; }
                    pools.Add(new MemberPool { systemId = t.systemId, members = t.members });
                    regionalSum += t.members;
                }
            pools.Sort((x, y) => x.systemId.CompareTo(y.systemId));
            if (excluded > 0) rec.restrictions.Add("勢力の星系でない地方集計 " + excluded + "件は除外");

            PartyMembershipTally n = c.party.nationalMembership;
            bool nationalKnown = n != null && n.known && n.members > 0;
            if (pools.Count > 0)
            {
                if (nationalKnown && n.members > regionalSum)
                {
                    pools.Add(new MemberPool { systemId = PartyMembershipTally.NationalScope, members = n.members - regionalSum });
                    rec.restrictions.Add("全国集計のうち星系が不明な " + (n.members - regionalSum) + "人は党員票に含め地方票には数えない");
                }
                else if (nationalKnown && n.members < regionalSum)
                    rec.restrictions.Add("全国集計が星系の合計より少ないため星系の集計を採用");
            }
            else if (nationalKnown)
                pools.Add(new MemberPool { systemId = PartyMembershipTally.NationalScope, members = n.members });
            else
                rec.restrictions.Add("一般党員数が不明＝党員票なし（議員票だけで決める・ネームド党員の数から換算しない）");
            return pools;
        }

        /// <summary>一つの母集団の生票：候補の党員への訴求（人気×弁舌、その星系が地盤なら割増）の比率で党員数を最大剰余配分。</summary>
        private static long[] RawMemberVotes(Ctx c, List<int> candidates, MemberPool pool)
        {
            var result = new long[candidates.Count];
            if (pool.members <= 0) return result;
            var weights = new List<float>(candidates.Count);
            float sum = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                Person p = c.Get(candidates[i]);
                float w = PoliticianRules.MemberVoteAppeal(ElectionCycleRules.ProfileOf(p, StandingForAppeal));
                if (p != null && pool.systemId >= 0 && p.birthSystemId == pool.systemId) w *= PoliticianRules.HomeTurnoutBonus;
                weights.Add(w);
                sum += w;
            }
            if (sum <= 0f) for (int i = 0; i < weights.Count; i++) weights[i] = 1f;
            int seats = pool.members > int.MaxValue ? int.MaxValue : (int)pool.members;
            int[] alloc = SeatAllocationRules.LargestRemainder(candidates, weights, seats);
            for (int i = 0; i < alloc.Length; i++) result[i] = alloc[i];
            return result;
        }

        /// <summary>第1回の順位：得票→議員票→党員算定票→党員生票→seed のくじ→ID小（入力順に依らない）。</summary>
        private static List<int> RankRound1(Ctx c, LeadershipElectionRecord rec, List<int> candidates)
        {
            var list = new List<int>(candidates);
            list.Sort((x, y) =>
            {
                LeadershipCandidateResult a = rec.Candidate(x), b = rec.Candidate(y);
                if (a.round1Total != b.round1Total) return b.round1Total.CompareTo(a.round1Total);
                int la = a.round1Named + a.round1Aggregate, lb = b.round1Named + b.round1Aggregate;
                if (la != lb) return lb.CompareTo(la);
                if (a.round1MemberConverted != b.round1MemberConverted) return b.round1MemberConverted.CompareTo(a.round1MemberConverted);
                if (a.round1MemberRaw != b.round1MemberRaw) return b.round1MemberRaw.CompareTo(a.round1MemberRaw);
                float ra = LeadershipElectionRules.SeededRoll(c.seed, x, Round1TieStream);
                float rb = LeadershipElectionRules.SeededRoll(c.seed, y, Round1TieStream);
                if (ra != rb) return rb.CompareTo(ra);
                return x.CompareTo(y);
            });
            return list;
        }

        /// <summary>候補評価の高い方が前（同じならID小）。</summary>
        private static int CompareStrength(Ctx c, int x, int y)
        {
            float sx = c.Strength(x), sy = c.Strength(y);
            if (sx != sy) return sy.CompareTo(sx);
            return x.CompareTo(y);
        }

        private static void SetStatus(LeadershipElectionRecord rec, int personId, LeadershipCandidateStatus status)
        {
            LeadershipCandidateResult cr = rec.Candidate(personId);
            if (cr != null) cr.status = status;
        }

        // ===== 内部：確定 =====

        /// <summary>当選の確定：党首に就け（<see cref="Party.leaderId"/> のみ）、任期・派閥の主流/反主流・記録を残す。首相・議席・軍は触らない。</summary>
        private static LeadershipElectionRecord Finish(Ctx c, LeadershipElectionRecord rec, PartyLeadershipState st, int winner,
            int previousLeaderId, int year, Dictionary<int, int> finalEndorse)
        {
            bool continuing = winner == previousLeaderId && previousLeaderId >= 0;
            PartyOrganizationRules.AppointPost(c.party, PartyPost.党首, winner);
            st.consecutiveTerms = continuing && st.pendingReason.Length == 0 ? st.consecutiveTerms + 1 : 1;
            st.termStartYear = year;
            st.termEndYear = year + c.prm.termYears;
            st.nextElectionYear = st.termEndYear;
            st.pendingReason = "";
            st.termNote = "";
            rec.winnerId = winner;

            if (c.party.factions != null)
                for (int i = 0; i < c.party.factions.Count; i++)
                {
                    PartyFaction pf = c.party.factions[i];
                    if (pf == null) continue;
                    int e = finalEndorse != null && finalEndorse.TryGetValue(pf.id, out int v) ? v : -1;
                    pf.endorsedCandidateId = e;
                    pf.mainstream = e >= 0 && e == winner;
                    LeadershipFactionStance stance = StanceOf(rec, pf);
                    stance.mainstream = pf.mainstream;
                    if (rec.outcome == LeadershipOutcome.無投票当選) stance.endorsedRound1 = e;
                }
            return Record(c, rec, st, year);
        }

        /// <summary>
        /// 投票で決められないとき：現党首がいれば暫定続投、いなければ党内の候補評価の最上位を暫定党首（1年）、適格な党員もいなければ選出不能（空席のまま）。
        /// </summary>
        private static LeadershipElectionRecord FinishWithoutVote(Ctx c, LeadershipElectionRecord rec, PartyLeadershipState st, List<int> eligible,
            int incumbent, int year, string why, bool allowActing)
        {
            int until = year + c.prm.provisionalTermYears;
            if (incumbent >= 0 && c.party.leaderId == incumbent)
            {
                rec.outcome = LeadershipOutcome.暫定続投;
                rec.winnerId = incumbent;
                rec.reason = why + "＝現党首が暫定で続投（SE" + until + " に再実施）";
                st.pendingReason = "暫定続投：" + why;
            }
            else if (allowActing && eligible.Count > 0)
            {
                var sorted = new List<int>(eligible);
                sorted.Sort((x, y) => CompareStrength(c, x, y));
                int pick = sorted[0];
                PartyOrganizationRules.AppointPost(c.party, PartyPost.党首, pick);
                rec.outcome = LeadershipOutcome.暫定選出;
                rec.winnerId = pick;
                rec.reason = why + "＝党員で候補評価が最上位の人物を暫定党首に（SE" + until + " に再実施）";
                st.pendingReason = "暫定選出：" + why;
                st.consecutiveTerms = 0;
            }
            else
            {
                rec.outcome = LeadershipOutcome.選出不能;
                rec.winnerId = -1;
                rec.reason = why + (eligible.Count == 0 ? "（党首に就ける適格な党員がいない）" : "") + "＝党首は空席のまま（SE" + until + " に再実施）";
                st.pendingReason = "党首選出待ち：" + why;
                if (c.party.leaderId >= 0 && !eligible.Contains(c.party.leaderId)) PartyOrganizationRules.DismissPost(c.party, PartyPost.党首);
            }
            st.termStartYear = rec.winnerId >= 0 ? (rec.outcome == LeadershipOutcome.暫定続投 ? st.termStartYear : year) : 0;
            st.termEndYear = rec.winnerId >= 0 ? until : 0;
            st.nextElectionYear = until;
            st.termNote = "";
            return Record(c, rec, st, year);
        }

        private static LeadershipElectionRecord Record(Ctx c, LeadershipElectionRecord rec, PartyLeadershipState st, int year)
        {
            rec.termStartYear = st.termStartYear;
            rec.termEndYear = st.termEndYear;
            rec.nextElectionYear = st.nextElectionYear;
            st.lastElectionYear = year;
            if (st.records == null) st.records = new List<LeadershipElectionRecord>();
            st.records.Add(rec);
            while (st.records.Count > c.prm.maxRecords) st.records.RemoveAt(0);
            return rec;
        }

        private static void NormalizeRecord(LeadershipElectionRecord r)
        {
            if (r.trigger == null) r.trigger = "";
            if (r.reason == null) r.reason = "";
            if (r.voterBasis == null) r.voterBasis = "";
            if (r.aggregateFormula == null) r.aggregateFormula = "";
            if (r.restrictions == null) r.restrictions = new List<string>();
            if (r.regions == null) r.regions = new List<LeadershipRegionResult>();
            if (r.candidates == null) r.candidates = new List<LeadershipCandidateResult>();
            if (r.factions == null) r.factions = new List<LeadershipFactionStance>();
            r.restrictions.RemoveAll(x => x == null);
            r.regions.RemoveAll(x => x == null);
            r.candidates.RemoveAll(x => x == null);
            r.factions.RemoveAll(x => x == null);
            for (int i = 0; i < r.candidates.Count; i++)
                if (r.candidates[i].endorserIds == null) r.candidates[i].endorserIds = new List<int>();
            for (int i = 0; i < r.factions.Count; i++)
                if (r.factions[i].name == null) r.factions[i].name = "";
            if (r.memberRawTotal < 0) r.memberRawTotal = 0;
        }
    }
}
