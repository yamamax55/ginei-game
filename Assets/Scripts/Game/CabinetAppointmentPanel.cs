using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 戦略画面の「内閣人事」メニュー（#2768 #141）。上メニューの観測ウィンドウ（政治タブ）と政府観測（Alt+G）の窓からクリックで開く。
    ///
    /// 表示：操作者（主人公）・首相・組閣状況・職務執行、省ごとの大臣/副大臣/政務官（人物・所属・当選履歴の年功・委任と期限・空席理由）。
    /// 操作：職を選ぶ→候補一覧（絞り込み・スクロール）から人物を選ぶ→理由を入力→確認→確定で任命。解任・委任・委任撤回も同じ確認→確定。
    ///
    /// 操作者は <see cref="GalaxyView.PlayerCharacter"/> だけ（任意の人物を渡せない）。可否の表示は GalaxyView の Check*、実行は Player*＝
    /// どちらも <see cref="CabinetAppointmentRules"/> の同じ判定を通り、確定時にもう一度判定する。在任・委任・履歴は PoliticsState.cabinet だけ
    /// （独自の台帳・GovernmentRegistry 登録を作らない）。閣僚職は艦隊指揮権・国庫支出を与えない（Core の権限判定のまま）。
    ///
    /// 作法は <see cref="CorpsOrganizationPanel"/> に合わせる：非モーダル窓・タイトルバーは <see cref="WindowChrome"/>・
    /// Esc は <see cref="UIWindowStack"/> へ登録するだけ・一覧は <see cref="UiScrollbars"/> で見えるバー。
    /// </summary>
    public class CabinetAppointmentPanel : MonoBehaviour
    {
        [Header("外観")]
        [Tooltip("Canvas の描画順（軍団編成1001と同格・観測オーバーレイ1090より後ろ）")]
        public int canvasSortingOrder = 1002;

        [Tooltip("パネルの幅（設計ピクセル）")]
        public float panelDesignWidth = 1080f;

        [Tooltip("パネルの高さ（設計ピクセル・MAP の高さに収まるよう自動で切り詰める）")]
        public float panelDesignHeight = 900f;

        [Tooltip("パネル背景色")]
        public Color panelColor = new Color(0.05f, 0.06f, 0.09f, 0.97f);

        [Header("更新")]
        [Tooltip("一覧を作り直す間隔（実時間秒・ポーズ中も進む）")]
        public float refreshInterval = 1f;

        [Header("候補一覧")]
        [Tooltip("候補一覧に並べる最大人数（超えた分は件数を明示し、絞り込みで探す）")]
        public int maxCandidateRows = 80;

        private const float BaseFont = 16f;
        private const float MinFontPx = 13f;
        private const float BaseRow = 30f;
        private const float MinRowPx = 26f;
        private const float MinPanelWidthPx = 640f;
        private const float PostListShare = 1f;
        private const float CandidateListShare = 1f;

        /// <summary>確定待ちの操作（確認表示と実行を同じ種類で結ぶ）。</summary>
        public enum PendingOp { なし, 任命, 解任, 委任, 委任撤回 }

        private static CabinetAppointmentPanel instance;

        private Canvas canvas;
        private RectTransform frameRT;
        private RectTransform postContent;
        private RectTransform candidateContent;
        private ScrollRect postScroll;
        private ScrollRect candidateScroll;
        private TMP_FontAsset jpFont;
        private TextMeshProUGUI headerLabel;
        private TextMeshProUGUI selectionLabel;
        private TextMeshProUGUI confirmLabel;
        private TextMeshProUGUI messageLabel;
        private TMP_InputField filterField;
        private TMP_InputField reasonField;
        private TextMeshProUGUI eligibleToggleCaption;
        private TextMeshProUGUI scopeCaption;
        private TextMeshProUGUI untilCaption;
        private Button confirmButton;
        private object escWindowToken;

        private float refreshTimer;
        private string message = "";
        private string filterText = "";
        private bool eligibleOnly = true;

        // 選択（IDで持つ＝一覧を作り直しても保つ）
        private int selectedMinistryId = -1;
        private CabinetPostKind selectedKind = CabinetPostKind.大臣;
        private int selectedCandidateId = -1;
        private PendingOp pending = PendingOp.なし;
        private CabinetDelegation scope = CabinetDelegation.所管決裁;
        private int untilOffset = 1; // 委任の終了年＝今年＋この年数（0..最長）

        private readonly List<GameObject> postRows = new List<GameObject>();
        private readonly List<GameObject> candidateRows = new List<GameObject>();
        private readonly List<Button> actionButtons = new List<Button>();

        // ===== 自動生成（CorpsOrganizationPanel と同型・手置き不要）=====

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Strategy") return;
            if (FindAnyObjectByType<CabinetAppointmentPanel>() != null) return;
            new GameObject("CabinetAppointmentPanel").AddComponent<CabinetAppointmentPanel>();
        }

        // ===== static の窓口 =====

        public static bool IsOpen => instance != null && instance.canvas != null && instance.canvas.gameObject.activeSelf;

        public static void Show()
        {
            CabinetAppointmentPanel p = EnsureInstance();
            if (p != null) p.Open();
        }

        public static void Hide()
        {
            if (instance != null) instance.Close();
        }

        public static void Toggle()
        {
            if (IsOpen) Hide();
            else Show();
        }

        private static CabinetAppointmentPanel EnsureInstance()
        {
            if (instance != null) return instance;
            instance = FindAnyObjectByType<CabinetAppointmentPanel>();
            if (instance == null) instance = new GameObject("CabinetAppointmentPanel").AddComponent<CabinetAppointmentPanel>();
            return instance;
        }

        // ===== ライフサイクル =====

        private void Awake()
        {
            instance = this;
            BuildUI();
            if (canvas != null) canvas.gameObject.SetActive(false);
            escWindowToken = UIWindowStack.Register(
                () => canvas != null && canvas.gameObject.activeSelf, Close, canvasSortingOrder, "内閣人事");
        }

        private void OnDestroy()
        {
            UIWindowStack.Unregister(escWindowToken);
            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (!IsOpen) return;
            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer < Mathf.Max(0.1f, refreshInterval)) return;
            refreshTimer = 0f;
            Rebuild();
        }

        private void Open()
        {
            message = "";
            if (canvas != null) canvas.gameObject.SetActive(true);
            Layout();
            Rebuild();
        }

        private void Close()
        {
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        private static GalaxyView View => GalaxyView.Active;

        // ===== 一覧 =====

        private void Rebuild()
        {
            ClearRows(postRows);
            ClearRows(candidateRows);
            float font = FontSize();
            float rowH = RowHeight();

            GalaxyView gv = View;
            if (gv == null)
            {
                MakeNotice(postContent, postRows, "戦略マップ（GalaxyView）がありません。", font, rowH);
                SetHeader("戦略マップでのみ操作できます。");
                UpdateConfirm(null, default);
                return;
            }

            GalaxyView.CabinetOperation op = gv.CabinetOperationForPlayer();
            if (op.actor == null || op.politics == null)
            {
                MakeNotice(postContent, postRows, op.problem ?? "内閣の材料が組めません。", font, rowH);
                SetHeader("内閣人事：" + (op.problem ?? ""));
                UpdateConfirm(gv, op);
                return;
            }

            BuildHeader(gv, op);
            BuildPostRows(gv, op, font, rowH);
            BuildCandidateRows(gv, op, font, rowH);
            UpdateSelection(gv, op);
            UpdateConfirm(gv, op);
            if (messageLabel != null) messageLabel.text = message;
        }

        private void SetHeader(string text)
        {
            if (headerLabel != null) headerLabel.text = text;
        }

        private void BuildHeader(GalaxyView gv, GalaxyView.CabinetOperation op)
        {
            CabinetState cab = op.politics.cabinet;
            int premier = CabinetAppointmentRules.FormalPremier(op.politics, op.faction, op.roster, out string premierProblem);
            var sb = new StringBuilder(256);
            sb.Append("操作者：").Append(op.actor.name).Append("（").Append(ActorRoleText(op, premier)).Append("）");
            sb.Append("　首相：").Append(premier >= 0 ? gv.CabinetPersonName(premier) : "<color=#ff7a6a>不在</color>（" + premierProblem + "）");
            if (cab != null && cab.posts != null && cab.posts.Count > 0)
            {
                sb.Append("　組閣 SE").Append(cab.formedYear)
                  .Append("　大臣").Append(CabinetAppointmentRules.FilledCount(cab, CabinetPostKind.大臣))
                  .Append("・副大臣").Append(CabinetAppointmentRules.FilledCount(cab, CabinetPostKind.副大臣))
                  .Append("・政務官").Append(CabinetAppointmentRules.FilledCount(cab, CabinetPostKind.政務官)).Append("名");
                sb.Append("　SE").Append(op.year);
                if (cab.caretaker) sb.Append("\n<color=#ffb070>職務執行内閣：").Append(cab.caretakerReason).Append("</color>");
            }
            else sb.Append("\n<color=#9aa7b2>").Append(op.problem ?? "内閣が置かれていない").Append("</color>");
            SetHeader(sb.ToString());
        }

        /// <summary>操作者の立場（首相／自分の職／閣外）。権限は Core が判定し、ここは表示だけ。</summary>
        private static string ActorRoleText(GalaxyView.CabinetOperation op, int premier)
        {
            if (op.actor.id == premier) return "首相＝閣僚の任免権者";
            CabinetPost held = CabinetAppointmentRules.PostHeldBy(op.politics.cabinet, op.actor.id);
            if (held != null) return CabinetAppointmentRules.PostTitle(held) + "＝" + CabinetAppointmentRules.RoleText(held.kind);
            return "閣外＝任免・委任の権限なし";
        }

        private void BuildPostRows(GalaxyView gv, GalaxyView.CabinetOperation op, float font, float rowH)
        {
            CabinetState cab = op.politics.cabinet;
            if (cab == null || cab.posts == null || cab.posts.Count == 0)
            {
                MakeNotice(postContent, postRows, op.problem ?? "内閣が置かれていない", font, rowH);
                return;
            }
            string lastMinistry = null;
            int lastMinistryId = int.MinValue;
            for (int i = 0; i < cab.posts.Count; i++)
            {
                CabinetPost p = cab.posts[i];
                if (p == null) continue;
                if (p.ministryId != lastMinistryId)
                {
                    lastMinistryId = p.ministryId;
                    lastMinistry = p.ministryName;
                    MakeGroupHeader(postContent, postRows, lastMinistry + "（" + p.domain + "）", font, rowH);
                }
                MakePostRow(gv, op, p, font, rowH);
            }
        }

        private void MakePostRow(GalaxyView gv, GalaxyView.CabinetOperation op, CabinetPost p, float font, float rowH)
        {
            bool sel = p.ministryId == selectedMinistryId && p.kind == selectedKind;
            RectTransform rt = MakeRow(postContent, postRows, "Post_" + p.ministryId + "_" + p.kind, rowH, sel, () =>
            {
                bool same = selectedMinistryId == p.ministryId && selectedKind == p.kind;
                selectedMinistryId = same ? -1 : p.ministryId;
                selectedKind = p.kind;
                selectedCandidateId = -1;
                pending = PendingOp.なし;
                message = "";
                Rebuild();
            });

            MakeCell(rt, CabinetAppointmentRules.PostTitle(p), font, sel ? new Color(1f, 0.92f, 0.62f) : new Color(0.92f, 0.95f, 1f), 0.03f, 0.22f, 6f);
            if (p.holderId >= 0)
            {
                Party party = ElectionCycleRules.FindParty(op.politics.parties, p.partyId);
                SeniorityInfo si = PartySeniorityRules.InfoOf(op.politics, p.holderId, PartySeniorityParams.Default);
                MakeCell(rt, gv.CabinetPersonName(p.holderId), font, new Color(1f, 0.90f, 0.78f), 0.22f, 0.38f, 4f);
                MakeCell(rt, (party != null ? party.partyName : "無所属") + "・" + SeniorityText(si) + "・SE" + p.appointedYear + "就任",
                    font, new Color(0.80f, 0.86f, 0.95f), 0.38f, 0.68f, 4f);
                string note = p.kind == CabinetPostKind.副大臣
                    ? (p.delegation != CabinetDelegation.なし
                        ? "<color=#8ce08c>委任 " + p.delegation + "（SE" + p.delegationEndYear + "まで）</color>"
                        : "委任なし＝提案・調整のみ")
                    : CabinetAppointmentRules.RoleText(p.kind);
                MakeCell(rt, note, font, new Color(0.72f, 0.82f, 0.90f), 0.68f, 1f, 4f);
            }
            else
            {
                MakeCell(rt, "<color=#ff7a6a>空席</color>", font, Color.white, 0.22f, 0.38f, 4f);
                MakeCell(rt, string.IsNullOrEmpty(p.vacancyReason) ? "" : p.vacancyReason, font, new Color(1f, 0.72f, 0.45f), 0.38f, 1f, 4f);
            }
        }

        private static string SeniorityText(SeniorityInfo si)
            => si.historyRegistered ? "国政当選" + si.nationalWins + "回（" + si.tier + "）" : "当選履歴未登録";

        /// <summary>候補の表示用の束（評価・資格の問題は Core の関数から取る）。</summary>
        private struct CandidateView
        {
            public Person person;
            public string problem;
            public CandidateEvaluation eval;
        }

        private void BuildCandidateRows(GalaxyView gv, GalaxyView.CabinetOperation op, float font, float rowH)
        {
            if (selectedMinistryId < 0)
            {
                MakeNotice(candidateContent, candidateRows, "上の一覧で職を選ぶと、その職の候補がここに並びます。", font, rowH);
                return;
            }
            CabinetParams prm = GalaxyView.CabinetParamsInUse;
            int premier = CabinetAppointmentRules.FormalPremier(op.politics, op.faction, op.roster, out string premierProblem);
            if (premier < 0)
                MakeNotice(candidateContent, candidateRows, "任免権者（首相）不在のため任命できない：" + premierProblem, font, rowH);

            int rulingId = op.politics.government != null ? op.politics.government.partyId : -1;
            var list = new List<CandidateView>();
            int eligibleCount = 0;
            if (op.roster != null)
                for (int i = 0; i < op.roster.Count; i++)
                {
                    Person p = op.roster[i];
                    if (p == null || p.faction != op.faction) continue;
                    if (!string.IsNullOrEmpty(filterText) && (p.name == null || p.name.IndexOf(filterText, System.StringComparison.Ordinal) < 0)) continue;
                    string problem = CabinetAppointmentRules.CandidateProblem(op.politics, op.faction, op.politics.cabinet, p.id, premier, op.roster, prm);
                    if (problem == null) eligibleCount++;
                    else if (eligibleOnly || (!p.isPolitician && string.IsNullOrEmpty(filterText))) continue; // 全員表示でも軍人等は絞り込み時だけ（一覧を埋めない）
                    list.Add(new CandidateView
                    {
                        person = p, problem = problem,
                        eval = CabinetAppointmentRules.Evaluate(op.politics, p, selectedKind, rulingId, prm),
                    });
                }
            list.Sort((a, b) =>
            {
                bool ea = a.problem == null, eb = b.problem == null;
                if (ea != eb) return ea ? -1 : 1;
                int c = b.eval.score.CompareTo(a.eval.score);
                return c != 0 ? c : a.person.id.CompareTo(b.person.id);
            });

            if (list.Count == 0)
            {
                MakeNotice(candidateContent, candidateRows,
                    eligibleCount == 0 ? "適格な候補がいない（生存・同勢力の文民政治家で、首相・他の閣僚・知事・党三役・党首・野党の党員でない人物が必要）＝空席のまま"
                                       : "絞り込みに合う候補がいない", font, rowH);
                return;
            }

            int limit = Mathf.Min(list.Count, Mathf.Max(1, maxCandidateRows));
            for (int i = 0; i < limit; i++) MakeCandidateRow(list[i], font, rowH);
            if (list.Count > limit)
                MakeNotice(candidateContent, candidateRows, "ほか " + (list.Count - limit) + " 名（名前で絞り込んでください）", font, rowH);
        }

        private void MakeCandidateRow(CandidateView c, float font, float rowH)
        {
            bool sel = c.person.id == selectedCandidateId;
            int captured = c.person.id;
            RectTransform rt = MakeRow(candidateContent, candidateRows, "Candidate_" + captured, rowH, sel, () =>
            {
                selectedCandidateId = selectedCandidateId == captured ? -1 : captured;
                pending = PendingOp.なし;
                message = "";
                Rebuild();
            });
            MakeCell(rt, c.person.name, font, sel ? new Color(1f, 0.92f, 0.62f) : new Color(0.92f, 0.95f, 1f), 0f, 0.18f, 6f);
            MakeCell(rt, c.eval.reason, font, new Color(0.80f, 0.86f, 0.95f), 0.18f, 0.70f, 4f);
            MakeCell(rt, c.problem == null ? "<color=#8ce08c>適格</color>" : "<color=#ff9a7a>" + c.problem + "</color>",
                font, Color.white, 0.70f, 1f, 4f);
        }

        private void UpdateSelection(GalaxyView gv, GalaxyView.CabinetOperation op)
        {
            if (selectionLabel == null) return;
            if (eligibleToggleCaption != null) eligibleToggleCaption.text = eligibleOnly ? "適格者のみ表示中" : "不適格者も表示中";
            int max = GalaxyView.CabinetParamsInUse.maxDelegationYears;
            untilOffset = Mathf.Clamp(untilOffset, 0, max);
            if (scopeCaption != null) scopeCaption.text = "委任範囲：" + scope;
            if (untilCaption != null) untilCaption.text = "委任期限：SE" + (op.year + untilOffset) + "（最長+" + max + "年）";

            var sb = new StringBuilder(160);
            CabinetPost post = SelectedPost(op);
            if (post == null) sb.Append("選択：職なし（上の一覧から職を選ぶ）");
            else
            {
                sb.Append("選択：").Append(CabinetAppointmentRules.PostTitle(post))
                  .Append("　在任：").Append(gv.CabinetPersonName(post.holderId));
                sb.Append("　任命対象：").Append(selectedCandidateId >= 0 ? gv.CabinetPersonName(selectedCandidateId) : "（候補未選択）");
            }
            selectionLabel.text = sb.ToString();
        }

        private CabinetPost SelectedPost(GalaxyView.CabinetOperation op)
            => selectedMinistryId >= 0 && op.politics != null
                ? CabinetAppointmentRules.FindPost(op.politics.cabinet, selectedMinistryId, selectedKind) : null;

        // ===== 確認と実行（表示＝Check*、確定＝Player*＝同じ共通入口で再判定） =====

        private string Reason => reasonField != null ? reasonField.text : "";

        /// <summary>確定待ちの操作の確認結果（状態は変えない）。</summary>
        private AppointmentResult CheckPending(GalaxyView gv, GalaxyView.CabinetOperation op, out string detail)
        {
            detail = "";
            CabinetPost post = SelectedPost(op);
            if (pending == PendingOp.なし) return AppointmentResult.Deny("操作を選んでいない");
            if (post == null) return AppointmentResult.Deny("職を選んでいない");
            int until = op.year + untilOffset;
            CabinetPost vice = CabinetAppointmentRules.FindPost(op.politics.cabinet, post.ministryId, CabinetPostKind.副大臣);
            switch (pending)
            {
                case PendingOp.任命:
                    if (selectedCandidateId < 0) return AppointmentResult.Deny("候補を選んでいない");
                    detail = CabinetAppointmentRules.PostTitle(post) + " に " + gv.CabinetPersonName(selectedCandidateId) + " を任命";
                    return gv.CheckPlayerCabinetAppoint(post.ministryId, post.kind, selectedCandidateId);
                case PendingOp.解任:
                    detail = CabinetAppointmentRules.PostTitle(post) + " の " + gv.CabinetPersonName(post.holderId) + " を解任";
                    return gv.CheckPlayerCabinetDismiss(post.ministryId, post.kind);
                case PendingOp.委任:
                    detail = (vice != null ? CabinetAppointmentRules.PostTitle(vice) + " " + gv.CabinetPersonName(vice.holderId) : "副大臣")
                             + " へ " + scope + " を SE" + until + " まで委任";
                    return gv.CheckPlayerCabinetDelegate(post.ministryId, scope, until);
                default:
                    detail = (vice != null ? CabinetAppointmentRules.PostTitle(vice) + " " + gv.CabinetPersonName(vice.holderId) : "副大臣")
                             + " への委任（" + (vice != null ? vice.delegation.ToString() : "") + "）を撤回";
                    return gv.CheckPlayerCabinetRevokeDelegation(post.ministryId);
            }
        }

        private bool NeedsReason => pending == PendingOp.任命 || pending == PendingOp.解任 || pending == PendingOp.委任撤回;

        private void UpdateConfirm(GalaxyView gv, GalaxyView.CabinetOperation op)
        {
            if (confirmLabel == null) return;
            if (gv == null || op.politics == null || pending == PendingOp.なし)
            {
                confirmLabel.text = pending == PendingOp.なし
                    ? "<color=#9aa7b2>操作ボタンで確認を出し、内容を見てから［確定して実行］を押します。</color>"
                    : "";
                if (confirmButton != null) confirmButton.interactable = false;
                return;
            }
            AppointmentResult r = CheckPending(gv, op, out string detail);
            bool reasonOk = !NeedsReason || !string.IsNullOrWhiteSpace(Reason);
            var sb = new StringBuilder(200);
            sb.Append("確認［").Append(pending).Append("］").Append(detail);
            if (NeedsReason) sb.Append("　理由：").Append(reasonOk ? Reason.Trim() : "<color=#ff9a7a>未入力</color>");
            sb.Append('\n').Append(r.ok ? "<color=#8ce08c>可：" : "<color=#ff9a7a>不可：").Append(r.reason).Append("</color>");
            if (!r.ok && r.canPetition) sb.Append("<color=#ffb070>（権限者 ").Append(gv.CabinetPersonName(r.petitionToId)).Append("）</color>");
            confirmLabel.text = sb.ToString();
            if (confirmButton != null) confirmButton.interactable = r.ok && reasonOk;
        }

        private void Request(PendingOp op)
        {
            pending = op;
            message = "";
            Rebuild();
        }

        /// <summary>確定：Player* が実行時に同じ共通入口で再判定する（確認の結果を信用して書き換えない）。</summary>
        private void ExecutePending()
        {
            GalaxyView gv = View;
            if (gv == null) { message = "戦略マップがありません。"; Rebuild(); return; }
            GalaxyView.CabinetOperation op = gv.CabinetOperationForPlayer();
            CabinetPost post = SelectedPost(op);
            if (post == null || pending == PendingOp.なし) { message = "操作と職を選んでください。"; Rebuild(); return; }

            AppointmentResult r;
            switch (pending)
            {
                case PendingOp.任命: r = gv.PlayerCabinetAppoint(post.ministryId, post.kind, selectedCandidateId, Reason); break;
                case PendingOp.解任: r = gv.PlayerCabinetDismiss(post.ministryId, post.kind, Reason); break;
                case PendingOp.委任: r = gv.PlayerCabinetDelegate(post.ministryId, scope, op.year + untilOffset); break;
                default: r = gv.PlayerCabinetRevokeDelegation(post.ministryId, Reason); break;
            }
            message = (r.ok ? "<color=#8ce08c>実行：" : "<color=#ff9a7a>失敗：") + r.reason + "</color>";
            if (r.ok)
            {
                pending = PendingOp.なし;
                selectedCandidateId = -1;
                if (reasonField != null) reasonField.text = "";
            }
            Rebuild();
        }

        private void CycleScope()
        {
            scope = scope == CabinetDelegation.所管決裁 ? CabinetDelegation.所管政策
                  : scope == CabinetDelegation.所管政策 ? (CabinetDelegation.所管政策 | CabinetDelegation.所管決裁)
                  : CabinetDelegation.所管決裁;
            Rebuild();
        }

        private void CycleUntil()
        {
            int max = GalaxyView.CabinetParamsInUse.maxDelegationYears;
            untilOffset = untilOffset >= max ? 0 : untilOffset + 1;
            Rebuild();
        }

        // ===== 試験用の入口（UI の選択を再現するだけ＝操作者は差し替えない） =====

        // 試験アセンブリは UnityEngine.UI を参照しないため、UI 部品は値で返す
        public bool ListsHaveScrollbarsForTest
            => postScroll != null && postScroll.verticalScrollbar != null && candidateScroll != null && candidateScroll.verticalScrollbar != null;
        public bool ConfirmInteractableForTest => confirmButton != null && confirmButton.interactable;
        public int ActionButtonCountForTest
        {
            get
            {
                int n = 0;
                for (int i = 0; i < actionButtons.Count; i++)
                    if (actionButtons[i] != null) n++;
                return n;
            }
        }
        public bool FilterAndReasonFieldsExistForTest => filterField != null && reasonField != null;
        public string ConfirmTextForTest => confirmLabel != null ? confirmLabel.text : "";
        public string MessageTextForTest => message;
        public int CandidateRowCountForTest => candidateRows.Count;
        public static CabinetAppointmentPanel InstanceForTest => instance;

        public void SelectForTest(int ministryId, CabinetPostKind kind, int candidateId, string reason, PendingOp op)
        {
            selectedMinistryId = ministryId;
            selectedKind = kind;
            selectedCandidateId = candidateId;
            if (reasonField != null) reasonField.text = reason ?? "";
            pending = op;
            Rebuild();
        }

        public void ExecuteForTest() => ExecutePending();

        // ===== 大きさ・位置 =====

        private static float FontSize()
            => StrategyScreenLayoutRules.MinDesignForActual(BaseFont, MinFontPx, Screen.width);

        private static float RowHeight()
            => StrategyScreenLayoutRules.MinDesignForActual(BaseRow, MinRowPx, Screen.width);

        private void Layout()
        {
            if (frameRT == null) return;
            float uiScale = Screen.width > 0 ? Screen.width / StrategyScreenLayoutRules.ReferenceWidth : 1f;
            if (uiScale <= 0.0001f) uiScale = 1f;
            StrategyScreenLayout l = StrategyMapWindow.Layout;

            float width = StrategyScreenLayoutRules.MinDesignForActual(panelDesignWidth, MinPanelWidthPx, Screen.width);
            float mapDesignWidth = StrategyScreenLayoutRules.ToDesignWidth(l.mapWidth);
            width = Mathf.Min(width, Mathf.Max(MinPanelWidthPx, mapDesignWidth - 24f));

            float mapHeightDesign = (l.MapHeight * Screen.height) / uiScale;
            float height = Mathf.Clamp(panelDesignHeight, RowHeight() * 16f,
                                       Mathf.Max(RowHeight() * 16f, mapHeightDesign - 24f));

            frameRT.sizeDelta = new Vector2(width, height);
            float leftPx = l.mapLeft * Screen.width + 14f;
            float topPx = l.mapTop * Screen.height - 14f;
            frameRT.anchoredPosition = new Vector2(leftPx / uiScale, topPx / uiScale);
        }

        // ===== UI 構築 =====

        private void BuildUI()
        {
            EnsureEventSystem();
            jpFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");

            GameObject canvasObj = new GameObject("CabinetAppointmentCanvas");
            canvasObj.transform.SetParent(transform, false);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(StrategyScreenLayoutRules.ReferenceWidth, StrategyScreenLayoutRules.ReferenceHeight);
            scaler.matchWidthOrHeight = 0f;
            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject frame = new GameObject("Frame", typeof(RectTransform));
            frame.transform.SetParent(canvasObj.transform, false);
            frameRT = frame.GetComponent<RectTransform>();
            frameRT.anchorMin = frameRT.anchorMax = Vector2.zero;
            frameRT.pivot = new Vector2(0f, 1f);
            frame.AddComponent<Image>().color = panelColor;
            Outline outline = frame.AddComponent<Outline>();
            outline.effectColor = new Color(0.90f, 0.82f, 0.55f, 0.7f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            VerticalLayoutGroup vlg = frame.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 8, 10);
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            WindowChrome.AddTitleBarLayout(frameRT, "内閣人事", Close);

            float font = FontSize();
            float rowH = RowHeight();

            headerLabel = MakeSectionLabel(frameRT, "", font, new Color(1f, 0.88f, 0.55f), rowH * 1.8f);
            headerLabel.textWrappingMode = TextWrappingModes.Normal;
            headerLabel.richText = true;

            postContent = MakeScrollArea(frameRT, "PostList", PostListShare, rowH * 5f, out postScroll);

            selectionLabel = MakeSectionLabel(frameRT, "", font, new Color(0.86f, 0.92f, 1f), rowH);
            BuildFilterRow(frameRT, font, rowH);
            candidateContent = MakeScrollArea(frameRT, "CandidateList", CandidateListShare, rowH * 5f, out candidateScroll);

            reasonField = MakeInputField(frameRT, "ReasonField", "任命・解任・委任撤回の理由を入力（必須）", font, rowH, null);
            BuildDelegationRow(frameRT, font, rowH);
            BuildActionRow(frameRT, font, rowH);

            confirmLabel = MakeSectionLabel(frameRT, "", font, new Color(0.92f, 0.94f, 1f), rowH * 1.8f);
            confirmLabel.textWrappingMode = TextWrappingModes.Normal;
            confirmLabel.richText = true;

            messageLabel = MakeSectionLabel(frameRT, "", font, new Color(0.86f, 0.92f, 1f), rowH * 1.3f);
            messageLabel.textWrappingMode = TextWrappingModes.Normal;
            messageLabel.overflowMode = TextOverflowModes.Truncate;
            messageLabel.richText = true;

            Layout();
        }

        private void BuildFilterRow(RectTransform parent, float font, float rowH)
        {
            RectTransform row = MakeHRow(parent, "Filter", rowH);
            filterField = MakeInputField(row, "FilterField", "候補を名前で絞り込む（空欄＝すべて）", font, rowH, v => { filterText = v ?? ""; Rebuild(); });
            filterField.GetComponent<LayoutElement>().flexibleWidth = 3f;
            Button b = MakeButton(row, "", font, () => { eligibleOnly = !eligibleOnly; Rebuild(); }, out eligibleToggleCaption);
            b.GetComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private void BuildDelegationRow(RectTransform parent, float font, float rowH)
        {
            RectTransform row = MakeHRow(parent, "Delegation", rowH);
            MakeButton(row, "", font, CycleScope, out scopeCaption);
            MakeButton(row, "", font, CycleUntil, out untilCaption);
        }

        private void BuildActionRow(RectTransform parent, float font, float rowH)
        {
            RectTransform row = MakeHRow(parent, "Actions", rowH * 1.1f);
            actionButtons.Add(MakeButton(row, "任命を確認", font, () => Request(PendingOp.任命), out _));
            actionButtons.Add(MakeButton(row, "解任を確認", font, () => Request(PendingOp.解任), out _));
            actionButtons.Add(MakeButton(row, "副大臣へ委任を確認", font, () => Request(PendingOp.委任), out _));
            actionButtons.Add(MakeButton(row, "委任撤回を確認", font, () => Request(PendingOp.委任撤回), out _));
            confirmButton = MakeButton(row, "確定して実行", font, ExecutePending, out _);
            confirmButton.interactable = false;
            MakeButton(row, "取消", font, () => { pending = PendingOp.なし; Rebuild(); }, out _);
        }

        private RectTransform MakeHRow(RectTransform parent, string name, float height)
        {
            GameObject row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            LayoutElement le = row.AddComponent<LayoutElement>();
            le.minHeight = height; le.preferredHeight = height; le.flexibleHeight = 0f;
            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            return (RectTransform)row.transform;
        }

        private TMP_InputField MakeInputField(RectTransform parent, string name, string placeholderText, float font, float rowH,
            UnityEngine.Events.UnityAction<string> onChanged)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            // ★TMP_InputField は OnEnable で textComponent/textViewport を要求する＝組み立て終わるまで無効（FleetOrderPanel と同じ）
            go.SetActive(false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = rowH; le.preferredHeight = rowH; le.flexibleHeight = 0f;
            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.13f, 0.19f, 1f);

            GameObject area = new GameObject("TextArea", typeof(RectTransform));
            area.transform.SetParent(go.transform, false);
            RectTransform areaRT = area.GetComponent<RectTransform>();
            areaRT.anchorMin = Vector2.zero; areaRT.anchorMax = Vector2.one;
            areaRT.offsetMin = new Vector2(10f, 2f); areaRT.offsetMax = new Vector2(-10f, -2f);
            area.AddComponent<RectMask2D>();

            TextMeshProUGUI placeholder = MakeText(areaRT, placeholderText, font, new Color(0.55f, 0.62f, 0.72f));
            StretchFull(placeholder.rectTransform);
            TextMeshProUGUI text = MakeText(areaRT, "", font, new Color(0.95f, 0.97f, 1f));
            StretchFull(text.rectTransform);
            text.richText = false;

            TMP_InputField field = go.AddComponent<TMP_InputField>();
            field.textViewport = areaRT;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.targetGraphic = bg;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.richText = false;
            if (jpFont != null) field.fontAsset = jpFont;
            field.pointSize = font;
            if (onChanged != null) field.onValueChanged.AddListener(onChanged);
            else field.onValueChanged.AddListener(_ => { if (pending != PendingOp.なし) UpdateConfirmNow(); });
            go.SetActive(true);
            return field;
        }

        /// <summary>理由の入力に合わせて確認表示だけ更新する（一覧は作り直さない＝入力中の手触りを保つ）。</summary>
        private void UpdateConfirmNow()
        {
            GalaxyView gv = View;
            UpdateConfirm(gv, gv != null ? gv.CabinetOperationForPlayer() : default);
        }

        private RectTransform MakeScrollArea(RectTransform parent, string name, float flexibleHeight, float minHeight, out ScrollRect scroll)
        {
            GameObject scrollGo = new GameObject(name, typeof(RectTransform));
            scrollGo.transform.SetParent(parent, false);
            LayoutElement le = scrollGo.AddComponent<LayoutElement>();
            le.flexibleHeight = flexibleHeight;
            le.minHeight = minHeight;
            scrollGo.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.06f, 0.9f);

            scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 26f;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(scrollGo.transform, false);
            RectTransform vpRT = viewport.GetComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = new Vector2(4f, 4f); vpRT.offsetMax = new Vector2(-4f, -4f);
            viewport.AddComponent<RectMask2D>();

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform cRT = content.GetComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0f, 1f); cRT.anchorMax = new Vector2(1f, 1f);
            cRT.pivot = new Vector2(0.5f, 1f);
            cRT.sizeDelta = new Vector2(0f, 0f);   // 左右のはみ出しを防ぐ（既知の罠）

            VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = vpRT;
            scroll.content = cRT;
            UiScrollbars.Attach(scroll);   // #H スクロールできることを画面で示す
            return cRT;
        }

        private RectTransform MakeRow(RectTransform parent, List<GameObject> into, string name, float rowH, bool selected,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = rowH; le.preferredHeight = rowH;
            Image img = go.AddComponent<Image>();
            img.color = selected ? new Color(0.20f, 0.32f, 0.46f, 1f) : new Color(0.11f, 0.15f, 0.22f, 1f);
            Button btn = go.AddComponent<Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            into.Add(go);
            return (RectTransform)go.transform;
        }

        private void MakeGroupHeader(RectTransform parent, List<GameObject> into, string text, float font, float rowH)
        {
            GameObject go = new GameObject("Group", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = rowH; le.preferredHeight = rowH;
            go.AddComponent<Image>().color = new Color(0.16f, 0.14f, 0.10f, 1f);
            TextMeshProUGUI t = MakeCell((RectTransform)go.transform, text, font, new Color(0.91f, 0.88f, 0.69f), 0f, 1f, 8f);
            t.fontStyle = FontStyles.Bold;
            into.Add(go);
        }

        private void MakeNotice(RectTransform parent, List<GameObject> into, string text, float font, float rowH)
        {
            GameObject go = new GameObject("Notice", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = rowH; le.preferredHeight = rowH;
            MakeCell((RectTransform)go.transform, text, font, new Color(0.78f, 0.84f, 0.94f), 0f, 1f, 8f);
            into.Add(go);
        }

        private Button MakeButton(Transform parent, string caption, float font, UnityEngine.Events.UnityAction onClick, out TextMeshProUGUI label)
        {
            GameObject go = new GameObject("Button_" + caption, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>();
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.16f, 0.20f, 0.30f, 1f);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            label = MakeText((RectTransform)go.transform, caption, font, new Color(0.94f, 0.96f, 1f));
            label.alignment = TextAlignmentOptions.Center;
            label.overflowMode = TextOverflowModes.Ellipsis;
            RectTransform rt = label.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(4f, 0f); rt.offsetMax = new Vector2(-4f, 0f);
            return btn;
        }

        private TextMeshProUGUI MakeSectionLabel(RectTransform parent, string text, float font, Color color, float height)
        {
            GameObject go = new GameObject("Section", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = height; le.preferredHeight = height; le.flexibleHeight = 0f;
            TextMeshProUGUI t = MakeText((RectTransform)go.transform, text, font, color);
            t.overflowMode = TextOverflowModes.Ellipsis;
            RectTransform rt = t.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(4f, 0f); rt.offsetMax = new Vector2(-4f, 0f);
            return t;
        }

        private TextMeshProUGUI MakeCell(RectTransform parent, string text, float font, Color color, float x0, float x1, float leftMargin)
        {
            TextMeshProUGUI t = MakeText(parent, text, font, color);
            t.alignment = TextAlignmentOptions.Left;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.richText = true;
            t.raycastTarget = false;
            RectTransform rt = t.rectTransform;
            rt.anchorMin = new Vector2(x0, 0f); rt.anchorMax = new Vector2(Mathf.Min(1f, x1), 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            t.margin = new Vector4(leftMargin, 0f, 4f, 0f);
            return t;
        }

        private TextMeshProUGUI MakeText(RectTransform parent, string text, float size, Color color)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Left;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            if (jpFont != null) t.font = jpFont;
            return t;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void ClearRows(List<GameObject> rows)
        {
            for (int i = 0; i < rows.Count; i++) if (rows[i] != null) Destroy(rows[i]);
            rows.Clear();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }
}
