using System.Collections.Generic;

namespace Ginei
{
    /// <summary>所属の変化の種類。</summary>
    public enum PartyMembershipChangeKind { 入党, 離党, 移籍 }

    /// <summary>政府との関係（下院の組閣と確定議席から導く。支持率では決めない）。</summary>
    public enum PartyGovernmentRole
    {
        未確定,   // 組閣が無い・未成立・対象外（与党を決められない）
        与党,     // 組閣した党（単独過半／少数政権）
        野党,     // 組閣した党以外で、どちらかの議院に確定議席を持つ
        議席なし  // 組閣はあるが、この党は確定議席を持たない
    }

    /// <summary>入党・離党・移籍の一件の結果（通知・試験用）。</summary>
    public struct PartyMembershipChange
    {
        public bool ok;
        public PartyMembershipChangeKind kind;
        public int personId;
        /// <summary>元の党（-1=無所属）。</summary>
        public int fromPartyId;
        /// <summary>移った先の党（-1=無所属）。</summary>
        public int toPartyId;
        /// <summary>元の党の党首が空席になった。</summary>
        public bool leaderVacated;
        /// <summary>空席になった党役職（党首を除く）の数。</summary>
        public int postsVacated;
        /// <summary>元の党に帰属する議席を失った（当選回数は変えない）。</summary>
        public bool seatVacated;
        /// <summary>変化の理由（失敗時は拒否の理由）。</summary>
        public string reason;
    }

    /// <summary>一政党の数字の内訳（支持率・議席・ネームド政治家・国政議員・一般党員を混同しない）。</summary>
    public struct PartyStatusSummary
    {
        public int partyId;
        public PartyGovernmentRole role;
        /// <summary>支持率（0..1）。</summary>
        public float support;
        /// <summary>確定議席（未構成の議院は0）。</summary>
        public int lowerSeats;
        public int upperSeats;
        /// <summary>ネームドの所属政治家（重複IDを数えない）。</summary>
        public int namedPoliticians;
        /// <summary>この党の議席を持つ実在の議員。</summary>
        public int namedLowerLegislators;
        public int namedUpperLegislators;
        /// <summary>一般党員の全国集計があるか（false=不明）。</summary>
        public bool nationalMembershipKnown;
        /// <summary>一般党員の全国集計（人）。</summary>
        public long nationalMembership;
        /// <summary>集計値のある星系の数。</summary>
        public int regionalTalliesKnown;
    }

    /// <summary>
    /// 政党の所属管理の純ロジック（#159 / #165 / #2768・入党/離党/移籍の共通入口）。
    /// <see cref="Party.memberIds"/> はネームドの所属政治家（一人一党）で、議員（<see cref="LegislatorRosterRules"/>）とも
    /// 一般党員の集計（<see cref="PartyMembershipTally"/>）とも別。個々の党の操作（役職・派閥の名簿）は <see cref="PartyOrganizationRules"/> に任せ、
    /// 本クラスは勢力の党全体を見て一人一党・同勢力・資格・議員資格の整合をとる。
    /// <para>所属の変化だけで首相・内閣・軍の指揮権は動かさない（組閣は <see cref="ElectionCycleRules"/>）。確定議席と当選回数は増やさない。</para>
    /// 決定論・test-first。
    /// </summary>
    public static class PartyMembershipRules
    {
        // 無所属者の自動配属で、綱領と信条・支持基盤と出自が一致したときの加点（信条を出自より重く見る）。
        private const int CreedMatchScore = 2;
        private const int ClassBaseMatchScore = 1;

        // ===== 照会 =====

        /// <summary>その人物が属する政党（無所属は null・重複所属のデータでは党一覧の先頭）。</summary>
        public static Party PartyOf(PoliticsState pol, int personId)
            => pol != null ? ElectionCycleRules.PartyOf(pol.parties, personId) : null;

        /// <summary>その人物を党員に載せている党の数（一人一党なら0か1）。</summary>
        public static int MembershipCount(IList<Party> parties, int personId)
        {
            if (parties == null || personId < 0) return 0;
            int n = 0;
            for (int i = 0; i < parties.Count; i++)
                if (parties[i] != null && parties[i].memberIds != null && parties[i].memberIds.Contains(personId)) n++;
            return n;
        }

        /// <summary>ネームドの所属政治家の数（重複ID・負のIDを数えない）。一般党員の数ではない。</summary>
        public static int NamedPoliticianCount(Party party)
        {
            if (party == null || party.memberIds == null) return 0;
            var seen = new HashSet<int>();
            for (int i = 0; i < party.memberIds.Count; i++)
                if (party.memberIds[i] >= 0) seen.Add(party.memberIds[i]);
            return seen.Count;
        }

        // ===== 入党・離党・移籍 =====

        /// <summary>
        /// 入党できない理由（入れるなら null）：政党が無い／他勢力の党／人物が名簿に無い・資格がない（生存・自由・同勢力の文民政治家）／
        /// 既にこの党に所属／他党に所属（移籍を使う）。
        /// </summary>
        public static string JoinBlockReason(PoliticsState pol, Faction f, IList<Person> roster, int personId, int partyId)
        {
            string r = TargetBlockReason(pol, f, roster, personId, partyId);
            if (r != null) return r;
            Party current = PartyOf(pol, personId);
            if (current != null)
                return current.id == partyId ? "既にこの党に所属している" : "既に" + current.partyName + "に所属している（移籍を使う）";
            return null;
        }

        /// <summary>無所属の人物を入党させる（<see cref="JoinBlockReason"/> が null のときだけ）。</summary>
        public static PartyMembershipChange Join(PoliticsState pol, Faction f, IList<Person> roster, int personId, int partyId, string reason)
        {
            var c = NewChange(PartyMembershipChangeKind.入党, personId, -1, -1);
            string block = JoinBlockReason(pol, f, roster, personId, partyId);
            if (block != null) { c.reason = block; return c; }
            Party target = ElectionCycleRules.FindParty(pol.parties, partyId);
            c.ok = PartyOrganizationRules.Join(target, personId);
            c.toPartyId = c.ok ? partyId : -1;
            c.reason = c.ok ? (reason ?? "") : "入党できなかった";
            if (c.ok && pol.independentPersonIds != null) pol.independentPersonIds.RemoveAll(x => x == personId);
            return c;
        }

        /// <summary>
        /// 離党させる（載っている全ての党から外す＝重複所属も残さない）。党首・党役職・派閥の名簿から外し、
        /// 元の党に帰属する議席を失わせる（当選回数・確定議席は変えない＝議席は集計議席へ戻る）。
        /// 本人は無所属を選んだ扱い（<see cref="PoliticsState.independentPersonIds"/>）＝自動補充で再入党させない。無所属なら失敗。
        /// </summary>
        public static PartyMembershipChange Leave(PoliticsState pol, int personId, string reason)
        {
            Party from = PartyOf(pol, personId);
            var c = NewChange(PartyMembershipChangeKind.離党, personId, from != null ? from.id : -1, -1);
            if (from == null) { c.reason = "無所属のため離党できない"; return c; }
            RemoveFromAll(pol, personId, from, reason, ref c);
            if (pol.independentPersonIds == null) pol.independentPersonIds = new List<int>();
            if (!pol.independentPersonIds.Contains(personId)) pol.independentPersonIds.Add(personId);
            c.ok = true;
            c.reason = reason ?? "";
            return c;
        }

        /// <summary>
        /// 他党へ移籍させる：移籍先が同勢力の党で、本人が資格を満たすときだけ。元の党の役職・派閥・議席は外れ、
        /// 移籍先ではただの党員（役職・議席・首相の地位は与えない）。無所属は入党を使う。
        /// </summary>
        public static PartyMembershipChange Transfer(PoliticsState pol, Faction f, IList<Person> roster, int personId, int toPartyId, string reason)
        {
            Party from = PartyOf(pol, personId);
            var c = NewChange(PartyMembershipChangeKind.移籍, personId, from != null ? from.id : -1, -1);
            if (from == null) { c.reason = "無所属のため移籍でなく入党を使う"; return c; }
            if (from.id == toPartyId) { c.reason = "既にこの党に所属している"; return c; }
            string block = TargetBlockReason(pol, f, roster, personId, toPartyId);
            if (block != null) { c.reason = block; return c; }

            RemoveFromAll(pol, personId, from, reason, ref c);
            Party target = ElectionCycleRules.FindParty(pol.parties, toPartyId);
            c.ok = PartyOrganizationRules.Join(target, personId);
            c.toPartyId = c.ok ? toPartyId : -1;
            c.reason = reason ?? "";
            if (pol.independentPersonIds != null) pol.independentPersonIds.RemoveAll(x => x == personId);
            return c;
        }

        // ===== 整理（資格・一人一党・役職と派閥の整合） =====

        /// <summary>
        /// 党員名簿を現況へ整える（入党はさせない）：①資格を失った党員（死亡・名簿不在・他勢力・在野・政治家でない。拘束中は残す）を離党、
        /// ②同じ党の重複ID・負のIDを除く、③複数の党に載る人は1党に絞る（その党の議席を持つ党→党首を務める党→党ID小）、
        /// ④党首・党役職・派閥の名簿と領袖を党員だけにする。<paramref name="roster"/> が null なら①を行わない（読込時）。
        /// 議員資格は外さない（<see cref="LegislatorRosterRules.Reconcile"/> が理由つきで外す）。変更件数を返す。
        /// </summary>
        public static int Normalize(IList<Party> parties, PoliticsState pol, Faction f, IList<Person> roster)
        {
            if (parties == null) return 0;
            int changes = 0;

            // ①② 資格と重複
            for (int i = 0; i < parties.Count; i++)
            {
                Party p = parties[i];
                if (p == null) continue;
                if (p.memberIds == null) p.memberIds = new List<int>();
                var seen = new HashSet<int>();
                for (int m = 0; m < p.memberIds.Count; m++)
                {
                    int id = p.memberIds[m];
                    if (id < 0 || !seen.Add(id)) { p.memberIds.RemoveAt(m); m--; changes++; }
                }
                if (roster == null) continue;
                for (int m = p.memberIds.Count - 1; m >= 0; m--)
                {
                    int id = p.memberIds[m];
                    if (!ElectionCycleRules.IsValidPartyMember(ElectionCycleRules.FindPerson(roster, id), f))
                    {
                        PartyOrganizationRules.Leave(p, id);
                        changes++;
                    }
                }
            }

            // ③ 一人一党
            var ids = new List<int>();
            var counted = new HashSet<int>();
            for (int i = 0; i < parties.Count; i++)
                if (parties[i] != null)
                    for (int m = 0; m < parties[i].memberIds.Count; m++)
                        if (counted.Add(parties[i].memberIds[m])) ids.Add(parties[i].memberIds[m]);
            ids.Sort();
            for (int k = 0; k < ids.Count; k++)
            {
                int id = ids[k];
                if (MembershipCount(parties, id) <= 1) continue;
                Party keep = KeeperParty(parties, pol, id);
                for (int i = 0; i < parties.Count; i++)
                {
                    Party p = parties[i];
                    if (p == null || p == keep || !p.memberIds.Contains(id)) continue;
                    PartyOrganizationRules.Leave(p, id);
                    changes++;
                }
            }

            // ④ 党首・役職・派閥
            for (int i = 0; i < parties.Count; i++)
            {
                Party p = parties[i];
                if (p != null) changes += NormalizeOffices(p);
            }
            return changes;
        }

        /// <summary>
        /// 無所属の適格な政治家（ID昇順）を党へ配属する（既に所属している人は動かさない。<paramref name="pol"/> があれば自ら無所属を選んだ人も除く）。
        /// 配属先は同勢力の党から理由つきで決める（<see cref="ChooseParty"/>）。党が無ければ誰も配属しない。配属の一覧を返す。
        /// </summary>
        public static List<PartyMembershipChange> AssignUnaffiliated(IList<Party> parties, Faction f, IList<Person> roster, PoliticsState pol = null)
        {
            var list = new List<PartyMembershipChange>();
            if (parties == null || parties.Count == 0) return list;
            List<int> independents = pol != null ? pol.independentPersonIds : null;
            List<Person> unaffiliated = ElectionCycleRules.SortedById(roster,
                x => ElectionCycleRules.IsEligiblePolitician(x, f) && ElectionCycleRules.PartyOf(parties, x.id) == null
                     && (independents == null || !independents.Contains(x.id)));
            for (int i = 0; i < unaffiliated.Count; i++)
            {
                Person person = unaffiliated[i];
                Party target = ChooseParty(parties, f, person, out string why);
                if (target == null || !PartyOrganizationRules.Join(target, person.id)) continue;
                var c = NewChange(PartyMembershipChangeKind.入党, person.id, -1, target.id);
                c.ok = true;
                c.reason = why;
                list.Add(c);
            }
            return list;
        }

        /// <summary>
        /// 無所属の人物の配属先を決める（同勢力の党のみ）：綱領が本人の信条（<see cref="Person.creed"/>・無関心を除く）と同名なら +2、
        /// 支持基盤が出自（<see cref="Person.socialOrigin"/>）と同名なら +1。得点の高い党→党員（ネームド）の少ない党→党ID小。
        /// 綱領・支持基盤が未設定の党は対応なし（人物の特性を推測で作らない）。候補が無ければ null。
        /// </summary>
        public static Party ChooseParty(IList<Party> parties, Faction f, Person person, out string reason)
        {
            reason = "";
            if (parties == null || person == null) return null;
            Party best = null;
            int bestScore = -1;
            bool bestCreed = false, bestClass = false;
            for (int i = 0; i < parties.Count; i++)
            {
                Party p = parties[i];
                if (p == null || p.faction != f) continue;
                if (p.memberIds == null) p.memberIds = new List<int>();
                bool creed = person.creed != Creed.無関心 && !string.IsNullOrEmpty(p.platform) && p.platform == person.creed.ToString();
                bool cls = !string.IsNullOrEmpty(p.classBase) && p.classBase == person.socialOrigin.ToString();
                int score = (creed ? CreedMatchScore : 0) + (cls ? ClassBaseMatchScore : 0);
                if (best == null || score > bestScore
                    || (score == bestScore && (p.memberIds.Count < best.memberIds.Count
                        || (p.memberIds.Count == best.memberIds.Count && p.id < best.id))))
                {
                    best = p;
                    bestScore = score;
                    bestCreed = creed;
                    bestClass = cls;
                }
            }
            if (best == null) return null;
            if (bestCreed && bestClass)
                reason = "綱領「" + best.platform + "」が信条と、支持基盤「" + best.classBase + "」が出自と一致";
            else if (bestCreed)
                reason = "綱領「" + best.platform + "」が信条と一致";
            else if (bestClass)
                reason = "支持基盤「" + best.classBase + "」が出自と一致";
            else
                reason = "綱領・支持基盤との対応なし＝党員の最も少ない党（同数は党ID小）";
            return best;
        }

        // ===== 与党・野党と内訳 =====

        /// <summary>組閣した党（単独過半／少数政権で、その党が存在するとき）。無ければ -1。支持率では決めない。</summary>
        public static int GovernmentPartyId(PoliticsState pol)
        {
            GovernmentFormation g = pol != null ? pol.government : null;
            if (g == null || g.partyId < 0) return -1;
            if (g.status != CabinetStatus.単独過半 && g.status != CabinetStatus.少数政権) return -1;
            return ElectionCycleRules.FindParty(pol.parties, g.partyId) != null ? g.partyId : -1;
        }

        /// <summary>政府との関係（組閣が無ければ全党が未確定）。</summary>
        public static PartyGovernmentRole RoleOf(PoliticsState pol, int partyId)
        {
            int gov = GovernmentPartyId(pol);
            if (gov < 0) return PartyGovernmentRole.未確定;
            if (partyId == gov) return PartyGovernmentRole.与党;
            return SeatedSeats(pol.lowerSeats, partyId) + SeatedSeats(pol.upperSeats, partyId) > 0
                ? PartyGovernmentRole.野党 : PartyGovernmentRole.議席なし;
        }

        /// <summary>一政党の数字の内訳（観測・試験用・状態は変えない）。</summary>
        public static PartyStatusSummary Summarize(PoliticsState pol, Party party)
        {
            var s = new PartyStatusSummary { partyId = party != null ? party.id : -1, role = PartyGovernmentRole.未確定 };
            if (party == null) return s;
            s.role = RoleOf(pol, party.id);
            s.support = party.support;
            s.namedPoliticians = NamedPoliticianCount(party);
            if (pol != null)
            {
                s.lowerSeats = SeatedSeats(pol.lowerSeats, party.id);
                s.upperSeats = SeatedSeats(pol.upperSeats, party.id);
                s.namedLowerLegislators = LegislatorRosterRules.NamedSeats(pol, LegislativeChamber.下院, party.id);
                s.namedUpperLegislators = LegislatorRosterRules.NamedSeats(pol, LegislativeChamber.上院, party.id);
            }
            PartyMembershipTally n = party.nationalMembership;
            s.nationalMembershipKnown = n != null && n.known;
            s.nationalMembership = s.nationalMembershipKnown ? n.members : 0;
            if (party.regionalMemberships != null)
                for (int i = 0; i < party.regionalMemberships.Count; i++)
                    if (party.regionalMemberships[i] != null && party.regionalMemberships[i].known) s.regionalTalliesKnown++;
            return s;
        }

        // ===== 一般党員の集計 =====

        /// <summary>
        /// 一般党員の集計を明示値で設定する（<paramref name="systemId"/>＝<see cref="PartyMembershipTally.NationalScope"/> で全国）。
        /// 負の人数・不正な星系IDは拒否。人物IDの数から換算しない＝呼び出し側が出所を持つ値だけを渡す。
        /// </summary>
        public static bool SetGeneralMembership(Party party, int systemId, long members, string source, int asOfYear)
        {
            if (party == null || members < 0 || systemId < PartyMembershipTally.NationalScope) return false;
            PartyMembershipTally t = TallyFor(party, systemId, true);
            t.known = true;
            t.members = members;
            t.source = source ?? "";
            t.asOfYear = asOfYear > 0 ? asOfYear : 0;
            return true;
        }

        /// <summary>一般党員の集計を不明に戻す。集計があれば true。</summary>
        public static bool ClearGeneralMembership(Party party, int systemId)
        {
            PartyMembershipTally t = party != null ? TallyFor(party, systemId, false) : null;
            if (t == null || !t.known) return false;
            t.known = false;
            t.members = 0;
            t.source = "";
            t.asOfYear = 0;
            return true;
        }

        /// <summary>一般党員の集計を引く（不明なら false）。</summary>
        public static bool TryGetGeneralMembership(Party party, int systemId, out long members)
        {
            PartyMembershipTally t = party != null ? TallyFor(party, systemId, false) : null;
            members = t != null && t.known ? t.members : 0;
            return t != null && t.known;
        }

        // ===== 読込 =====

        /// <summary>
        /// セーブから読んだ政党の穴埋め（読込だけで入党・選挙は起こさない）：一般党員の集計の null を「不明」で作り、壊れた値を不明に戻し、
        /// 星系集計の null・不正ID・重複を除く。党員名簿は人物名簿なしで整える（重複ID・一人一党・役職と派閥の整合のみ）。
        /// </summary>
        public static void NormalizeLoaded(PoliticsState pol)
        {
            if (pol == null || pol.parties == null) return;
            for (int i = 0; i < pol.parties.Count; i++)
            {
                Party p = pol.parties[i];
                if (p == null) continue;
                if (p.memberIds == null) p.memberIds = new List<int>();
                if (p.nationalMembership == null) p.nationalMembership = new PartyMembershipTally(PartyMembershipTally.NationalScope);
                NormalizeTally(p.nationalMembership);
                p.nationalMembership.systemId = PartyMembershipTally.NationalScope;
                if (p.regionalMemberships == null) p.regionalMemberships = new List<PartyMembershipTally>();
                var seen = new HashSet<int>();
                for (int k = 0; k < p.regionalMemberships.Count; k++)
                {
                    PartyMembershipTally t = p.regionalMemberships[k];
                    if (t == null || t.systemId < 0 || !seen.Add(t.systemId)) { p.regionalMemberships.RemoveAt(k); k--; continue; }
                    NormalizeTally(t);
                }
            }
            if (pol.independentPersonIds == null) pol.independentPersonIds = new List<int>();
            var ind = new HashSet<int>();
            for (int k = 0; k < pol.independentPersonIds.Count; k++)
            {
                int id = pol.independentPersonIds[k];
                // 負のID・重複・党に載っている人（無所属でない）は除く
                if (id < 0 || !ind.Add(id) || ElectionCycleRules.PartyOf(pol.parties, id) != null)
                {
                    pol.independentPersonIds.RemoveAt(k);
                    k--;
                }
            }
            Normalize(pol.parties, pol, default(Faction), null);
        }

        // ===== 内部 =====

        private static PartyMembershipChange NewChange(PartyMembershipChangeKind kind, int personId, int from, int to)
            => new PartyMembershipChange { kind = kind, personId = personId, fromPartyId = from, toPartyId = to, reason = "" };

        /// <summary>移籍先・入党先として拒否する理由（所属の有無は見ない）。</summary>
        private static string TargetBlockReason(PoliticsState pol, Faction f, IList<Person> roster, int personId, int partyId)
        {
            if (pol == null || pol.parties == null) return "政党の情報が無い";
            Party target = ElectionCycleRules.FindParty(pol.parties, partyId);
            if (target == null) return "政党#" + partyId + " が見つからない";
            if (target.faction != f) return target.partyName + "は他勢力の政党";
            Person person = ElectionCycleRules.FindPerson(roster, personId);
            if (person == null) return "人物#" + personId + " が名簿に居ない";
            if (!ElectionCycleRules.IsEligiblePolitician(person, f))
                return person.name + "は所属の資格がない（生存・自由・同勢力の文民政治家が必要）";
            return null;
        }

        private static void RemoveFromAll(PoliticsState pol, int personId, Party from, string reason, ref PartyMembershipChange c)
        {
            c.leaderVacated = from.leaderId == personId;
            if (from.posts != null)
                for (int i = 0; i < from.posts.Count; i++)
                    if (from.posts[i] != null && from.posts[i].holderId == personId) c.postsVacated++;
            for (int i = 0; i < pol.parties.Count; i++)
            {
                Party p = pol.parties[i];
                if (p != null && p.memberIds != null && p.memberIds.Contains(personId))
                    PartyOrganizationRules.Leave(p, personId);
            }
            string why = from.partyName + "を離れたため議席を失った（議席は党に帰属）";
            if (!string.IsNullOrEmpty(reason)) why += "：" + reason;
            c.seatVacated = LegislatorRosterRules.VacateSeat(pol, personId, why, from.id);
        }

        /// <summary>重複所属の残す党：その党の議席を持つ党→党首を務める党→党ID小。</summary>
        private static Party KeeperParty(IList<Party> parties, PoliticsState pol, int personId)
        {
            LegislatorRecord seat = pol != null ? LegislatorRosterRules.Find(pol, personId) : null;
            int seatParty = seat != null && seat.seated ? seat.seatPartyId : -1;
            Party best = null;
            int bestRank = int.MaxValue;
            for (int i = 0; i < parties.Count; i++)
            {
                Party p = parties[i];
                if (p == null || !p.memberIds.Contains(personId)) continue;
                int rank = p.id == seatParty ? 0 : (p.leaderId == personId ? 1 : 2);
                if (best == null || rank < bestRank || (rank == bestRank && p.id < best.id))
                {
                    best = p;
                    bestRank = rank;
                }
            }
            return best;
        }

        /// <summary>党首・党役職・派閥を党員だけにする（重複した役職は先頭を残し、党首は posts でなく leaderId が出所）。</summary>
        private static int NormalizeOffices(Party p)
        {
            int changes = 0;
            if (p.leaderId >= 0 && !p.memberIds.Contains(p.leaderId)) { p.leaderId = -1; changes++; }

            if (p.posts == null) p.posts = new List<PartyAppointment>();
            var filled = new HashSet<PartyPost>();
            for (int i = 0; i < p.posts.Count; i++)
            {
                PartyAppointment a = p.posts[i];
                bool bad = a == null || a.post == PartyPost.党首 || a.holderId < 0
                           || !p.memberIds.Contains(a.holderId) || !filled.Add(a.post);
                if (bad) { p.posts.RemoveAt(i); i--; changes++; }
            }

            if (p.factions == null) p.factions = new List<PartyFaction>();
            var order = new List<PartyFaction>();
            for (int i = 0; i < p.factions.Count; i++) if (p.factions[i] != null) order.Add(p.factions[i]);
            order.Sort((a, b) => a.id.CompareTo(b.id));
            var inFaction = new HashSet<int>();
            for (int i = 0; i < order.Count; i++)
            {
                PartyFaction pf = order[i];
                if (pf.memberIds == null) pf.memberIds = new List<int>();
                for (int m = 0; m < pf.memberIds.Count; m++)
                {
                    int id = pf.memberIds[m];
                    if (!p.memberIds.Contains(id) || !inFaction.Add(id)) { pf.memberIds.RemoveAt(m); m--; changes++; }
                }
                if (pf.bossId >= 0 && !p.memberIds.Contains(pf.bossId)) { pf.bossId = -1; changes++; }
            }
            return changes;
        }

        private static int SeatedSeats(ChamberSeats cs, int partyId)
            => cs != null && cs.seated ? ElectionCycleRules.SeatsOf(cs, partyId) : 0;

        private static PartyMembershipTally TallyFor(Party party, int systemId, bool create)
        {
            if (systemId == PartyMembershipTally.NationalScope)
            {
                if (party.nationalMembership == null && create)
                    party.nationalMembership = new PartyMembershipTally(PartyMembershipTally.NationalScope);
                return party.nationalMembership;
            }
            if (systemId < 0) return null;
            if (party.regionalMemberships == null)
            {
                if (!create) return null;
                party.regionalMemberships = new List<PartyMembershipTally>();
            }
            for (int i = 0; i < party.regionalMemberships.Count; i++)
                if (party.regionalMemberships[i] != null && party.regionalMemberships[i].systemId == systemId)
                    return party.regionalMemberships[i];
            if (!create) return null;
            var t = new PartyMembershipTally(systemId);
            party.regionalMemberships.Add(t);
            party.regionalMemberships.Sort((a, b) => (a != null ? a.systemId : -1).CompareTo(b != null ? b.systemId : -1));
            return t;
        }

        private static void NormalizeTally(PartyMembershipTally t)
        {
            if (t.source == null) t.source = "";
            if (t.members < 0) t.known = false;
            if (!t.known) { t.members = 0; t.asOfYear = 0; }
            if (t.asOfYear < 0) t.asOfYear = 0;
        }
    }
}
