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

        // ===== 省内職位の人事メニューの操作入口（#141：直接実行 or 稟議への上申） =====
        // 操作者は PlayerCharacter()（主人公）だけ＝UI から任意の人物を操作者に渡せない。
        // 可否・資格・空席・在職年・承認権限は CivilServicePostRules.Check/Execute だけが判定する（ここでは判定しない）。
        // 権限があれば即時に台帳へ反映し、権限外なら既存の稟議（Petition）＋決裁カード（PendingDecision）へ載せて裁可を仰ぐ。
        // 裁可後の実行も同じ CivilServicePostRules.Execute を1回だけ通る（RingiDirector.OnResolved → ExecuteApprovedCivilServicePost）。

        /// <summary>人事の稟議で使う決裁id の番号帯（税 80000／編制 85000 と衝突させない）。</summary>
        public const int CivilServiceDecisionIdBand = 87000;

        /// <summary>人事メニューが読む材料（操作者の勢力の政治状態・省庁・名簿・暦年・人事台帳）。組めなければ理由を返す。</summary>
        public struct CivilServiceOperation
        {
            public Person actor;
            public Faction faction;
            public PoliticsState politics;
            public List<Ministry> tree;
            public List<Person> roster;
            public int year;
            /// <summary>人事台帳（<see cref="FactionState.civilService"/>＝単一の出所）。</summary>
            public CivilServiceState ledger;
            /// <summary>組めない理由（組めたら null）。</summary>
            public string problem;
        }

        /// <summary>いまの操作者（主人公）で人事の材料を組む（状態は変えない・省庁のシードもしない）。</summary>
        public CivilServiceOperation CivilServiceOperationForPlayer() => CivilServiceOperationFor(PlayerCharacter());

        /// <summary>指定の人物を操作者として人事の材料を組む（裁可の時点で決裁権者を通すためにも使う・状態は変えない）。</summary>
        private CivilServiceOperation CivilServiceOperationFor(Person actor)
        {
            var op = new CivilServiceOperation { actor = actor };
            if (actor == null) { op.problem = "操作する人物（主人公）が特定できない"; return op; }
            op.faction = actor.faction;
            FactionState s = StateOf(op.faction);
            if (s == null || s.politics == null) { op.problem = op.faction + " に政治状態がない"; return op; }
            op.politics = s.politics;
            int idx = FactionIndex(op.faction);
            op.tree = ministries != null && idx >= 0 && idx < ministries.Length ? ministries[idx] : null;
            if (op.tree == null) { op.problem = op.faction + " の省庁が編成されていない"; return op; }
            op.roster = ElectionRoster();
            op.year = ElectionYear();
            op.ledger = s.civilService;
            // 台帳の初期化と既存配属の移行は年次人事（RunCivilServiceAnnualTick）の領分＝ここでは作らない（移行を飛ばさない）。
            if (op.ledger == null) op.problem = "人事台帳がまだ作られていない（年次の官僚人事で初期化される）";
            return op;
        }

        /// <summary>
        /// 主人公が行う人事の<b>見込み</b>（状態は変えない）。<see cref="CivilServicePostRules.Check"/> そのもの＝
        /// ok なら直接実行できる、<see cref="AppointmentResult.canPetition"/> なら上申になる、それ以外は理由つきで受け付けない。
        /// </summary>
        public AppointmentResult PreviewPlayerCivilServicePost(int ministryId, CivilServiceAction action, int personId,
            BureaucratGrade targetGrade)
        {
            CivilServiceOperation op = CivilServiceOperationForPlayer();
            if (op.problem != null) return AppointmentResult.Deny(op.problem);
            return CivilServicePostRules.Check(op.politics, op.faction, op.actor.id, op.tree, ministryId, action, personId,
                targetGrade, op.roster, op.year, op.ledger, CivilServicePrm);
        }

        /// <summary>
        /// 主人公が人事を申し出る<b>唯一の入口</b>（操作画面はここを呼ぶ）。権限があれば即時に実行し、
        /// 権限外なら決裁デスクへ上申する（同じ未解決の人事は二重に起票しない）。理由は空なら既定文を使う。
        /// 台帳が動くのは「直接実行」か「裁可の執行」のどちらか一度だけ。
        /// </summary>
        public CivilServiceRequestResult SubmitPlayerCivilServicePost(int ministryId, CivilServiceAction action,
            int personId, BureaucratGrade targetGrade, string reason)
        {
            CivilServiceOperation op = CivilServiceOperationForPlayer();
            if (op.problem != null) return CivilServiceRequestResult.Rejected(op.problem);

            string why = CivilServiceRingiRules.SafeReason(action, reason);
            AppointmentResult check = CivilServicePostRules.Check(op.politics, op.faction, op.actor.id, op.tree, ministryId,
                action, personId, targetGrade, op.roster, op.year, op.ledger, CivilServicePrm);

            if (check.ok)
            {
                AppointmentResult done = CivilServicePostRules.Execute(op.politics, op.faction, op.actor.id, op.tree,
                    ministryId, action, personId, targetGrade, op.roster, op.year, why, op.ledger, CivilServicePrm);
                if (!done.ok) return CivilServiceRequestResult.Rejected(done.reason);
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"{op.faction} {done.reason}（決裁 {op.actor.name}・理由：{why}）");
                return CivilServiceRequestResult.Executed(done.reason);
            }

            if (!check.canPetition || check.petitionToId < 0)
                return CivilServiceRequestResult.Rejected(check.reason);
            return RaiseCivilServicePetition(op, ministryId, action, personId, targetGrade, why, check);
        }

        /// <summary>
        /// 権限外の人事を決裁デスクへ上申する（既存の稟議台帳＋決裁カードの経路をそのまま使う）。
        /// ★人事は官僚機構の生存ロール（<see cref="PetitionFlowRules"/>）で握り潰さない＝権限外の操作を正規の上申先へ必ず届ける。
        /// ★省益（<see cref="MinistryRules.DomainFriction"/>）で人事の内容を値切らない＝摩擦は 0（人事は規模を持たない二値の決定）。
        /// </summary>
        private CivilServiceRequestResult RaiseCivilServicePetition(in CivilServiceOperation op, int ministryId,
            CivilServiceAction action, int personId, BureaucratGrade targetGrade, string why, in AppointmentResult auth)
        {
            BureaucratGrade grade = CivilServiceRingiRules.ResolveApprovalGrade(op.ledger, action, personId, targetGrade);
            var req = new CivilServicePostRequest(action, ministryId, personId, grade);
            string effectKey = CivilServiceRingiRules.Encode(req);
            if (HasPendingCivilServiceDecision(effectKey))
                return CivilServiceRequestResult.Rejected("同じ人事がすでに決裁待ちです（二重に起票しない）");

            Ministry ministry = MinistryRules.Get(op.tree, ministryId);
            string ministryName = ministry != null ? (ministry.ministryName ?? "") : "省#" + ministryId;
            string personLabel = CabinetPersonName(personId);
            string title = CivilServiceRingiRules.Describe(req, ministryName, personLabel, grade);

            var pet = new Petition(0, title, op.faction, BoxKind.政治家, PetitionOrigin.建白, effectKey)
            {
                drafterId = op.actor.id,
                carrierId = op.actor.id,
                addresseeId = auth.petitionToId,
            };
            if (!RingiPipeline.Submit(RingiDirector.Ledger, pet))
                return CivilServiceRequestResult.Rejected("稟議を起票できませんでした");
            RingiPipeline.SendToDecision(pet); // 伝播の生存ロールを挟まず決裁待ちへ（握り潰さない）

            Person addressee = FindPersonById(auth.petitionToId);
            string addresseeLabel = addressee != null ? addressee.name : CabinetPersonName(auth.petitionToId);
            string body = CivilServiceRingiRules.ComposeBody(title,
                ministryName + " ／ " + personLabel + "（" + grade + "）", op.actor.name, addresseeLabel, auth.reason, why);

            var pd = new PendingDecision(DecisionDeck.NextDecisionId(CivilServiceDecisionIdBand), title,
                DecisionSeverity.通常, DecisionSource.建白結果, effectKey, defaultChoiceIndex: 1, body: body);
            pd.choices.Add("裁可する");
            pd.choices.Add("見送る（現状維持）");
            pd.petitionId = pet.id;   // 稟議との対応はカード自身が持つ（シーン往復・保存で失わない）
            pd.friction = 0f;         // 人事は省益で骨抜きにしない
            pd.proposerId = op.actor.id;
            pd.proposerName = op.actor.name;
            pd.deciderId = auth.petitionToId;
            pd.deciderName = addresseeLabel;
            pd.authorityBasis = auth.reason;
            DecisionDeck.Enqueue(pd);

            NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意,
                $"［人事上申］{title} が決裁待ち（決裁 {addresseeLabel}・右下の決裁デスクへ）");
            return CivilServiceRequestResult.Petitioned(pd.id, auth.petitionToId, effectKey, auth.reason);
        }

        /// <summary>同じ人事（同じ効果キー）の決裁が未解決で残っているか（シーン往復・保存でも失わないカードから判定）。</summary>
        private static bool HasPendingCivilServiceDecision(string effectKey)
        {
            DecisionQueue q = DecisionDeck.Queue;
            if (q == null || string.IsNullOrEmpty(effectKey)) return false;
            for (int i = 0; i < q.items.Count; i++)
            {
                PendingDecision d = q.items[i];
                if (d == null || DecisionResolutionRules.IsSettled(d)) continue;
                if (string.Equals(d.effectKey, effectKey, System.StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// その人物がその人事を承認できるか（決裁・上申・見込み表示で共通）。
        /// ★一般の効果キーの分野推定（<see cref="DecisionAuthorityRules.DomainOf"/>→内政）で代用せず、
        /// 復号した省・段・人物を <see cref="CivilServicePostRules.ApprovalAuthority"/> へ渡す＝内閣人事局の承認権限そのもので判定する。
        /// 毎回その時点の内閣・委任・名簿から組み直す（大臣交代・委任の期限切れ・首相交代を古い権限で通さない）。
        /// </summary>
        public DecisionAuthorityResult EvaluateCivilServiceAuthority(Person actor, string effectKey)
        {
            if (!CivilServiceRingiRules.TryDecode(effectKey, out CivilServicePostRequest req))
                return new DecisionAuthorityResult(DecisionAuthority.権限外, "人事の内容を復元できません（効果キーが不正）");
            if (actor == null)
                return new DecisionAuthorityResult(DecisionAuthority.権限外, "決裁する人物がいません");

            CivilServiceOperation op = CivilServiceOperationFor(actor);
            if (op.problem != null) return new DecisionAuthorityResult(DecisionAuthority.権限外, op.problem);
            if (actor.id == req.personId)
                return new DecisionAuthorityResult(DecisionAuthority.権限外, "官僚本人が自分の人事を承認することはできない");

            BureaucratGrade grade = CivilServiceRingiRules.ResolveApprovalGrade(op.ledger, req.action, req.personId, req.targetGrade);
            AppointmentResult a = CivilServicePostRules.ApprovalAuthority(op.politics, op.faction, actor.id, req.ministryId,
                grade, op.roster, op.year);
            if (a.ok) return new DecisionAuthorityResult(DecisionAuthority.裁可, a.reason);
            if (a.canPetition && a.petitionToId >= 0 && a.petitionToId != actor.id)
            {
                Person to = FindPersonById(a.petitionToId);
                return new DecisionAuthorityResult(DecisionAuthority.上申, a.reason, a.petitionToId,
                    to != null ? to.name : "");
            }
            return new DecisionAuthorityResult(DecisionAuthority.権限外, a.reason);
        }

        /// <summary>
        /// 裁可された人事を執行する（<see cref="RingiDirector"/> の決裁確定から1回だけ呼ばれる）。
        /// <b>決裁の時点の状態でやり直す</b>＝承認権限を改めて引き、<see cref="CivilServicePostRules.Execute"/> が
        /// 資格・空席・在職年をもう一度通す。空席が消えた・資格を失った・すでに異動した等なら台帳を変えず理由を返す
        /// （承認できたことと、実際に効いたことを区別する）。
        /// </summary>
        public PetitionActionResult ExecuteApprovedCivilServicePost(Faction faction, int deciderId, string effectKey,
            string reason)
        {
            if (!CivilServiceRingiRules.TryDecode(effectKey, out CivilServicePostRequest req))
                return PetitionActionResult.Fail(PetitionActionOutcome.対象なし, "人事の内容を復元できませんでした（効果キーが不正）");

            Person approver = ResolveCivilServiceApprover(faction, deciderId, effectKey, out string problem);
            if (approver == null) return PetitionActionResult.Fail(PetitionActionOutcome.対象外, problem);

            CivilServiceOperation op = CivilServiceOperationFor(approver);
            if (op.problem != null) return PetitionActionResult.Fail(PetitionActionOutcome.対象外, op.problem);

            string why = string.IsNullOrEmpty(reason)
                ? CivilServiceRingiRules.DefaultReason(req.action)
                : reason;
            AppointmentResult r = CivilServicePostRules.Execute(op.politics, op.faction, approver.id, op.tree,
                req.ministryId, req.action, req.personId, req.targetGrade, op.roster, op.year,
                "稟議の裁可（理由：" + why + "）", op.ledger, CivilServicePrm);
            if (!r.ok) return PetitionActionResult.Fail(PetitionActionOutcome.対象外, r.reason);

            NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                $"{op.faction} {r.reason}（裁可 {approver.name}・理由：{why}）");
            return new PetitionActionResult(PetitionActionOutcome.実行, r.reason, 1f);
        }

        /// <summary>
        /// 執行の時点で実際に承認できる決裁権者を選ぶ：カードに記録された決裁権者を優先し、
        /// その人が承認できなくなっていれば現在の操作者（自分の権限で裁可した場合）を見る。どちらも承認できなければ null＋理由。
        /// ＝起票時の権限で通さない（大臣交代・委任の期限切れ・首相交代・失職を執行の直前に弾く）。
        /// </summary>
        private Person ResolveCivilServiceApprover(Faction faction, int deciderId, string effectKey, out string problem)
        {
            problem = null;
            string first = null;

            Person recorded = FindPersonById(deciderId);
            if (recorded != null && recorded.faction == faction)
            {
                DecisionAuthorityResult a = EvaluateCivilServiceAuthority(recorded, effectKey);
                if (a.CanDecide) return recorded;
                first = recorded.name + " は決裁の時点で承認できません（" + a.basis + "）";
            }

            Person actor = PlayerCharacter();
            if (actor != null && actor.faction == faction && actor.id != deciderId)
            {
                DecisionAuthorityResult a = EvaluateCivilServiceAuthority(actor, effectKey);
                if (a.CanDecide) return actor;
                if (first == null) first = actor.name + " は決裁の時点で承認できません（" + a.basis + "）";
            }

            problem = first ?? "承認できる決裁権者がいません";
            return null;
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
