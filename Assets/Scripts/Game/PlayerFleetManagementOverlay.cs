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
    /// 全勢力の在庫）に対し、こちらは<b>自勢力の艦隊点検</b>に特化（要昇進/空席を赤で警告）。集計は既存窓口を読むだけ＝
    /// <b>観測専用・状態は変えない</b>（再配置・任命の操作化は後段）。`FleetObserverOverlay` と同型の自動生成（Strategy/Battle）。
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
                bodyLabel.text = BuildDump();
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

            sb.Append("\n\n<color=#6f8a9a>※ 観測専用＝点検のみ（再配置・司令任命の操作化は後段）。全勢力の在庫は艦艇(B)、編制ツリーは軍事(M)へ。</color>");
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
            lrt.offsetMin = new Vector2(20f, 20f); lrt.offsetMax = new Vector2(-20f, -(12f + topReserve));
            bodyLabel = labelGo.AddComponent<TextMeshProUGUI>();
            bodyLabel.fontSize = bodyFontSize;
            bodyLabel.color = new Color(0.92f, 0.94f, 0.97f);
            bodyLabel.alignment = TextAlignmentOptions.TopLeft;
            bodyLabel.enableWordWrapping = true;
            bodyLabel.raycastTarget = false;
            if (jpFont != null) bodyLabel.font = jpFont;

            root.SetActive(false);
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
