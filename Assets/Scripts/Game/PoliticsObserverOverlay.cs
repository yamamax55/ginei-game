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
    /// 政治オブザーバ（観測層・read-only・#159）。<b>Y キー</b>または上メニュー「政治」で開閉し、勢力ごとの
    /// 政体（<see cref="FactionState.governmentForm"/>＋<see cref="ElectoralSystemRules.ModeFor"/> の選び方）と、
    /// 民主政治なら<b>政党制</b>（各党の支持・与党／首班・有効政党数・成熟度・分極化・分断危機）と<b>二院の選挙日程</b>を
    /// ダンプする。数値は <see cref="PartySystemRules"/>/<see cref="PartyRules"/>/<see cref="ElectionScheduleRules"/> へ委譲＝
    /// `GalaxyView.RunPoliticsTick`（年次）が回す状態をそのまま映す。観測専用＝状態は変えない。Strategy/Battle へ自動生成。
    /// </summary>
    public class PoliticsObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1089;
        public float dimAlpha = 0.55f;
        public float panelWidth = 980f;
        public float panelMaxHeight = 900f;
        public Color panelColor = new Color(0.05f, 0.05f, 0.07f, 0.96f);
        public float bodyFontSize = 20f;
        public int barWidth = 14;

        private GameObject overlayRoot;
        private GameObject panel;
        private TextMeshProUGUI bodyLabel;
        private object escWindowToken;

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
            if (Object.FindAnyObjectByType<PoliticsObserverOverlay>() != null) return;
            new GameObject("PoliticsObserverOverlay").AddComponent<PoliticsObserverOverlay>();
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "政治");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.政治観測切替)) Toggle();
            if (panel != null && panel.activeSelf && bodyLabel != null)
                bodyLabel.text = BuildDump();
        }

        public void Toggle() => SetVisible(panel != null && !panel.activeSelf);
        public void SetVisible(bool visible) { if (panel != null) panel.SetActive(visible); }

        // ===== ダンプ本体 =====

        private string BuildDump()
        {
            var sb = new StringBuilder(2048);
            sb.Append("<b>政治オブザーバ</b>　政体・政党制・選挙日程・分極化　(Y で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            CampaignState c = StrategySession.Campaign;
            if (c == null || c.states == null || c.states.Count == 0)
            {
                sb.Append("\n<color=#ffcc66>戦役データ（StrategySession.Campaign）がまだありません。</color>\n");
                sb.Append("戦略マップ（GalaxyView）を起動すると各勢力の政体・政党がここに表示されます。");
                return sb.ToString();
            }

            for (int i = 0; i < c.states.Count; i++)
            {
                FactionState s = c.states[i];
                if (s != null) AppendFaction(sb, s);
            }

            sb.Append("\n<color=#6f8a9a>※ 成熟度が上がるほど二大政党へ収束（デュヴェルジェ）、だが二大政党で成熟するほど分極化＝分断危機。</color>");
            return sb.ToString();
        }

        private void AppendFaction(StringBuilder sb, FactionState s)
        {
            GovernmentForm form = s.governmentForm;
            LeaderSelectionMode mode = ElectoralSystemRules.ModeFor(form);

            sb.Append('\n').Append("<color=#e7e0b0>◤ ").Append(s.faction).Append("</color>　")
              .Append("<color=#9fb0c0>").Append(form).Append("（選び方:").Append(mode).Append("）</color>\n");

            if (!ElectoralSystemRules.IsElectoral(form))
            {
                string note = mode == LeaderSelectionMode.合議 ? "少数の合議で決まる（政党・選挙なし）"
                            : mode == LeaderSelectionMode.世襲 ? "世襲で継がれる（政党・選挙なし）"
                            : "指名/簒奪で替わる（政党・選挙なし）";
                sb.Append("  <color=#7a8694>").Append(note).Append("</color>\n");
                return;
            }

            PoliticsState pol = s.politics;
            if (pol == null || pol.parties == null || pol.parties.Count == 0)
            {
                sb.Append("  <color=#ffcc66>政党は年次で形成されます（未形成）。</color>\n");
                return;
            }

            float maturity = PartySystemRules.MaturityFrom(s);
            float enp = PartySystemRules.EffectiveNumberOfParties(pol.parties);
            float polar = PartySystemRules.Polarization(maturity, pol.parties);
            bool two = PartySystemRules.IsTwoPartySystem(pol.parties);
            bool crisis = PartySystemRules.IsDividedCrisis(maturity, pol.parties);
            Party ruling = PartyRules.RulingParty(pol.parties);

            AppendBar(sb, "  民主成熟度", maturity, "#7fd4ff");
            AppendBar(sb, "  分極化", polar, crisis ? "#ff8a8a" : "#ffd28a");
            sb.Append("  <color=#9fb0c0>有効政党数</color> ＝ <color=#a0e0a0>").Append(enp.ToString("0.0")).Append("</color>")
              .Append(two ? "　<color=#7fd4ff>二大政党制</color>" : "")
              .Append(crisis ? "　<color=#ff6b6b>⚠ 分断危機</color>" : "").Append('\n');
            sb.Append("  <color=#9fb0c0>与党</color> ＝ <color=#ffe08a>").Append(ruling != null ? ruling.partyName : "—").Append("</color>\n");

            // 政党一覧（支持率の高い順）
            var sorted = new List<Party>(pol.parties);
            sorted.Sort((a, b) => b.support.CompareTo(a.support));
            for (int i = 0; i < sorted.Count; i++)
            {
                Party p = sorted[i];
                if (p == null) continue;
                bool isRuling = ruling != null && p.id == ruling.id;
                string mark = isRuling ? "<color=#ffe08a>★</color>" : "・";
                sb.Append("    ").Append(mark).Append(' ').Append(p.partyName).Append("  ");
                AppendBar(sb, "", Mathf.Clamp01(p.support), isRuling ? "#ffe08a" : "#9fb0c0");
            }

            // 二院の選挙日程
            AppendChamber(sb, "  下院（解散あり）", pol.lowerHouse);
            AppendChamber(sb, "  上院（半数改選）", pol.upperHouse);
        }

        private void AppendChamber(StringBuilder sb, string label, ChamberSchedule sch)
        {
            if (sch == null) return;
            int interval = ElectionScheduleRules.ProfileFor(sch.chamber).ElectionIntervalYears;
            sb.Append(label).Append("　<color=#8aa0b0>次回選挙 ").Append(sch.nextElectionYear)
              .Append(" 年（間隔 ").Append(interval).Append("年）</color>\n");
        }

        /// <summary>「ラベル ████░░░░ 0.42」形式の1行（ラベル空可）。</summary>
        private void AppendBar(StringBuilder sb, string label, float v01, string colorHex)
        {
            v01 = Mathf.Clamp01(v01);
            int filled = Mathf.RoundToInt(v01 * barWidth);
            if (!string.IsNullOrEmpty(label)) sb.Append(label).Append("  ");
            sb.Append("<color=").Append(colorHex).Append('>');
            for (int i = 0; i < barWidth; i++) sb.Append(i < filled ? '█' : '░');
            sb.Append("</color> ").Append(v01.ToString("0.00")).Append('\n');
        }

        // ===== UI 構築（LawObserverOverlay と同型） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("PoliticsObserverCanvas");
            overlayRoot.transform.SetParent(transform);
            Canvas canvas = overlayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            CanvasScaler scaler = overlayRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            overlayRoot.AddComponent<GraphicRaycaster>();

            panel = new GameObject("ObserverPanel");
            panel.transform.SetParent(overlayRoot.transform, false);
            RectTransform panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.sizeDelta = Vector2.zero;
            panelRT.anchoredPosition = Vector2.zero;
            Image dimImage = panel.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, dimAlpha);
            WindowChrome.MakeNonModal(dimImage);

            BuildContentPanel(panel.transform);
        }

        private void BuildContentPanel(Transform parent)
        {
            GameObject frame = new GameObject("ObserverFrame");
            frame.transform.SetParent(parent, false);
            RectTransform frameRT = frame.AddComponent<RectTransform>();
            frameRT.anchorMin = new Vector2(0f, 0.5f);
            frameRT.anchorMax = new Vector2(0f, 0.5f);
            frameRT.pivot = new Vector2(0f, 0.5f);
            frameRT.anchoredPosition = new Vector2(24f, 0f);
            frameRT.sizeDelta = new Vector2(panelWidth, panelMaxHeight);

            Image frameImg = frame.AddComponent<Image>();
            frameImg.color = panelColor;

            VerticalLayoutGroup vlg = frame.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 12, 12);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            WindowChrome.AddTitleBarLayout(frameRT, "政治", () => SetVisible(false));
            BuildScrollBody(frame.transform);
        }

        private void BuildScrollBody(Transform parent)
        {
            GameObject scrollObj = new GameObject("ObserverScrollRect");
            scrollObj.transform.SetParent(parent, false);
            scrollObj.AddComponent<RectTransform>();
            LayoutElement scrollLE = scrollObj.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;

            ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            RectTransform viewportRT = viewport.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.sizeDelta = Vector2.zero;
            viewportRT.anchoredPosition = Vector2.zero;
            viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRT;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRT = content.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentVlg = content.AddComponent<VerticalLayoutGroup>();
            contentVlg.padding = new RectOffset(8, 8, 4, 4);
            contentVlg.childAlignment = TextAnchor.UpperLeft;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = true;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;

            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRT;

            GameObject bodyObj = new GameObject("Body");
            bodyObj.transform.SetParent(content.transform, false);
            bodyLabel = bodyObj.AddComponent<TextMeshProUGUI>();
            bodyLabel.text = "";
            bodyLabel.fontSize = bodyFontSize;
            bodyLabel.color = new Color(0.9f, 0.93f, 0.96f);
            bodyLabel.alignment = TextAlignmentOptions.TopLeft;
            bodyLabel.richText = true;
            bodyLabel.raycastTarget = false;
            ApplyJapaneseFont(bodyLabel);
        }

        private static void ApplyJapaneseFont(TextMeshProUGUI tmp)
        {
            TMP_FontAsset jaFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (jaFont != null) tmp.font = jaFont;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }
    }
}
