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
    /// 戦略画面の「官僚人事」メニュー（#141）。上メニューの観測ウィンドウ（内政タブ）からクリックで開く。
    ///
    /// <para>表示：操作者（主人公）とその役職、省庁一覧（配属定員／在籍数／段ごとの空席）、選択省の在任者
    /// （職位・在職年・官位・考課）、操作種別、候補／対象者、理由、確認欄、実行結果、直近の人事履歴。</para>
    ///
    /// <para>操作：操作種別（配属／異動／昇任／降任／解任）→ 省 → 対象者 → 理由 →［内容を確認］→［直接実行する／上申する］。
    /// 誤操作を防ぐ二段階確認で、選択や理由を変えると確認は解除される。</para>
    ///
    /// <para><b>この画面は人事の可否を判定しない</b>＝資格・空席・在職年・承認権限・上申先は
    /// <see cref="GalaxyView.PreviewPlayerCivilServicePost"/>（＝<see cref="CivilServicePostRules.Check"/>）の戻り値だけで表示し、
    /// 確定は <see cref="GalaxyView.SubmitPlayerCivilServicePost"/> だけを呼ぶ（確定時に共通入口がもう一度判定する）。
    /// 台帳（<see cref="FactionState.civilService"/>）や <see cref="Ministry.staffIds"/> を画面から直接書き換えない。
    /// 画面が自前で決めるのは「どの要求を組むか」だけ＝昇任は1段上・降任は1段下・異動/解任は現職の段（<see cref="BuildPlan"/>）。
    /// 最上段の昇任・最下段の降任は要求を組めないので確定不能にし、理由を出す。</para>
    ///
    /// <para>作法は <see cref="CabinetAppointmentPanel"/> に合わせる：非モーダル窓・タイトルバーは <see cref="WindowChrome"/>
    /// （つかんで移動・×で閉じる）・Esc は <see cref="UIWindowStack"/> へ登録するだけ・一覧は <see cref="UiScrollbars"/> で
    /// 見えるバー・画面比率別の別版を作らない。</para>
    /// </summary>
    public class CivilServiceAppointmentPanel : MonoBehaviour
    {
        [Header("外観")]
        [Tooltip("Canvas の描画順（内閣人事1002と同格）")]
        public int canvasSortingOrder = 1003;

        [Tooltip("パネルの幅（設計ピクセル）")]
        public float panelDesignWidth = 1160f;

        [Tooltip("パネルの高さ（設計ピクセル・MAP の高さに収まるよう自動で切り詰める）")]
        public float panelDesignHeight = 940f;

        [Tooltip("パネル背景色")]
        public Color panelColor = new Color(0.05f, 0.06f, 0.09f, 0.97f);

        [Header("更新")]
        [Tooltip("一覧を作り直す間隔（実時間秒・ポーズ中も進む）")]
        public float refreshInterval = 1.2f;

        [Header("一覧の上限（終盤に行が膨らまないよう打ち切り、超過件数を明示する）")]
        [Tooltip("対象一覧に並べる最大行数")]
        public int maxTargetRows = 60;

        [Tooltip("可否（Preview）を引く最大人数。超えた分は件数だけ示し、名前で絞り込んで探す")]
        public int maxTargetEvaluations = 120;

        [Tooltip("履歴一覧に並べる最大行数（新しい順）")]
        public int maxHistoryRows = 40;

        private const float BaseFont = 16f;
        private const float MinFontPx = 13f;
        private const float BaseRow = 30f;
        private const float MinRowPx = 26f;
        private const float MinPanelWidthPx = 680f;
        private const int MinPanelRowsTall = 22;

        private static readonly CivilServiceAction[] Actions =
        {
            CivilServiceAction.配属, CivilServiceAction.異動, CivilServiceAction.昇任,
            CivilServiceAction.降任, CivilServiceAction.解任,
        };

        /// <summary>段ごとの定員の表示にだけ使う調整値（可否は必ず Preview の戻り値で決める＝ここでは判定しない）。</summary>
        private static readonly CivilServicePostParams DisplayPrm = CivilServicePostParams.Default;

        private static readonly Color FullColor = new Color(1f, 0.72f, 0.42f);

        private static CivilServiceAppointmentPanel instance;

        private Canvas canvas;
        private RectTransform frameRT;
        private RectTransform ministryContent, targetContent, historyContent;
        private ScrollRect ministryScroll, targetScroll, historyScroll;
        private TMP_FontAsset jpFont;
        private TextMeshProUGUI headerLabel, selectionLabel, reasonHintLabel, confirmLabel, messageLabel;
        private TextMeshProUGUI eligibleToggleCaption, finalButtonCaption;
        private TMP_InputField filterField, reasonField;
        private Button confirmButton, finalButton;
        private object escWindowToken;

        private readonly List<GameObject> ministryRows = new List<GameObject>();
        private readonly List<GameObject> targetRows = new List<GameObject>();
        private readonly List<GameObject> historyRows = new List<GameObject>();
        private readonly List<Image> actionButtonImages = new List<Image>();
        private readonly List<TextMeshProUGUI> actionButtonLabels = new List<TextMeshProUGUI>();

        private float refreshTimer;
        private string message = "";
        private string filterText = "";
        private bool eligibleOnly = true;

        // 選択（ID で持つ＝一覧を作り直しても保つ）
        private CivilServiceAction selectedAction = CivilServiceAction.配属;
        private int selectedMinistryId = -1;
        private int selectedPersonId = -1;
        private bool confirmed;

        // 直近の見込み（表示と試験の観測用。判定はここでしない＝Preview の写し）
        private Plan lastPlan;
        private AppointmentResult lastPreview;
        private bool lastPreviewQueried;
        private CivilServiceRequestResult lastResult = CivilServiceRequestResult.Rejected("");
        private string lastResultAddresseeName = "";

        /// <summary>選択から組み立てた人事の要求（WHAT だけ。可否は持たない）。</summary>
        private struct Plan
        {
            /// <summary>要求を組めなかった理由（組めたら null）。</summary>
            public string problem;
            /// <summary>承認と資格を問う段（<see cref="GalaxyView.PreviewPlayerCivilServicePost"/> へ渡す）。</summary>
            public BureaucratGrade grade;
            /// <summary>対象者の現職（台帳の在任記録。無ければ null）。</summary>
            public CivilServiceRecord current;
            public int personId;
            public bool Valid => problem == null && personId >= 0;
        }

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
            if (FindAnyObjectByType<CivilServiceAppointmentPanel>() != null) return;
            new GameObject("CivilServiceAppointmentPanel").AddComponent<CivilServiceAppointmentPanel>();
        }

        // ===== static の窓口 =====

        public static bool IsOpen => instance != null && instance.canvas != null && instance.canvas.gameObject.activeSelf;

        public static void Show()
        {
            CivilServiceAppointmentPanel p = EnsureInstance();
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

        private static CivilServiceAppointmentPanel EnsureInstance()
        {
            if (instance != null) return instance;
            instance = FindAnyObjectByType<CivilServiceAppointmentPanel>();
            if (instance == null)
                instance = new GameObject("CivilServiceAppointmentPanel").AddComponent<CivilServiceAppointmentPanel>();
            return instance;
        }

        // ===== ライフサイクル =====

        private void Awake()
        {
            instance = this;
            BuildUI();
            if (canvas != null) canvas.gameObject.SetActive(false);
            escWindowToken = UIWindowStack.Register(
                () => canvas != null && canvas.gameObject.activeSelf, Close, canvasSortingOrder, "官僚人事");
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
            ClearRows(ministryRows);
            ClearRows(targetRows);
            ClearRows(historyRows);
            float font = FontSize();
            float rowH = RowHeight();
            UpdateActionButtons();

            GalaxyView gv = View;
            if (gv == null)
            {
                ShowUnavailable("戦略マップ（GalaxyView）がありません。", "戦略マップでのみ操作できます。", font, rowH);
                return;
            }

            GalaxyView.CivilServiceOperation op = gv.CivilServiceOperationForPlayer();
            if (op.problem != null || op.actor == null || op.tree == null || op.ledger == null)
            {
                string why = op.problem ?? "官僚人事の材料が組めません。";
                ShowUnavailable(why, "官僚人事：" + why, font, rowH);
                return;
            }

            BuildHeader(gv, op);
            BuildMinistryRows(op, font, rowH);
            BuildTargetRows(gv, op, font, rowH);
            BuildHistoryRows(gv, op, font, rowH);
            UpdateSelection(gv, op);
            UpdateConfirm(gv, op);
            if (messageLabel != null) messageLabel.text = message;
        }

        /// <summary>材料が無いときは壊さずに、理由つきで操作不能にする（台帳 null・省庁未編成・人物不在など）。</summary>
        private void ShowUnavailable(string notice, string header, float font, float rowH)
        {
            MakeNotice(ministryContent, ministryRows, notice, font, rowH);
            MakeNotice(targetContent, targetRows, "操作できません。", font, rowH);
            MakeNotice(historyContent, historyRows, "履歴を読めません。", font, rowH);
            if (headerLabel != null) headerLabel.text = header;
            if (selectionLabel != null) selectionLabel.text = "";
            lastPlan = new Plan { problem = notice, personId = -1 };
            lastPreviewQueried = false;
            lastPreview = AppointmentResult.Deny(notice);
            confirmed = false;
            if (confirmLabel != null) confirmLabel.text = "<color=#ff9a7a>" + notice + "</color>";
            if (confirmButton != null) confirmButton.interactable = false;
            if (finalButton != null) finalButton.interactable = false;
            if (finalButtonCaption != null) finalButtonCaption.text = "実行できません";
            if (messageLabel != null) messageLabel.text = message;
        }

        private void BuildHeader(GalaxyView gv, in GalaxyView.CivilServiceOperation op)
        {
            var sb = new StringBuilder(256);
            sb.Append("操作者：").Append(op.actor.name).Append("（").Append(ActorRoleText(gv, op)).Append("）");
            sb.Append("　勢力：").Append(op.faction).Append("　SE").Append(op.year);
            int serving = op.ledger.records != null ? op.ledger.records.Count : 0;
            sb.Append("　省内職位の在任：").Append(serving).Append("名　省庁：").Append(op.tree.Count).Append("庁");
            sb.Append('\n').Append("<color=#9aa7b2>可否・上申先は人事の共通入口（見込み）が決めます。この画面は判定しません。</color>");
            if (headerLabel != null) headerLabel.text = sb.ToString();
        }

        /// <summary>操作者の立場（首相／閣内の職／省内職位／いずれでもない）。権限そのものは Preview が返す。</summary>
        private static string ActorRoleText(GalaxyView gv, in GalaxyView.CivilServiceOperation op)
        {
            int premier = CabinetAppointmentRules.FormalPremier(op.politics, op.faction, op.roster, out _);
            if (premier >= 0 && op.actor.id == premier) return "首相＝内閣人事局";
            CabinetPost held = CabinetAppointmentRules.PostHeldBy(op.politics != null ? op.politics.cabinet : null, op.actor.id);
            if (held != null) return CabinetAppointmentRules.PostTitle(held);
            CivilServiceRecord rec = CivilServicePostRules.FindServing(op.ledger, op.actor.id);
            if (rec != null) return CivilServicePostRules.GradeTitle(rec.ministryName, rec.grade);
            return "官職なし";
        }

        private void BuildMinistryRows(in GalaxyView.CivilServiceOperation op, float font, float rowH)
        {
            if (op.tree.Count == 0)
            {
                MakeNotice(ministryContent, ministryRows, "省庁が編成されていません。", font, rowH);
                return;
            }
            for (int i = 0; i < op.tree.Count; i++)
            {
                Ministry m = op.tree[i];
                if (m == null) continue;
                MakeMinistryRow(op, m, font, rowH);
            }
        }

        private void MakeMinistryRow(in GalaxyView.CivilServiceOperation op, Ministry m, float font, float rowH)
        {
            bool sel = m.id == selectedMinistryId;
            int captured = m.id;
            RectTransform rt = MakeRow(ministryContent, ministryRows, "Ministry_" + captured, rowH, sel, () =>
            {
                selectedMinistryId = selectedMinistryId == captured ? -1 : captured;
                selectedPersonId = -1;
                ResetConfirmation();
                message = "";
                Rebuild();
            });

            int occupied = CivilServicePostRules.OccupiedStaffCount(m, op.ledger);
            MakeCell(rt, (m.IsTopLevel ? "" : "└ ") + (m.ministryName ?? ""), font,
                sel ? new Color(1f, 0.92f, 0.62f) : new Color(0.92f, 0.95f, 1f), 0f, 0.30f, 6f);
            MakeCell(rt, m.domain.ToString(), font, new Color(0.78f, 0.84f, 0.94f), 0.30f, 0.42f, 4f);
            MakeCell(rt, "在籍 " + occupied + " / 定員 " + m.staffSlots, font,
                occupied >= m.staffSlots ? FullColor : new Color(0.82f, 0.88f, 0.96f), 0.42f, 0.60f, 4f);
            MakeCell(rt, GradeSlotsText(op.ledger, m), font, new Color(0.76f, 0.83f, 0.92f), 0.60f, 1f, 4f);
        }

        /// <summary>段ごとの在任数／定員（表示のみ。空席の可否は Preview が返す）。</summary>
        private static string GradeSlotsText(CivilServiceState st, Ministry m)
        {
            var sb = new StringBuilder(64);
            sb.Append("課長 ").Append(CivilServicePostRules.ServingCount(st, m.id, BureaucratGrade.課長級))
              .Append('/').Append(CivilServicePostRules.SlotsFor(m, BureaucratGrade.課長級, DisplayPrm));
            sb.Append("・局長 ").Append(CivilServicePostRules.ServingCount(st, m.id, BureaucratGrade.局長級))
              .Append('/').Append(CivilServicePostRules.SlotsFor(m, BureaucratGrade.局長級, DisplayPrm));
            sb.Append("・次官 ").Append(CivilServicePostRules.ServingCount(st, m.id, BureaucratGrade.事務次官級))
              .Append('/').Append(CivilServicePostRules.SlotsFor(m, BureaucratGrade.事務次官級, DisplayPrm));
            return sb.ToString();
        }

        // ===== 対象（在任者と候補） =====

        /// <summary>選択中の操作で、在任者そのものが対象になるか（昇任・降任・解任）。</summary>
        private bool TargetsIncumbents
            => selectedAction == CivilServiceAction.昇任 || selectedAction == CivilServiceAction.降任
               || selectedAction == CivilServiceAction.解任;

        /// <summary>1人ぶんの見込み（<see cref="Plan"/> が組めたかと Preview の戻り値だけ。判定はしない＝写すだけ）。</summary>
        private struct Verdict
        {
            public string planProblem;
            public AppointmentResult preview;
            public bool queried;
            /// <summary>受け付けられるか（直接実行 or 上申）。</summary>
            public bool Accepted => planProblem == null && queried && (preview.ok || preview.canPetition);
            /// <summary>並べ替えの重み（0＝直接実行・1＝上申・2＝不可）。</summary>
            public int Rank => planProblem != null || !queried ? 2 : preview.ok ? 0 : preview.canPetition ? 1 : 2;
        }

        private struct TargetView
        {
            public int personId;
            public Verdict verdict;
        }

        /// <summary>選んだ操作・省でその人物の見込みを引く（この画面の唯一の判定源）。</summary>
        private Verdict Evaluate(GalaxyView gv, in GalaxyView.CivilServiceOperation op, int personId)
        {
            var v = new Verdict();
            Plan plan = BuildPlan(op, personId);
            v.planProblem = plan.problem;
            if (!plan.Valid)
            {
                v.preview = AppointmentResult.Deny(plan.problem ?? "対象を選んでいません。");
                return v;
            }
            v.preview = gv.PreviewPlayerCivilServicePost(selectedMinistryId, selectedAction, personId, plan.grade);
            v.queried = true;
            return v;
        }

        private void BuildTargetRows(GalaxyView gv, in GalaxyView.CivilServiceOperation op, float font, float rowH)
        {
            if (selectedMinistryId < 0)
            {
                MakeNotice(targetContent, targetRows, "上の省庁一覧から対象の省を選ぶと、在任者と候補がここに並びます。", font, rowH);
                return;
            }
            Ministry ministry = MinistryRules.Get(op.tree, selectedMinistryId);
            string ministryName = ministry != null ? (ministry.ministryName ?? "") : "省#" + selectedMinistryId;

            int budget = Mathf.Max(1, maxTargetEvaluations);
            int limit = Mathf.Max(1, maxTargetRows);
            int evaluated = 0;

            // ① 選択省の在任者（昇任・降任・解任ではこれが対象そのもの／配属・異動では参考表示）
            MakeGroupHeader(targetContent, targetRows,
                "■ " + ministryName + " の在任者" + (TargetsIncumbents ? "（ここから対象を選ぶ）" : "（参考・この操作の対象ではない）"),
                font, rowH);
            List<CivilServiceRecord> incumbents = ServingIn(op.ledger, selectedMinistryId);
            if (incumbents.Count == 0)
                MakeNotice(targetContent, targetRows, "在任者がいません。", font, rowH);

            int shown = 0, hidden = 0, notQueried = 0;
            for (int i = 0; i < incumbents.Count; i++)
            {
                CivilServiceRecord rec = incumbents[i];
                Person p = ElectionCycleRules.FindPerson(op.roster, rec.personId);
                if (!MatchesFilter(gv, rec.personId, p)) continue;

                if (!TargetsIncumbents)
                {
                    if (shown >= limit) { hidden++; continue; }
                    MakeTargetRow(gv, op, rec.personId, rec, p, font, rowH, false, "");
                    shown++;
                    continue;
                }
                if (evaluated >= budget) { notQueried++; continue; }
                Verdict v = Evaluate(gv, op, rec.personId);
                evaluated++;
                if (!v.Accepted && eligibleOnly && rec.personId != selectedPersonId) { hidden++; continue; }
                if (shown >= limit) { hidden++; continue; }
                MakeTargetRow(gv, op, rec.personId, rec, p, font, rowH, true, VerdictText(v));
                shown++;
            }
            EmitOverflowNotices(shown, hidden, notQueried, budget, font, rowH);

            // ② 候補（配属＝未在籍の人物／異動＝他省の在任者）
            if (TargetsIncumbents)
            {
                MakeNotice(targetContent, targetRows, "この操作は在任者が対象です（上の一覧から選びます）。", font, rowH);
                return;
            }
            MakeGroupHeader(targetContent, targetRows,
                selectedAction == CivilServiceAction.配属
                    ? "■ 配属の候補（どの省にも在籍していない人物）→ " + ministryName + " の一般官僚へ"
                    : "■ 異動の候補（他省の在任者）→ 行き先は " + ministryName,
                font, rowH);

            List<int> candidates = CollectCandidates(gv, op);
            if (candidates.Count == 0)
            {
                MakeNotice(targetContent, targetRows, "候補がいません（名前の絞り込みを外すと増えることがあります）。", font, rowH);
                return;
            }

            var views = new List<TargetView>();
            int candidateNotQueried = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (evaluated >= budget) { candidateNotQueried++; continue; }
                views.Add(new TargetView { personId = candidates[i], verdict = Evaluate(gv, op, candidates[i]) });
                evaluated++;
            }
            views.Sort(CompareTargets);

            int candidateShown = 0, candidateHidden = 0;
            for (int i = 0; i < views.Count; i++)
            {
                TargetView tv = views[i];
                if (!tv.verdict.Accepted && eligibleOnly && tv.personId != selectedPersonId) { candidateHidden++; continue; }
                if (candidateShown >= limit) { candidateHidden++; continue; }
                Person p = ElectionCycleRules.FindPerson(op.roster, tv.personId);
                CivilServiceRecord rec = CivilServicePostRules.FindServing(op.ledger, tv.personId);
                MakeTargetRow(gv, op, tv.personId, rec, p, font, rowH, true, VerdictText(tv.verdict));
                candidateShown++;
            }
            if (candidateShown == 0)
                MakeNotice(targetContent, targetRows,
                    eligibleOnly ? "受け付けられる候補がいません（［不可も表示］に切り替えると Preview の理由が見られます）。"
                                 : "候補がいません。", font, rowH);
            EmitOverflowNotices(candidateShown, candidateHidden, candidateNotQueried, budget, font, rowH);
        }

        /// <summary>打ち切りを黙って隠さない（隠した件数・照会しなかった件数を明示する）。</summary>
        private void EmitOverflowNotices(int shown, int hidden, int notQueried, int budget, float font, float rowH)
        {
            if (hidden > 0)
                MakeNotice(targetContent, targetRows,
                    "ほか " + hidden + " 名を表示していません（表示 " + shown + " 名／［不可も表示］と名前の絞り込みで探せます）。",
                    font, rowH);
            if (notQueried > 0)
                MakeNotice(targetContent, targetRows,
                    "ほか " + notQueried + " 名は可否を照会していません（1回の照会は " + budget + " 名まで。名前で絞り込んでください）。",
                    font, rowH);
        }

        /// <summary>受け付けられる順（直接実行→上申→不可）、同順は人物 ID 昇順＝決まった並び。</summary>
        private static int CompareTargets(TargetView a, TargetView b)
        {
            int c = a.verdict.Rank.CompareTo(b.verdict.Rank);
            return c != 0 ? c : a.personId.CompareTo(b.personId);
        }

        /// <summary>選択中の操作で対象になりうる人物 ID（選択規則だけ＝可否は Preview が返す）。</summary>
        private List<int> CollectCandidates(GalaxyView gv, in GalaxyView.CivilServiceOperation op)
        {
            var ids = new List<int>();
            if (selectedAction == CivilServiceAction.配属)
            {
                if (op.roster == null) return ids;
                for (int i = 0; i < op.roster.Count; i++)
                {
                    Person p = op.roster[i];
                    if (p == null || p.faction != op.faction) continue;
                    if (CivilServicePostRules.FindServing(op.ledger, p.id) != null) continue; // 在籍者は異動の領分
                    if (!MatchesFilter(gv, p.id, p)) continue;
                    ids.Add(p.id);
                }
                return ids;
            }
            // 異動＝他省の在任者
            List<CivilServiceRecord> recs = op.ledger.records;
            if (recs == null) return ids;
            for (int i = 0; i < recs.Count; i++)
            {
                CivilServiceRecord r = recs[i];
                if (r == null || !r.IsServing || r.ministryId == selectedMinistryId) continue;
                Person p = ElectionCycleRules.FindPerson(op.roster, r.personId);
                if (!MatchesFilter(gv, r.personId, p)) continue;
                ids.Add(r.personId);
            }
            return ids;
        }

        private static List<CivilServiceRecord> ServingIn(CivilServiceState st, int ministryId)
        {
            var list = new List<CivilServiceRecord>();
            if (st == null || st.records == null) return list;
            for (int i = 0; i < st.records.Count; i++)
            {
                CivilServiceRecord r = st.records[i];
                if (r != null && r.IsServing && r.ministryId == ministryId) list.Add(r);
            }
            return list;
        }

        private bool MatchesFilter(GalaxyView gv, int personId, Person p)
        {
            if (string.IsNullOrEmpty(filterText)) return true;
            string name = p != null ? p.name : (gv != null ? gv.CabinetPersonName(personId) : null);
            return name != null && name.IndexOf(filterText, System.StringComparison.Ordinal) >= 0;
        }

        private void MakeTargetRow(GalaxyView gv, in GalaxyView.CivilServiceOperation op, int personId,
            CivilServiceRecord rec, Person p, float font, float rowH, bool selectable, string verdict)
        {
            bool sel = selectable && personId == selectedPersonId;
            int captured = personId;
            RectTransform rt;
            if (selectable)
            {
                rt = MakeRow(targetContent, targetRows, "Target_" + captured, rowH, sel, () =>
                {
                    selectedPersonId = selectedPersonId == captured ? -1 : captured;
                    ResetConfirmation();
                    message = "";
                    Rebuild();
                });
            }
            else
            {
                rt = MakeStaticRow(targetContent, targetRows, "Incumbent_" + captured, rowH);
            }

            MakeCell(rt, gv.CabinetPersonName(personId), font,
                sel ? new Color(1f, 0.92f, 0.62f) : new Color(0.92f, 0.95f, 1f), 0f, 0.18f, 6f);
            MakeCell(rt, PostText(rec, op.year), font, new Color(0.82f, 0.88f, 0.96f), 0.18f, 0.44f, 4f);
            MakeCell(rt, QualificationText(p), font, new Color(0.78f, 0.84f, 0.94f), 0.44f, 0.64f, 4f);
            MakeCell(rt, selectable ? verdict : "<color=#9aa7b2>—</color>", font, Color.white, 0.64f, 1f, 4f);
        }

        private static string PostText(CivilServiceRecord rec, int year)
        {
            if (rec == null) return "未在籍";
            int tenure = Mathf.Max(0, year - rec.appointedYear);
            return CivilServicePostRules.GradeTitle(rec.ministryName, rec.grade)
                   + "・在職 " + tenure + "年（SE" + rec.appointedYear + " 就任）";
        }

        private static string QualificationText(Person p)
        {
            if (p == null) return "<color=#ff9a7a>名簿にいない</color>";
            string merit = p.merit != null && p.merit.HasRecord ? p.merit.AverageScore.ToString("0.0") : "未評定";
            return "官位 " + p.courtRank + "・考課 " + merit;
        }

        /// <summary>見込み（Preview）の1行表示。理由は必ず Preview 由来（画面で言い換えない）。</summary>
        private static string VerdictText(Verdict v)
        {
            if (v.planProblem != null) return "<color=#ff9a7a>" + v.planProblem + "</color>";
            if (!v.queried) return "<color=#9aa7b2>未照会</color>";
            if (v.preview.ok) return "<color=#8ce08c>直接実行できる：" + v.preview.reason + "</color>";
            if (v.preview.canPetition) return "<color=#ffb070>上申になる：" + v.preview.reason + "</color>";
            return "<color=#ff9a7a>不可：" + v.preview.reason + "</color>";
        }

        private void BuildHistoryRows(GalaxyView gv, in GalaxyView.CivilServiceOperation op, float font, float rowH)
        {
            List<CivilServiceRecord> h = op.ledger.history;
            if (h == null || h.Count == 0)
            {
                MakeNotice(historyContent, historyRows, "人事の履歴はまだありません。", font, rowH);
                return;
            }
            int limit = Mathf.Max(1, maxHistoryRows);
            int shown = 0;
            for (int i = h.Count - 1; i >= 0 && shown < limit; i--)
            {
                CivilServiceRecord r = h[i];
                if (r == null) continue;
                RectTransform rt = MakeStaticRow(historyContent, historyRows, "History_" + i, rowH);
                MakeCell(rt, "SE" + (r.vacatedYear > 0 ? r.vacatedYear : r.appointedYear), font,
                    new Color(0.80f, 0.86f, 0.95f), 0f, 0.10f, 6f);
                MakeCell(rt, CivilServicePostRules.GradeTitle(r.ministryName, r.grade), font,
                    new Color(0.88f, 0.92f, 1f), 0.10f, 0.38f, 4f);
                MakeCell(rt, gv.CabinetPersonName(r.personId), font, new Color(1f, 0.90f, 0.78f), 0.38f, 0.52f, 4f);
                MakeCell(rt, r.status.ToString(), font, new Color(0.82f, 0.88f, 0.96f), 0.52f, 0.64f, 4f);
                MakeCell(rt, r.reason ?? "", font, new Color(0.74f, 0.81f, 0.90f), 0.64f, 1f, 4f);
                shown++;
            }
            if (h.Count > shown || op.ledger.historyDropped > 0)
                MakeNotice(historyContent, historyRows,
                    "ほか " + (h.Count - shown) + " 件を表示していません（上限で捨てた古い記録 " + op.ledger.historyDropped + " 件）。",
                    font, rowH);
        }

        // ===== 要求の組み立て（WHAT だけ・可否は持たない） =====

        /// <summary>
        /// 選択から人事の要求を組む。段は操作で決まる＝配属は一般官僚、異動・解任は現職の段、昇任は1段上、降任は1段下。
        /// <b>段を決められない組合せ（最上段の昇任・最下段の降任）だけ</b>を理由つきで返す（可否の判定ではなく要求が組めないこと）。
        /// </summary>
        private Plan BuildPlan(in GalaxyView.CivilServiceOperation op, int personId)
        {
            var plan = new Plan { personId = personId, grade = BureaucratGrade.一般官僚 };
            if (selectedMinistryId < 0) { plan.problem = "対象の省を選んでいません。"; return plan; }
            if (personId < 0) { plan.problem = "対象の人物を選んでいません。"; return plan; }
            plan.current = CivilServicePostRules.FindServing(op.ledger, personId);

            switch (selectedAction)
            {
                case CivilServiceAction.配属:
                    plan.grade = BureaucratGrade.一般官僚; // 入省の段（飛び級はしない）
                    break;
                case CivilServiceAction.異動:
                case CivilServiceAction.解任:
                    plan.grade = plan.current != null ? plan.current.grade : BureaucratGrade.一般官僚;
                    break;
                case CivilServiceAction.昇任:
                    if (plan.current != null && plan.current.grade == BureaucratGrade.事務次官級)
                    {
                        plan.problem = "事務次官級より上の段がありません＝昇任できません（省内職位の最上段）。";
                        return plan;
                    }
                    plan.grade = plan.current != null ? (BureaucratGrade)((int)plan.current.grade + 1) : BureaucratGrade.課長級;
                    break;
                default: // 降任
                    if (plan.current != null && plan.current.grade == BureaucratGrade.一般官僚)
                    {
                        plan.problem = "一般官僚より下の段がありません＝降任できません（省内職位の最下段）。";
                        return plan;
                    }
                    plan.grade = plan.current != null ? (BureaucratGrade)((int)plan.current.grade - 1) : BureaucratGrade.一般官僚;
                    break;
            }
            return plan;
        }

        // ===== 選択・確認・実行 =====

        private string Reason => reasonField != null ? reasonField.text : "";

        private void ResetConfirmation()
        {
            confirmed = false;
        }

        private void UpdateSelection(GalaxyView gv, in GalaxyView.CivilServiceOperation op)
        {
            if (eligibleToggleCaption != null)
                eligibleToggleCaption.text = eligibleOnly ? "受付可のみ表示中" : "不可も表示中";
            if (reasonHintLabel != null)
                reasonHintLabel.text = "理由は空でも受け付けます（未入力時は既定理由「"
                                       + CivilServiceRingiRules.DefaultReason(selectedAction) + "」を使います）。";
            if (selectionLabel == null) return;

            Ministry m = selectedMinistryId >= 0 ? MinistryRules.Get(op.tree, selectedMinistryId) : null;
            var sb = new StringBuilder(220);
            sb.Append("操作：").Append(selectedAction);
            sb.Append("　省：").Append(m != null ? (m.ministryName ?? "") : "（未選択）");
            sb.Append("　対象：").Append(selectedPersonId >= 0 ? gv.CabinetPersonName(selectedPersonId) : "（未選択）");
            Plan plan = BuildPlan(op, selectedPersonId);
            if (plan.Valid)
                sb.Append("　就ける段：").Append(CivilServicePostRules.GradeTitle(m != null ? m.ministryName : "", plan.grade));
            selectionLabel.text = sb.ToString();
        }

        private void UpdateConfirm(GalaxyView gv, in GalaxyView.CivilServiceOperation op)
        {
            Plan plan = BuildPlan(op, selectedPersonId);
            lastPlan = plan;
            lastPreviewQueried = false;
            lastPreview = AppointmentResult.Deny(plan.problem ?? "対象を選んでいません。");
            if (plan.Valid)
            {
                lastPreview = gv.PreviewPlayerCivilServicePost(selectedMinistryId, selectedAction, selectedPersonId, plan.grade);
                lastPreviewQueried = true;
            }

            bool accepted = plan.Valid && (lastPreview.ok || lastPreview.canPetition);
            bool petition = plan.Valid && !lastPreview.ok && lastPreview.canPetition;

            if (confirmButton != null) confirmButton.interactable = accepted && !confirmed;
            if (finalButton != null) finalButton.interactable = accepted && confirmed;
            if (finalButtonCaption != null)
                finalButtonCaption.text = !accepted ? "実行できません"
                    : petition ? "上申する" : "直接実行する";

            if (confirmLabel == null) return;
            Ministry m = selectedMinistryId >= 0 ? MinistryRules.Get(op.tree, selectedMinistryId) : null;
            string ministryName = m != null ? (m.ministryName ?? "") : "";
            var sb = new StringBuilder(320);

            if (!plan.Valid)
            {
                sb.Append("<color=#ff9a7a>確定できません：").Append(plan.problem).Append("</color>");
                confirmLabel.text = sb.ToString();
                return;
            }

            string who = gv.CabinetPersonName(selectedPersonId);
            string post = CivilServicePostRules.GradeTitle(ministryName, plan.grade);
            sb.Append(confirmed ? "【確認】" : "［見込み］");
            sb.Append(selectedAction == CivilServiceAction.解任
                ? post + " " + who + " を解任"
                : who + " を " + post + " へ" + selectedAction);
            sb.Append("　理由：").Append(string.IsNullOrWhiteSpace(Reason)
                ? "<color=#9aa7b2>（未入力＝既定理由「" + CivilServiceRingiRules.DefaultReason(selectedAction) + "」）</color>"
                : Reason.Trim());
            sb.Append('\n');
            if (lastPreview.ok)
                sb.Append("<color=#8ce08c>直接実行：").Append(lastPreview.reason).Append("（押すと台帳へ反映されます）</color>");
            else if (petition)
            {
                sb.Append("<color=#ffb070>上申：").Append(lastPreview.reason)
                  .Append("　上申先＝").Append(gv.CabinetPersonName(lastPreview.petitionToId))
                  .Append("（人物#").Append(lastPreview.petitionToId).Append("）</color>");
            }
            else
                sb.Append("<color=#ff9a7a>不可：").Append(lastPreview.reason).Append("</color>");

            if (accepted && !confirmed)
                sb.Append("\n<color=#9aa7b2>［内容を確認］を押すと確認欄が固定され、次の押下で確定します。</color>");
            else if (confirmed)
                sb.Append("\n<color=#ffd27a>この内容で確定します（確定時に共通入口がもう一度判定します）。</color>");
            confirmLabel.text = sb.ToString();
        }

        /// <summary>理由の入力に合わせて確認だけ更新する（一覧は作り直さない＝入力中の手触りを保つ）。</summary>
        private void UpdateConfirmNow()
        {
            GalaxyView gv = View;
            if (gv == null) return;
            GalaxyView.CivilServiceOperation op = gv.CivilServiceOperationForPlayer();
            if (op.problem != null || op.ledger == null || op.tree == null) return;
            UpdateSelection(gv, op);
            UpdateConfirm(gv, op);
        }

        private void RequestConfirmation()
        {
            confirmed = true;
            message = "";
            Rebuild();
        }

        private void CancelConfirmation()
        {
            confirmed = false;
            message = "";
            Rebuild();
        }

        /// <summary>確定：<see cref="GalaxyView.SubmitPlayerCivilServicePost"/> だけを呼ぶ（共通入口が実行時にもう一度判定する）。</summary>
        private void ExecutePending()
        {
            GalaxyView gv = View;
            if (gv == null) { message = "<color=#ff9a7a>戦略マップがありません。</color>"; Rebuild(); return; }
            GalaxyView.CivilServiceOperation op = gv.CivilServiceOperationForPlayer();
            if (op.problem != null || op.ledger == null || op.tree == null)
            {
                message = "<color=#ff9a7a>" + (op.problem ?? "官僚人事の材料が組めません。") + "</color>";
                Rebuild();
                return;
            }
            Plan plan = BuildPlan(op, selectedPersonId);
            if (!plan.Valid)
            {
                message = "<color=#ff9a7a>" + plan.problem + "</color>";
                Rebuild();
                return;
            }
            if (!confirmed)
            {
                message = "<color=#ffb070>先に［内容を確認］を押してください。</color>";
                Rebuild();
                return;
            }

            CivilServiceRequestResult r = gv.SubmitPlayerCivilServicePost(selectedMinistryId, selectedAction,
                selectedPersonId, plan.grade, Reason);
            lastResult = r;
            lastResultAddresseeName = r.addresseeId >= 0 ? gv.CabinetPersonName(r.addresseeId) : "";

            switch (r.outcome)
            {
                case CivilServiceRequestOutcome.実行:
                    message = "<color=#8ce08c>直接実行しました：" + r.reason + "（台帳へ反映済み）</color>";
                    selectedPersonId = -1;
                    if (reasonField != null) reasonField.text = "";
                    break;
                case CivilServiceRequestOutcome.上申:
                    message = "<color=#ffb070>上申しました：決裁#" + r.decisionId + "　決裁者 "
                              + (string.IsNullOrEmpty(lastResultAddresseeName) ? "人物#" + r.addresseeId : lastResultAddresseeName)
                              + "（人物#" + r.addresseeId + "）。台帳はまだ変わっていません＝右下の決裁デスクで裁可されると反映されます。</color>";
                    if (reasonField != null) reasonField.text = "";
                    break;
                default:
                    message = "<color=#ff9a7a>受け付けられませんでした：" + r.reason + "</color>";
                    break;
            }
            confirmed = false;
            Rebuild();
        }

        private void SelectAction(CivilServiceAction action)
        {
            selectedAction = action;
            selectedPersonId = -1;
            ResetConfirmation();
            message = "";
            Rebuild();
        }

        private void UpdateActionButtons()
        {
            for (int i = 0; i < actionButtonImages.Count && i < Actions.Length; i++)
            {
                bool sel = Actions[i] == selectedAction;
                if (actionButtonImages[i] != null)
                    actionButtonImages[i].color = sel ? new Color(0.28f, 0.40f, 0.56f, 1f) : new Color(0.16f, 0.20f, 0.30f, 1f);
                if (actionButtonLabels[i] != null)
                    actionButtonLabels[i].color = sel ? new Color(1f, 0.92f, 0.62f) : new Color(0.94f, 0.96f, 1f);
            }
        }

        // ===== 試験用の入口（UI の選択を再現するだけ＝操作者・判定は差し替えない） =====

        public static CivilServiceAppointmentPanel InstanceForTest => instance;
        public bool EscRegisteredForTest => escWindowToken != null;
        public bool AllListsHaveScrollbarsForTest
            => ministryScroll != null && ministryScroll.verticalScrollbar != null
               && targetScroll != null && targetScroll.verticalScrollbar != null
               && historyScroll != null && historyScroll.verticalScrollbar != null;
        public bool ReasonAndFilterFieldsExistForTest => filterField != null && reasonField != null;
        public int ActionButtonCountForTest => actionButtonImages.Count;
        public int MinistryRowCountForTest => ministryRows.Count;
        public int TargetRowCountForTest => targetRows.Count;
        public int HistoryRowCountForTest => historyRows.Count;
        public bool ConfirmedForTest => confirmed;
        public bool ConfirmButtonInteractableForTest => confirmButton != null && confirmButton.interactable;
        public bool FinalButtonInteractableForTest => finalButton != null && finalButton.interactable;
        public string FinalButtonCaptionForTest => finalButtonCaption != null ? finalButtonCaption.text : "";
        public string ConfirmTextForTest => confirmLabel != null ? confirmLabel.text : "";
        public string SelectionTextForTest => selectionLabel != null ? selectionLabel.text : "";
        public string HeaderTextForTest => headerLabel != null ? headerLabel.text : "";
        public string MessageTextForTest => message;

        /// <summary>直近に引いた見込みの引数（省・操作・人物・段）と結果＝「正しい引数で Preview を呼んだか」の観測用。</summary>
        public bool LastPreviewQueriedForTest => lastPreviewQueried;
        public int LastPreviewMinistryIdForTest => selectedMinistryId;
        public CivilServiceAction LastPreviewActionForTest => selectedAction;
        public int LastPreviewPersonIdForTest => lastPlan.personId;
        public BureaucratGrade LastPreviewGradeForTest => lastPlan.grade;
        public string LastPlanProblemForTest => lastPlan.problem;
        public bool LastPreviewOkForTest => lastPreviewQueried && lastPreview.ok;
        public bool LastPreviewCanPetitionForTest => lastPreviewQueried && lastPreview.canPetition;
        public int LastPreviewPetitionToIdForTest => lastPreviewQueried ? lastPreview.petitionToId : -1;
        public string LastPreviewReasonForTest => lastPreviewQueried ? lastPreview.reason : "";
        public CivilServiceRequestResult LastResultForTest => lastResult;

        public void SelectForTest(CivilServiceAction action, int ministryId, int personId, string reason)
        {
            selectedAction = action;
            selectedMinistryId = ministryId;
            selectedPersonId = personId;
            if (reasonField != null) reasonField.text = reason ?? "";
            confirmed = false;
            Rebuild();
        }

        public void SelectPersonForTest(int personId)
        {
            selectedPersonId = personId;
            ResetConfirmation();
            Rebuild();
        }

        public void SelectActionForTest(CivilServiceAction action) => SelectAction(action);
        public void SetReasonForTest(string reason)
        {
            if (reasonField != null) reasonField.text = reason ?? "";
            ResetConfirmation();
            Rebuild();
        }
        public void SetFilterForTest(string text) { filterText = text ?? ""; Rebuild(); }
        public void SetEligibleOnlyForTest(bool only) { eligibleOnly = only; Rebuild(); }
        public void ConfirmForTest() => RequestConfirmation();
        public void CancelConfirmForTest() => CancelConfirmation();
        public void ExecuteForTest() => ExecutePending();
        public void RebuildForTest() => Rebuild();

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
            float minHeight = RowHeight() * MinPanelRowsTall;
            float height = Mathf.Clamp(panelDesignHeight, minHeight, Mathf.Max(minHeight, mapHeightDesign - 24f));

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

            GameObject canvasObj = new GameObject("CivilServiceAppointmentCanvas");
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

            WindowChrome.AddTitleBarLayout(frameRT, "官僚人事", Close);

            float font = FontSize();
            float rowH = RowHeight();

            headerLabel = MakeSectionLabel(frameRT, "", font, new Color(1f, 0.88f, 0.55f), rowH * 2f);
            headerLabel.textWrappingMode = TextWrappingModes.Normal;

            BuildActionRow(frameRT, font, rowH);
            ministryContent = MakeScrollArea(frameRT, "MinistryList", 1f, rowH * 4f, out ministryScroll);

            selectionLabel = MakeSectionLabel(frameRT, "", font, new Color(0.86f, 0.92f, 1f), rowH);
            BuildFilterRow(frameRT, font, rowH);
            targetContent = MakeScrollArea(frameRT, "TargetList", 1.6f, rowH * 5f, out targetScroll);

            reasonField = MakeInputField(frameRT, "ReasonField", "人事の理由を入力（空欄可）", font, rowH, null);
            reasonHintLabel = MakeSectionLabel(frameRT, "", font, new Color(0.72f, 0.80f, 0.90f), rowH);
            BuildConfirmRow(frameRT, font, rowH);

            confirmLabel = MakeSectionLabel(frameRT, "", font, new Color(0.92f, 0.94f, 1f), rowH * 2.4f);
            confirmLabel.textWrappingMode = TextWrappingModes.Normal;

            messageLabel = MakeSectionLabel(frameRT, "", font, new Color(0.86f, 0.92f, 1f), rowH * 1.8f);
            messageLabel.textWrappingMode = TextWrappingModes.Normal;

            MakeSectionLabel(frameRT, "直近の人事履歴（読み取り専用）", font, new Color(0.91f, 0.88f, 0.69f), rowH);
            historyContent = MakeScrollArea(frameRT, "HistoryList", 0.8f, rowH * 3f, out historyScroll);

            Layout();
        }

        private void BuildActionRow(RectTransform parent, float font, float rowH)
        {
            RectTransform row = MakeHRow(parent, "Actions", rowH * 1.1f);
            for (int i = 0; i < Actions.Length; i++)
            {
                CivilServiceAction captured = Actions[i];
                Button b = MakeButton(row, captured.ToString(), font, () => SelectAction(captured), out TextMeshProUGUI label);
                actionButtonImages.Add(b.GetComponent<Image>());
                actionButtonLabels.Add(label);
            }
        }

        private void BuildFilterRow(RectTransform parent, float font, float rowH)
        {
            RectTransform row = MakeHRow(parent, "Filter", rowH);
            filterField = MakeInputField(row, "FilterField", "対象を名前で絞り込む（空欄＝すべて）", font, rowH,
                v => { filterText = v ?? ""; Rebuild(); });
            filterField.GetComponent<LayoutElement>().flexibleWidth = 3f;
            Button b = MakeButton(row, "", font, () => { eligibleOnly = !eligibleOnly; Rebuild(); }, out eligibleToggleCaption);
            b.GetComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private void BuildConfirmRow(RectTransform parent, float font, float rowH)
        {
            RectTransform row = MakeHRow(parent, "Confirm", rowH * 1.1f);
            confirmButton = MakeButton(row, "内容を確認", font, RequestConfirmation, out _);
            confirmButton.interactable = false;
            finalButton = MakeButton(row, "実行できません", font, ExecutePending, out finalButtonCaption);
            finalButton.interactable = false;
            MakeButton(row, "確認を取り消す", font, CancelConfirmation, out _);
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
            // ★TMP_InputField は OnEnable で textComponent/textViewport を要求する＝組み立て終わるまで無効
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
            else field.onValueChanged.AddListener(_ => { ResetConfirmation(); UpdateConfirmNow(); }); // 理由を変えたら確認を解除
            go.SetActive(true);
            return field;
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
            UiScrollbars.Attach(scroll);   // スクロールできることを画面で示す（常時見えるバー）
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

        /// <summary>選べない行（参考表示・履歴）。クリックを受けない＝押しても何も起きないボタンを作らない。</summary>
        private RectTransform MakeStaticRow(RectTransform parent, List<GameObject> into, string name, float rowH)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = rowH; le.preferredHeight = rowH;
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.08f, 0.11f, 0.17f, 1f);
            img.raycastTarget = false;
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
            if (parent == null) return;
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
            t.richText = true;
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
