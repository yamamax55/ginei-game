using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ginei
{
    /// <summary>#1066 艦艇設計。登録済み設計、技術スロット、性能比較、改名・登録を一画面で扱う。</summary>
    public sealed class ShipDesignPanel : MonoBehaviour
    {
        private static ShipDesignPanel instance;
        public static bool IsOpen => instance != null && instance.isOpen;

        private bool isOpen;
        private VisualElement root;
        private GineiList<ShipDesign> designList;
        private DropdownField classField;
        private DropdownField hullField;
        private TextField nameField;
        private VisualElement slots;
        private Label capacityLabel;
        private Label performanceLabel;
        private Label messageLabel;
        private Button registerButton;
        private Button renameButton;
        private Button activateButton;
        private ShipDesignState state;
        private ShipDesign selected;
        private HullSpec draftHull;
        private ShipModule[] draftModules = Array.Empty<ShipModule>();
        private object windowToken;

        public static ShipDesignPanel InstanceForTest => instance;
        public ShipDesignState StateForTest => state;
        public ShipModule[] DraftModulesForTest => draftModules;
        public Label PerformanceLabelForTest => performanceLabel;
        public Label MessageLabelForTest => messageLabel;

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
            if (scene.name != "Strategy" || FindAnyObjectByType<ShipDesignPanel>() != null) return;
            new GameObject("ShipDesignPanel").AddComponent<ShipDesignPanel>();
        }

        private void Awake()
        {
            instance = this;
            Build();
            windowToken = UIWindowStack.Register(() => isOpen, Hide, 98, "艦艇設計");
        }

        public static void Show()
        {
            if (instance == null)
            {
                var go = new GameObject("ShipDesignPanel");
                instance = go.AddComponent<ShipDesignPanel>();
            }
            instance.Open();
        }

        public static void Toggle() { if (IsOpen) Hide(); else Show(); }

        public static void Hide()
        {
            if (instance == null) return;
            instance.isOpen = false;
            if (instance.root != null) instance.root.style.display = DisplayStyle.None;
        }

        private void Open()
        {
            if (root == null) return;
            isOpen = true;
            root.style.display = DisplayStyle.Flex;
            Faction faction = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            FactionState factionState = StrategySession.Campaign == null ? null : CampaignRules.GetState(StrategySession.Campaign, faction);
            state = factionState?.shipDesigns;
            if (state == null)
            {
                messageLabel.text = "戦役の設計台帳がありません";
                registerButton.SetEnabled(false);
                renameButton.SetEnabled(false);
                activateButton.SetEnabled(false);
                designList.SetItems(Array.Empty<ShipDesign>());
                return;
            }
            ShipDesignRules.NormalizeLoaded(state);
            selected = null;
            StartDraft((ShipClass)classField.index, 0);
            RefreshList();
        }

        private void Build()
        {
            GineiUITK.Attach(gameObject, 1180, out root);
            if (root == null) return;
            var dim = new VisualElement(); dim.AddToClassList("dim");
            dim.RegisterCallback<ClickEvent>(evt => { if (evt.target == dim) Hide(); });
            root.Add(dim);
            var panel = new VisualElement(); panel.AddToClassList("panel"); panel.style.width = 1120f; panel.style.maxHeight = Length.Percent(94); dim.Add(panel);
            var title = new Label("艦艇設計局"); title.AddToClassList("title"); panel.Add(title);
            var hint = new Label("登録済み設計を確認し、艦体と技術スロットを編集します。水色は現役設計より向上、ピンクは低下です。"); hint.AddToClassList("hint"); panel.Add(hint);

            var columns = new VisualElement(); columns.style.flexDirection = FlexDirection.Row; columns.style.flexGrow = 1f; columns.style.minHeight = 500f; panel.Add(columns);
            designList = new GineiList<ShipDesign>("登録済み設計") { PageSize = 12 }; designList.style.width = 480f; designList.style.flexGrow = 0f;
            designList.SetColumns(new[]
            {
                new GineiListColumn<ShipDesign>("採用", d => d.active ? "●" : "", (a,b) => a.active.CompareTo(b.active), 54f),
                new GineiListColumn<ShipDesign>("艦種", d => d.shipClass.ToString(), (a,b) => a.shipClass.CompareTo(b.shipClass), 82f),
                new GineiListColumn<ShipDesign>("設計名", d => d.designName, (a,b) => string.Compare(a.designName,b.designName,StringComparison.Ordinal), 210f),
                new GineiListColumn<ShipDesign>("戦闘力", d => ArmamentDesignRules.CombatRating(d.modules).ToString("0"), (a,b) => ArmamentDesignRules.CombatRating(a.modules).CompareTo(ArmamentDesignRules.CombatRating(b.modules)), 82f)
            });
            designList.SetDetailFormatter(d => $"{d.hull?.hullName} / スロット {ArmamentDesignRules.ModuleCount(d.modules)}/{d.hull?.slots ?? 0} / {(d.active ? "現役" : "保管")}");
            designList.SelectionChanged += SelectDesign;
            designList.Confirmed += d => { if (state != null && ShipDesignRules.SetActive(state, d)) { messageLabel.text = $"{d.designName}を現役設計にしました"; RefreshList(); RefreshPerformance(); } };
            designList.Cancelled += () => { selected = null; StartDraft((ShipClass)classField.index, hullField.index); };
            columns.Add(designList);

            var editor = new ScrollView(ScrollViewMode.Vertical); editor.style.flexGrow = 1f; editor.style.marginLeft = 18f; columns.Add(editor);
            classField = new DropdownField("艦種", new List<string>(Enum.GetNames(typeof(ShipClass))), 0); classField.RegisterValueChangedCallback(_ => StartDraft((ShipClass)classField.index, 0)); editor.Add(classField);
            hullField = new DropdownField("艦体", new List<string>(), 0); hullField.RegisterValueChangedCallback(_ => StartDraft((ShipClass)classField.index, hullField.index)); editor.Add(hullField);
            nameField = new TextField("設計名"); nameField.value = "新型艦設計"; editor.Add(nameField);
            capacityLabel = new Label(); capacityLabel.AddToClassList("hint"); editor.Add(capacityLabel);
            var slotTitle = new Label("技術スロット"); slotTitle.style.unityFontStyleAndWeight = FontStyle.Bold; slotTitle.style.fontSize = 18f; editor.Add(slotTitle);
            slots = new VisualElement(); editor.Add(slots);
            performanceLabel = new Label(); performanceLabel.style.whiteSpace = WhiteSpace.Normal; performanceLabel.style.fontSize = 18f; performanceLabel.style.marginTop = 12f; editor.Add(performanceLabel);

            var actions = new VisualElement(); actions.style.flexDirection = FlexDirection.Row; actions.style.marginTop = 10f; editor.Add(actions);
            registerButton = ActionButton("新規登録", RegisterDraft, actions);
            renameButton = ActionButton("選択設計を改名", RenameSelected, actions);
            activateButton = ActionButton("選択設計を採用", ActivateSelected, actions);
            messageLabel = new Label(); messageLabel.AddToClassList("hint"); messageLabel.style.marginTop = 8f; editor.Add(messageLabel);
            var close = new Button(Hide) { text = "閉じる (Esc)" }; close.AddToClassList("footer-btn"); panel.Add(close);
            root.style.display = DisplayStyle.None;
            PopulateHulls(ShipClass.戦艦, 0);
        }

        private static Button ActionButton(string text, Action action, VisualElement parent)
        {
            var button = new Button(action) { text = text }; button.AddToClassList("btn"); button.style.flexGrow = 1f; parent.Add(button); return button;
        }

        private void PopulateHulls(ShipClass shipClass, int index)
        {
            HullSpec[] hulls = ShipDesignCatalog.HullsFor(shipClass);
            var names = new List<string>(); for (int i = 0; i < hulls.Length; i++) names.Add(hulls[i].hullName);
            hullField.choices = names; hullField.index = Mathf.Clamp(index, 0, names.Count - 1);
            draftHull = ShipDesignCatalog.Clone(hulls[hullField.index]);
        }

        private void StartDraft(ShipClass shipClass, int hullIndex)
        {
            selected = null;
            PopulateHulls(shipClass, hullIndex);
            draftModules = new ShipModule[draftHull.slots];
            nameField.value = $"新型{shipClass}設計";
            messageLabel.text = "";
            RebuildSlots(); RefreshPerformance(); RefreshButtons();
        }

        private void SelectDesign(ShipDesign design)
        {
            if (design == null) return;
            selected = design;
            classField.SetValueWithoutNotify(design.shipClass.ToString());
            classField.index = (int)design.shipClass;
            HullSpec[] hulls = ShipDesignCatalog.HullsFor(design.shipClass);
            var names = new List<string>(); for (int i = 0; i < hulls.Length; i++) names.Add(hulls[i].hullName);
            hullField.choices = names; hullField.SetValueWithoutNotify(design.hull?.hullName ?? "登録艦体");
            draftHull = ShipDesignCatalog.Clone(design.hull);
            int length = Mathf.Max(draftHull?.slots ?? 0, design.modules?.Length ?? 0);
            draftModules = new ShipModule[length];
            for (int i = 0; i < length; i++) if (design.modules != null && i < design.modules.Length) draftModules[i] = ShipDesignCatalog.Clone(design.modules[i]);
            nameField.value = design.designName;
            messageLabel.text = design.active ? "現役設計を表示中" : "登録済み設計を表示中";
            RebuildSlots(); RefreshPerformance(); RefreshButtons();
        }

        private void RebuildSlots()
        {
            slots.Clear();
            ShipModule[] catalog = ShipDesignCatalog.Modules();
            var choices = new List<string> { "（空き）" }; for (int i = 0; i < catalog.Length; i++) choices.Add(ShipDesignCatalog.ModuleLabel(catalog[i]));
            for (int slot = 0; slot < draftModules.Length; slot++)
            {
                int s = slot; int selectedIndex = 0;
                if (draftModules[s] != null) for (int i = 0; i < catalog.Length; i++) if (catalog[i].moduleName == draftModules[s].moduleName) { selectedIndex = i + 1; break; }
                var field = new DropdownField($"枠 {slot + 1}", choices, selectedIndex);
                field.RegisterValueChangedCallback(_ => { draftModules[s] = field.index <= 0 ? null : ShipDesignCatalog.Clone(catalog[field.index - 1]); RefreshPerformance(); });
                slots.Add(field);
            }
        }

        private void RefreshPerformance()
        {
            if (draftHull == null || performanceLabel == null) return;
            bool valid = ArmamentDesignRules.IsValidDesign(draftHull, draftModules);
            capacityLabel.text = $"搭載 {ArmamentDesignRules.TotalWeight(draftModules):0}/{draftHull.maxWeight:0}　電力 {ArmamentDesignRules.TotalPowerDraw(draftModules):0}/{ArmamentDesignRules.TotalPowerSupply(draftHull, draftModules):0}　費用 {ArmamentDesignRules.TotalCost(draftModules):0}";
            ShipDesign baselineDesign = state == null ? null : ShipDesignRules.ActiveFor(state, (ShipClass)classField.index);
            ShipDesignPerformance baseline = ShipDesignRules.PerformanceOf(baselineDesign);
            var draft = new ShipDesign { shipClass = (ShipClass)classField.index, hull = draftHull, modules = draftModules };
            ShipDesignPerformance perf = valid ? ShipDesignRules.PerformanceOf(draft) : ShipDesignPerformance.Baseline;
            performanceLabel.text = "性能（現役設計比）\n" + Metric("火力", perf.firepower, baseline.firepower) + "　" + Metric("耐久", perf.durability, baseline.durability) + "\n" + Metric("機動", perf.mobility, baseline.mobility) + "　" + Metric("支援", perf.support, baseline.support);
            performanceLabel.style.color = valid ? new Color(0.75f, 0.85f, 0.95f) : new Color(1f, 0.45f, 0.68f);
            string failure = ShipDesignRules.DesignFailureReason(draftHull, draftModules);
            messageLabel.text = failure ?? (selected == null ? "登録できます" : messageLabel.text);
            RefreshButtons();
        }

        private static string Metric(string name, float value, float baseline)
        {
            float delta = (value - baseline) * 100f;
            string color = delta > 0.01f ? "#63DFF2" : delta < -0.01f ? "#FF78B4" : "#D0D6E0";
            return $"<color={color}>{name} ×{value:0.00} ({delta:+0.0;-0.0;0.0}%)</color>";
        }

        private void RefreshButtons()
        {
            bool available = state != null;
            registerButton?.SetEnabled(available && draftHull != null && ArmamentDesignRules.IsValidDesign(draftHull, draftModules));
            renameButton?.SetEnabled(available && selected != null);
            activateButton?.SetEnabled(available && selected != null && !selected.active);
        }

        private void RegisterDraft()
        {
            if (ShipDesignRules.TryRegister(state, nameField.value, (ShipClass)classField.index, draftHull, draftModules, out ShipDesign registered, out string reason))
            { selected = registered; messageLabel.text = $"{registered.designName}を登録し、現役設計にしました"; RefreshList(); SelectDesign(registered); }
            else messageLabel.text = reason;
        }

        private void RenameSelected()
        {
            if (selected == null) return;
            if (ShipDesignRules.TryRename(state, selected.id, nameField.value, out string reason)) { messageLabel.text = "設計名を変更しました"; RefreshList(); }
            else messageLabel.text = reason;
        }

        private void ActivateSelected()
        {
            if (selected != null && ShipDesignRules.SetActive(state, selected)) { messageLabel.text = $"{selected.designName}を現役設計にしました"; RefreshList(); RefreshPerformance(); }
        }

        public void SetDraftModuleForTest(int slot, ShipModule module)
        {
            if (slot < 0 || slot >= draftModules.Length) return;
            draftModules[slot] = ShipDesignCatalog.Clone(module); RefreshPerformance();
        }

        public bool RegisterForTest(string name)
        {
            nameField.value = name; RegisterDraft();
            return selected != null && selected.designName == name;
        }

        public bool RenameForTest(string name)
        {
            if (selected == null) return false; nameField.value = name; RenameSelected(); return selected.designName == name;
        }

        private void RefreshList() { designList.SetItems(state?.designs ?? new List<ShipDesign>()); RefreshButtons(); }

        private void OnDestroy() { UIWindowStack.Unregister(windowToken); if (instance == this) instance = null; }
    }
}
