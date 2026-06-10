using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Ginei
{
    /// <summary>
    /// 編制管理UI（#147・オーダー・オブ・バトルのビューア＋組み替え）。Battle シーンに O キーで開閉するモーダル。
    /// UI Toolkit（段階移行のパイロット）で実装：flexbox＋ScrollView でレイアウト自動＝見切れが起きない。
    /// プレイヤー勢力の梯団ツリー（軍集団 ⊃ 軍団 ⊃ 第N艦隊）を表示し、艦隊を別の軍団へ移す（中身流動）。
    /// 梯団の［任命］で在席提督を階級ゲート(#14)付きに選任（必要tier未満は選べない）。
    /// 数値ロジックは持たず <see cref="OrderOfBattle"/>/<see cref="FleetRoster"/> の static 窓口を読むだけ。
    /// 表示中は <see cref="Time.timeScale"/>=0 でポーズ（PauseManager は IsOpen の間ポーズ入力を譲る）。
    /// HelpOverlay と同じ RuntimeInitializeOnLoadMethod で Battle に自動生成。基盤は <see cref="GineiUITK"/>。
    /// </summary>
    public class OrderOfBattlePanel : MonoBehaviour
    {
        private static OrderOfBattlePanel instance;
        public static bool IsOpen => instance != null && instance.isOpen;

        private bool isOpen;
        private bool built;
        private float savedTimeScale = 1f;
        private VisualElement root;     // UIDocument のルート（全画面）
        private VisualElement list;     // ScrollView（行の追加先）
        private Label hintLabel;
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
            if (UnityEngine.Object.FindAnyObjectByType<OrderOfBattlePanel>() != null) return;
            new GameObject("OrderOfBattlePanel").AddComponent<OrderOfBattlePanel>();
        }

        private void Awake()
        {
            instance = this;
            Build();
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
            if (!built || root == null) return;
            faction = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            selectedFleet = 0;
            commandTarget = 0;
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            isOpen = true;
            root.style.display = DisplayStyle.Flex;
            Rebuild();
        }

        public void Close()
        {
            if (isOpen) { Time.timeScale = savedTimeScale; isOpen = false; }
            if (root != null) root.style.display = DisplayStyle.None;
        }

        // ===== ツリー構築 =====

        private void Rebuild()
        {
            if (list == null) return;
            list.Clear();

            if (hintLabel != null)
            {
                hintLabel.text = commandTarget > 0
                    ? "司令を選任中（×印は階級不足で選べない）"
                    : selectedFleet > 0
                        ? $"移動中: 第{selectedFleet}艦隊　→　移動先の軍団の［ここへ］を押す"
                        : "O:閉じる　／　艦隊[移動]→軍団[ここへ]で組替　／　梯団[任命]で司令選任";
            }

            if (commandTarget > 0) { RenderCommanderSelect(); return; }

            if (selectedFleet > 0)
                AddRow("　［移動を取消］", "取消", () => { selectedFleet = 0; Rebuild(); });

            var rendered = new HashSet<int>();
            foreach (var f in OrderOfBattle.AllFormations(faction))
                if (f.parentId == 0) RenderFormation(f, 0, rendered);

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
            string btn = null; Action act = null;
            if (selectedFleet > 0 && f.echelon == EchelonType.軍団)
            {
                btn = "ここへ"; act = () => MoveFleetTo(f, selectedFleet);
            }
            else if (selectedFleet == 0)
            {
                int fid = f.id; btn = "任命"; act = () => { commandTarget = fid; Rebuild(); };
            }
            AddRow($"{indent}◆ {f.echelon} {f.DisplayName}　司令: {cmd}", btn, act);

            foreach (int num in f.fleetNumbers) AddFleetRow(num, depth + 1, rendered);
            foreach (int childId in f.childFormationIds)
            {
                var child = OrderOfBattle.Get(childId);
                if (child != null) RenderFormation(child, depth + 1, rendered);
            }
        }

        private void AddFleetRow(int num, int depth, HashSet<int> rendered)
        {
            if (!rendered.Add(num)) return;
            string indent = new string('　', depth);
            FleetUnitData u = FleetRoster.GetFleet(faction, num);
            string admiral = (u != null && u.assignedAdmiral != null) ? AdmiralLabel(u.assignedAdmiral) : "（空席）";
            bool isSelected = selectedFleet == num;
            AddRow($"{indent}└ 第{num}艦隊　{admiral}{(isSelected ? "　★移動中" : "")}",
                "移動", () => { selectedFleet = num; Rebuild(); });
        }

        /// <summary>司令選任モード：候補提督を階級ゲート付きで一覧（×は不可・選べない）。</summary>
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
                    ok ? (Action)(() => { OrderOfBattle.AssignCommander(f.id, adm); commandTarget = 0; Rebuild(); }) : null);
            }
        }

        private List<AdmiralData> CandidateAdmirals()
        {
            var seen = new HashSet<AdmiralData>();
            var list2 = new List<AdmiralData>();
            foreach (var u in FleetRoster.AllFleets(faction))
                if (u != null && u.assignedAdmiral != null && seen.Add(u.assignedAdmiral)) list2.Add(u.assignedAdmiral);
            foreach (var f in OrderOfBattle.AllFormations(faction))
                if (f.commander != null && seen.Add(f.commander)) list2.Add(f.commander);
            return list2;
        }

        private void MoveFleetTo(MilitaryFormation corps, int fleetNumber)
        {
            OrderOfBattle.AttachFleet(corps.id, fleetNumber);
            string groupName = "";
            var parent = OrderOfBattle.Get(corps.parentId);
            if (parent != null && parent.echelon == EchelonType.軍集団) groupName = parent.name;
            FleetStrength fs = FindFlagship(fleetNumber);
            if (fs != null) { fs.corpsName = corps.name; fs.armyGroupName = groupName; }
            selectedFleet = 0;
            Rebuild();
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

        // ===== UI 生成（UI Toolkit） =====

        private void Build()
        {
            GineiUITK.Attach(gameObject, 95, out root);
            if (root == null) { built = false; return; }

            // 背景ディマー（クリックで閉じる＝ディマー自身がクリック対象のときだけ）
            var dim = new VisualElement();
            dim.AddToClassList("dim");
            dim.RegisterCallback<ClickEvent>(evt => { if (evt.target == dim) Close(); });
            root.Add(dim);

            var panel = new VisualElement();
            panel.AddToClassList("panel");
            dim.Add(panel);

            var title = new Label("編制（オーダー・オブ・バトル）");
            title.AddToClassList("title");
            panel.Add(title);

            hintLabel = new Label("");
            hintLabel.AddToClassList("hint");
            panel.Add(hintLabel);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("scroll");
            panel.Add(scroll);
            list = scroll; // scroll.Add は contentContainer に入る

            var close = new Button(Close) { text = "閉じる (O / Esc)" };
            close.AddToClassList("footer-btn");
            panel.Add(close);

            root.style.display = DisplayStyle.None; // 初期は非表示
            built = true;
        }

        private void AddRow(string text, string btnLabel = null, Action onClick = null)
        {
            if (list == null) return;
            var row = new VisualElement();
            row.AddToClassList("row");

            var label = new Label(text);
            label.AddToClassList("row-label");
            row.Add(label);

            if (!string.IsNullOrEmpty(btnLabel) && onClick != null)
            {
                var btn = new Button(() => onClick()) { text = btnLabel };
                btn.AddToClassList("btn");
                row.Add(btn);
            }
            list.Add(row);
        }

        private void OnDestroy()
        {
            if (isOpen) Time.timeScale = savedTimeScale;
            if (instance == this) instance = null;
        }
    }
}
