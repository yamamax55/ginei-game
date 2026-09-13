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
    /// 戦略画面の「軍団編成」メニュー（#E）。
    ///
    /// 戦略MAPから停泊中の艦隊表示を外した代わりに、<b>「軍団の枠の中に配下艦隊」という見え方をここへ移した</b>
    /// （分かりやすいと評価された構造を捨てない）。軍団ごとに見出しを立て、その下に配下艦隊を字下げして並べる。
    ///
    /// 出す情報：軍団名（と軍団の指揮を担う艦隊）／艦隊名・<b>艦隊指揮官</b>・<b>艦艇数</b>・状態・所在地。
    /// どの軍団にも属さない艦隊も「独立艦隊」として最後に並べる＝<b>自軍艦隊を一隊も漏らさない</b>。
    ///
    /// 操作：選んだ艦隊を軍団へ<b>配属</b>／軍団から<b>解除</b>／その軍団の<b>指揮を移す</b>。
    /// 可否と実際の書き換えは Core の <see cref="CorpsAssignmentRules"/>＝<b>所属台帳を新設しない</b>
    /// （所属は <see cref="StrategicFleet.corpsId"/> が唯一の出所で、戦略・戦術・セーブが同じものを見る）。
    /// 交戦中などで変更できないときは理由を出すだけで、戦闘や移動には触れない。
    ///
    /// 作法は <see cref="FleetOrderPanel"/> に合わせる：非モーダル窓・実ピクセル下限の文字/行高・
    /// タイトルバーは <see cref="WindowChrome"/>・Esc は <see cref="UIWindowStack"/> へ登録するだけ。
    /// </summary>
    public class CorpsOrganizationPanel : MonoBehaviour
    {
        [Header("外観")]
        [Tooltip("Canvas の描画順（艦隊メニュー1000と同格・観測オーバーレイ1090より後ろ）")]
        public int canvasSortingOrder = 1001;

        [Tooltip("パネルの幅（設計ピクセル）")]
        public float panelDesignWidth = 980f;

        [Tooltip("パネルの高さ（設計ピクセル・MAP の高さに収まるよう自動で切り詰める）")]
        public float panelDesignHeight = 760f;

        [Tooltip("パネル背景色")]
        public Color panelColor = new Color(0.05f, 0.07f, 0.11f, 0.97f);

        [Header("更新")]
        [Tooltip("一覧を作り直す間隔（実時間秒・ポーズ中も進む）")]
        public float refreshInterval = 0.5f;

        private const float BaseFont = 17f;
        private const float MinFontPx = 14f;
        private const float BaseRow = 34f;
        private const float MinRowPx = 30f;
        private const float MinPanelWidthPx = 620f;

        private static CorpsOrganizationPanel instance;

        private Canvas canvas;
        private RectTransform frameRT;
        private RectTransform listContent;
        private TMP_FontAsset jpFont;
        private TextMeshProUGUI headerLabel;
        private TextMeshProUGUI messageLabel;
        private object escWindowToken;

        private float refreshTimer;
        private string message = "";
        private int selectedFleetId = -1;     // 操作対象（行のクリックで選ぶ・IDで持つ）
        private int selectedCorpsId = -1;     // 配属先（軍団の見出しのクリックで選ぶ）

        private readonly List<GameObject> rows = new List<GameObject>();
        private readonly List<StrategicFleet> fleetBuffer = new List<StrategicFleet>();

        // ===== 自動生成（FleetOrderPanel と同型・手置き不要）=====

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
            if (FindAnyObjectByType<CorpsOrganizationPanel>() != null) return;
            new GameObject("CorpsOrganizationPanel").AddComponent<CorpsOrganizationPanel>();
        }

        // ===== static の窓口 =====

        public static bool IsOpen => instance != null && instance.canvas != null && instance.canvas.gameObject.activeSelf;

        public static void Show()
        {
            CorpsOrganizationPanel p = EnsureInstance();
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

        private static CorpsOrganizationPanel EnsureInstance()
        {
            if (instance != null) return instance;
            instance = FindAnyObjectByType<CorpsOrganizationPanel>();
            if (instance == null) instance = new GameObject("CorpsOrganizationPanel").AddComponent<CorpsOrganizationPanel>();
            return instance;
        }

        // ===== ライフサイクル =====

        private void Awake()
        {
            instance = this;
            BuildUI();
            if (canvas != null) canvas.gameObject.SetActive(false);
            escWindowToken = UIWindowStack.Register(
                () => canvas != null && canvas.gameObject.activeSelf, Close, canvasSortingOrder, "軍団編成");
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

        // ===== 盤面への問い合わせ =====

        private static GalaxyView View => GalaxyView.Active;

        private static Faction PlayerFaction
            => GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;

        /// <summary>自軍の戦略艦隊（艦隊ID順・決定論）。</summary>
        private List<StrategicFleet> PlayerFleets()
        {
            fleetBuffer.Clear();
            GalaxyView gv = View;
            StrategicFleetRegistry reg = gv != null ? gv.Registry : null;
            if (reg == null || reg.fleets == null) return fleetBuffer;

            Faction player = PlayerFaction;
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || f.faction != player) continue;
                fleetBuffer.Add(f);
            }
            fleetBuffer.Sort((a, b) => a.id.CompareTo(b.id));
            return fleetBuffer;
        }

        private StrategicFleet SelectedFleet()
        {
            if (selectedFleetId < 0) return null;
            List<StrategicFleet> all = PlayerFleets();
            for (int i = 0; i < all.Count; i++) if (all[i].id == selectedFleetId) return all[i];
            return null;
        }

        // ===== 一覧 =====

        private void Rebuild()
        {
            ClearRows();

            float font = FontSize();
            float rowH = RowHeight();

            GalaxyView gv = View;
            List<StrategicFleet> all = PlayerFleets();
            if (gv == null || all.Count == 0)
            {
                MakeNotice("自軍の艦隊がありません。", font, rowH);
                UpdateHeader(0, 0);
                return;
            }

            Faction player = PlayerFaction;
            List<CorpsAssignmentRules.CorpsInfo> corps = CorpsAssignmentRules.CorpsOf(all, player);

            int listed = 0;
            for (int i = 0; i < corps.Count; i++)
            {
                CorpsAssignmentRules.CorpsInfo c = corps[i];
                MakeCorpsHeader(c, font, rowH);
                List<StrategicFleet> members = CorpsAssignmentRules.FleetsIn(all, player, c.corpsId);
                for (int k = 0; k < members.Count; k++) { MakeFleetRow(members[k], gv, font, rowH, true); listed++; }
            }

            List<StrategicFleet> free = CorpsAssignmentRules.Unassigned(all, player);
            if (free.Count > 0)
            {
                MakeGroupHeader("独立艦隊（どの軍団にも属していない）", new Color(0.72f, 0.78f, 0.88f), font, rowH, -1);
                for (int k = 0; k < free.Count; k++) { MakeFleetRow(free[k], gv, font, rowH, false); listed++; }
            }

            UpdateHeader(corps.Count, listed);
            if (messageLabel != null) messageLabel.text = message;
        }

        private void UpdateHeader(int corpsCount, int fleetCount)
        {
            if (headerLabel == null) return;
            StrategicFleet sel = SelectedFleet();
            var sb = new StringBuilder(160);
            sb.Append("軍団 ").Append(corpsCount).Append(" ／ 自軍艦隊 ").Append(fleetCount).Append(" 隊");
            if (sel != null)
            {
                sb.Append("　選択：").Append(FleetCommandLabelRules.FleetTitle(sel));
                sb.Append("（").Append(FleetCommandLabelRules.CorpsLabel(sel)).Append("）");
            }
            else sb.Append("　艦隊の行をクリックすると編成を変えられます");
            if (selectedCorpsId >= 0) sb.Append("　配属先：").Append(CorpsNameOf(selectedCorpsId));
            headerLabel.text = sb.ToString();
        }

        private string CorpsNameOf(int corpsId)
        {
            List<CorpsAssignmentRules.CorpsInfo> corps = CorpsAssignmentRules.CorpsOf(PlayerFleets(), PlayerFaction);
            for (int i = 0; i < corps.Count; i++)
                if (corps[i].corpsId == corpsId)
                    return string.IsNullOrEmpty(corps[i].corpsName) ? ("軍団#" + corpsId) : corps[i].corpsName;
            return "軍団#" + corpsId;
        }

        /// <summary>軍団の見出し（クリックで配属先に選ぶ）。枠の代わりに色帯と字下げで「軍団の中」を表す。</summary>
        private void MakeCorpsHeader(in CorpsAssignmentRules.CorpsInfo c, float font, float rowH)
        {
            string flag = c.flagshipFleetId >= 0
                ? $"　{FleetCommandLabelRules.CorpsFlagship}：第{c.flagshipFleetId}艦隊"
                : $"　{FleetCommandLabelRules.CorpsFlagship}：未設定";
            string name = string.IsNullOrEmpty(c.corpsName) ? ("軍団#" + c.corpsId) : c.corpsName;
            MakeGroupHeader($"{name}（{c.fleetCount}隊）{flag}", new Color(0.85f, 0.82f, 1f), font, rowH, c.corpsId);
        }

        private void MakeGroupHeader(string text, Color color, float font, float rowH, int corpsId)
        {
            GameObject go = new GameObject("Group", typeof(RectTransform));
            go.transform.SetParent(listContent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = rowH; le.preferredHeight = rowH;

            Image img = go.AddComponent<Image>();
            bool chosen = corpsId >= 0 && corpsId == selectedCorpsId;
            img.color = chosen ? new Color(0.24f, 0.22f, 0.40f, 1f) : new Color(0.14f, 0.13f, 0.22f, 1f);

            if (corpsId >= 0)
            {
                Button btn = go.AddComponent<Button>();
                btn.transition = UnityEngine.UI.Selectable.Transition.None;
                btn.targetGraphic = img;
                int captured = corpsId;
                btn.onClick.AddListener(() =>
                {
                    selectedCorpsId = selectedCorpsId == captured ? -1 : captured;
                    Rebuild();
                });
            }

            TextMeshProUGUI t = MakeCell((RectTransform)go.transform, text, font, color, 0f, 1f, 10f);
            t.fontStyle = FontStyles.Bold;
            rows.Add(go);
        }

        /// <summary>配下艦隊の1行（字下げして「軍団の中」を示す）。</summary>
        private void MakeFleetRow(StrategicFleet f, GalaxyView gv, float font, float rowH, bool inCorps)
        {
            GameObject go = new GameObject("Fleet_" + f.id, typeof(RectTransform));
            go.transform.SetParent(listContent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = rowH; le.preferredHeight = rowH;

            Image img = go.AddComponent<Image>();
            bool sel = f.id == selectedFleetId;
            img.color = sel ? new Color(0.20f, 0.32f, 0.46f, 1f) : new Color(0.11f, 0.15f, 0.22f, 1f);

            Button btn = go.AddComponent<Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            btn.targetGraphic = img;
            int captured = f.id;
            btn.onClick.AddListener(() =>
            {
                selectedFleetId = selectedFleetId == captured ? -1 : captured;
                message = "";
                Rebuild();
            });

            RectTransform rt = (RectTransform)go.transform;
            float indent = inCorps ? 0.04f : 0.02f;   // 字下げ＝軍団の中にいることを示す

            string title = FleetCommandLabelRules.FleetTitle(f);
            if (f.isCorpsFlagship) title += "・" + FleetCommandLabelRules.CorpsFlagship;

            MakeCell(rt, title, font, sel ? new Color(1f, 0.92f, 0.62f) : new Color(0.92f, 0.95f, 1f),
                     indent, indent + 0.20f, 8f);
            // 指揮官＝実在の人物の階級つき氏名。艦隊司令の任命条件（中将）に足りない場合は
            // 「（要 中将）」を添えて色を変える＝人手不足で若手が預かっている事実を隠さない。
            string who = gv != null ? gv.FleetCommanderLabel(f) : FleetCommandLabelRules.Unassigned;
            string gate = gv != null ? gv.FleetCommandGateNote(f) : "";
            Color whoColor = who == FleetCommandLabelRules.Unassigned ? new Color(0.62f, 0.66f, 0.74f)
                           : string.IsNullOrEmpty(gate) ? new Color(1f, 0.90f, 0.78f)
                                                        : new Color(1f, 0.72f, 0.55f);
            MakeCell(rt, who + gate, font, whoColor, indent + 0.20f, indent + 0.42f, 4f);
            MakeCell(rt, FleetShipCountRules.Label(f.Ships), font, new Color(0.86f, 0.93f, 1f),
                     indent + 0.42f, indent + 0.58f, 4f);
            MakeCell(rt, FleetOrderRules.StateLabel(f), font, new Color(0.72f, 0.88f, 0.78f),
                     indent + 0.58f, indent + 0.70f, 4f);
            MakeCell(rt, LocationText(f, gv), font, new Color(0.80f, 0.86f, 0.95f),
                     indent + 0.70f, 1f, 4f);

            rows.Add(go);
        }

        /// <summary>所在地（停泊中は星系名・航行中は「出発地 → 行先」）。</summary>
        private static string LocationText(StrategicFleet f, GalaxyView gv)
        {
            if (gv == null) return "";
            if (!FleetOrderRules.TryDescribeRoute(f, out int fromId, out int hopToId, out int finalId)) return "";
            if (fromId == hopToId && hopToId == finalId) return gv.SystemNameOf(fromId);
            string text = gv.SystemNameOf(fromId) + " → " + gv.SystemNameOf(hopToId);
            if (FleetOrderRules.IsMultiHop(f) && finalId != hopToId) text += "（最終 " + gv.SystemNameOf(finalId) + "）";
            return text;
        }

        // ===== 操作 =====

        private void DoAssign()
        {
            StrategicFleet f = SelectedFleet();
            if (f == null) { message = "艦隊を選んでください。"; Rebuild(); return; }
            if (selectedCorpsId < 0) { message = "配属先の軍団（見出し）を選んでください。"; Rebuild(); return; }

            List<CorpsAssignmentRules.CorpsInfo> corps = CorpsAssignmentRules.CorpsOf(PlayerFleets(), PlayerFaction);
            CorpsAssignmentRules.CorpsInfo target = default;
            for (int i = 0; i < corps.Count; i++) if (corps[i].corpsId == selectedCorpsId) target = corps[i];

            if (CorpsAssignmentRules.Assign(f, PlayerFaction, target, out CorpsAssignmentRejection r))
                message = $"{FleetCommandLabelRules.FleetTitle(f)} を {CorpsNameOf(selectedCorpsId)} へ配属しました。";
            else
                message = $"配属できません：{CorpsAssignmentRules.RejectionText(r)}";
            Rebuild();
        }

        private void DoUnassign()
        {
            StrategicFleet f = SelectedFleet();
            if (f == null) { message = "艦隊を選んでください。"; Rebuild(); return; }

            if (CorpsAssignmentRules.Unassign(f, PlayerFaction, out CorpsAssignmentRejection r))
                message = $"{FleetCommandLabelRules.FleetTitle(f)} を軍団から外しました（独立艦隊）。";
            else
                message = $"解除できません：{CorpsAssignmentRules.RejectionText(r)}";
            Rebuild();
        }

        private void DoSetFlagship()
        {
            StrategicFleet f = SelectedFleet();
            if (f == null) { message = "艦隊を選んでください。"; Rebuild(); return; }

            if (CorpsAssignmentRules.SetCorpsFlagship(PlayerFleets(), f, PlayerFaction, out CorpsAssignmentRejection r))
                message = $"{FleetCommandLabelRules.FleetTitle(f)} が {FleetCommandLabelRules.CorpsLabel(f)} の指揮を執ります。";
            else
                message = $"指揮を移せません：{CorpsAssignmentRules.RejectionText(r)}";
            Rebuild();
        }

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
            float height = Mathf.Clamp(panelDesignHeight, RowHeight() * 8f,
                                       Mathf.Max(RowHeight() * 8f, mapHeightDesign - 24f));

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

            GameObject canvasObj = new GameObject("CorpsOrganizationCanvas");
            canvasObj.transform.SetParent(transform, false);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(StrategyScreenLayoutRules.ReferenceWidth,
                                                     StrategyScreenLayoutRules.ReferenceHeight);
            scaler.matchWidthOrHeight = 0f;
            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject frame = new GameObject("Frame", typeof(RectTransform));
            frame.transform.SetParent(canvasObj.transform, false);
            frameRT = frame.GetComponent<RectTransform>();
            frameRT.anchorMin = frameRT.anchorMax = Vector2.zero;
            frameRT.pivot = new Vector2(0f, 1f);
            Image frameImg = frame.AddComponent<Image>();
            frameImg.color = panelColor;
            Outline outline = frame.AddComponent<Outline>();
            outline.effectColor = new Color(0.72f, 0.66f, 1f, 0.7f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            VerticalLayoutGroup vlg = frame.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 8, 10);
            vlg.spacing = 5f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            WindowChrome.AddTitleBarLayout(frameRT, "軍団編成", Close);

            float font = FontSize();
            float rowH = RowHeight();

            headerLabel = MakeSectionLabel(frameRT, "", font, new Color(1f, 0.88f, 0.55f), rowH);
            MakeSectionLabel(frameRT,
                "軍団の見出し＝配属先／その下の字下げ＝配下艦隊（艦隊名・指揮官・艦艇数・状態・所在地）",
                font, new Color(0.72f, 0.82f, 0.92f), rowH * 0.9f);

            listContent = MakeScrollArea(frameRT, "CorpsList", 1f, rowH * 6f);

            BuildActionRow(frameRT, font, rowH);

            messageLabel = MakeSectionLabel(frameRT, "", font, new Color(0.86f, 0.92f, 1f), rowH * 1.5f);
            messageLabel.textWrappingMode = TextWrappingModes.Normal;
            messageLabel.overflowMode = TextOverflowModes.Truncate;

            Layout();
        }

        private void BuildActionRow(RectTransform parent, float font, float rowH)
        {
            GameObject row = new GameObject("Actions", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            LayoutElement le = row.AddComponent<LayoutElement>();
            le.minHeight = rowH * 1.1f; le.preferredHeight = rowH * 1.1f; le.flexibleHeight = 0f;
            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

            MakeButton(row.transform, "選んだ軍団へ配属", font, DoAssign);
            MakeButton(row.transform, "軍団から外す", font, DoUnassign);
            MakeButton(row.transform, "この艦隊に指揮を移す", font, DoSetFlagship);
        }

        private RectTransform MakeScrollArea(RectTransform parent, string name, float flexibleHeight, float minHeight)
        {
            GameObject scrollGo = new GameObject(name, typeof(RectTransform));
            scrollGo.transform.SetParent(parent, false);
            LayoutElement le = scrollGo.AddComponent<LayoutElement>();
            le.flexibleHeight = flexibleHeight;
            le.minHeight = minHeight;

            Image frameBg = scrollGo.AddComponent<Image>();
            frameBg.color = new Color(0.03f, 0.04f, 0.06f, 0.9f);

            ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
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
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = vpRT;
            scroll.content = cRT;
            UiScrollbars.Attach(scroll);   // #H スクロールできることを画面で示す（見えて掴めるバー）
            return cRT;
        }

        private void MakeButton(Transform parent, string caption, float font, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject("Button_" + caption, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.16f, 0.20f, 0.30f, 1f);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            TextMeshProUGUI t = MakeText((RectTransform)go.transform, caption, font, new Color(0.94f, 0.96f, 1f));
            t.alignment = TextAlignmentOptions.Center;
            RectTransform rt = t.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(4f, 0f); rt.offsetMax = new Vector2(-4f, 0f);
        }

        private TextMeshProUGUI MakeSectionLabel(RectTransform parent, string text, float font, Color color, float height)
        {
            GameObject go = new GameObject("Section", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = height; le.preferredHeight = height; le.flexibleHeight = 0f;
            TextMeshProUGUI t = MakeText((RectTransform)go.transform, text, font, color);
            RectTransform rt = t.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(4f, 0f); rt.offsetMax = new Vector2(-4f, 0f);
            return t;
        }

        private void MakeNotice(string text, float font, float rowH)
        {
            GameObject go = new GameObject("Notice", typeof(RectTransform));
            go.transform.SetParent(listContent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = rowH; le.preferredHeight = rowH;
            MakeCell((RectTransform)go.transform, text, font, new Color(0.78f, 0.84f, 0.94f), 0f, 1f, 10f);
            rows.Add(go);
        }

        private TextMeshProUGUI MakeCell(RectTransform parent, string text, float font, Color color,
                                         float x0, float x1, float leftMargin)
        {
            TextMeshProUGUI t = MakeText(parent, text, font, color);
            t.alignment = TextAlignmentOptions.Left;
            t.overflowMode = TextOverflowModes.Ellipsis;
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

        private void ClearRows()
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
