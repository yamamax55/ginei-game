using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 造船オブザーバ（観測層・read-only）。<b>Alt+B キー</b>で開閉し、勢力ごとに造船所・建艦キュー・進捗を集約して
    /// 毎フレームライブダンプする（#884 造船供給）。各造船所（<see cref="Shipyard"/>）の稼働状況と先頭の建艦オーダー
    /// （<see cref="BuildOrder"/>＝艦種/役割・進捗・残）を映す。`GalaxyView` の日次Tickが造船所を進め、完成は勢力の艦艇プールへ就役する。
    /// 観測専用ゆえ既存フィールドのみ読む。操作はさせない＝**状態は変えない**。`HelpOverlay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class ShipyardObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1105;
        public float dimAlpha = 0.55f;
        public float panelWidth = 980f;
        public float panelMaxHeight = 900f;
        public Color panelColor = new Color(0.05f, 0.05f, 0.04f, 0.96f);
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
            if (Object.FindAnyObjectByType<ShipyardObserverOverlay>() != null) return;
            new GameObject("ShipyardObserverOverlay").AddComponent<ShipyardObserverOverlay>();
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "造船");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.造船観測切替))
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
            var sb = new StringBuilder(2048);
            CampaignState c = StrategySession.Campaign;
            GalaxyView gv = GalaxyView.Active;

            sb.Append("<b>造船オブザーバ</b>　造船所・建艦キュー・進捗　(Alt+B で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            if (gv == null)
            {
                sb.Append("\n<color=#ffcc66>造船所は戦略マップ（GalaxyView）でのみ観測できます（会戦中は非公開）。</color>");
                return sb.ToString();
            }

            IReadOnlyList<Shipyard> yards = gv.Shipyards;
            if (yards == null || yards.Count == 0)
            {
                sb.Append("\n<color=#9aa7b2>造船所なし</color>");
                return sb.ToString();
            }

            if (c == null || c.states == null || c.states.Count == 0)
            {
                sb.Append("\n<color=#ffcc66>戦役データ（StrategySession.Campaign）がありません。</color>");
                return sb.ToString();
            }

            for (int i = 0; i < c.states.Count; i++)
            {
                FactionState s = c.states[i];
                if (s == null) continue;
                AppendFaction(sb, yards, s.faction);
            }

            sb.Append("\n<color=#6f8a9a>※ 建艦は星系の安定度（生産力）と建艦予算に比例。完成は勢力の艦艇プール（艦艇オブザーバ B）へ就役。</color>");
            return sb.ToString();
        }

        private void AppendFaction(StringBuilder sb, IReadOnlyList<Shipyard> yards, Faction fac)
        {
            int count = 0, active = 0, totalQueued = 0;
            foreach (Shipyard yard in yards)
            {
                if (yard == null || yard.faction != fac) continue;
                count++;
                int qc = yard.queue != null ? yard.queue.Count : 0;
                if (qc > 0) active++;
                totalQueued += qc;
            }

            sb.Append('\n').Append("<color=#e7e0b0>◤ ").Append(fac).Append("</color>\n");
            sb.Append("  <color=#9fb0c0>造船所</color> ").Append(count)
              .Append("　稼働 ").Append(active)
              .Append("　キュー ").Append(totalQueued).Append('\n');

            // 軍産複合体（MCN-4 #1389・CAP-3 #204）：造船利権の政治圧力＝補助金（建艦加速）・調達腐敗・戦争バイアスの源。
            float micP = GalaxyView.Active != null ? GalaxyView.Active.GetMilitaryIndustrialPressure(fac) : 0f;
            if (micP > 0f)
            {
                bool complex = MilitaryIndustrialRules.IsComplex(micP);
                sb.Append("  <color=#9fb0c0>軍産複合体</color> 政治圧力 <color=").Append(complex ? "#ff7a6a" : (micP > 0.3f ? "#ffd28a" : "#a0e0a0")).Append('>')
                  .Append((micP * 100f).ToString("0")).Append("%</color>")
                  .Append("　補助金 +").Append((MilitaryIndustrialRules.ProductionSubsidy(micP) * 100f).ToString("0")).Append('%')
                  .Append("　調達腐敗 +").Append((MilitaryIndustrialRules.CorruptionGain(micP) * 100f).ToString("0")).Append('%')
                  .Append("　戦争バイアス ").Append((MilitaryIndustrialRules.WarBias(micP) * 100f).ToString("0")).Append('%');
                if (complex) sb.Append("　<color=#ff7a6a>⚠ 複合体成立</color>");
                sb.Append('\n');
            }

            if (count == 0)
            {
                sb.Append("  <color=#9aa7b2>（造船所なし）</color>\n");
                return;
            }

            int shown = 0;
            foreach (Shipyard yard in yards)
            {
                if (yard == null || yard.faction != fac) continue;
                if (yard.queue == null || yard.queue.Count == 0) continue;
                if (shown >= 8) break;
                shown++;

                BuildOrder o = yard.queue[0];
                sb.Append("  ").Append(o.shipClass).Append('/').Append(o.shipRole);
                AppendBar(sb, "", Mathf.Clamp01(o.cost > 0 ? o.progress / o.cost : 0f), o.IsComplete ? "#a0e0a0" : "#7fd4ff");
                sb.Append(" 残 ").Append(o.Remaining.ToString("0")).Append('\n');
            }
        }

        private void AppendBar(StringBuilder sb, string label, float v01, string colorHex)
        {
            v01 = Mathf.Clamp01(v01);
            int filled = Mathf.RoundToInt(v01 * barWidth);
            sb.Append(label).Append("  <color=").Append(colorHex).Append('>');
            for (int i = 0; i < barWidth; i++) sb.Append(i < filled ? '█' : '░');
            sb.Append("</color> ").Append(v01.ToString("0.00")).Append('\n');
        }

        // ===== UI 構築（EconomyObserverOverlay と同型・単一スクロールラベル版） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("ShipyardObserverCanvas");
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

            WindowChrome.AddTitleBarLayout(frameRT, "造船", () => SetVisible(false));
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
