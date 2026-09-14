using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 閣僚の決裁権限を、決裁・稟議の共通の権限判定（<see cref="DecisionAuthorityRules"/>）へ接続する純ロジック（#2768 #141 #67）。
    ///
    /// <b>合成の順序</b>（新しい権限体系を作らず、既存の2窓口を束ねるだけ）
    /// <list type="number">
    ///   <item>役職（<see cref="GovernmentRegistry"/>）による既存判定＝裁可できるならそのまま（元首・首相・既存の役職者の権限を狭めない）。</item>
    ///   <item>裁可できなければ、案件の所管省を解決し（明示の省ID→効果キーの所掌で一意に決まる省）、
    ///   閣僚職の権限（<see cref="CabinetAppointmentRules.Authority"/> の <see cref="CabinetAction.所管決裁"/>）で裁可できるかを見る。
    ///   所管大臣＝可、副大臣＝有効な 所管決裁 の委任があるときだけ可、政務官・党三役・他省の大臣＝不可。</item>
    ///   <item>それでも不可なら、<b>今も有効な所管大臣</b>へ上申する。大臣が居ない・所管省が決まらないなら既存の上申先（理由を添える）。</item>
    /// </list>
    ///
    /// <b>決裁の時点で再判定</b>：首相交代・首相不在・職務執行の期限・失職（死亡/拘束/他勢力/知事/党首/野党）・委任の撤回と期限切れを、
    /// 内閣の整理（<see cref="CabinetAppointmentRules.Reconcile"/>）を待たずにここで見る＝古い在任記録で承認しない。
    /// 閣僚職は艦隊・軍団の作戦指揮権を含まない（<see cref="IsOperationalCommand"/> の案件には閣僚の権限を足さない）。
    /// 国庫の支出は決裁後の既存の執行（稟議・予算）が担う＝ここは可否だけで状態を変えない。決定論・test-first。
    /// </summary>
    public static class CabinetDecisionAuthorityRules
    {
        /// <summary>艦隊・軍団を直接動かす作戦指揮の効果キー（閣僚の決裁権限の対象外）。</summary>
        public static readonly string[] OperationalCommandKeys = { "mil.offensive" };

        /// <summary>その効果キーが艦隊・軍団の作戦指揮か（軍事政策の決裁とは区別する）。</summary>
        public static bool IsOperationalCommand(string effectKey)
        {
            if (string.IsNullOrEmpty(effectKey)) return false;
            for (int i = 0; i < OperationalCommandKeys.Length; i++)
                if (effectKey == OperationalCommandKeys[i]) return true;
            return false;
        }

        /// <summary>案件を閣僚の職で問う行為へ写す（作戦指揮はそのまま作戦指揮＝どの職も持たない）。</summary>
        public static CabinetAction ActionOf(string effectKey)
            => IsOperationalCommand(effectKey) ? CabinetAction.艦隊作戦指揮 : CabinetAction.所管決裁;

        // ===== 所管省の解決 =====

        /// <summary>
        /// 案件の所管省（大臣を置く省の id）。<paramref name="explicitMinistryId"/>≥0 ならその省（大臣を置く省で所掌が一致するときだけ）、
        /// 無ければ効果キーの所掌（<see cref="DecisionAuthorityRules.DomainOf"/>）と <see cref="Ministry.domain"/> が一致する省が
        /// <b>ちょうど1つ</b>のときだけ。複数・皆無は -1 と理由（推測で複数の省へ権限を広げない）。
        /// </summary>
        public static int ResolveMinistry(IList<Ministry> tree, int topId, string effectKey, int explicitMinistryId, out string problem)
        {
            problem = null;
            OfficeDomain domain = DecisionAuthorityRules.DomainOf(effectKey);
            List<Ministry> cabinet = CabinetAppointmentRules.CabinetMinistries(tree, topId);
            if (cabinet.Count == 0) { problem = "大臣を置く省が無い"; return -1; }

            if (explicitMinistryId >= 0)
            {
                for (int i = 0; i < cabinet.Count; i++)
                {
                    if (cabinet[i].id != explicitMinistryId) continue;
                    if (cabinet[i].domain != domain)
                    {
                        problem = "指定の省（" + cabinet[i].ministryName + "・" + cabinet[i].domain + "）は案件の所掌（" + domain + "）を所管しない";
                        return -1;
                    }
                    return explicitMinistryId;
                }
                problem = "指定の省（#" + explicitMinistryId + "）は大臣を置く省でない";
                return -1;
            }

            int found = -1, count = 0;
            string names = "";
            for (int i = 0; i < cabinet.Count; i++)
            {
                if (cabinet[i].domain != domain) continue;
                if (count == 0) found = cabinet[i].id;
                names += (count > 0 ? "・" : "") + cabinet[i].ministryName;
                count++;
            }
            if (count == 1) return found;
            problem = count == 0
                ? "所掌「" + domain + "」を所管する省が内閣に無い"
                : "所掌「" + domain + "」の省が複数（" + names + "）あり、案件に省の指定が無いため所管大臣を特定できない（推測で権限を広げない）";
            return -1;
        }

        // ===== 閣僚職による決裁 =====

        /// <summary>
        /// その人物が閣僚職の権限でその案件を決裁できるか（状態は変えない）。可なら ok と根拠、不可なら理由と
        /// <b>今も有効な</b>所管大臣（<see cref="AppointmentResult.petitionToId"/>・居なければ -1）。
        /// </summary>
        public static AppointmentResult CabinetAuthority(CabinetDecisionContext ctx, int personId, string effectKey, int explicitMinistryId = -1)
        {
            if (ctx == null || !ctx.HasCabinet) return AppointmentResult.Deny("内閣が置かれていない");
            if (IsOperationalCommand(effectKey))
                return CabinetAppointmentRules.Authority(ctx.politics, ctx.faction, personId, -1, CabinetAction.艦隊作戦指揮, ctx.roster, ctx.year);

            int ministryId = ResolveMinistry(ctx.ministries, ctx.topMinistryId, effectKey, explicitMinistryId, out string mp);
            if (ministryId < 0) return AppointmentResult.Deny("閣僚の所管を特定できない：" + mp);

            int minister = ValidMinisterOf(ctx, ministryId);
            string cabinetProblem = CabinetProblem(ctx);
            if (cabinetProblem != null)
                return AppointmentResult.Petition("内閣の権限が効かない：" + cabinetProblem, minister);

            AppointmentResult r = CabinetAppointmentRules.Authority(ctx.politics, ctx.faction, personId, ministryId,
                CabinetAction.所管決裁, ctx.roster, ctx.year);
            if (!r.ok) return AppointmentResult.Petition(r.reason, minister == personId ? -1 : minister);

            CabinetPost post = CabinetAppointmentRules.PostHeldBy(ctx.politics.cabinet, personId);
            string lapse = HolderLapseProblem(ctx, personId);
            if (lapse != null) return AppointmentResult.Petition(CabinetAppointmentRules.PostTitle(post) + "は" + lapse, minister == personId ? -1 : minister);
            if (post != null && post.kind == CabinetPostKind.副大臣)
            {
                string ml = post.delegatedById >= 0 ? HolderLapseProblem(ctx, post.delegatedById) : "委任した大臣が不明";
                if (ml != null) return AppointmentResult.Petition(CabinetAppointmentRules.PostTitle(post) + "の委任が効かない：委任した大臣が" + ml, minister);
            }
            return AppointmentResult.Allow(r.reason + "・所管 " + (post != null ? post.ministryName : "#" + ministryId));
        }

        /// <summary>その省の大臣で、今この時点で所管の決裁ができる人物（居なければ -1）。</summary>
        public static int ValidMinisterOf(CabinetDecisionContext ctx, int ministryId)
        {
            if (ctx == null || !ctx.HasCabinet || CabinetProblem(ctx) != null) return -1;
            CabinetPost m = CabinetAppointmentRules.FindPost(ctx.politics.cabinet, ministryId, CabinetPostKind.大臣);
            if (m == null || m.holderId < 0) return -1;
            if (!CabinetAppointmentRules.Authority(ctx.politics, ctx.faction, m.holderId, ministryId, CabinetAction.所管決裁, ctx.roster, ctx.year).ok)
                return -1;
            return HolderLapseProblem(ctx, m.holderId) == null ? m.holderId : -1;
        }

        /// <summary>
        /// 内閣そのものが今も権限を持つかの問題（持つなら null）：政体が内閣を置かない・首相不在（職務執行でない）・首相交代や改選後の首班指名で
        /// 前内閣が整理待ち・職務執行の期限切れ／新首相の組閣待ち。
        /// </summary>
        public static string CabinetProblem(CabinetDecisionContext ctx)
        {
            if (ctx == null || !ctx.HasCabinet) return "内閣が置かれていない";
            PoliticsState pol = ctx.politics;
            CabinetState cab = pol.cabinet;
            GovernmentFormation g = pol.government;
            if (g != null && g.status == CabinetStatus.対象外) return "政体が選挙で首班を選ばない（内閣を置かない）";
            int premier = CabinetAppointmentRules.FormalPremier(pol, ctx.faction, ctx.roster, out string premierProblem);
            if (cab.caretaker)
            {
                if (premier >= 0) return "新しい首相（人物#" + premier + "）の組閣待ち＝職務執行内閣は退く";
                if (ctx.year > cab.caretakerUntilYear) return "職務執行の期限（SE" + cab.caretakerUntilYear + "）を過ぎた";
                return null;
            }
            if (premier < 0) return "首相が不在（" + premierProblem + "）";
            if (cab.premierPersonId != premier)
                return "首相交代（前 人物#" + cab.premierPersonId + " → 人物#" + premier + "）で前内閣は総辞職";
            string source = g != null && g.sourceElectionId != null ? g.sourceElectionId : "";
            if (!string.IsNullOrEmpty(cab.sourceElectionId) && source.Length > 0 && cab.sourceElectionId != source)
                return "改選後の首班指名（根拠 " + source + "）で内閣を組み直す";
            return null;
        }

        /// <summary>
        /// 在任者が決裁時点で失職しているかの理由（在任できるなら null）：死亡・拘束・他勢力・政治家でない・軍人・知事就任・党首就任（首相でない党首）・野党への所属。
        /// <see cref="CabinetAppointmentRules.Reconcile"/> の失職条件を読み取りだけで先取りする。
        /// </summary>
        public static string HolderLapseProblem(CabinetDecisionContext ctx, int personId)
        {
            if (ctx == null || ctx.politics == null) return "政治状態がない";
            string pp = CabinetAppointmentRules.PersonProblem(ElectionCycleRules.FindPerson(ctx.roster, personId), ctx.faction);
            if (pp != null) return pp + "のため職務を続けられない";
            PoliticsState pol = ctx.politics;
            if (LocalElectionRules.GovernedSystemOf(pol, personId, -1) >= 0) return "知事に就いたため失職";
            if (pol.parties != null)
                for (int i = 0; i < pol.parties.Count; i++)
                    if (pol.parties[i] != null && pol.parties[i].leaderId == personId)
                        return "党首（" + pol.parties[i].partyName + "）に就いたため失職";
            Party own = ElectionCycleRules.PartyOf(pol.parties, personId);
            int rulingId = pol.government != null ? pol.government.partyId : -1;
            bool caretaker = pol.cabinet != null && pol.cabinet.caretaker;
            if (own != null && rulingId >= 0 && own.id != rulingId && !caretaker)
                return "与党でない党（" + own.partyName + "）へ所属したため失職";
            return null;
        }

        // ===== 共通の権限判定との合成 =====

        /// <summary>
        /// 決裁・稟議の共通入口で使う権限判定＝既存の役職判定（<see cref="DecisionAuthorityRules.Evaluate"/>）に閣僚職の権限と上申先を足す。
        /// <paramref name="cabinet"/> が null なら既存判定そのもの（後方互換）。<paramref name="findPerson"/>＝人物IDから上申先の表示名を引く（null 可）。
        /// </summary>
        public static DecisionAuthorityResult Evaluate(ICharacter actor, string effectKey, IEnumerable<Office> offices,
            CivilianControlType control, System.Func<OfficeDomain, ICharacter> findAddressee,
            CabinetDecisionContext cabinet, System.Func<int, ICharacter> findPerson, int explicitMinistryId = -1)
        {
            DecisionAuthorityResult office = DecisionAuthorityRules.Evaluate(actor, effectKey, OfficeScope.国家, offices, control, findAddressee);
            if (actor == null || office.CanDecide || cabinet == null || !cabinet.HasCabinet) return office;

            CabinetPost held = CabinetAppointmentRules.PostHeldBy(cabinet.politics.cabinet, actor.Id);
            if (IsOperationalCommand(effectKey))
                return held == null ? office
                    : WithNote(office, CabinetAppointmentRules.PostTitle(held) + "の職は艦隊・軍団の作戦指揮権を含まない");

            AppointmentResult r = CabinetAuthority(cabinet, actor.Id, effectKey, explicitMinistryId);
            if (r.ok) return new DecisionAuthorityResult(DecisionAuthority.裁可, r.reason);

            int to = r.petitionToId;
            ICharacter minister = to >= 0 && to != actor.Id && findPerson != null ? findPerson(to) : null;
            if (minister != null)
                return new DecisionAuthorityResult(DecisionAuthority.上申,
                    (held != null ? r.reason : office.basis) + "（上申先＝所管の大臣）", minister.Id, minister.CharacterName);

            // 所管大臣が居ない／省が決まらない＝既存の上申先（無ければ権限外）。閣僚職にある人には理由を明示する。
            return held != null ? WithNote(office, r.reason) : office;
        }

        private static DecisionAuthorityResult WithNote(DecisionAuthorityResult r, string note)
            => new DecisionAuthorityResult(r.authority, r.basis + "（閣僚：" + note + "）", r.addresseeId, r.addresseeName);
    }
}
