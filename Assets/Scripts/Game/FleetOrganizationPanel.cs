using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Ginei
{
    /// <summary>
    /// 戦略マップの艦隊編成画面（#148）。Strategy シーンに <b>B キー</b>で開閉するモーダル（表示中はポーズ）。
    /// 勢力の艦隊プール（総艦艇/割当/残）を各艦隊へ配分し、各艦隊へ<b>提督・副提督・参謀</b>（#885）を階級ゲート(#14)付きで配属する。
    /// 数値ロジックは持たず <see cref="FleetPoolRules"/>/<see cref="FleetRoster"/>/<see cref="CommandStaffRules"/> の static 窓口を読むだけ。
    /// 戦略デモにはロスターが無いため、空なら<b>デモ提督＋艦隊をシード</b>する（実シナリオは既存ロスターを使う＝シードしない）。
    /// UI Toolkit（<see cref="GineiUITK"/>）。Battle 版の梯団管理 <see cref="OrderOfBattlePanel"/> の姉妹（艦隊個別の編成を担う）。
    /// </summary>
    public class FleetOrganizationPanel : MonoBehaviour
    {
        private enum StaffSlot { None, 司令, 副提督, 参謀 }

        private static FleetOrganizationPanel instance;
        public static bool IsOpen => instance != null && instance.isOpen;

        [Tooltip("勢力プールの初期シード値（FleetPool 未設定時のみ適用）。実プールは FleetPool＝#884 造船で増減")]
        public int factionPoolTotal = 12000;
        [Tooltip("艦艇数の増減ステップ")]
        public int allocationStep = 500;

        private bool isOpen;
        private bool built;
        private float savedTimeScale = 1f;
        private VisualElement root;
        private VisualElement list;
        private Label hintLabel;
        private Faction faction;

        private int detailFleet;            // 編集中の艦隊番号（0=一覧）
        private StaffSlot selectingSlot;    // 選任中のスロット（None=詳細）
        private List<AdmiralData> admiralPool; // デモ提督プール（候補）

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
            if (UnityEngine.Object.FindAnyObjectByType<FleetOrganizationPanel>() != null) return;
            new GameObject("FleetOrganizationPanel").AddComponent<FleetOrganizationPanel>();
        }

        private void Awake()
        {
            instance = this;
            Build();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;
            if (Keyboard.current.bKey.wasPressedThisFrame) Toggle();
            if (isOpen)
            {
                Time.timeScale = 0f; // 開いている間はポーズを維持
                if (Keyboard.current.escapeKey.wasPressedThisFrame) Back();
            }
        }

        private void Toggle() { if (isOpen) Close(); else Open(); }

        /// <summary>Esc：選任→詳細→一覧→閉じる、と1段ずつ戻る。</summary>
        private void Back()
        {
            if (selectingSlot != StaffSlot.None) { selectingSlot = StaffSlot.None; Rebuild(); }
            else if (detailFleet > 0) { detailFleet = 0; Rebuild(); }
            else Close();
        }

        private void Open()
        {
            if (!built || root == null) return;
            faction = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            detailFleet = 0;
            selectingSlot = StaffSlot.None;
            SeedDemoIfEmpty();
            // 実プール（#884 造船供給先）が未設定ならシード値で初期化。GalaxyView の造船で増えていく。
            if (FleetPool.Get(faction) <= 0) FleetPool.Set(faction, Mathf.Max(0, factionPoolTotal));
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

        // ===== デモシード（戦略デモにロスターが無い場合のみ） =====

        private void SeedDemoIfEmpty()
        {
            foreach (var u in FleetRoster.AllFleets(faction))
                if (u != null && u.IsActive) return; // 既にロスターあり＝シードしない

            admiralPool = new List<AdmiralData>
            {
                MakeAdmiral("ロイエンタール", 9),
                MakeAdmiral("ミッターマイアー", 9),
                MakeAdmiral("ワーレン", 8),
                MakeAdmiral("ビッテンフェルト", 8),
                MakeAdmiral("ケンプ", 7),
                MakeAdmiral("参謀ベルゲングリューン", 6),
                MakeAdmiral("若手士官", 5), // 司令には階級不足（×）＝ゲートの体感用
            };

            int req = OrderOfBattle.RequiredTier(EchelonType.艦隊);
            var f1 = FleetRoster.CreateFleet(faction); f1.baseStrength = 3000; FleetRoster.AssignAdmiral(f1, admiralPool[0], req);
            var f2 = FleetRoster.CreateFleet(faction); f2.baseStrength = 3000; FleetRoster.AssignAdmiral(f2, admiralPool[1], req);
            var f3 = FleetRoster.CreateFleet(faction); f3.baseStrength = 2000; FleetRoster.AssignAdmiral(f3, admiralPool[2], req);
            // 残りは予備（候補プール）
        }

        private AdmiralData MakeAdmiral(string name, int tier)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.admiralName = name; a.rankTier = tier; a.faction = faction;
            a.leadership = 60; a.attack = 60; a.defense = 60; a.mobility = 60; a.operation = 55; a.intelligence = 55;
            return a;
        }

        // ===== 描画 =====

        private void Rebuild()
        {
            if (list == null) return;
            list.Clear();
            if (detailFleet > 0)
            {
                if (selectingSlot != StaffSlot.None) RenderStaffSelect();
                else RenderFleetDetail();
            }
            else RenderMain();
        }

        private void RenderMain()
        {
            int total = FleetPool.Get(faction);
            int alloc = FleetPoolRules.Allocated(faction);
            int avail = FleetPoolRules.Available(faction);
            if (hintLabel != null) hintLabel.text = "B/Esc:閉じる　／　各艦隊[編集]で艦艇数・指揮班を設定（プールは造船#884で増える）";

            AddRow($"勢力: {faction}　艦艇プール  総 {total} ／ 割当 {alloc} ／ 残 {avail}");
            AddRow("― 艦隊一覧 ―", "新艦隊", () => { FleetRoster.CreateFleet(faction); Rebuild(); });

            foreach (var u in FleetRoster.AllFleets(faction))
            {
                if (u == null || !u.IsActive) continue;
                int num = u.fleetNumber;
                string cmd = u.assignedAdmiral != null ? AdmiralLabel(u.assignedAdmiral) : "（空席）";
                AddRow($"第{num}艦隊　司令:{cmd}　副:{Short(u.viceCommander)}　参謀:{Short(u.chiefOfStaff)}　艦艇:{u.baseStrength}",
                    "編集", () => { detailFleet = num; selectingSlot = StaffSlot.None; Rebuild(); });
            }
        }

        private void RenderFleetDetail()
        {
            FleetUnitData u = FleetRoster.GetFleet(faction, detailFleet);
            if (u == null) { detailFleet = 0; Rebuild(); return; }
            int avail = FleetPoolRules.Available(faction);
            if (hintLabel != null) hintLabel.text = "艦艇数±／司令・副提督・参謀を配属（×は階級不足/不適格）";

            AddRow($"◆ 第{detailFleet}艦隊（{u.DisplayName}）　プール残 {avail}");
            AddButtonsRow($"艦艇数: {u.baseStrength}",
                ($"−{allocationStep}", () => { FleetPoolRules.Adjust(u, -allocationStep); Rebuild(); }),
                ($"＋{allocationStep}", () => { FleetPoolRules.Adjust(u, +allocationStep); Rebuild(); }));
            AddRow($"司令　: {Slot(u.assignedAdmiral)}", "変更", () => { selectingSlot = StaffSlot.司令; Rebuild(); });
            AddRow($"副提督: {Slot(u.viceCommander)}", "変更", () => { selectingSlot = StaffSlot.副提督; Rebuild(); });
            AddRow($"参謀　: {Slot(u.chiefOfStaff)}", "変更", () => { selectingSlot = StaffSlot.参謀; Rebuild(); });
            AddRow("　", "戻る", () => { detailFleet = 0; Rebuild(); });
        }

        private void RenderStaffSelect()
        {
            FleetUnitData u = FleetRoster.GetFleet(faction, detailFleet);
            if (u == null) { detailFleet = 0; selectingSlot = StaffSlot.None; Rebuild(); return; }
            int req = OrderOfBattle.RequiredTier(EchelonType.艦隊);
            if (hintLabel != null) hintLabel.text = $"第{detailFleet}艦隊の {selectingSlot} を選任（×は不可）";

            AddRow($"◆ 第{detailFleet}艦隊　{selectingSlot} を選任");
            AddRow("　現任を解任して空席に", "解任", () => { ClearSlot(u, selectingSlot); selectingSlot = StaffSlot.None; Rebuild(); });
            AddRow("― 候補 ―", "戻る", () => { selectingSlot = StaffSlot.None; Rebuild(); });

            if (admiralPool != null)
            {
                foreach (AdmiralData adm in admiralPool)
                {
                    if (adm == null) continue;
                    bool ok = EvalCandidate(u, adm, req, out string reason);
                    string mark = ok ? "○" : "×";
                    AddRow($"　{mark} {AdmiralLabel(adm)}{(ok ? "" : "　" + reason)}",
                        ok ? "選ぶ" : null,
                        ok ? (Action)(() => { AssignSlot(u, selectingSlot, adm, req); selectingSlot = StaffSlot.None; Rebuild(); }) : null);
                }
            }
        }

        // ===== 配属の評価・適用（Core 窓口へ委譲） =====

        private bool EvalCandidate(FleetUnitData u, AdmiralData adm, int req, out string reason)
        {
            reason = "";
            switch (selectingSlot)
            {
                case StaffSlot.司令:
                    if (!CommandStaffRules.CanAssignCommander(adm, req)) { reason = "（階級不足）"; return false; }
                    if (IsCommanderElsewhere(adm, detailFleet)) { reason = "（他艦隊司令）"; return false; }
                    return true;
                case StaffSlot.副提督:
                    if (u.assignedAdmiral == null) { reason = "（先に司令）"; return false; }
                    if (!CommandStaffRules.CanAssignVice(u, adm)) { reason = "（提督超/兼任）"; return false; }
                    return true;
                case StaffSlot.参謀:
                    if (!CommandStaffRules.CanAssignChief(u, adm)) { reason = "（兼任不可）"; return false; }
                    return true;
            }
            return false;
        }

        private void AssignSlot(FleetUnitData u, StaffSlot slot, AdmiralData adm, int req)
        {
            switch (slot)
            {
                case StaffSlot.司令: FleetRoster.ReassignAdmiral(u, adm, req); break;
                case StaffSlot.副提督: CommandStaffRules.AssignVice(u, adm); break;
                case StaffSlot.参謀: CommandStaffRules.AssignChief(u, adm); break;
            }
        }

        private void ClearSlot(FleetUnitData u, StaffSlot slot)
        {
            switch (slot)
            {
                case StaffSlot.司令: FleetRoster.Unassign(u); break;
                case StaffSlot.副提督: u.viceCommander = null; break;
                case StaffSlot.参謀: u.chiefOfStaff = null; break;
            }
        }

        private bool IsCommanderElsewhere(AdmiralData adm, int exceptFleetNum)
        {
            foreach (var u in FleetRoster.AllFleets(faction))
                if (u != null && u.IsActive && u.fleetNumber != exceptFleetNum && u.assignedAdmiral == adm) return true;
            return false;
        }

        // ===== ラベル =====

        private static string Slot(AdmiralData a) => a != null ? AdmiralLabel(a) : "（空席）";
        private static string Short(AdmiralData a) => a == null ? "—" : a.ShortName;

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

            var dim = new VisualElement();
            dim.AddToClassList("dim");
            dim.RegisterCallback<ClickEvent>(evt => { if (evt.target == dim) Close(); });
            root.Add(dim);

            var panel = new VisualElement();
            panel.AddToClassList("panel");
            dim.Add(panel);

            var title = new Label("艦隊編成");
            title.AddToClassList("title");
            panel.Add(title);

            hintLabel = new Label("");
            hintLabel.AddToClassList("hint");
            panel.Add(hintLabel);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("scroll");
            panel.Add(scroll);
            list = scroll;

            var close = new Button(Close) { text = "閉じる (B / Esc)" };
            close.AddToClassList("footer-btn");
            panel.Add(close);

            root.style.display = DisplayStyle.None;
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

        private void AddButtonsRow(string text, params (string label, Action act)[] buttons)
        {
            if (list == null) return;
            var row = new VisualElement();
            row.AddToClassList("row");
            var label = new Label(text);
            label.AddToClassList("row-label");
            row.Add(label);
            if (buttons != null)
            {
                foreach (var b in buttons)
                {
                    if (string.IsNullOrEmpty(b.label) || b.act == null) continue;
                    var btn = new Button(() => b.act()) { text = b.label };
                    btn.AddToClassList("btn");
                    row.Add(btn);
                }
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
