using System.Collections.Generic;
using System.Text;

namespace Ginei
{
    public partial class GalaxyView
    {
        // ===== 内閣の政治任用（大臣・副大臣・政務官）と党三役の配線（#2768 #141 #159 #145） =====
        // 在任・委任・履歴は PoliticsState.cabinet / Party.posts（戦役セーブに乗る）が単一の出所。GovernmentRegistry へは登録しない
        // （軍事所掌の役職として登録すると全軍の指揮権に数えられるため＝閣僚職は艦隊・軍団の作戦指揮権を含まない）。
        // 省庁の職業官僚の配属（Ministry.staffIds）には触れない。

        private static readonly CabinetParams CabinetPrm = CabinetParams.Default;

        /// <summary>
        /// 内閣と党三役を現況へ合わせ（首相交代の総辞職・首相不在の職務執行・死亡/不在/離党の失職・党首交代の改任・委任の失効）、
        /// <paramref name="autoFill"/> なら首相・党首を任命者として空席を補充する（空席だけを埋める＝手動で任命した在任者は差し替えない。手動任免は <see cref="CabinetAppointmentPanel"/>／党三役は <see cref="PartyExecutivePanel"/> から同じ共通入口）。
        /// 同じ状態で繰り返しても履歴・通知は増えない。<paramref name="notify"/> が false なら通知しない（読込時）。
        /// </summary>
        private void RunCabinetAndPartyExecutives(FactionState s, int year, List<Person> roster, bool notify, bool autoFill)
        {
            if (s == null || s.politics == null) return;
            SeedMinistries();
            int idx = FactionIndex(s.faction);
            List<Ministry> tree = ministries != null && idx >= 0 && idx < ministries.Length ? ministries[idx] : null;
            int top = TopMinistryIdOf(s.faction);

            var changes = new List<AppointmentHistoryEntry>();
            var partyChanges = new List<AppointmentHistoryEntry>();
            partyChanges.AddRange(PartyExecutiveRules.Reconcile(s.politics, s.faction, roster, year, CabinetPrm));
            changes.AddRange(CabinetAppointmentRules.Reconcile(s.politics, s.faction, tree, top, roster, year, CabinetPrm));
            if (autoFill)
            {
                // 組閣を先に（閣僚を決めてから残る党員で三役）。どちらも兼任しない。
                changes.AddRange(CabinetAppointmentRules.AutoFill(s.politics, s.faction, tree, top, roster, year, CabinetPrm));
                partyChanges.AddRange(PartyExecutiveRules.AutoFill(s.politics, s.faction, roster, year, CabinetPrm));
            }
            if (!notify) return;
            NotifyCabinet(s, changes);
            NotifyPartyExecutives(s, partyChanges);
        }

        /// <summary>内閣の変化を通知する（就任はまとめて1通・総辞職/職務執行/失職は1件ずつ）。</summary>
        private void NotifyCabinet(FactionState s, List<AppointmentHistoryEntry> changes)
        {
            if (changes == null || changes.Count == 0) return;
            var appointed = new StringBuilder();
            int count = 0;
            for (int i = 0; i < changes.Count; i++)
            {
                AppointmentHistoryEntry e = changes[i];
                switch (e.action)
                {
                    case "就任":
                    case "続投":
                        if (count < MaxCabinetNamesInNotice)
                        {
                            if (count > 0) appointed.Append('・');
                            appointed.Append(e.postLabel).Append(' ').Append(ElectionPersonName(e.personId));
                            if (e.action == "続投") appointed.Append("（続投）");
                        }
                        count++;
                        break;
                    case "総辞職":
                    case "職務執行":
                        NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意, $"{s.faction} 内閣{e.action}：{e.reason}");
                        break;
                    case "失職":
                        NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意,
                            $"{s.faction} {e.postLabel} {ElectionPersonName(e.personId)} 失職（{e.reason}）");
                        break;
                }
            }
            if (count > 0)
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"{s.faction} 内閣の任命 {count}名：{appointed}{(count > MaxCabinetNamesInNotice ? $" ほか{count - MaxCabinetNamesInNotice}名" : "")}");
        }

        /// <summary>党三役の変化を党ごとに1通へまとめて通知する。</summary>
        private void NotifyPartyExecutives(FactionState s, List<AppointmentHistoryEntry> changes)
        {
            if (changes == null || changes.Count == 0 || s.politics == null) return;
            for (int p = 0; p < s.politics.parties.Count; p++)
            {
                Party party = s.politics.parties[p];
                if (party == null) continue;
                var sb = new StringBuilder();
                int n = 0;
                for (int i = 0; i < changes.Count; i++)
                {
                    AppointmentHistoryEntry e = changes[i];
                    if (!e.eventId.Contains(":党" + party.id + ":")) continue;
                    if (e.action == "暫定") continue;
                    if (n > 0) sb.Append('・');
                    sb.Append(e.postLabel).Append(' ').Append(ElectionPersonName(e.personId)).Append(' ').Append(e.action);
                    if (e.action != "就任" && e.action != "続投") sb.Append('（').Append(e.reason).Append('）');
                    n++;
                }
                if (n > 0)
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報, $"{s.faction} {party.partyName} 党三役：{sb}");
            }
        }

        private const int MaxCabinetNamesInNotice = 6;

        /// <summary>
        /// 閣僚の決裁権限の判定材料（#2768 #67）。決裁・上申の確定・見込み表示のたびに組み直す（読み取りのみ・省庁のシードもしない）。
        /// 内閣が置かれていなければ null＝従来の役職判定だけになる。
        /// </summary>
        public CabinetDecisionContext CabinetDecisionContextOf(Faction f)
        {
            FactionState s = StateOf(f);
            if (s == null || s.politics == null || s.politics.cabinet == null) return null;
            int idx = FactionIndex(f);
            List<Ministry> tree = ministries != null && idx >= 0 && idx < ministries.Length ? ministries[idx] : null;
            return new CabinetDecisionContext(s.politics, f, tree, TopMinistryIdOf(f), ElectionRoster(), ElectionYear());
        }

        // ===== 内閣人事メニューの操作入口（#2768 #141） =====
        // 操作者は PlayerCharacter()（主人公）だけ＝UI から任意の人物を操作者に渡せない。確認（Check*）と実行は同じ
        // CabinetAppointmentRules の判定経路を通し、実行時にもう一度判定する。在任・委任・履歴は PoliticsState.cabinet だけに書く。

        /// <summary>内閣人事メニューが読む材料（操作者の勢力の政治状態・省庁・名簿・暦年）。組めなければ理由を返す。</summary>
        public struct CabinetOperation
        {
            public Person actor;
            public Faction faction;
            public PoliticsState politics;
            public List<Ministry> tree;
            public int topId;
            public List<Person> roster;
            public int year;
            /// <summary>組めない理由（組めたら null）。</summary>
            public string problem;
        }

        /// <summary>いまの操作者で内閣人事の材料を組む（状態は変えない・省庁のシードもしない）。</summary>
        public CabinetOperation CabinetOperationForPlayer()
        {
            var op = new CabinetOperation { actor = PlayerCharacter(), topId = -1 };
            if (op.actor == null) { op.problem = "操作する人物（主人公）が特定できない"; return op; }
            op.faction = op.actor.faction;
            FactionState s = StateOf(op.faction);
            if (s == null || s.politics == null) { op.problem = op.faction + " に政治状態がない"; return op; }
            op.politics = s.politics;
            int idx = FactionIndex(op.faction);
            op.tree = ministries != null && idx >= 0 && idx < ministries.Length ? ministries[idx] : null;
            op.topId = TopMinistryIdOf(op.faction);
            op.roster = ElectionRoster();
            op.year = ElectionYear();
            if (s.politics.cabinet == null || s.politics.cabinet.posts == null || s.politics.cabinet.posts.Count == 0)
                op.problem = "内閣が置かれていない（選挙で首班を選ぶ政体で組閣後に操作できる）";
            return op;
        }

        /// <summary>任免で使う調整値（メニューの候補表示が本番の判定と同じ値を読む）。</summary>
        public static CabinetParams CabinetParamsInUse => CabinetPrm;

        public AppointmentResult CheckPlayerCabinetAppoint(int ministryId, CabinetPostKind kind, int personId)
        {
            CabinetOperation op = CabinetOperationForPlayer();
            if (op.problem != null) return AppointmentResult.Deny(op.problem);
            return CabinetAppointmentRules.CheckAppoint(op.politics, op.faction, op.actor.id, op.tree, op.topId, ministryId, kind, personId, op.roster, CabinetPrm);
        }

        /// <summary>主人公が首相として任命する（実行時に共通入口で再判定）。理由は必須。</summary>
        public AppointmentResult PlayerCabinetAppoint(int ministryId, CabinetPostKind kind, int personId, string reason)
        {
            CabinetOperation op = CabinetOperationForPlayer();
            if (op.problem != null) return AppointmentResult.Deny(op.problem);
            if (string.IsNullOrWhiteSpace(reason)) return AppointmentResult.Deny("任命の理由が未入力");
            AppointmentResult r = CabinetAppointmentRules.TryAppoint(op.politics, op.faction, op.actor.id, op.tree, op.topId,
                ministryId, kind, personId, op.roster, op.year, "首相の任命（理由：" + reason.Trim() + "）", CabinetPrm);
            if (r.ok)
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"{op.faction} {PostTitleOf(op, ministryId, kind)} に {ElectionPersonName(personId)} を任命（首相 {op.actor.name}・理由：{reason.Trim()}）");
            return r;
        }

        public AppointmentResult CheckPlayerCabinetDismiss(int ministryId, CabinetPostKind kind)
        {
            CabinetOperation op = CabinetOperationForPlayer();
            if (op.problem != null) return AppointmentResult.Deny(op.problem);
            return CabinetAppointmentRules.CheckDismiss(op.politics, op.faction, op.actor.id, ministryId, kind, op.roster, CabinetPrm);
        }

        /// <summary>主人公が首相として解任する（実行時に共通入口で再判定）。理由は必須。</summary>
        public AppointmentResult PlayerCabinetDismiss(int ministryId, CabinetPostKind kind, string reason)
        {
            CabinetOperation op = CabinetOperationForPlayer();
            if (op.problem != null) return AppointmentResult.Deny(op.problem);
            if (string.IsNullOrWhiteSpace(reason)) return AppointmentResult.Deny("解任の理由が未入力");
            CabinetPost post = CabinetAppointmentRules.FindPost(op.politics.cabinet, ministryId, kind);
            int who = post != null ? post.holderId : -1;
            AppointmentResult r = CabinetAppointmentRules.Dismiss(op.politics, op.faction, op.actor.id, ministryId, kind, op.roster, op.year,
                "首相の解任（理由：" + reason.Trim() + "）", CabinetPrm);
            if (r.ok)
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意,
                    $"{op.faction} {PostTitleOf(op, ministryId, kind)} {ElectionPersonName(who)} を解任（首相 {op.actor.name}・理由：{reason.Trim()}）");
            return r;
        }

        public AppointmentResult CheckPlayerCabinetDelegate(int ministryId, CabinetDelegation scope, int untilYear)
        {
            CabinetOperation op = CabinetOperationForPlayer();
            if (op.problem != null) return AppointmentResult.Deny(op.problem);
            return CabinetAppointmentRules.CheckDelegate(op.politics, op.faction, op.actor.id, ministryId, scope, untilYear, op.roster, op.year, CabinetPrm);
        }

        /// <summary>主人公が大臣として自省の副大臣へ期限つきで委任する（実行時に共通入口で再判定）。</summary>
        public AppointmentResult PlayerCabinetDelegate(int ministryId, CabinetDelegation scope, int untilYear)
        {
            CabinetOperation op = CabinetOperationForPlayer();
            if (op.problem != null) return AppointmentResult.Deny(op.problem);
            AppointmentResult r = CabinetAppointmentRules.Delegate(op.politics, op.faction, op.actor.id, ministryId, scope, untilYear, op.roster, op.year, CabinetPrm);
            if (r.ok)
            {
                CabinetPost vice = CabinetAppointmentRules.FindPost(op.politics.cabinet, ministryId, CabinetPostKind.副大臣);
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"{op.faction} {PostTitleOf(op, ministryId, CabinetPostKind.副大臣)} {ElectionPersonName(vice != null ? vice.holderId : -1)} へ {scope} を SE{untilYear} まで委任（大臣 {op.actor.name}）");
            }
            return r;
        }

        public AppointmentResult CheckPlayerCabinetRevokeDelegation(int ministryId)
        {
            CabinetOperation op = CabinetOperationForPlayer();
            if (op.problem != null) return AppointmentResult.Deny(op.problem);
            return CabinetAppointmentRules.CheckRevokeDelegation(op.politics, op.faction, op.actor.id, ministryId, CabinetPrm);
        }

        /// <summary>主人公が大臣として委任を撤回する（実行時に共通入口で再判定）。理由は必須。</summary>
        public AppointmentResult PlayerCabinetRevokeDelegation(int ministryId, string reason)
        {
            CabinetOperation op = CabinetOperationForPlayer();
            if (op.problem != null) return AppointmentResult.Deny(op.problem);
            if (string.IsNullOrWhiteSpace(reason)) return AppointmentResult.Deny("委任撤回の理由が未入力");
            AppointmentResult r = CabinetAppointmentRules.RevokeDelegation(op.politics, op.faction, op.actor.id, ministryId, op.year, reason.Trim(), CabinetPrm);
            if (r.ok)
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"{op.faction} {PostTitleOf(op, ministryId, CabinetPostKind.副大臣)} への委任を撤回（大臣 {op.actor.name}・理由：{reason.Trim()}）");
            return r;
        }

        // ===== 党人事メニューの操作入口（#2768 #159 #165：党三役＝幹事長・政調会長・総務会長） =====
        // 操作者は PlayerCharacter()（主人公）だけ。任免権者は PartyExecutiveRules が判定する「その党の正当な党首」本人のみ
        // （他党の人物・党三役・党首でない首相は権限外）。党首は総裁選で選ぶため任命の入口を作らない。
        // 確認（Check*）と実行は同じ PartyExecutiveRules の判定経路、在任・履歴は Party.posts / postHistory だけに書く。
        // 党三役は政府の決裁・国庫・軍の指揮権を持たない（Core の PartyExecutiveRules.Authority のまま＝ここでは何も付与しない）。

        /// <summary>党人事メニューが読む材料（操作者の勢力の政治状態・名簿・暦年）。組めなければ理由を返す。</summary>
        public CabinetOperation PartyOperationForPlayer()
        {
            var op = new CabinetOperation { actor = PlayerCharacter(), topId = -1 };
            if (op.actor == null) { op.problem = "操作する人物（主人公）が特定できない"; return op; }
            op.faction = op.actor.faction;
            FactionState s = StateOf(op.faction);
            if (s == null || s.politics == null) { op.problem = op.faction + " に政治状態がない"; return op; }
            op.politics = s.politics;
            op.roster = ElectionRoster();
            op.year = ElectionYear();
            if (s.politics.parties == null || s.politics.parties.Count == 0)
                op.problem = "政党がない（選挙政治の政体で政党が置かれてから操作できる）";
            return op;
        }

        /// <summary>操作者の勢力の党（無ければ null）。</summary>
        private static Party OperationPartyOf(CabinetOperation op, int partyId)
            => op.politics != null ? ElectionCycleRules.FindParty(op.politics.parties, partyId) : null;

        public AppointmentResult CheckPlayerPartyAppoint(int partyId, PartyPost post, int personId)
        {
            CabinetOperation op = PartyOperationForPlayer();
            if (op.problem != null) return AppointmentResult.Deny(op.problem);
            Party party = OperationPartyOf(op, partyId);
            if (party == null) return AppointmentResult.Deny(op.faction + " に党#" + partyId + " がない");
            return PartyExecutiveRules.CheckAppoint(op.politics, op.faction, party, op.actor.id, post, personId, op.roster, CabinetPrm);
        }

        /// <summary>主人公が党首として党三役を任命する（実行時に共通入口で再判定）。理由は必須。</summary>
        public AppointmentResult PlayerPartyAppoint(int partyId, PartyPost post, int personId, string reason)
        {
            CabinetOperation op = PartyOperationForPlayer();
            if (op.problem != null) return AppointmentResult.Deny(op.problem);
            Party party = OperationPartyOf(op, partyId);
            if (party == null) return AppointmentResult.Deny(op.faction + " に党#" + partyId + " がない");
            if (string.IsNullOrWhiteSpace(reason)) return AppointmentResult.Deny("任命の理由が未入力");
            AppointmentResult r = PartyExecutiveRules.TryAppoint(op.politics, op.faction, party, op.actor.id, post, personId, op.roster, op.year,
                "党首の任命（理由：" + reason.Trim() + "）", CabinetPrm);
            if (r.ok)
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"{op.faction} {party.partyName} {post} に {ElectionPersonName(personId)} を任命（党首 {op.actor.name}・理由：{reason.Trim()}）");
            return r;
        }

        public AppointmentResult CheckPlayerPartyDismiss(int partyId, PartyPost post)
        {
            CabinetOperation op = PartyOperationForPlayer();
            if (op.problem != null) return AppointmentResult.Deny(op.problem);
            Party party = OperationPartyOf(op, partyId);
            if (party == null) return AppointmentResult.Deny(op.faction + " に党#" + partyId + " がない");
            return PartyExecutiveRules.CheckDismiss(party, op.faction, op.actor.id, post, op.roster, CabinetPrm);
        }

        /// <summary>主人公が党首として党三役を解任する（実行時に共通入口で再判定）。理由は必須。</summary>
        public AppointmentResult PlayerPartyDismiss(int partyId, PartyPost post, string reason)
        {
            CabinetOperation op = PartyOperationForPlayer();
            if (op.problem != null) return AppointmentResult.Deny(op.problem);
            Party party = OperationPartyOf(op, partyId);
            if (party == null) return AppointmentResult.Deny(op.faction + " に党#" + partyId + " がない");
            if (string.IsNullOrWhiteSpace(reason)) return AppointmentResult.Deny("解任の理由が未入力");
            int who = PartyOrganizationRules.HolderOf(party, post);
            AppointmentResult r = PartyExecutiveRules.Dismiss(party, op.faction, op.actor.id, post, op.roster, op.year,
                "党首の解任（理由：" + reason.Trim() + "）", CabinetPrm);
            if (r.ok)
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意,
                    $"{op.faction} {party.partyName} {post} {ElectionPersonName(who)} を解任（党首 {op.actor.name}・理由：{reason.Trim()}）");
            return r;
        }

        /// <summary>人物名（名簿に無ければ 人物#id）。内閣人事メニューの表示用。</summary>
        public string CabinetPersonName(int personId) => personId < 0 ? "（空席）" : ElectionPersonName(personId);

        private static string PostTitleOf(CabinetOperation op, int ministryId, CabinetPostKind kind)
        {
            CabinetPost p = op.politics != null ? CabinetAppointmentRules.FindPost(op.politics.cabinet, ministryId, kind) : null;
            return p != null ? CabinetAppointmentRules.PostTitle(p) : "省#" + ministryId + " " + kind;
        }
    }
}
