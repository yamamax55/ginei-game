using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>内閣・党三役の任免の調整値（ゲーム用。現実の公式な規則ではない）。</summary>
    public readonly struct CabinetParams
    {
        /// <summary>首相・党首が不在の間、在任者が職務を続けられる年数（無期限の代行にしない）。</summary>
        public readonly int caretakerYears;
        /// <summary>大臣が副大臣へ委任できる最長年数。</summary>
        public readonly int maxDelegationYears;
        /// <summary>保持する任免履歴の上限（内閣・党ごと）。</summary>
        public readonly int maxHistory;
        /// <summary>党三役と閣僚の兼任を認めるか（既定＝認めない）。</summary>
        public readonly bool allowPartyExecutiveConcurrent;
        /// <summary>候補評価：能力（行政素養と民望）の重み。</summary>
        public readonly float abilityWeight;
        /// <summary>候補評価：当選回数の年功（逓減）の重み。</summary>
        public readonly float seniorityWeight;
        /// <summary>候補評価：経験の目安（大臣=ベテラン/副大臣=中堅/政務官=新人）に合うときの加点。</summary>
        public readonly float tierFitBonus;
        /// <summary>候補評価：与党の党員への加点。</summary>
        public readonly float rulingPartyBonus;
        /// <summary>候補評価：同じ派閥の在任者が少ないほど効く加点（簡易の派閥均衡）。</summary>
        public readonly float factionBalanceBonus;

        public CabinetParams(int caretakerYears, int maxDelegationYears, int maxHistory, bool allowPartyExecutiveConcurrent,
            float abilityWeight, float seniorityWeight, float tierFitBonus, float rulingPartyBonus, float factionBalanceBonus)
        {
            this.caretakerYears = Mathf.Max(0, caretakerYears);
            this.maxDelegationYears = Mathf.Max(0, maxDelegationYears);
            this.maxHistory = Mathf.Max(1, maxHistory);
            this.allowPartyExecutiveConcurrent = allowPartyExecutiveConcurrent;
            this.abilityWeight = Mathf.Max(0f, abilityWeight);
            this.seniorityWeight = Mathf.Max(0f, seniorityWeight);
            this.tierFitBonus = Mathf.Max(0f, tierFitBonus);
            this.rulingPartyBonus = Mathf.Max(0f, rulingPartyBonus);
            this.factionBalanceBonus = Mathf.Max(0f, factionBalanceBonus);
        }

        /// <summary>既定＝暫定1年・委任最長2年・履歴60件・党三役と閣僚は兼任しない・能力0.6/年功0.25/目安0.1/与党0.1/派閥0.05。</summary>
        public static CabinetParams Default => new CabinetParams(1, 2, 60, false, 0.6f, 0.25f, 0.1f, 0.1f, 0.05f);
    }

    /// <summary>内閣の職で問う行為（権限の差を固定するための列挙）。</summary>
    public enum CabinetAction
    {
        閣僚任免,
        所管政策決定,
        所管決裁,
        政策提案,
        政策調整,
        艦隊作戦指揮,
        国庫支出
    }

    /// <summary>任免・権限の判定結果（可否・理由・権限外なら上申先）。</summary>
    public struct AppointmentResult
    {
        public bool ok;
        /// <summary>画面にそのまま出す理由（許可の根拠／拒否の理由）。</summary>
        public string reason;
        /// <summary>権限外だが任免権者・所管者へ上申として回せるか。</summary>
        public bool canPetition;
        /// <summary>上申先の人物ID（-1＝なし）。</summary>
        public int petitionToId;

        public static AppointmentResult Allow(string reason)
            => new AppointmentResult { ok = true, reason = reason ?? "", petitionToId = -1 };

        public static AppointmentResult Deny(string reason)
            => new AppointmentResult { ok = false, reason = reason ?? "", petitionToId = -1 };

        public static AppointmentResult Petition(string reason, int toId)
            => new AppointmentResult { ok = false, reason = reason ?? "", canPetition = toId >= 0, petitionToId = toId };
    }

    /// <summary>候補の評価（点数と選定理由）。</summary>
    public struct CandidateEvaluation
    {
        public int personId;
        public float score;
        public string reason;
    }

    /// <summary>
    /// 内閣の政治任用（大臣・副大臣・政務官）の任免の純ロジック（#2768 #141 #159 #145・唯一の窓口）。
    /// 任免権者は国政選挙で正式に首班となった首相（<see cref="GovernmentFormation.premierPersonId"/>）だけ。資格は <see cref="OfficeRules.CanHold"/>
    /// （政治任用・文民）に委ね、同勢力の実在・生存・自由な政治家だけを就ける（候補不足は空席のまま＝人物を作らない）。
    /// 大臣は所管の政策と決裁、副大臣は大臣の明示の委任を期限つきで代行、政務官は提案・調整だけ。どの職も艦隊・軍団の作戦指揮権と国庫の直接支出を含まない。
    /// 省庁の職業官僚（<see cref="Ministry.staffIds"/>）・<see cref="GovernmentRegistry"/> の任命には触れない。AI 組閣も <see cref="TryAppoint"/> を通す。決定論・test-first。
    /// </summary>
    public static class CabinetAppointmentRules
    {
        /// <summary>職業官僚の職と区別するための政治任用職の資格の型（政治家・文民のみ）。</summary>
        private static readonly Office PoliticalPostTemplate = new Office(-1, "内閣の政治任用職", OfficeScope.国家, OfficeDomain.内政)
        { politicalAppointmentOnly = true, civilianOnly = true };

        /// <summary>候補評価で能力に混ぜる民望の割合。</summary>
        private const float RenownShare = 0.3f;
        private const float MaxStat = 100f;
        /// <summary>副大臣の候補評価で年功を効かせる割合（大臣＝1・政務官＝0）。</summary>
        private const float ViceSeniorityShare = 0.5f;

        // ===== 参照 =====

        /// <summary>職名（省名の末尾「省」を除いて 大臣／副大臣／大臣政務官 を付ける）。</summary>
        public static string PostTitle(string ministryName, CabinetPostKind kind)
        {
            string b = ministryName ?? "";
            if (b.EndsWith("省", System.StringComparison.Ordinal)) b = b.Substring(0, b.Length - 1);
            switch (kind)
            {
                case CabinetPostKind.大臣: return b + "大臣";
                case CabinetPostKind.副大臣: return b + "副大臣";
                default: return b + "大臣政務官";
            }
        }

        public static string PostTitle(CabinetPost post) => post != null ? PostTitle(post.ministryName, post.kind) : "";

        /// <summary>職の役割の説明（権限の差を表示するための文）。</summary>
        public static string RoleText(CabinetPostKind kind)
        {
            switch (kind)
            {
                case CabinetPostKind.大臣: return "所管の政策方針と決裁";
                case CabinetPostKind.副大臣: return "大臣の補佐（明示の委任範囲だけ期限つきで代行）";
                default: return "政策の提案・調整（最終決裁権なし）";
            }
        }

        /// <summary>
        /// 大臣を置く省＝最上位（<paramref name="topId"/>）の直下の省。<paramref name="topId"/> が負なら最上位の省を使う。ID昇順。
        /// </summary>
        public static List<Ministry> CabinetMinistries(IList<Ministry> tree, int topId)
        {
            var list = new List<Ministry>();
            if (tree == null) return list;
            for (int i = 0; i < tree.Count; i++)
            {
                Ministry m = tree[i];
                if (m == null) continue;
                if (topId >= 0 ? (m.parentId == topId && m.id != topId) : m.IsTopLevel) list.Add(m);
            }
            list.Sort((a, b) => a.id.CompareTo(b.id));
            return list;
        }

        /// <summary>省×種別の職（無ければ null）。</summary>
        public static CabinetPost FindPost(CabinetState cab, int ministryId, CabinetPostKind kind)
        {
            if (cab == null || cab.posts == null) return null;
            for (int i = 0; i < cab.posts.Count; i++)
            {
                CabinetPost p = cab.posts[i];
                if (p != null && p.ministryId == ministryId && p.kind == kind) return p;
            }
            return null;
        }

        /// <summary>その人物が在任する内閣の職（無ければ null）。</summary>
        public static CabinetPost PostHeldBy(CabinetState cab, int personId)
        {
            if (cab == null || cab.posts == null || personId < 0) return null;
            for (int i = 0; i < cab.posts.Count; i++)
                if (cab.posts[i] != null && cab.posts[i].holderId == personId) return cab.posts[i];
            return null;
        }

        /// <summary>在任者のいる職の数。</summary>
        public static int FilledCount(CabinetState cab, CabinetPostKind kind)
        {
            int n = 0;
            if (cab == null || cab.posts == null) return 0;
            for (int i = 0; i < cab.posts.Count; i++)
                if (cab.posts[i] != null && cab.posts[i].kind == kind && cab.posts[i].holderId >= 0) n++;
            return n;
        }

        /// <summary>
        /// 政治任用職に就けない理由（就けるなら null）：名簿に無い・死亡・拘束/不在・他勢力・在野・政治家でない・軍人。
        /// 資格の型は <see cref="OfficeRules.CanHold"/> に委ねる。人物は書き換えない。
        /// </summary>
        public static string PersonProblem(Person p, Faction f)
        {
            if (p == null) return "名簿に存在しない人物";
            if (p.IsDeceased) return "死亡";
            if (!p.IsAvailable) return "拘束・不在（" + p.captiveStatus + "）";
            if (p.faction != f) return "他勢力（" + p.faction + "）の人物";
            if (p.isFreeAgent) return "在野";
            if (!p.isPolitician) return "政治家でない（政治任用の資格なし）";
            if (p.role != PersonRole.文民) return "軍人（文民の政治任用職に就けない）";
            if (!OfficeRules.CanHold(p, PoliticalPostTemplate)) return "政治任用職の資格を満たさない";
            return null;
        }

        /// <summary>
        /// 正式な首相（任免権者）を返す。首相がいなければ -1 と理由：未組閣・対象外の政体・組閣未成立・首相が職務を続けられない。
        /// </summary>
        public static int FormalPremier(PoliticsState pol, Faction f, IList<Person> roster, out string problem)
        {
            problem = null;
            GovernmentFormation g = pol != null ? pol.government : null;
            if (g == null) { problem = "組閣されていない（首相不在）"; return -1; }
            if (g.status == CabinetStatus.対象外) { problem = "政体が選挙で首班を選ばない（内閣を置かない）"; return -1; }
            if (g.premierPersonId < 0 || (g.status != CabinetStatus.単独過半 && g.status != CabinetStatus.少数政権))
            {
                problem = "組閣が成立していない" + (string.IsNullOrEmpty(g.reason) ? "" : "（" + g.reason + "）");
                return -1;
            }
            string pp = PersonProblem(ElectionCycleRules.FindPerson(roster, g.premierPersonId), f);
            if (pp != null) { problem = "首相（人物#" + g.premierPersonId + "）が職務を続けられない（" + pp + "）"; return -1; }
            return g.premierPersonId;
        }

        // ===== 候補評価 =====

        /// <summary>
        /// 候補の評価＝能力（行政素養＋民望）×重み＋年功（当選回数の逓減・大臣ほど効く）＋経験の目安に合う加点＋与党の加点＋簡易の派閥均衡。
        /// 年功は目安の一要素で、能力の高い若手は抜擢されうる。状態は変えない。
        /// </summary>
        public static CandidateEvaluation Evaluate(PoliticsState pol, Person p, CabinetPostKind kind, int rulingPartyId, CabinetParams prm)
        {
            var e = new CandidateEvaluation { personId = p != null ? p.id : -1, reason = "" };
            if (p == null) return e;
            float ability = AbilityOf(p);
            SeniorityInfo si = PartySeniorityRules.InfoOf(pol, p.id, PartySeniorityParams.Default);
            float seniorityShare = kind == CabinetPostKind.大臣 ? 1f : kind == CabinetPostKind.副大臣 ? ViceSeniorityShare : 0f;
            SeniorityTier target = kind == CabinetPostKind.大臣 ? SeniorityTier.ベテラン
                                 : kind == CabinetPostKind.副大臣 ? SeniorityTier.中堅 : SeniorityTier.新人;
            bool fit = si.historyRegistered && si.tier == target;
            Party party = pol != null ? ElectionCycleRules.PartyOf(pol.parties, p.id) : null;
            bool ruling = party != null && party.id == rulingPartyId;
            PartyFaction pf = FactionOf(party, p.id);
            int factionPosts = pf != null ? PostsHeldByFaction(pol != null ? pol.cabinet : null, pf) : 0;

            e.score = prm.abilityWeight * ability + prm.seniorityWeight * seniorityShare * si.standing
                    + (fit ? prm.tierFitBonus : 0f) + (ruling ? prm.rulingPartyBonus : 0f)
                    + (pf != null ? prm.factionBalanceBonus / (1 + factionPosts) : 0f);
            e.reason = "能力" + ability.ToString("0.00")
                     + "・" + (si.historyRegistered ? "国政当選" + si.nationalWins + "回（" + si.tier + "）" : "当選履歴未登録")
                     + (fit ? "・経験の目安に合う" : "")
                     + "・" + (party == null ? "無所属" : (ruling ? "与党 " : "") + party.partyName)
                     + (pf != null ? "・" + pf.name + "（在任" + factionPosts + "名）" : "")
                     + "・評価" + e.score.ToString("0.000");
            return e;
        }

        /// <summary>能力（0..1）＝行政素養（運営と情報）と民望の混合。</summary>
        public static float AbilityOf(Person p)
        {
            if (p == null) return 0f;
            float civil = Mathf.Clamp01(p.CivilAptitude / MaxStat);
            float renown = Mathf.Clamp01(p.popularRenown / MaxStat);
            return Mathf.Clamp01(civil * (1f - RenownShare) + renown * RenownShare);
        }

        /// <summary>その人物が属する党内派閥（無派閥は null・派閥ID小を優先）。</summary>
        public static PartyFaction FactionOf(Party party, int personId)
        {
            if (party == null || party.factions == null || personId < 0) return null;
            PartyFaction best = null;
            for (int i = 0; i < party.factions.Count; i++)
            {
                PartyFaction pf = party.factions[i];
                if (pf == null || pf.memberIds == null || !pf.memberIds.Contains(personId)) continue;
                if (best == null || pf.id < best.id) best = pf;
            }
            return best;
        }

        private static int PostsHeldByFaction(CabinetState cab, PartyFaction pf)
        {
            if (cab == null || cab.posts == null || pf == null || pf.memberIds == null) return 0;
            int n = 0;
            for (int i = 0; i < cab.posts.Count; i++)
                if (cab.posts[i] != null && cab.posts[i].holderId >= 0 && pf.memberIds.Contains(cab.posts[i].holderId)) n++;
            return n;
        }

        // ===== 任免 =====

        /// <summary>
        /// 内閣の職へ任命する（唯一の入口・AI 組閣も通す）。権限（正式な首相本人）・内閣が現首相で整理済み・存在する省・空席・人物の資格・
        /// 兼任（首相本人／他の閣僚職／知事／党三役／首相でない党首／野党の党員）を順に確かめ、拒否は理由つきで何も変えない。
        /// 首相でない人の任命は権限外＝首相への上申として返す。
        /// </summary>
        public static AppointmentResult TryAppoint(PoliticsState pol, Faction f, int actorId, IList<Ministry> tree, int topId,
            int ministryId, CabinetPostKind kind, int personId, IList<Person> roster, int year, string reason, CabinetParams prm)
        {
            if (pol == null) return AppointmentResult.Deny("政治状態がない");
            int premier = FormalPremier(pol, f, roster, out string premierProblem);
            if (premier < 0) return AppointmentResult.Deny("任免権者（首相）が不在：" + premierProblem);
            if (actorId != premier)
                return AppointmentResult.Petition("権限外：閣僚の任免権者は首相（人物#" + premier + "）＝任命は上申として扱う", premier);
            CabinetState cab = pol.cabinet;
            if (cab == null || cab.premierPersonId != premier)
                return AppointmentResult.Deny("内閣が現首相で整理されていない（先に整理＝Reconcile）");

            Ministry ministry = FindCabinetMinistry(tree, topId, ministryId);
            if (ministry == null) return AppointmentResult.Deny("存在しない省（#" + ministryId + "）には任命できない");
            EnsurePosts(cab, f, tree, topId, year, prm);
            CabinetPost post = FindPost(cab, ministryId, kind);
            if (post == null) return AppointmentResult.Deny("存在しない職");
            if (post.holderId == personId && personId >= 0) return AppointmentResult.Deny("既に " + PostTitle(post) + " に在任している");
            if (post.holderId >= 0)
                return AppointmentResult.Deny(PostTitle(post) + " には在任者（人物#" + post.holderId + "）がいる＝先に解任");

            string problem = CandidateProblem(pol, f, cab, personId, premier, roster, prm);
            if (problem != null) return AppointmentResult.Deny(problem);

            Party party = ElectionCycleRules.PartyOf(pol.parties, personId);
            bool continued = post.lastHolderId == personId && post.vacatedYear == year;
            post.holderId = personId;
            post.partyId = party != null ? party.id : -1;
            post.appointedYear = year;
            post.appointedById = actorId;
            post.appointmentReason = reason ?? "";
            post.vacancyReason = "";
            ClearDelegation(post);
            AddHistory(cab, f, year, continued ? "続投" : "就任", post, personId, actorId, post.appointmentReason, prm);
            return AppointmentResult.Allow(PostTitle(post) + " に人物#" + personId + " を任命（首相の任命）");
        }

        /// <summary>その人物が党首を務める党（無ければ null）。首相本人は閣僚職に就かないため、閣僚の在任整理では「首相でない党首」の判定になる。</summary>
        private static Party LeaderPartyOf(PoliticsState pol, int personId)
        {
            if (pol == null || pol.parties == null || personId < 0) return null;
            for (int i = 0; i < pol.parties.Count; i++)
                if (pol.parties[i] != null && pol.parties[i].leaderId == personId) return pol.parties[i];
            return null;
        }

        /// <summary>任命の資格・兼任の問題（任命できるなら null）。権限と空席は見ない。</summary>
        public static string CandidateProblem(PoliticsState pol, Faction f, CabinetState cab, int personId, int premierId,
            IList<Person> roster, CabinetParams prm)
        {
            Person p = ElectionCycleRules.FindPerson(roster, personId);
            string pp = PersonProblem(p, f);
            if (pp != null) return "任命できない：" + pp;
            if (personId == premierId) return "首相は閣僚職を兼任しない";
            CabinetPost held = PostHeldBy(cab, personId);
            if (held != null) return "兼任不可：既に " + PostTitle(held) + " に在任";
            int sys = LocalElectionRules.GovernedSystemOf(pol, personId, -1);
            if (sys >= 0) return "星系#" + sys + " の知事と兼任できない";
            if (pol != null && pol.parties != null)
                for (int i = 0; i < pol.parties.Count; i++)
                {
                    Party party = pol.parties[i];
                    if (party == null) continue;
                    if (party.leaderId == personId) return "首相でない党首（" + party.partyName + "）は入閣しない（連立は未実装）";
                    if (!prm.allowPartyExecutiveConcurrent)
                        for (int k = 0; k < PartyExecutiveRules.ExecutivePosts.Length; k++)
                        {
                            PartyPost ep = PartyExecutiveRules.ExecutivePosts[k];
                            if (PartyOrganizationRules.HolderOf(party, ep) == personId)
                                return "党三役（" + party.partyName + " " + ep + "）と閣僚は兼任しない";
                        }
                }
            Party own = pol != null ? ElectionCycleRules.PartyOf(pol.parties, personId) : null;
            int rulingId = pol != null && pol.government != null ? pol.government.partyId : -1;
            if (own != null && rulingId >= 0 && own.id != rulingId)
                return "与党でない党（" + own.partyName + "）の党員（連立は未実装）";
            return null;
        }

        /// <summary>内閣の職を解任する（首相本人のみ）。大臣の解任で副大臣への委任も失効する。</summary>
        public static AppointmentResult Dismiss(PoliticsState pol, Faction f, int actorId, int ministryId, CabinetPostKind kind,
            IList<Person> roster, int year, string reason, CabinetParams prm)
        {
            if (pol == null) return AppointmentResult.Deny("政治状態がない");
            int premier = FormalPremier(pol, f, roster, out string premierProblem);
            if (premier < 0) return AppointmentResult.Deny("任免権者（首相）が不在：" + premierProblem);
            if (actorId != premier)
                return AppointmentResult.Petition("権限外：閣僚の任免権者は首相（人物#" + premier + "）＝解任は上申として扱う", premier);
            CabinetState cab = pol.cabinet;
            CabinetPost post = FindPost(cab, ministryId, kind);
            if (post == null) return AppointmentResult.Deny("存在しない職（省#" + ministryId + " " + kind + "）");
            if (post.holderId < 0) return AppointmentResult.Deny(PostTitle(post) + " は既に空席");
            int who = post.holderId;
            string why = string.IsNullOrEmpty(reason) ? "首相の解任" : reason;
            Vacate(cab, f, post, year, "解任", why, actorId, prm);
            return AppointmentResult.Allow(PostTitle(post) + " の人物#" + who + " を解任");
        }

        // ===== 委任 =====

        /// <summary>
        /// 大臣が同じ省の副大臣へ委任する。委任者は現職の大臣本人（職務執行内閣では不可）、範囲は 所管政策/所管決裁 だけ、
        /// 期限はこの年から <see cref="CabinetParams.maxDelegationYears"/> 年以内（無期限の代行にしない）。
        /// </summary>
        public static AppointmentResult Delegate(PoliticsState pol, Faction f, int actorId, int ministryId, CabinetDelegation scope,
            int untilYear, IList<Person> roster, int year, CabinetParams prm)
        {
            CabinetState cab = pol != null ? pol.cabinet : null;
            CabinetPost minister = FindPost(cab, ministryId, CabinetPostKind.大臣);
            CabinetPost vice = FindPost(cab, ministryId, CabinetPostKind.副大臣);
            if (minister == null || vice == null) return AppointmentResult.Deny("存在しない省（#" + ministryId + "）");
            if (minister.holderId < 0) return AppointmentResult.Deny(PostTitle(minister) + " が空席＝委任できない");
            if (actorId != minister.holderId)
                return AppointmentResult.Petition("権限外：委任できるのは " + PostTitle(minister) + " 本人", minister.holderId);
            if (cab.caretaker) return AppointmentResult.Deny("職務執行内閣では委任しない");
            string mp = PersonProblem(ElectionCycleRules.FindPerson(roster, actorId), f);
            if (mp != null) return AppointmentResult.Deny("委任者の大臣が職務を続けられない（" + mp + "）");
            if (vice.holderId < 0) return AppointmentResult.Deny(PostTitle(vice) + " が空席＝委任先がない");
            string vp = PersonProblem(ElectionCycleRules.FindPerson(roster, vice.holderId), f);
            if (vp != null) return AppointmentResult.Deny("副大臣が職務を続けられない（" + vp + "）");
            const CabinetDelegation allowed = CabinetDelegation.所管政策 | CabinetDelegation.所管決裁;
            if (scope == CabinetDelegation.なし || (scope & ~allowed) != 0)
                return AppointmentResult.Deny("委任範囲が不正（所管政策・所管決裁のみ）");
            if (untilYear < year) return AppointmentResult.Deny("委任の期限が過去（SE" + untilYear + "）");
            if (untilYear > year + prm.maxDelegationYears)
                return AppointmentResult.Deny("委任の期限が長すぎる（最長 " + prm.maxDelegationYears + " 年＝無期限の代行はしない）");

            vice.delegation = scope;
            vice.delegatedById = actorId;
            vice.delegationEndYear = untilYear;
            AddHistory(cab, f, year, "委任", vice, vice.holderId, actorId, "範囲 " + scope + "・SE" + untilYear + "まで", prm);
            return AppointmentResult.Allow(PostTitle(vice) + " へ " + scope + " を SE" + untilYear + " まで委任");
        }

        /// <summary>大臣本人が委任を解く。</summary>
        public static AppointmentResult RevokeDelegation(PoliticsState pol, Faction f, int actorId, int ministryId, int year, string reason, CabinetParams prm)
        {
            CabinetState cab = pol != null ? pol.cabinet : null;
            CabinetPost minister = FindPost(cab, ministryId, CabinetPostKind.大臣);
            CabinetPost vice = FindPost(cab, ministryId, CabinetPostKind.副大臣);
            if (minister == null || vice == null) return AppointmentResult.Deny("存在しない省（#" + ministryId + "）");
            if (actorId != minister.holderId || actorId < 0)
                return AppointmentResult.Petition("権限外：委任を解けるのは大臣本人", minister.holderId);
            if (vice.delegation == CabinetDelegation.なし) return AppointmentResult.Deny("委任していない");
            AddHistory(cab, f, year, "委任解除", vice, vice.holderId, actorId, string.IsNullOrEmpty(reason) ? "大臣が委任を解いた" : reason, prm);
            ClearDelegation(vice);
            return AppointmentResult.Allow("委任を解いた");
        }

        // ===== 権限 =====

        /// <summary>
        /// その人物がその省についてその行為をできるか（状態は変えない）。首相＝任免と提案・調整、大臣＝所管の政策決定・決裁・提案・調整
        /// （職務執行内閣では所管の決裁と調整だけ）、副大臣＝提案・調整＋有効な委任の範囲、政務官＝提案・調整だけ。
        /// 艦隊作戦指揮と国庫の直接支出はどの職でも許さない。死亡・拘束・他勢力などの在任者は何もできない。
        /// </summary>
        public static AppointmentResult Authority(PoliticsState pol, Faction f, int personId, int ministryId, CabinetAction action,
            IList<Person> roster, int year)
        {
            if (action == CabinetAction.艦隊作戦指揮)
                return AppointmentResult.Deny("閣僚・政務の職は艦隊・軍団の作戦指揮権を含まない（直接の操作は自身の指揮系統だけ）");
            if (action == CabinetAction.国庫支出)
                return AppointmentResult.Deny("閣僚の地位だけで国庫を直接動かさない（予算・稟議の手続きによる）");
            string pp = PersonProblem(ElectionCycleRules.FindPerson(roster, personId), f);
            if (pp != null) return AppointmentResult.Deny("権限なし：" + pp);

            int premier = FormalPremier(pol, f, roster, out _);
            CabinetState cab = pol != null ? pol.cabinet : null;
            if (personId == premier)
            {
                if (action == CabinetAction.閣僚任免) return AppointmentResult.Allow("首相（政府の長）＝閣僚の任免権者");
                if (action == CabinetAction.政策提案 || action == CabinetAction.政策調整) return AppointmentResult.Allow("首相（政府の長）");
                CabinetPost m = FindPost(cab, ministryId, CabinetPostKind.大臣);
                return AppointmentResult.Petition("所管の" + action + "は各省の大臣（首相は任免と調整）", m != null ? m.holderId : -1);
            }
            if (action == CabinetAction.閣僚任免)
                return AppointmentResult.Petition("権限外：閣僚の任免は首相だけ", premier);

            CabinetPost post = PostHeldBy(cab, personId);
            if (post == null || post.ministryId != ministryId)
            {
                CabinetPost m = FindPost(cab, ministryId, CabinetPostKind.大臣);
                return AppointmentResult.Petition("当該省（#" + ministryId + "）の閣僚職に就いていない", m != null ? m.holderId : -1);
            }

            string title = PostTitle(post);
            switch (post.kind)
            {
                case CabinetPostKind.大臣:
                    if (cab.caretaker)
                    {
                        if (action == CabinetAction.所管決裁 || action == CabinetAction.政策調整)
                            return AppointmentResult.Allow(title + "（職務執行・SE" + cab.caretakerUntilYear + "まで所管の決裁と調整のみ）");
                        return AppointmentResult.Deny(title + "は職務執行中＝" + action + "はしない（新しい首相の組閣を待つ）");
                    }
                    return AppointmentResult.Allow(title + "（" + RoleText(post.kind) + "）");

                case CabinetPostKind.副大臣:
                    if (action == CabinetAction.政策提案 || action == CabinetAction.政策調整)
                        return AppointmentResult.Allow(title + "（補佐＝提案・調整）");
                    return ViceDelegatedAuthority(cab, post, action, roster, f, year);

                default:
                    if (action == CabinetAction.政策提案 || action == CabinetAction.政策調整)
                        return AppointmentResult.Allow(title + "（提案・調整）");
                    CabinetPost mm = FindPost(cab, ministryId, CabinetPostKind.大臣);
                    return AppointmentResult.Petition(title + "は最終決裁権を持たない＝大臣へ上申", mm != null ? mm.holderId : -1);
            }
        }

        private static AppointmentResult ViceDelegatedAuthority(CabinetState cab, CabinetPost vice, CabinetAction action,
            IList<Person> roster, Faction f, int year)
        {
            string title = PostTitle(vice);
            CabinetPost minister = FindPost(cab, vice.ministryId, CabinetPostKind.大臣);
            int to = minister != null ? minister.holderId : -1;
            CabinetDelegation need = action == CabinetAction.所管政策決定 ? CabinetDelegation.所管政策 : CabinetDelegation.所管決裁;
            string why = DelegationProblem(cab, vice, minister, roster, f, year);
            if (why == null && (vice.delegation & need) == 0) why = need + " は委任されていない（委任範囲 " + vice.delegation + "）";
            if (why != null) return AppointmentResult.Petition(title + "は" + action + "できない：" + why, to);
            return AppointmentResult.Allow(title + "（大臣 人物#" + vice.delegatedById + " の委任 " + vice.delegation + "・SE" + vice.delegationEndYear + "まで）");
        }

        /// <summary>副大臣の委任が今も有効でない理由（有効なら null）。</summary>
        private static string DelegationProblem(CabinetState cab, CabinetPost vice, CabinetPost minister, IList<Person> roster, Faction f, int year)
        {
            if (vice == null || vice.delegation == CabinetDelegation.なし) return "委任なし";
            if (cab != null && cab.caretaker) return "職務執行内閣では委任が効かない";
            if (minister == null || minister.holderId < 0 || minister.holderId != vice.delegatedById) return "委任した大臣がもう在任していない";
            if (roster != null && PersonProblem(ElectionCycleRules.FindPerson(roster, minister.holderId), f) != null) return "委任した大臣が職務を続けられない";
            if (year > vice.delegationEndYear) return "委任の期限（SE" + vice.delegationEndYear + "）を過ぎた";
            return null;
        }

        // ===== 整理（首相交代・死亡・不在・政体移行・委任の失効） =====

        /// <summary>
        /// 内閣を現況へ合わせる（通知・任命はしない＝空席化と委任の失効だけ）：政体が対象外なら内閣を置かない（全員退任）、
        /// 首相が替わった／総選挙後の首班指名なら前内閣は総辞職、首相が不在になれば職務執行内閣（期限つき・委任は失効）→期限切れで総辞職、
        /// 在任者の死亡・拘束・他勢力・政治家でない・知事就任・党首就任（首相でない党首）・野党への所属は失職（首相本人が党首でなくなっても首相は失職させない）、委任した大臣の交代・期限切れで委任失効。
        /// 返り値はこの呼出しで加えた履歴。同じ状態で繰り返しても何も起きない。
        /// </summary>
        public static List<AppointmentHistoryEntry> Reconcile(PoliticsState pol, Faction f, IList<Ministry> tree, int topId,
            IList<Person> roster, int year, CabinetParams prm)
        {
            var added = new List<AppointmentHistoryEntry>();
            if (pol == null) return added;
            if (pol.cabinet == null) pol.cabinet = new CabinetState();
            CabinetState cab = pol.cabinet;
            if (cab.posts == null) cab.posts = new List<CabinetPost>();
            if (cab.history == null) cab.history = new List<AppointmentHistoryEntry>();
            int before = cab.history.Count;
            int dropped = cab.historyDropped;
            if (tree != null) EnsurePosts(cab, f, tree, topId, year, prm);

            GovernmentFormation g = pol.government;
            int premier = FormalPremier(pol, f, roster, out string problem);
            if (g != null && g.status == CabinetStatus.対象外)
            {
                if (AnyHolder(cab)) ResignAll(cab, f, year, "総辞職", "政体が選挙で首班を選ばない＝内閣を置かない", prm);
                cab.premierPersonId = -1;
                EndCaretaker(cab);
            }
            else if (premier >= 0)
            {
                string source = g.sourceElectionId ?? "";
                if (cab.premierPersonId != premier)
                {
                    if (AnyHolder(cab))
                        ResignAll(cab, f, year, "総辞職", "首相交代（前 人物#" + (cab.caretaker ? cab.caretakerOfPremierId : cab.premierPersonId)
                                                     + " → 人物#" + premier + "）＝前内閣は総辞職", prm);
                    Bind(cab, g, premier, year);
                }
                else if (!string.IsNullOrEmpty(cab.sourceElectionId) && source.Length > 0 && cab.sourceElectionId != source)
                {
                    if (AnyHolder(cab)) ResignAll(cab, f, year, "総辞職", "下院の改選後の首班指名（根拠 " + source + "）＝内閣総辞職して組み直す", prm);
                    Bind(cab, g, premier, year);
                }
                else
                {
                    if (string.IsNullOrEmpty(cab.sourceElectionId)) cab.sourceElectionId = source;
                    cab.partyId = g.partyId;
                    EndCaretaker(cab);
                }
            }
            else
            {
                if (cab.premierPersonId >= 0 && !cab.caretaker)
                {
                    cab.caretakerOfPremierId = cab.premierPersonId;
                    cab.premierPersonId = -1;
                    if (AnyHolder(cab))
                    {
                        cab.caretaker = true;
                        cab.caretakerUntilYear = year + prm.caretakerYears;
                        cab.caretakerReason = problem + "＝職務執行内閣：大臣は所管の決裁と調整のみ（任免・政策決定・委任なし）SE" + cab.caretakerUntilYear + "まで";
                        for (int i = 0; i < cab.posts.Count; i++)
                        {
                            CabinetPost p = cab.posts[i];
                            if (p != null && p.delegation != CabinetDelegation.なし)
                            {
                                AddHistory(cab, f, year, "委任失効", p, p.holderId, -1, "職務執行内閣になった", prm);
                                ClearDelegation(p);
                            }
                        }
                        AddHistory(cab, f, year, "職務執行", null, -1, -1, cab.caretakerReason, prm);
                    }
                }
                else if (cab.caretaker && (year > cab.caretakerUntilYear || !AnyHolder(cab)))
                {
                    if (AnyHolder(cab)) ResignAll(cab, f, year, "総辞職", "職務執行の期限（SE" + cab.caretakerUntilYear + "）を過ぎた", prm);
                    EndCaretaker(cab);
                }
                for (int i = 0; i < cab.posts.Count; i++)
                    if (cab.posts[i] != null && cab.posts[i].holderId < 0)
                        cab.posts[i].vacancyReason = "任免権者（首相）不在：" + problem;
            }

            // 在任者ごとの失職
            int rulingId = g != null ? g.partyId : -1;
            for (int i = 0; i < cab.posts.Count; i++)
            {
                CabinetPost p = cab.posts[i];
                if (p == null || p.holderId < 0) continue;
                string pp = PersonProblem(ElectionCycleRules.FindPerson(roster, p.holderId), f);
                string why = null;
                if (pp != null) why = pp + "のため失職";
                else if (LocalElectionRules.GovernedSystemOf(pol, p.holderId, -1) >= 0) why = "知事に就いたため失職（兼任不可）";
                else if (LeaderPartyOf(pol, p.holderId) is Party led)
                    why = "党首（" + led.partyName + "）に就いたため失職（首相でない党首は入閣しない）";
                else
                {
                    Party own = ElectionCycleRules.PartyOf(pol.parties, p.holderId);
                    if (own != null && rulingId >= 0 && own.id != rulingId && !cab.caretaker)
                        why = "与党でない党（" + own.partyName + "）へ所属したため失職（連立は未実装）";
                    else p.partyId = own != null ? own.id : -1;
                }
                if (why != null) Vacate(cab, f, p, year, "失職", why, -1, prm);
            }

            // 委任の失効
            for (int i = 0; i < cab.posts.Count; i++)
            {
                CabinetPost v = cab.posts[i];
                if (v == null || v.kind != CabinetPostKind.副大臣 || v.delegation == CabinetDelegation.なし) continue;
                string why = v.holderId < 0 ? "副大臣が空席" : DelegationProblem(cab, v, FindPost(cab, v.ministryId, CabinetPostKind.大臣), roster, f, year);
                if (why == null) continue;
                AddHistory(cab, f, year, "委任失効", v, v.holderId, -1, why, prm);
                ClearDelegation(v);
            }

            CollectAdded(cab.history, before, cab.historyDropped - dropped, added);
            return added;
        }

        /// <summary>
        /// AI の組閣・補充：正式な首相を任命者として、空いた職（大臣→副大臣→政務官・省ID順）へ候補評価の最上位を <see cref="TryAppoint"/> で就ける。
        /// 候補がいなければ空席のまま理由を残す（人物を作らない・履歴は増やさない）。先に <see cref="Reconcile"/> を呼ぶこと。
        /// </summary>
        public static List<AppointmentHistoryEntry> AutoFill(PoliticsState pol, Faction f, IList<Ministry> tree, int topId,
            IList<Person> roster, int year, CabinetParams prm)
        {
            var added = new List<AppointmentHistoryEntry>();
            if (pol == null || pol.cabinet == null) return added;
            CabinetState cab = pol.cabinet;
            int premier = FormalPremier(pol, f, roster, out _);
            if (premier < 0 || cab.premierPersonId != premier) return added;
            if (tree == null) return added; // 省庁が無い＝大臣を置く先がない
            int before = cab.history.Count;
            int dropped = cab.historyDropped;
            EnsurePosts(cab, f, tree, topId, year, prm);
            int rulingId = pol.government != null ? pol.government.partyId : -1;
            List<Person> people = ElectionCycleRules.SortedById(roster, null);

            CabinetPostKind[] order = { CabinetPostKind.大臣, CabinetPostKind.副大臣, CabinetPostKind.政務官 };
            List<Ministry> ministries = CabinetMinistries(tree, topId);
            for (int k = 0; k < order.Length; k++)
                for (int m = 0; m < ministries.Count; m++)
                {
                    CabinetPost post = FindPost(cab, ministries[m].id, order[k]);
                    if (post == null || post.holderId >= 0) continue;
                    CandidateEvaluation best = default;
                    bool found = false;
                    for (int i = 0; i < people.Count; i++)
                    {
                        Person p = people[i];
                        if (CandidateProblem(pol, f, cab, p.id, premier, roster, prm) != null) continue;
                        CandidateEvaluation e = Evaluate(pol, p, order[k], rulingId, prm);
                        if (!found || e.score > best.score) { best = e; found = true; } // 同点は ID 小（昇順で先に来た方）
                    }
                    if (!found)
                    {
                        post.vacancyReason = "適格な候補なし（生存・同勢力の文民政治家で、首相・他の閣僚・知事・党三役・党首・野党の党員でない人物が足りない）";
                        continue;
                    }
                    AppointmentResult r = TryAppoint(pol, f, premier, tree, topId, post.ministryId, post.kind, best.personId, roster, year,
                        "首相の組閣（選定理由：" + best.reason + "）", prm);
                    if (!r.ok) post.vacancyReason = r.reason;
                }
            CollectAdded(cab.history, before, cab.historyDropped - dropped, added);
            return added;
        }

        /// <summary>セーブ読込の穴埋め（読込だけでは任命も総辞職もしない）。</summary>
        public static void NormalizeLoaded(PoliticsState pol)
        {
            if (pol == null || pol.cabinet == null) return;
            CabinetState cab = pol.cabinet;
            if (cab.posts == null) cab.posts = new List<CabinetPost>();
            if (cab.history == null) cab.history = new List<AppointmentHistoryEntry>();
            if (cab.sourceElectionId == null) cab.sourceElectionId = "";
            if (cab.caretakerReason == null) cab.caretakerReason = "";
            var seen = new HashSet<long>();
            for (int i = cab.posts.Count - 1; i >= 0; i--)
            {
                CabinetPost p = cab.posts[i];
                long key = p != null ? ((long)p.ministryId << 8) | (long)(int)p.kind : 0;
                if (p == null || p.ministryId < 0 || !seen.Add(key)) { cab.posts.RemoveAt(i); continue; }
                if (p.ministryName == null) p.ministryName = "";
                if (p.appointmentReason == null) p.appointmentReason = "";
                if (p.vacancyReason == null) p.vacancyReason = "";
            }
            cab.posts.Sort(ComparePosts);
            // 同じ人物が複数の職に載っていれば、先頭（省ID小・大臣優先）だけを残す
            var holders = new HashSet<int>();
            for (int i = 0; i < cab.posts.Count; i++)
            {
                CabinetPost p = cab.posts[i];
                if (p.holderId < 0) continue;
                if (!holders.Add(p.holderId))
                {
                    p.holderId = -1;
                    p.vacancyReason = "保存データの兼任を整理（空席）";
                    ClearDelegation(p);
                }
            }
            NormalizeHistory(cab.history);
        }

        internal static void NormalizeHistory(List<AppointmentHistoryEntry> history)
        {
            if (history == null) return;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                AppointmentHistoryEntry e = history[i];
                if (e == null) { history.RemoveAt(i); continue; }
                if (e.eventId == null) e.eventId = "";
                if (e.action == null) e.action = "";
                if (e.postLabel == null) e.postLabel = "";
                if (e.reason == null) e.reason = "";
            }
        }

        // ===== 内部 =====

        private static Ministry FindCabinetMinistry(IList<Ministry> tree, int topId, int ministryId)
        {
            List<Ministry> list = CabinetMinistries(tree, topId);
            for (int i = 0; i < list.Count; i++) if (list[i].id == ministryId) return list[i];
            return null;
        }

        /// <summary>大臣を置く省ごとに3職を用意し、名前・所掌を写す。廃止された省の職は在任者を失職させて除く。</summary>
        private static void EnsurePosts(CabinetState cab, Faction f, IList<Ministry> tree, int topId, int year, CabinetParams prm)
        {
            if (cab == null || tree == null) return;
            List<Ministry> ministries = CabinetMinistries(tree, topId);
            var ids = new HashSet<int>();
            for (int m = 0; m < ministries.Count; m++)
            {
                Ministry mn = ministries[m];
                ids.Add(mn.id);
                for (int k = 0; k <= (int)CabinetPostKind.政務官; k++)
                {
                    CabinetPost p = FindPost(cab, mn.id, (CabinetPostKind)k);
                    if (p == null)
                    {
                        p = new CabinetPost { ministryId = mn.id, kind = (CabinetPostKind)k, vacancyReason = "未任命" };
                        cab.posts.Add(p);
                    }
                    p.ministryName = mn.ministryName ?? "";
                    p.domain = mn.domain;
                }
            }
            for (int i = cab.posts.Count - 1; i >= 0; i--)
            {
                CabinetPost p = cab.posts[i];
                if (p == null) { cab.posts.RemoveAt(i); continue; }
                if (ids.Contains(p.ministryId)) continue;
                if (p.holderId >= 0) AddHistory(cab, f, year, "失職", p, p.holderId, -1, "所管の省が廃止された", prm);
                cab.posts.RemoveAt(i);
            }
            cab.posts.Sort(ComparePosts);
        }

        private static int ComparePosts(CabinetPost a, CabinetPost b)
        {
            if (a.ministryId != b.ministryId) return a.ministryId.CompareTo(b.ministryId);
            return ((int)a.kind).CompareTo((int)b.kind);
        }

        private static bool AnyHolder(CabinetState cab)
        {
            if (cab == null || cab.posts == null) return false;
            for (int i = 0; i < cab.posts.Count; i++)
                if (cab.posts[i] != null && cab.posts[i].holderId >= 0) return true;
            return false;
        }

        private static void Bind(CabinetState cab, GovernmentFormation g, int premier, int year)
        {
            cab.premierPersonId = premier;
            cab.partyId = g != null ? g.partyId : -1;
            cab.formedYear = year;
            cab.sourceElectionId = g != null && g.sourceElectionId != null ? g.sourceElectionId : "";
            EndCaretaker(cab);
        }

        private static void EndCaretaker(CabinetState cab)
        {
            cab.caretaker = false;
            cab.caretakerOfPremierId = -1;
            cab.caretakerUntilYear = 0;
            cab.caretakerReason = "";
        }

        private static void ResignAll(CabinetState cab, Faction f, int year, string action, string reason, CabinetParams prm)
        {
            AddHistory(cab, f, year, action, null, -1, -1, reason, prm);
            for (int i = 0; i < cab.posts.Count; i++)
                if (cab.posts[i] != null && cab.posts[i].holderId >= 0)
                    Vacate(cab, f, cab.posts[i], year, "退任", reason, -1, prm);
        }

        private static void Vacate(CabinetState cab, Faction f, CabinetPost post, int year, string action, string reason, int actorId, CabinetParams prm)
        {
            int who = post.holderId;
            if (post.delegation != CabinetDelegation.なし) ClearDelegation(post);
            AddHistory(cab, f, year, action, post, who, actorId, reason, prm);
            post.lastHolderId = who;
            post.vacatedYear = year;
            post.holderId = -1;
            post.partyId = -1;
            post.appointedYear = 0;
            post.appointedById = -1;
            post.appointmentReason = "";
            post.vacancyReason = reason ?? "";
            if (post.kind == CabinetPostKind.大臣)
            {
                CabinetPost vice = FindPost(cab, post.ministryId, CabinetPostKind.副大臣);
                if (vice != null && vice.delegation != CabinetDelegation.なし)
                {
                    AddHistory(cab, f, year, "委任失効", vice, vice.holderId, -1, "委任した大臣が退いた", prm);
                    ClearDelegation(vice);
                }
            }
        }

        private static void ClearDelegation(CabinetPost p)
        {
            p.delegation = CabinetDelegation.なし;
            p.delegatedById = -1;
            p.delegationEndYear = 0;
        }

        private static void AddHistory(CabinetState cab, Faction f, int year, string action, CabinetPost post, int personId, int actorId,
            string reason, CabinetParams prm)
        {
            string label = post != null ? PostTitle(post) : "内閣";
            int ministryId = post != null ? post.ministryId : -1;
            var e = new AppointmentHistoryEntry
            {
                eventId = (int)f + ":内閣:" + year + ":" + ministryId + ":" + (post != null ? post.kind.ToString() : "全体")
                          + ":" + personId + ":" + action,
                year = year, action = action, postLabel = label, ministryId = ministryId,
                personId = personId, actorId = actorId, reason = reason ?? "",
            };
            AppendCapped(cab.history, e, prm.maxHistory, ref cab.historyDropped);
        }

        /// <summary>上限つきで履歴を足す（あふれた古い件数は <paramref name="dropped"/> に数える）。</summary>
        internal static void AppendCapped(List<AppointmentHistoryEntry> history, AppointmentHistoryEntry e, int max, ref int dropped)
        {
            history.Add(e);
            while (history.Count > max)
            {
                history.RemoveAt(0);
                dropped++;
            }
        }

        /// <summary>この呼出しで足した履歴（上限で古い件が捨てられても、足した分は末尾にある）。</summary>
        internal static void CollectAdded(List<AppointmentHistoryEntry> history, int countBefore, int droppedNow, List<AppointmentHistoryEntry> into)
        {
            int addedCount = history.Count - countBefore + droppedNow;
            int start = Mathf.Max(0, history.Count - addedCount);
            for (int i = start; i < history.Count; i++) into.Add(history[i]);
        }
    }
}
