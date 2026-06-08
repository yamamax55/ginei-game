using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 編制管理UI（#147・オーダー・オブ・バトルのビューア＋組み替え）。Battle シーンに O キーで開閉するモーダル。
    /// プレイヤー勢力の梯団ツリー（軍集団 ⊃ 軍団 ⊃ 第N艦隊）を表示し、艦隊を別の軍団へ移す（中身流動）。
    /// 数値ロジックは持たず <see cref="OrderOfBattle"/>/<see cref="FleetRoster"/> の static 窓口を読むだけ。
    /// 表示中は <see cref="Time.timeScale"/>=0 でポーズ（PauseManager は IsOpen の間ポーズ入力を譲る）。
    /// HelpOverlay と同じく RuntimeInitializeOnLoadMethod で Battle シーンに自動生成（手配線不要）。
    /// </summary>
    public class OrderOfBattlePanel : MonoBehaviour
    {
        private static OrderOfBattlePanel instance;
        public static bool IsOpen => instance != null && instance.isOpen;

        private bool isOpen;
        private float savedTimeScale = 1f;
        private GameObject root;
        private Transform listContent;
        private TextMeshProUGUI hintText;
        private Faction faction;
        private int selectedFleet;  // 移動対象の艦隊番号（0=未選択）
        private int commandTarget;  // 司令選任中の梯団id（0=未選択）

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
            if (scene.name != "Battle") return;
            if (Object.FindAnyObjectByType<OrderOfBattlePanel>() != null) return;
            new GameObject("OrderOfBattlePanel").AddComponent<OrderOfBattlePanel>();
        }

        private void Awake()
        {
            instance = this;
            Build();
            if (root != null) root.SetActive(false);
        }

        private void Update()
        {
            if (Keyboard.current == null) return;
            if (Keyboard.current.oKey.wasPressedThisFrame) Toggle();
            if (isOpen)
            {
                Time.timeScale = 0f; // 倍速キー等で解除されてもポーズ維持
                if (Keyboard.current.escapeKey.wasPressedThisFrame) Close();
            }
        }

        private void Toggle() { if (isOpen) Close(); else Open(); }

        private void Open()
        {
            faction = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            selectedFleet = 0;
            commandTarget = 0;
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            isOpen = true;
            if (root != null) root.SetActive(true);
            Rebuild();
        }

        public void Close()
        {
            if (isOpen) { Time.timeScale = savedTimeScale; isOpen = false; }
            if (root != null) root.SetActive(false);
        }

        // ===== ツリー構築 =====

        private void Rebuild()
        {
            if (listContent == null) return;
            for (int i = listContent.childCount - 1; i >= 0; i--) Destroy(listContent.GetChild(i).gameObject);

            if (hintText != null)
            {
                hintText.text = commandTarget > 0
                    ? "司令を選任中（×印は階級不足で選べない）"
                    : selectedFleet > 0
                        ? $"移動中: 第{selectedFleet}艦隊　→　移動先の軍団の［ここへ］を押す"
                        : "O:閉じる　／　艦隊[移動]→軍団[ここへ]で組替　／　梯団[任命]で司令選任";
            }

            // 司令選任モード：候補提督を階級ゲート付きで一覧（×は不可）。
            if (commandTarget > 0) { RenderCommanderSelect(); return; }

            if (selectedFleet > 0)
                AddRow("　［移動を取消］", "取消", () => { selectedFleet = 0; Rebuild(); });

            var rendered = new HashSet<int>();
            // 最上位（親なし）の梯団から描画。軍集団→軍団→艦隊の順に。
            foreach (var f in OrderOfBattle.AllFormations(faction))
                if (f.parentId == 0) RenderFormation(f, 0, rendered);

            // どの梯団にも属さない艦隊（未編入）
            var unassigned = new List<FleetUnitData>();
            foreach (var u in FleetRoster.AllFleets(faction))
                if (u != null && u.IsActive && !rendered.Contains(u.fleetNumber)) unassigned.Add(u);
            if (unassigned.Count > 0)
            {
                AddRow("― 未編入 ―");
                foreach (var u in unassigned) AddFleetRow(u.fleetNumber, 1, rendered);
            }
        }

        private void RenderFormation(MilitaryFormation f, int depth, HashSet<int> rendered)
        {
            string indent = new string('　', depth);
            string cmd = CommanderLabel(f.commander);
            // 移動中＝軍団に「ここへ」、通常＝梯団に「任命」（司令選任へ）。
            string btn = null; UnityEngine.Events.UnityAction act = null;
            if (selectedFleet > 0 && f.echelon == EchelonType.軍団)
            {
                btn = "ここへ"; act = () => MoveFleetTo(f, selectedFleet);
            }
            else if (selectedFleet == 0)
            {
                int fid = f.id; btn = "任命"; act = () => { commandTarget = fid; Rebuild(); };
            }
            AddRow($"{indent}◆ {f.echelon} {f.DisplayName}　司令: {cmd}", btn, act);

            // 直下の艦隊
            foreach (int num in f.fleetNumbers) AddFleetRow(num, depth + 1, rendered);
            // 下位梯団
            foreach (int childId in f.childFormationIds)
            {
                var child = OrderOfBattle.Get(childId);
                if (child != null) RenderFormation(child, depth + 1, rendered);
            }
        }

        private void AddFleetRow(int num, int depth, HashSet<int> rendered)
        {
            if (!rendered.Add(num)) return; // 二重描画防止
            string indent = new string('　', depth);
            FleetUnitData u = FleetRoster.GetFleet(faction, num);
            string admiral = (u != null && u.assignedAdmiral != null)
                ? AdmiralLabel(u.assignedAdmiral) : "（空席）";
            bool isSelected = selectedFleet == num;
            AddRow($"{indent}└ 第{num}艦隊　{admiral}{(isSelected ? "　★移動中" : "")}",
                "移動", () => { selectedFleet = num; Rebuild(); });
        }

        /// <summary>艦隊を指定軍団へ移す（中身流動）。表示中の FleetStrength の梯団名も更新する。</summary>
        private void MoveFleetTo(MilitaryFormation corps, int fleetNumber)
        {
            OrderOfBattle.AttachFleet(corps.id, fleetNumber);

            // 軍団の親が軍集団ならその名も反映
            string groupName = "";
            var parent = OrderOfBattle.Get(corps.parentId);
            if (parent != null && parent.echelon == EchelonType.軍集団) groupName = parent.name;

            FleetStrength fs = FindFlagship(fleetNumber);
            if (fs != null) { fs.corpsName = corps.name; fs.armyGroupName = groupName; }

            selectedFleet = 0;
            Rebuild();
        }

        /// <summary>司令選任モードの描画：候補提督を階級ゲート付きで一覧（×は不可・選べない）。</summary>
        private void RenderCommanderSelect()
        {
            MilitaryFormation f = OrderOfBattle.Get(commandTarget);
            if (f == null) { commandTarget = 0; Rebuild(); return; }

            int req = OrderOfBattle.RequiredTier(f.echelon);
            string reqName = RankSystem.ResolveRankNameOrDefault(null, req);
            AddRow($"◆ {f.echelon} {f.DisplayName}　現任司令: {CommanderLabel(f.commander)}");
            AddRow($"必要階級: {reqName} 以上", "戻る", () => { commandTarget = 0; Rebuild(); });
            if (f.HasCommander)
                AddRow("　現司令を解任して空席に", "解任", () => { OrderOfBattle.UnassignCommander(f.id); commandTarget = 0; Rebuild(); });

            AddRow("― 任命候補 ―");
            foreach (AdmiralData adm in CandidateAdmirals())
            {
                bool ok = OrderOfBattle.CanCommand(adm, f.echelon);
                string mark = ok ? "○" : "×";
                string suffix = ok ? "" : "　（階級不足）";
                AddRow($"　{mark} {AdmiralLabel(adm)}{suffix}",
                    ok ? "選ぶ" : null,
                    ok ? (UnityEngine.Events.UnityAction)(() => { OrderOfBattle.AssignCommander(f.id, adm); commandTarget = 0; Rebuild(); }) : null);
            }
        }

        /// <summary>任命候補＝プレイヤー勢力の在席提督（艦隊の配属指揮官＋現任の梯団司令）を重複なく集める。</summary>
        private List<AdmiralData> CandidateAdmirals()
        {
            var seen = new HashSet<AdmiralData>();
            var list = new List<AdmiralData>();
            foreach (var u in FleetRoster.AllFleets(faction))
                if (u != null && u.assignedAdmiral != null && seen.Add(u.assignedAdmiral)) list.Add(u.assignedAdmiral);
            foreach (var f in OrderOfBattle.AllFormations(faction))
                if (f.commander != null && seen.Add(f.commander)) list.Add(f.commander);
            return list;
        }

        private FleetStrength FindFlagship(int fleetNumber)
        {
            var flags = FleetRegistry.AllFlagships;
            for (int i = 0; i < flags.Count; i++)
            {
                FleetStrength fs = flags[i];
                if (fs != null && fs.faction == faction && fs.fleetNumber == fleetNumber) return fs;
            }
            return null;
        }

        private static string CommanderLabel(AdmiralData a) => a == null ? "（空席）" : AdmiralLabel(a);

        private static string AdmiralLabel(AdmiralData a)
        {
            string rank = RankSystem.ResolveRankNameOrDefault(null, a.rankTier);
            string name = a.ShortName;
            return string.IsNullOrEmpty(rank) ? name : $"{rank} {name}";
        }

        // ===== UI 生成 =====

        private void Build()
        {
            EnsureEventSystem();

            var canvasObj = new GameObject("OrderOfBattleCanvas");
            canvasObj.transform.SetParent(transform, false);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(canvasObj.transform, false);
            StretchFull(root.GetComponent<RectTransform>());

            var dim = new GameObject("Dimmer", typeof(RectTransform));
            dim.transform.SetParent(root.transform, false);
            StretchFull(dim.GetComponent<RectTransform>());
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.6f);
            var dimBtn = dim.AddComponent<Button>();
            dimBtn.transition = UnityEngine.UI.Selectable.Transition.None;
            dimBtn.onClick.AddListener(Close);

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            var pRT = panel.GetComponent<RectTransform>();
            pRT.anchorMin = pRT.anchorMax = pRT.pivot = new Vector2(0.5f, 0.5f);
            pRT.sizeDelta = new Vector2(740f, 760f);
            var pImg = panel.AddComponent<Image>();
            pImg.color = new Color(0.06f, 0.07f, 0.12f, 0.97f);
            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 20, 20);
            vlg.spacing = 8f;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperLeft;

            CreateText(panel.transform, "編制（オーダー・オブ・バトル）", 26f, FontStyles.Bold, TextAlignmentOptions.Center);
            hintText = CreateText(panel.transform, "", 17f, FontStyles.Normal, TextAlignmentOptions.Left);
            hintText.color = new Color(0.7f, 0.8f, 0.9f);

            // ツリー本体（スクロール可能。艦隊数が多くても溢れない）
            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.transform.SetParent(panel.transform, false);
            var scrollLE = scrollGo.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(scrollGo.transform, false);
            StretchFull(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<RectMask2D>();
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0.12f); // スクロールのレイキャスト受け
            scroll.viewport = viewport.GetComponent<RectTransform>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var cRT = content.GetComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0f, 1f); cRT.anchorMax = new Vector2(1f, 1f);
            cRT.pivot = new Vector2(0.5f, 1f); cRT.anchoredPosition = Vector2.zero;
            cRT.sizeDelta = new Vector2(0f, 0f); // 幅＝ビューポート幅（横溢れ＝ボタン見切れ防止）。高さは ContentSizeFitter
            var cVlg = content.AddComponent<VerticalLayoutGroup>();
            cVlg.spacing = 4f; cVlg.childControlWidth = true; cVlg.childForceExpandWidth = true;
            cVlg.childControlHeight = true; cVlg.childForceExpandHeight = false;
            cVlg.childAlignment = TextAnchor.UpperLeft;
            var cFit = content.AddComponent<ContentSizeFitter>();
            cFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = cRT;
            listContent = content.transform;

            CreateButton(panel.transform, "閉じる (O / Esc)", Close);

            root.SetActive(false);
        }

        private void AddRow(string text, string btnLabel = null, UnityEngine.Events.UnityAction onClick = null)
        {
            var row = new GameObject("Row", typeof(RectTransform));
            row.transform.SetParent(listContent, false);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(0, 6, 0, 0); // 右に少し余白（ボタンがマスク端に触れない）
            hlg.spacing = 8f; hlg.childControlWidth = true; hlg.childForceExpandWidth = false;
            hlg.childControlHeight = true; hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            var rle = row.AddComponent<LayoutElement>(); rle.minHeight = 28f;

            var t = CreateText(row.transform, text, 18f, FontStyles.Normal, TextAlignmentOptions.Left);
            var tle = t.gameObject.AddComponent<LayoutElement>(); tle.flexibleWidth = 1f;

            if (!string.IsNullOrEmpty(btnLabel) && onClick != null)
            {
                var b = new GameObject("Btn", typeof(RectTransform));
                b.transform.SetParent(row.transform, false);
                var img = b.AddComponent<Image>(); img.color = new Color(0.25f, 0.35f, 0.5f, 1f);
                var btn = b.AddComponent<Button>();
                btn.transition = UnityEngine.UI.Selectable.Transition.None;
                btn.onClick.AddListener(onClick);
                var ble = b.AddComponent<LayoutElement>(); ble.preferredWidth = 96f; ble.preferredHeight = 28f;
                var bt = CreateText(b.transform, btnLabel, 16f, FontStyles.Bold, TextAlignmentOptions.Center);
                StretchFull(bt.rectTransform);
            }
        }

        private TextMeshProUGUI CreateText(Transform parent, string text, float size, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.fontStyle = style; t.alignment = align;
            t.color = Color.white; t.raycastTarget = false;
            var ja = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (ja != null) t.font = ja;
            return t;
        }

        private void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = new Color(0.2f, 0.25f, 0.4f, 1f);
            var btn = go.AddComponent<Button>(); btn.transition = UnityEngine.UI.Selectable.Transition.None;
            btn.onClick.AddListener(onClick);
            var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 46f;
            var txt = CreateText(go.transform, label, 22f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchFull(txt.rectTransform);
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private void OnDestroy()
        {
            if (isOpen) Time.timeScale = savedTimeScale;
            if (instance == this) instance = null;
        }
    }
}
