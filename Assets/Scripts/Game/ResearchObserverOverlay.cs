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
    /// 研究ツリーオブザーバ（観測層・read-only・#123-127／#1065）。<b>Alt+R</b>で開閉し、勢力ごとに
    /// <b>離散技術ツリー</b>（<see cref="TechCatalog"/>＝軍事/生産/情報/社会の依存グラフ）を分野ごとに描く：
    /// ✓ 習得済み／▶ 研究中（進捗バー）／○ 解禁可能（前提充足の最前線）／🔒 未解禁（不足前提つき）。
    /// 集約スカラの技術水準（<see cref="ResearchState.techLevel"/>＝建艦/戦力の質）も併記。進行は年次に
    /// <see cref="GalaxyView.RunTechTreeTickFor"/> が回す（研究予算×技能の研究力で前提を積み上げ解禁）。
    /// 観測専用ゆえ既存フィールドのみ読む＝<b>状態は変えない</b>。他オブザーバと同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class ResearchObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1106;
        public float dimAlpha = 0.55f;
        public float panelWidth = 1040f;
        public float panelMaxHeight = 900f;
        public Color panelColor = new Color(0.05f, 0.05f, 0.04f, 0.96f);
        public float bodyFontSize = 20f;

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
            if (Object.FindAnyObjectByType<ResearchObserverOverlay>() != null) return;
            new GameObject("ResearchObserverOverlay").AddComponent<ResearchObserverOverlay>();
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "研究");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.研究観測切替))
                Toggle();

            if (panel != null && panel.activeSelf && bodyLabel != null)
                bodyLabel.text = BuildDump();
        }

        public void Toggle() => SetVisible(panel != null && !panel.activeSelf);

        public void SetVisible(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        // ===== ダンプ本体 =====

        private string BuildDump()
        {
            var sb = new StringBuilder(4096);
            CampaignState c = StrategySession.Campaign;
            GalaxyView gv = GalaxyView.Active;

            sb.Append("<b>研究ツリーオブザーバ</b>　技術の依存ツリー（軍事/生産/情報/社会）　(Alt+R で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");
            sb.Append("<color=#7f8a96>✓習得済み　▶研究中　○解禁可能　×未解禁</color>　全 ")
              .Append(TechCatalog.Count).Append(" 技術\n");

            if (c == null || c.states == null || c.states.Count == 0)
            {
                sb.Append("\n<color=#ffcc66>戦役データ（StrategySession.Campaign）がありません。</color>\n");
                sb.Append("戦略マップ（GalaxyView）を起動すると、各勢力の研究ツリーがライブ表示されます。");
                return sb.ToString();
            }
            if (gv == null)
            {
                sb.Append("\n<color=#ffcc66>研究状態は戦略マップ（GalaxyView）でのみ表示されます。</color>");
                return sb.ToString();
            }

            for (int i = 0; i < c.states.Count; i++)
            {
                FactionState s = c.states[i];
                if (s == null) continue;
                AppendFaction(sb, gv, s.faction);
            }

            sb.Append("\n<color=#6f8a9a>※ 研究予算×平均労働技能の研究力で前提を積み上げ次の技術が解禁される（教育→技能→技術）。\n");
            sb.Append("　 政体の得意分野を優先（専制=軍事/民主=社会/商業=生産/技術志向=情報）。建艦/戦力の質に効くスカラ技術水準は別軸。</color>");
            return sb.ToString();
        }

        private void AppendFaction(StringBuilder sb, GalaxyView gv, Faction fac)
        {
            FactionTechProgress p = gv.GetTechProgress(fac);
            ResearchState rs = gv.GetResearch(fac);
            int researched = p != null ? p.ResearchedCount : 0;

            sb.Append('\n').Append("<color=#e7e0b0>◤ ").Append(fac).Append("</color>")
              .Append("　<color=#9aa7b2>習得 </color>").Append(researched).Append('/').Append(TechCatalog.Count);
            if (rs != null)
                sb.Append("　<color=#9aa7b2>技術水準 </color>").Append(rs.techLevel.ToString("0.0"));
            sb.Append('\n');

            if (p == null)
            {
                sb.Append("  <color=#9aa7b2>研究 未配線（戦略マップ稼働で進行）</color>\n");
                return;
            }

            IList<string> done = p.researchedTechs;
            foreach (ResearchField field in System.Enum.GetValues(typeof(ResearchField)))
            {
                List<TechNode> inField = TechCatalog.NodesInField(field);
                if (inField.Count == 0) continue;
                sb.Append("  <color=#bcd0e0>【").Append(field).Append("】</color> ");

                for (int i = 0; i < inField.Count; i++)
                {
                    TechNode n = inField[i];
                    if (i > 0) sb.Append("  ");
                    AppendTech(sb, n, p, done);
                }
                sb.Append('\n');
            }
        }

        private static void AppendTech(StringBuilder sb, TechNode n, FactionTechProgress p, IList<string> done)
        {
            string name = TechCatalog.DisplayName(n.techId);

            if (p.IsResearched(n.techId))
            {
                sb.Append("<color=#6fae6f>✓").Append(name).Append("</color>");
            }
            else if (n.techId == p.currentTechId)
            {
                float cost = Mathf.Max(0.0001f, n.researchCost);
                int pct = Mathf.RoundToInt(Mathf.Clamp01(p.currentProgress / cost) * 100f);
                sb.Append("<color=#f0d060>▶").Append(name).Append(" ").Append(pct).Append("%</color>");
            }
            else if (TechTreeRules.IsUnlockable(n, done))
            {
                sb.Append("<color=#cfe0f0>○").Append(name).Append("</color>");
            }
            else
            {
                int missing = TechTreeRules.PrerequisitesMissing(n, done);
                sb.Append("<color=#7f8a96>×").Append(name);
                if (missing > 0) sb.Append("(要").Append(missing).Append(')');
                sb.Append("</color>");
            }
        }

        // ===== UI 構築（GovernmentObserverOverlay と同型・単一スクロールラベル版） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("ResearchObserverCanvas");
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

            WindowChrome.AddTitleBarLayout(frameRT, "研究ツリー", () => SetVisible(false));
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
