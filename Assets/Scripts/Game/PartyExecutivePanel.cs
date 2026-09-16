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
    /// 戦略画面の「党人事」メニュー（#2768 #159 #165）。党三役（幹事長・政調会長・総務会長）を党首がクリックで任免する。
    /// 上メニューの観測ウィンドウ（政治タブ）と政治観測（O）の窓から開く。
    ///
    /// 表示：操作者（主人公）と各党での立場・党首、選んだ党の三役（在任者・派閥・当選年功・就任年・任命者・暫定期限・役割・空席理由）と直近の任免履歴。
    /// 操作：党を選ぶ→職を選ぶ→候補一覧（名前絞り込み・適格者優先・スクロール）から人物を選ぶ→理由を入力→確認→確定。解任も理由必須で同じ確認→確定。
    /// 党首は総裁選で選ぶため任命の入口を作らない。
    ///
    /// 操作者は <see cref="GalaxyView.PlayerCharacter"/> だけ（任意の人物を渡せない）。可否の表示は GalaxyView の CheckPlayerParty*、
    /// 実行は PlayerParty*＝どちらも <see cref="PartyExecutiveRules"/> の同じ判定を通り、確定時にもう一度判定する。
    /// 在任・履歴は Party.posts / postHistory だけ（独自の台帳を作らない）。党三役は政府の決裁・国庫・軍の指揮権を与えない。
    ///
    /// 作法は <see cref="CabinetAppointmentPanel"/> と同型：非モーダル窓・タイトルバーは <see cref="WindowChrome"/>・
    /// Esc は <see cref="UIWindowStack"/> へ登録するだけ・一覧は <see cref="UiScrollbars"/> で見えるバー。
    /// </summary>
    public class PartyExecutivePanel : MonoBehaviour
    {
        [Header("外観")]
        [Tooltip("Canvas の描画順（内閣人事1002と同格・観測オーバーレイ1090より後ろ）")]
        public int canvasSortingOrder = 1003;

        [Tooltip("パネルの幅（設計ピクセル）")]
        public float panelDesignWidth = 1080f;

        [Tooltip("パネルの高さ（設計ピクセル・MAP の高さに収まるよう自動で切り詰める）")]
        public float panelDesignHeight = 900f;

        [Tooltip("パネル背景色")]
        public Color panelColor = new Color(0.05f, 0.06f, 0.09f, 0.97f);

        [Header("更新")]
        [Tooltip("一覧を作り直す間隔（実時間秒・ポーズ中も進む）")]
        public float refreshInterval = 1f;

        [Header("一覧")]
        [Tooltip("候補一覧に並べる最大人数（超えた分は件数を明示し、絞り込みで探す）")]
        public int maxCandidateRows = 80;

        [Tooltip("選んだ党の直近の任免履歴を並べる件数")]
        public int maxHistoryRows = 5;

        private const float BaseFont = 16f;
        private const float MinFontPx = 13f;
        private const float BaseRow = 30f;
        private const float MinRowPx = 26f;
        private const float MinPanelWidthPx = 640f;
        private const float PostListShare = 1f;
        private const float CandidateListShare = 1f;

        /// <summary>確定待ちの操作（確認表示と実行を同じ種類で結ぶ）。</summary>
        public enum PendingOp { なし, 任命, 解任 }

        private static PartyExecutivePanel instance;

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
        private Button confirmButton;
        private object escWindowToken;

        private float refreshTimer;
        private string message = "";
        private string filterText = "";
        private bool eligibleOnly = true;

        // 選択（IDで持つ＝一覧を作り直しても保つ）
        private int selectedPartyId = -1;
        private bool postSelected;
        private PartyPost selectedPost = PartyPost.幹事長;
        private int selectedCandidateId = -1;
        private PendingOp pending = PendingOp.なし;

        private readonly List<GameObject> postRows = new List<GameObject>();
        private readonly List<GameObject> candidateRows = new List<GameObject>();
        private readonly List<Button> actionButtons = new List<Button>();

        // ===== 自動生成（CabinetAppointmentPanel と同型・手置き不要）=====

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
            if (FindAnyObjectByType<PartyExecutivePanel>() != null) return;
            new GameObject("PartyExecutivePanel").AddComponent<PartyExecutivePanel>();
        }

        // ===== static の窓口 =====

        public static bool IsOpen => instance != null && instance.canvas != null && instance.canvas.gameObject.activeSelf;

        public static void Show()
        {
            PartyExecutivePanel p = EnsureInstance();
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

        private static PartyExecutivePanel EnsureInstance()
        {
            if (instance != null) return instance;
            instance = FindAnyObjectByType<PartyExecutivePanel>();
            if (instance == null) instance = new GameObject("PartyExecutivePanel").AddComponent<PartyExecutivePanel>();
            return instance;
        }

        // ===== ライフサイクル =====

        private void Awake()
        {
            instance = this;
            BuildUI();
            if (canvas != null) canvas.gameObject.SetActive(false);
            escWindowToken = UIWindowStack.Register(
                () => canvas != null && canvas.gameObject.activeSelf, Close, canvasSortingOrder, "党人事");
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

            GalaxyView.CabinetOperation op = gv.PartyOperationForPlayer();
            if (op.actor == null || op.politics == null || op.problem != null)
            {
                MakeNotice(postContent, postRows, op.problem ?? "党人事の材料が組めません。", font, rowH);
                SetHeader("党人事：" + (op.problem ?? ""));
                UpdateConfirm(gv, op);
                if (messageLabel != null) messageLabel.text = message;
                return;
            }

            BuildHeader(op);
            BuildPartyRows(gv, op, font, rowH);
            BuildCandidateRows(op, font, rowH);
            UpdateSelection(gv, op);
            UpdateConfirm(gv, op);
            if (messageLabel != null) messageLabel.text = message;
        }

        private void SetHeader(string text)
        {
            if (headerLabel != null) headerLabel.text = text;
        }

        private void BuildHeader(GalaxyView.CabinetOperation op)
        {
            var sb = new StringBuilder(256);
            sb.Append("操作者：").Append(op.actor.name).Append("　SE").Append(op.year);
            Party own = ElectionCycleRules.PartyOf(op.politics.parties, op.actor.id);
            sb.Append("　所属：").Append(own != null ? own.partyName : "無所属");
            sb.Append("\n<color=#9aa7b2>党三役の任免権者はその党の党首だけ（党首は総裁選で選ぶ）。党三役は政府の決裁・国庫・軍の指揮権を持たない。</color>");
            SetHeader(sb.ToString());
        }

        /// <summary>操作者のその党での立場（表示だけ。権限は Core が判定）。</summary>
        private static string ActorRoleText(GalaxyView.CabinetOperation op, Party party, int leader)
        {
            if (op.actor.id == leader) return "<color=#8ce08c>あなたが党首＝三役の任免権者</color>";
            for (int k = 0; k < PartyExecutiveRules.ExecutivePosts.Length; k++)
                if (PartyOrganizationRules.HolderOf(party, PartyExecutiveRules.ExecutivePosts[k]) == op.actor.id)
                    return "<color=#ffb070>あなたは" + PartyExecutiveRules.ExecutivePosts[k] + "＝任免権なし</color>";
            if (PartyOrganizationRules.IsMember(party, op.actor.id)) return "<color=#ffb070>党員＝任免権なし</color>";
            return "<color=#9aa7b2>他党＝権限なし</color>";
        }

        private void BuildPartyRows(GalaxyView gv, GalaxyView.CabinetOperation op, float font, float rowH)
        {
            List<Party> parties = op.politics.parties;
            for (int i = 0; i < parties.Count; i++)
            {
                Party party = parties[i];
                if (party == null) continue;
                MakePartyRow(gv, op, party, font, rowH);
                if (party.id != selectedPartyId) continue;
                for (int k = 0; k < PartyExecutiveRules.ExecutivePosts.Length; k++)
                    MakePostRow(gv, op, party, PartyExecutiveRules.ExecutivePosts[k], font, rowH);
                BuildHistoryRows(gv, party, font, rowH);
            }
        }

        private void MakePartyRow(GalaxyView gv, GalaxyView.CabinetOperation op, Party party, float font, float rowH)
        {
            bool sel = party.id == selectedPartyId;
            int captured = party.id;
            RectTransform rt = MakeRow(postContent, postRows, "Party_" + captured, rowH, sel, new Color(0.16f, 0.14f, 0.10f, 1f), () =>
            {
                selectedPartyId = selectedPartyId == captured ? -1 : captured;
                postSelected = false;
                selectedCandidateId = -1;
                pending = PendingOp.なし;
                message = "";
                Rebuild();
            });
            int leader = PartyExecutiveRules.FormalLeader(party, op.faction, op.roster, out string leaderProblem);
            TextMeshProUGUI nameCell = MakeCell(rt, (sel ? "▼ " : "▶ ") + party.partyName, font, new Color(0.91f, 0.88f, 0.69f), 0f, 0.24f, 8f);
            nameCell.fontStyle = FontStyles.Bold;
            MakeCell(rt, "党首 " + (leader >= 0 ? gv.CabinetPersonName(leader) : "<color=#ff7a6a>不在</color>（" + leaderProblem + "）"),
                font, new Color(1f, 0.90f, 0.78f), 0.24f, 0.60f, 4f);
            MakeCell(rt, ActorRoleText(op, party, leader), font, Color.white, 0.60f, 1f, 4f);
        }

        private void MakePostRow(GalaxyView gv, GalaxyView.CabinetOperation op, Party party, PartyPost post, float font, float rowH)
        {
            bool sel = postSelected && party.id == selectedPartyId && post == selectedPost;
            RectTransform rt = MakeRow(postContent, postRows, "Post_" + party.id + "_" + post, rowH, sel, new Color(0.11f, 0.15f, 0.22f, 1f), () =>
            {
                bool same = postSelected && selectedPost == post;
                postSelected = !same;
                selectedPost = post;
                selectedCandidateId = -1;
                pending = PendingOp.なし;
                message = "";
                Rebuild();
            });

            MakeCell(rt, "　" + post, font, sel ? new Color(1f, 0.92f, 0.62f) : new Color(0.92f, 0.95f, 1f), 0f, 0.14f, 8f);
            PartyAppointment a = PartyExecutiveRules.AppointmentOf(party, post);
            if (a != null)
            {
                SeniorityInfo si = PartySeniorityRules.InfoOf(op.politics, a.holderId, PartySeniorityParams.Default);
                PartyFaction pf = CabinetAppointmentRules.FactionOf(party, a.holderId);
                MakeCell(rt, gv.CabinetPersonName(a.holderId), font, new Color(1f, 0.90f, 0.78f), 0.14f, 0.30f, 4f);
                var sb = new StringBuilder(96);
                sb.Append(pf != null ? pf.name : "無派閥").Append("・").Append(SeniorityText(si));
                if (a.appointedYear > 0) sb.Append("・SE").Append(a.appointedYear).Append("就任");
                sb.Append("・任命 ").Append(a.appointedById >= 0 ? gv.CabinetPersonName(a.appointedById) : "不明（旧セーブ）");
                MakeCell(rt, sb.ToString(), font, new Color(0.80f, 0.86f, 0.95f), 0.30f, 0.66f, 4f);
                string note = a.caretakerUntilYear > 0
                    ? "<color=#ffb070>党首不在の暫定・SE" + a.caretakerUntilYear + "まで</color>"
                    : PartyExecutiveRules.RoleText(post);
                MakeCell(rt, note, font, new Color(0.72f, 0.82f, 0.90f), 0.66f, 1f, 4f);
            }
            else
            {
                MakeCell(rt, "<color=#ff7a6a>空席</color>", font, Color.white, 0.14f, 0.30f, 4f);
                string why = PartyExecutiveRules.VacancyReason(party, post);
                MakeCell(rt, string.IsNullOrEmpty(why) ? PartyExecutiveRules.RoleText(post) : "<color=#ffb070>" + why + "</color>",
                    font, new Color(0.72f, 0.82f, 0.90f), 0.30f, 1f, 4f);
            }
        }

        /// <summary>選んだ党の直近の任免履歴（新しい順・台帳は Party.postHistory のまま）。</summary>
        private void BuildHistoryRows(GalaxyView gv, Party party, float font, float rowH)
        {
            if (party.postHistory == null || party.postHistory.Count == 0)
            {
                MakeNotice(postContent, postRows, "　任免履歴：なし", font, rowH);
                return;
            }
            int shown = 0;
            for (int i = party.postHistory.Count - 1; i >= 0 && shown < Mathf.Max(0, maxHistoryRows); i--)
            {
                AppointmentHistoryEntry e = party.postHistory[i];
                if (e == null) continue;
                MakeNotice(postContent, postRows,
                    "　<color=#6f8a9a>履歴 SE" + e.year + " " + e.postLabel + " " + (e.personId >= 0 ? gv.CabinetPersonName(e.personId) : "")
                    + " " + e.action + (string.IsNullOrEmpty(e.reason) ? "" : "（" + e.reason + "）") + "</color>", font, rowH);
                shown++;
            }
        }

        private static string SeniorityText(SeniorityInfo si)
            => si.historyRegistered ? "国政当選" + si.nationalWins + "回（" + si.tier + "）" : "当選履歴未登録";

        /// <summary>候補の表示用の束（評価・資格の問題は Core の関数から取る）。</summary>
        private struct CandidateView
        {
            public Person person;
            public string problem;
            public float score;
            public string reason;
            public string partyName;
        }

        private void BuildCandidateRows(GalaxyView.CabinetOperation op, float font, float rowH)
        {
            Party party = SelectedParty(op);
            if (party == null || !postSelected)
            {
                MakeNotice(candidateContent, candidateRows, "上の一覧で党を開き、職（幹事長・政調会長・総務会長）を選ぶと候補がここに並びます。", font, rowH);
                return;
            }
            CabinetParams prm = GalaxyView.CabinetParamsInUse;
            int leader = PartyExecutiveRules.FormalLeader(party, op.faction, op.roster, out string leaderProblem);
            if (leader < 0)
                MakeNotice(candidateContent, candidateRows, "任免権者（党首）不在のため任命できない：" + leaderProblem, font, rowH);

            var list = new List<CandidateView>();
            int eligibleCount = 0;
            if (op.roster != null)
                for (int i = 0; i < op.roster.Count; i++)
                {
                    Person p = op.roster[i];
                    if (p == null || p.faction != op.faction) continue;
                    if (!string.IsNullOrEmpty(filterText) && (p.name == null || p.name.IndexOf(filterText, System.StringComparison.Ordinal) < 0)) continue;
                    bool member = PartyOrganizationRules.IsMember(party, p.id);
                    string problem = PartyExecutiveRules.CandidateProblem(op.politics, op.faction, party, p.id, op.roster, prm);
                    if (problem == null) eligibleCount++;
                    else if (eligibleOnly || (!member && string.IsNullOrEmpty(filterText))) continue; // 全員表示でも他党・無所属は絞り込み時だけ
                    Party own = ElectionCycleRules.PartyOf(op.politics.parties, p.id);
                    float score = PartyExecutiveRules.Score(op.politics, party, p, prm, out string why);
                    list.Add(new CandidateView
                    {
                        person = p, problem = problem, score = score, reason = why,
                        partyName = own != null ? own.partyName : "無所属",
                    });
                }
            list.Sort((a, b) =>
            {
                bool ea = a.problem == null, eb = b.problem == null;
                if (ea != eb) return ea ? -1 : 1;
                int c = b.score.CompareTo(a.score);
                return c != 0 ? c : a.person.id.CompareTo(b.person.id);
            });

            if (list.Count == 0)
            {
                MakeNotice(candidateContent, candidateRows,
                    eligibleCount == 0 ? "適格な候補がいない（生存・自由な党員の政治家で、党首・首相・知事・他の三役・閣僚でない人物が必要）＝空席のまま"
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
            RectTransform rt = MakeRow(candidateContent, candidateRows, "Candidate_" + captured, rowH, sel, new Color(0.11f, 0.15f, 0.22f, 1f), () =>
            {
                selectedCandidateId = selectedCandidateId == captured ? -1 : captured;
                pending = PendingOp.なし;
                message = "";
                Rebuild();
            });
            MakeCell(rt, c.person.name, font, sel ? new Color(1f, 0.92f, 0.62f) : new Color(0.92f, 0.95f, 1f), 0f, 0.16f, 6f);
            MakeCell(rt, c.partyName + "・" + c.reason, font, new Color(0.80f, 0.86f, 0.95f), 0.16f, 0.70f, 4f);
            MakeCell(rt, c.problem == null ? "<color=#8ce08c>適格</color>" : "<color=#ff9a7a>" + c.problem + "</color>",
                font, Color.white, 0.70f, 1f, 4f);
        }

        private Party SelectedParty(GalaxyView.CabinetOperation op)
            => selectedPartyId >= 0 && op.politics != null ? ElectionCycleRules.FindParty(op.politics.parties, selectedPartyId) : null;

        private void UpdateSelection(GalaxyView gv, GalaxyView.CabinetOperation op)
        {
            if (selectionLabel == null) return;
            if (eligibleToggleCaption != null) eligibleToggleCaption.text = eligibleOnly ? "適格者のみ表示中" : "不適格者も表示中";
            var sb = new StringBuilder(160);
            Party party = SelectedParty(op);
            if (party == null) sb.Append("選択：党なし（上の一覧から党を開く）");
            else if (!postSelected) sb.Append("選択：").Append(party.partyName).Append("　職なし（幹事長・政調会長・総務会長から選ぶ）");
            else
            {
                sb.Append("選択：").Append(party.partyName).Append(' ').Append(selectedPost)
                  .Append("　在任：").Append(gv.CabinetPersonName(PartyOrganizationRules.HolderOf(party, selectedPost)))
                  .Append("　任命対象：").Append(selectedCandidateId >= 0 ? gv.CabinetPersonName(selectedCandidateId) : "（候補未選択）");
            }
            selectionLabel.text = sb.ToString();
        }

        // ===== 確認と実行（表示＝CheckPlayerParty*、確定＝PlayerParty*＝同じ共通入口で再判定） =====

        private string Reason => reasonField != null ? reasonField.text : "";

        /// <summary>確定待ちの操作の確認結果（状態は変えない）。</summary>
        private AppointmentResult CheckPending(GalaxyView gv, GalaxyView.CabinetOperation op, out string detail)
        {
            detail = "";
            if (pending == PendingOp.なし) return AppointmentResult.Deny("操作を選んでいない");
            Party party = SelectedParty(op);
            if (party == null || !postSelected) return AppointmentResult.Deny("党と職を選んでいない");
            if (pending == PendingOp.任命)
            {
                if (selectedCandidateId < 0) return AppointmentResult.Deny("候補を選んでいない");
                detail = party.partyName + " " + selectedPost + " に " + gv.CabinetPersonName(selectedCandidateId) + " を任命";
                return gv.CheckPlayerPartyAppoint(party.id, selectedPost, selectedCandidateId);
            }
            detail = party.partyName + " " + selectedPost + " の " + gv.CabinetPersonName(PartyOrganizationRules.HolderOf(party, selectedPost)) + " を解任";
            return gv.CheckPlayerPartyDismiss(party.id, selectedPost);
        }

        private void UpdateConfirm(GalaxyView gv, GalaxyView.CabinetOperation op)
        {
            if (confirmLabel == null) return;
            if (gv == null || op.politics == null || op.problem != null || pending == PendingOp.なし)
            {
                confirmLabel.text = pending == PendingOp.なし
                    ? "<color=#9aa7b2>操作ボタンで確認を出し、内容を見てから［確定して実行］を押します。</color>"
                    : "<color=#ff9a7a>不可：" + (op.problem ?? "戦略マップがない") + "</color>";
                if (confirmButton != null) confirmButton.interactable = false;
                return;
            }
            AppointmentResult r = CheckPending(gv, op, out string detail);
            bool reasonOk = !string.IsNullOrWhiteSpace(Reason);
            var sb = new StringBuilder(200);
            sb.Append("確認［").Append(pending).Append("］").Append(detail);
            sb.Append("　理由：").Append(reasonOk ? Reason.Trim() : "<color=#ff9a7a>未入力</color>");
            sb.Append('\n').Append(r.ok ? "<color=#8ce08c>可：" : "<color=#ff9a7a>不可：").Append(r.reason).Append("</color>");
            if (!r.ok && r.canPetition) sb.Append("<color=#ffb070>（任免権者 ").Append(gv.CabinetPersonName(r.petitionToId)).Append("）</color>");
            confirmLabel.text = sb.ToString();
            if (confirmButton != null) confirmButton.interactable = r.ok && reasonOk;
        }

        private void Request(PendingOp op)
        {
            pending = op;
            message = "";
            Rebuild();
        }

        /// <summary>確定：PlayerParty* が実行時に同じ共通入口で再判定する（確認の結果を信用して書き換えない）。</summary>
        private void ExecutePending()
        {
            GalaxyView gv = View;
            if (gv == null) { message = "戦略マップがありません。"; Rebuild(); return; }
            GalaxyView.CabinetOperation op = gv.PartyOperationForPlayer();
            Party party = SelectedParty(op);
            if (party == null || !postSelected || pending == PendingOp.なし) { message = "操作と党・職を選んでください。"; Rebuild(); return; }

            AppointmentResult r = pending == PendingOp.任命
                ? gv.PlayerPartyAppoint(party.id, selectedPost, selectedCandidateId, Reason)
                : gv.PlayerPartyDismiss(party.id, selectedPost, Reason);
            message = (r.ok ? "<color=#8ce08c>実行：" : "<color=#ff9a7a>失敗：") + r.reason + "</color>";
            if (r.ok)
            {
                pending = PendingOp.なし;
                selectedCandidateId = -1;
                if (reasonField != null) reasonField.text = "";
            }
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
        public int PostRowCountForTest => postRows.Count;
        public static PartyExecutivePanel InstanceForTest => instance;

        public void SelectForTest(int partyId, PartyPost post, int candidateId, string reason, PendingOp op)
        {
            selectedPartyId = partyId;
            postSelected = true;
            selectedPost = post;
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

            GameObject canvasObj = new GameObject("PartyExecutiveCanvas");
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

            WindowChrome.AddTitleBarLayout(frameRT, "党人事", Close);

            float font = FontSize();
            float rowH = RowHeight();

            headerLabel = MakeSectionLabel(frameRT, "", font, new Color(1f, 0.88f, 0.55f), rowH * 1.8f);
            headerLabel.textWrappingMode = TextWrappingModes.Normal;
            headerLabel.richText = true;

            postContent = MakeScrollArea(frameRT, "PartyPostList", PostListShare, rowH * 5f, out postScroll);

            selectionLabel = MakeSectionLabel(frameRT, "", font, new Color(0.86f, 0.92f, 1f), rowH);
            BuildFilterRow(frameRT, font, rowH);
            candidateContent = MakeScrollArea(frameRT, "CandidateList", CandidateListShare, rowH * 5f, out candidateScroll);

            reasonField = MakeInputField(frameRT, "ReasonField", "任命・解任の理由を入力（必須）", font, rowH, null);
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

        private void BuildActionRow(RectTransform parent, float font, float rowH)
        {
            RectTransform row = MakeHRow(parent, "Actions", rowH * 1.1f);
            actionButtons.Add(MakeButton(row, "任命を確認", font, () => Request(PendingOp.任命), out _));
            actionButtons.Add(MakeButton(row, "解任を確認", font, () => Request(PendingOp.解任), out _));
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
            // ★TMP_InputField は OnEnable で textComponent/textViewport を要求する＝組み立て終わるまで無効（CabinetAppointmentPanel と同じ）
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
            UpdateConfirm(gv, gv != null ? gv.PartyOperationForPlayer() : default);
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

        private RectTransform MakeRow(RectTransform parent, List<GameObject> into, string name, float rowH, bool selected, Color baseColor,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = rowH; le.preferredHeight = rowH;
            Image img = go.AddComponent<Image>();
            img.color = selected ? new Color(0.20f, 0.32f, 0.46f, 1f) : baseColor;
            Button btn = go.AddComponent<Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            into.Add(go);
            return (RectTransform)go.transform;
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
