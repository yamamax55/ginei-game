using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 艦隊管理ウィンドウ。上メニュー「艦隊管理」で開閉し、<b>プレイヤー勢力</b>の自艦隊を一望・点検する。
    /// 艦艇プール（総/割当/残＝<see cref="FleetPool"/>＋<see cref="FleetPoolRules"/>）、編制サマリ
    /// （現役数・総兵力・<b>空席指揮</b>・<b>過大指揮</b>＝<see cref="FleetCommandSummaryRules"/>）、各艦隊の番号・名・兵力・
    /// 役割・状態・司令（階級＋実効能力 <see cref="CommandStaffRules"/>）を表示する。艦艇オブザーバ（<see cref="FleetObserverOverlay"/>＝
    /// 全勢力の在庫）に対し、こちらは<b>自勢力の艦隊点検</b>に特化（要昇進/空席を赤で警告）。集計は既存窓口を読むだけ。
    /// 下端の<b>発令フッター</b>で、選んだ艦隊の<b>割り当て予算内</b>で「やること」（補給/訓練/整備/哨戒/休養）を決められる
    /// （予算は <see cref="FleetOperationsRules"/>＝兵力比例・超過は発令不可、発令は <see cref="NotificationCenter"/> に記録＝#操作化の第一歩）。
    /// `FleetObserverOverlay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class PlayerFleetManagementOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1118;
        public float dimAlpha = 0.92f;
        public float bodyFontSize = 18f;
        [Tooltip("一覧に出す最大艦隊数（超過分は『他N隊』と表示）")]
        public int maxFleets = 40;

        public Color accentColor = new Color(0.6f, 0.85f, 1f, 1f);

        private GameObject root;
        private TextMeshProUGUI bodyLabel;
        private TMP_FontAsset jpFont;

        // 発令フッター（割り当て予算内で「やること」を決める操作・#操作化）。
        private const float FooterHeight = 124f;
        private static readonly FleetTask[] TaskOrder = { FleetTask.補給, FleetTask.訓練, FleetTask.整備, FleetTask.哨戒, FleetTask.休養 };
        private static readonly Color IdleBtn = new Color(0.13f, 0.16f, 0.22f, 1f);
        private static readonly Color OnBtn = new Color(0.30f, 0.55f, 0.85f, 1f);
        private static readonly Color OkBtn = new Color(0.25f, 0.55f, 0.30f, 1f);
        private static readonly Color OffBtn = new Color(0.20f, 0.22f, 0.26f, 1f);

        private int selectedIdx;
        private readonly HashSet<FleetTask> chosen = new HashSet<FleetTask>();
        private readonly List<FleetTask> chosenScratch = new List<FleetTask>(); // 合計費用計算用（GC回避）
        private readonly List<FleetUnitData> activeScratch = new List<FleetUnitData>();
        private TextMeshProUGUI fleetLabel;
        private TextMeshProUGUI budgetLabel;
        private readonly Image[] taskBtnImgs = new Image[5];
        private Image issueBtnImg;

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
            if (scene.name != "Strategy" && scene.name != "Battle") return;
            if (UnityEngine.Object.FindAnyObjectByType<PlayerFleetManagementOverlay>() != null) return;
            new GameObject("PlayerFleetManagementOverlay").AddComponent<PlayerFleetManagementOverlay>();
        }

        private object escWindowToken;

        private void Awake()
        {
            jpFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            EnsureEventSystem();
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => root != null && root.activeSelf, () => SetVisible(false), canvasSortingOrder, "艦隊管理");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (root != null && root.activeSelf && bodyLabel != null)
            {
                bodyLabel.text = BuildDump();
                RefreshFooter();
            }
        }

        // ===== 発令フッター（割り当て予算内で「やること」を決める） =====

        /// <summary>現役のプレイヤー艦隊を集める（毎回再構築＝台帳変化に追従）。</summary>
        private List<FleetUnitData> ActiveFleets()
        {
            activeScratch.Clear();
            IReadOnlyList<FleetUnitData> all = FleetRoster.AllFleets(PlayerFaction());
            if (all != null)
                for (int i = 0; i < all.Count; i++)
                    if (all[i] != null && all[i].status == FleetStatus.現役) activeScratch.Add(all[i]);
            return activeScratch;
        }

        private void CycleFleet(int delta)
        {
            var act = ActiveFleets();
            if (act.Count == 0) return;
            selectedIdx = (selectedIdx + delta % act.Count + act.Count) % act.Count;
            chosen.Clear(); // 艦隊を変えたら選択をリセット（やることは艦隊ごとの判断）
        }

        private void ToggleTask(FleetTask task)
        {
            if (!chosen.Remove(task)) chosen.Add(task);
        }

        private void IssueOrders()
        {
            var act = ActiveFleets();
            if (act.Count == 0 || chosen.Count == 0) return;
            selectedIdx = Mathf.Clamp(selectedIdx, 0, act.Count - 1);
            FleetUnitData sel = act[selectedIdx];
            int strength = FleetStrength(sel);
            BuildChosenList();
            if (!FleetOperationsRules.CanAfford(chosenScratch, strength, FleetOpsParams.Default)) return; // 予算超過は発令不可

            int rem = FleetOperationsRules.Remaining(chosenScratch, strength, FleetOpsParams.Default);
            var sb = new StringBuilder(64);
            sb.Append(sel.DisplayName).Append(" に発令：");
            for (int i = 0; i < chosenScratch.Count; i++) { if (i > 0) sb.Append('・'); sb.Append(chosenScratch[i]); }
            sb.Append("（予算内・残 ").Append(rem.ToString("#,0")).Append("）");
            NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.情報, sb.ToString());
            chosen.Clear();
        }

        private void BuildChosenList()
        {
            chosenScratch.Clear();
            for (int i = 0; i < TaskOrder.Length; i++)
                if (chosen.Contains(TaskOrder[i])) chosenScratch.Add(TaskOrder[i]);
        }

        private void RefreshFooter()
        {
            if (fleetLabel == null || budgetLabel == null) return;
            var act = ActiveFleets();
            var p = FleetOpsParams.Default;

            if (act.Count == 0)
            {
                fleetLabel.text = "（現役艦隊なし）";
                budgetLabel.text = "";
                for (int i = 0; i < taskBtnImgs.Length; i++) if (taskBtnImgs[i] != null) taskBtnImgs[i].color = OffBtn;
                if (issueBtnImg != null) issueBtnImg.color = OffBtn;
                return;
            }

            selectedIdx = Mathf.Clamp(selectedIdx, 0, act.Count - 1);
            FleetUnitData sel = act[selectedIdx];
            int strength = FleetStrength(sel);
            int budget = FleetOperationsRules.Budget(strength, p);
            BuildChosenList();
            int used = FleetOperationsRules.TotalCost(chosenScratch, strength, p);
            int rem = budget - used;
            bool afford = used <= budget;

            fleetLabel.text = $"<color=#bfe9c0>{sel.DisplayName}</color>　兵力 {strength:#,0}　({selectedIdx + 1}/{act.Count})";
            string remCol = rem < 0 ? "#ff8080" : "#bfe9c0";
            budgetLabel.text = $"予算 {budget:#,0}　使用 {used:#,0}　残 <color={remCol}>{rem:#,0}</color>";

            for (int i = 0; i < TaskOrder.Length; i++)
                if (taskBtnImgs[i] != null)
                    taskBtnImgs[i].color = chosen.Contains(TaskOrder[i]) ? OnBtn : IdleBtn;

            if (issueBtnImg != null)
                issueBtnImg.color = (chosen.Count > 0 && afford) ? OkBtn : OffBtn;
        }

        public void Toggle() { SetVisible(root != null && !root.activeSelf); }
        public void SetVisible(bool v) { if (root != null) root.SetActive(v); }

        // ===== 集約＋整形 =====

        private static Faction PlayerFaction()
            => GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;

        private static int FleetStrength(FleetUnitData u)
            => u.baseStrength > 0 ? u.baseStrength : (u.assignedAdmiral != null ? u.assignedAdmiral.baseStrength : 0);

        private string BuildDump()
        {
            var sb = new StringBuilder(4096);
            Faction fac = PlayerFaction();
            sb.Append("<b>艦隊管理</b>　プレイヤー勢力：<color=#9fe0ff>").Append(fac).Append("</color>　(× で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            // 艦艇プール（総/割当/残）。
            int pool = FleetPool.Get(fac);
            int allocated = FleetPoolRules.Allocated(fac);
            int available = FleetPoolRules.Available(fac, pool);
            sb.Append("\n<color=#cfe8ff>◤ 艦艇プール</color>\n");
            sb.Append("  総 <color=#ffe08a>").Append(pool.ToString("#,0")).Append("</color>")
              .Append("　割当 ").Append(allocated.ToString("#,0"))
              .Append("　残 <color=#bfe9c0>").Append(available.ToString("#,0")).Append("</color>\n");

            IReadOnlyList<FleetUnitData> fleets = FleetRoster.AllFleets(fac);

            // 編制サマリ（純ロジックで集計）。
            var entries = new List<FleetCommandEntry>();
            if (fleets != null)
            {
                for (int i = 0; i < fleets.Count; i++)
                {
                    FleetUnitData u = fleets[i];
                    if (u == null) continue;
                    int str = FleetStrength(u);
                    bool active = u.status == FleetStatus.現役;
                    bool hasCmd = u.assignedAdmiral != null;
                    int cap = hasCmd ? CommandCapacityRules.MaxStrengthForTier(u.assignedAdmiral.rankTier) : 0;
                    entries.Add(new FleetCommandEntry(str, active, hasCmd, cap));
                }
            }
            FleetCommandSummary sum = FleetCommandSummaryRules.Summarize(entries);
            sb.Append("\n<color=#cfe8ff>◤ 編制サマリ</color>\n");
            sb.Append("  現役 <color=#ffe08a>").Append(sum.activeFleets).Append("</color>/").Append(sum.totalFleets).Append(" 隊")
              .Append("　総兵力 <color=#ffe08a>").Append(sum.totalStrength.ToString("#,0")).Append("</color>")
              .Append("　平均 ").Append(sum.averageStrength.ToString("#,0")).Append("\n");
            sb.Append("  ").Append(Warn("空席指揮", sum.vacantCommands))
              .Append("　").Append(Warn("過大指揮(要昇進/再配置)", sum.overCapacity)).Append('\n');

            // 各艦隊の点検。
            sb.Append("\n<color=#cfe8ff>◤ 艦隊一覧</color>\n");
            var prm = CommandStaffRules.CommandParams.Default;
            int shown = 0, totalActive = 0;
            if (fleets != null)
            {
                for (int i = 0; i < fleets.Count; i++)
                {
                    FleetUnitData u = fleets[i];
                    if (u == null) continue;
                    if (u.status == FleetStatus.現役) totalActive++;
                    if (shown >= maxFleets) continue;
                    AppendFleet(sb, u, prm); shown++;
                }
            }
            if (shown == 0) sb.Append("\n<color=#ffcc66>艦隊がありません（戦役を始めると編成されます）。</color>");
            else if (totalActive > shown || (fleets != null && fleets.Count > shown))
                sb.Append("\n<color=#8aa0b0>…他 ").Append(fleets.Count - shown).Append(" 隊</color>");

            sb.Append("\n\n<color=#6f8a9a>※ 下の発令欄で、選んだ艦隊の割り当て予算内で「やること」を決められる（再配置・司令任命の操作化は後段）。全勢力の在庫は艦艇(B)、編制ツリーは軍事(M)へ。</color>");
            return sb.ToString();
        }

        private void AppendFleet(StringBuilder sb, FleetUnitData u, CommandStaffRules.CommandParams prm)
        {
            int strength = FleetStrength(u);
            string status = u.status == FleetStatus.現役 ? "現役"
                          : u.status == FleetStatus.解隊 ? "<color=#ff9a8a>解隊</color>" : "<color=#8aa0b0>欠番</color>";
            sb.Append("\n<color=#bfe9c0>◆ ").Append(u.DisplayName).Append("</color>")
              .Append("　兵力 <color=#ffe08a>").Append(strength.ToString("#,0")).Append("</color>")
              .Append("　<color=#8aa0b0>[").Append(status).Append(" / ").Append(u.shipRole).Append("]</color>\n");

            if (u.assignedAdmiral != null)
            {
                AdmiralData a = u.assignedAdmiral;
                int cap = CommandCapacityRules.MaxStrengthForTier(a.rankTier);
                string rank = RankSystem.ResolveRankNameOrDefault(u.factionData, a.rankTier);
                string rankPart = string.IsNullOrEmpty(rank) ? "" : rank + " ";
                bool over = u.status == FleetStatus.現役 && FleetCommandSummaryRules.IsOverCapacity(strength, cap);
                sb.Append("    司令: ").Append(rankPart).Append(a.ShortName)
                  .Append("　指揮可能 〜").Append(cap.ToString("#,0")).Append("隻");
                if (over) sb.Append("　<color=#ff8080>⚠ 過大指揮</color>");
                sb.Append('\n');
                sb.Append("    <color=#9fb0c0>実効</color> 統率 ").Append(CommandStaffRules.EffectiveLeadership(u, prm))
                  .Append(" ／ 防御 ").Append(CommandStaffRules.EffectiveDefense(u, prm))
                  .Append(" ／ 運営 ").Append(CommandStaffRules.EffectiveOperation(u, prm))
                  .Append(" ／ 情報 ").Append(CommandStaffRules.EffectiveIntelligence(u, prm)).Append('\n');
            }
            else if (u.status == FleetStatus.現役)
            {
                sb.Append("    <color=#ff8080>司令空席 ⚠</color>\n");
            }
        }

        private static string Warn(string label, int count)
        {
            string col = count > 0 ? "#ff8080" : "#bfe9c0";
            return $"<color={col}>{label} {count}</color>";
        }

        // ===== UI =====

        private void BuildUI()
        {
            var canvasObj = new GameObject("PlayerFleetManagementCanvas");
            canvasObj.transform.SetParent(transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();

            root = new GameObject("Root");
            root.transform.SetParent(canvasObj.transform, false);
            var rrt = root.AddComponent<RectTransform>();
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;
            var dimImage = root.AddComponent<Image>();
            dimImage.color = new Color(0.02f, 0.03f, 0.06f, dimAlpha);
            WindowChrome.MakeNonModal(dimImage);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(root.transform, false);
            var prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.07f, 0.07f); prt.anchorMax = new Vector2(0.93f, 0.93f);
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.11f, 0.96f);
            panel.AddComponent<RectMask2D>();

            WindowChrome.AddTitleBarAnchored(prt, "艦隊管理", () => SetVisible(false));

            float topReserve = WindowChrome.TitleBarHeight;

            var labelGo = new GameObject("Body");
            labelGo.transform.SetParent(panel.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            // 下端は発令フッターのぶん空ける。
            lrt.offsetMin = new Vector2(20f, 20f + FooterHeight); lrt.offsetMax = new Vector2(-20f, -(12f + topReserve));
            bodyLabel = labelGo.AddComponent<TextMeshProUGUI>();
            bodyLabel.fontSize = bodyFontSize;
            bodyLabel.color = new Color(0.92f, 0.94f, 0.97f);
            bodyLabel.alignment = TextAlignmentOptions.TopLeft;
            bodyLabel.enableWordWrapping = true;
            bodyLabel.raycastTarget = false;
            if (jpFont != null) bodyLabel.font = jpFont;

            BuildFooter(panel.transform);
            root.SetActive(false);
        }

        /// <summary>下端の発令フッター＝艦隊選択（◀▶）＋やることトグル（補給/訓練/整備/哨戒/休養）＋予算表示＋発令。</summary>
        private void BuildFooter(Transform panel)
        {
            var footer = new GameObject("Footer");
            footer.transform.SetParent(panel, false);
            var frt = footer.AddComponent<RectTransform>();
            frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(1f, 0f);
            frt.pivot = new Vector2(0.5f, 0f);
            frt.offsetMin = new Vector2(16f, 12f); frt.offsetMax = new Vector2(-16f, 12f + FooterHeight);
            footer.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.15f, 0.95f);
            var vlg = footer.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 6, 6);
            vlg.spacing = 4f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = true;

            // 行1：艦隊選択。
            var row1 = MakeRow(footer.transform);
            MakeFooterButton(row1.transform, "◀", 44f, () => CycleFleet(-1), out _);
            fleetLabel = MakeFlexLabel(row1.transform, "");
            MakeFooterButton(row1.transform, "▶", 44f, () => CycleFleet(1), out _);

            // 行2：やること（トグル）。
            var row2 = MakeRow(footer.transform);
            for (int i = 0; i < TaskOrder.Length; i++)
            {
                FleetTask t = TaskOrder[i];
                MakeFooterButton(row2.transform, t.ToString(), 0f, () => ToggleTask(t), out taskBtnImgs[i]);
            }

            // 行3：予算表示＋発令。
            var row3 = MakeRow(footer.transform);
            budgetLabel = MakeFlexLabel(row3.transform, "");
            MakeFooterButton(row3.transform, "発令", 120f, IssueOrders, out issueBtnImg);
        }

        private GameObject MakeRow(Transform parent)
        {
            var row = new GameObject("Row");
            row.transform.SetParent(parent, false);
            row.AddComponent<RectTransform>();
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            return row;
        }

        private TextMeshProUGUI MakeFlexLabel(Transform parent, string text)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = 15f; t.alignment = TextAlignmentOptions.MidlineLeft;
            t.color = new Color(0.9f, 0.93f, 0.97f); t.raycastTarget = false;
            if (jpFont != null) t.font = jpFont;
            return t;
        }

        /// <summary>フッターのボタン（width>0 で固定幅・0 で均等伸長）。img を返して色更新に使う。</summary>
        private void MakeFooterButton(Transform parent, string label, float width, System.Action onClick, out Image img)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            img = go.AddComponent<Image>();
            img.color = IdleBtn;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick());
            var le = go.AddComponent<LayoutElement>();
            if (width > 0f) le.preferredWidth = width; else le.flexibleWidth = 1f;

            var t = new GameObject("T").AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(go.transform, false);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            t.text = label; t.fontSize = 15f; t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.92f, 0.95f, 0.98f); t.raycastTarget = false;
            if (jpFont != null) t.font = jpFont;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
    }
}
