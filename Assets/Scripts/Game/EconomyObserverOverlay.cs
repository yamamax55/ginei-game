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
    /// 経済オブザーバ（観測層・read-only）。<b>E キー</b>で開閉し、勢力ごとの**配線済みの経済状態**
    /// （<see cref="FactionState.treasury"/> 国庫／<see cref="FactionState.taxRate"/> 税率）と、そこから導かれる
    /// 課税ベース（<see cref="CampaignRules.EconomyBase"/>）・税収（<see cref="FiscalRules.TaxRevenue"/>）・
    /// 高税の不満（<see cref="FiscalRules.TaxBurdenPenalty"/>）・版図一体化度（<see cref="LogisticsRules.CohesionFactor"/>）を
    /// 毎フレームライブダンプする。`GalaxyView` の `CampaignRules.TickEconomyDay`（日次）が回している分＝盤面で実際に動く経済だけを映す。
    /// 操作はさせない＝**観測専用＝状態は変えない**（税率の調整は Galaxy!のキー[/]）。
    /// `CampaignObserverOverlay`（G・国家状態）の経済版。`HelpOverlay`/`TimeDisplay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class EconomyObserverOverlay : MonoBehaviour
    {
        // ===== 調整可能なパラメーター =====

        [Header("外観")]
        [Tooltip("オーバーレイ Canvas の描画順（他UIより手前）")]
        public int canvasSortingOrder = 1094;

        [Tooltip("背景ディマーの不透明度（0〜1）")]
        public float dimAlpha = 0.55f;

        [Tooltip("パネルの幅（ピクセル）")]
        public float panelWidth = 980f;

        [Tooltip("パネルの最大高さ（ピクセル）")]
        public float panelMaxHeight = 900f;

        [Tooltip("パネル背景色")]
        public Color panelColor = new Color(0.05f, 0.05f, 0.04f, 0.96f);

        [Tooltip("本文のフォントサイズ")]
        public float bodyFontSize = 20f;

        [Tooltip("バー（0..1可視化）の桁数")]
        public int barWidth = 14;

        // ===== 内部状態 =====

        private GameObject overlayRoot;
        private GameObject panel;
        private TextMeshProUGUI bodyLabel;

        // ===== 自動生成エントリーポイント（HelpOverlay と同型） =====

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; // 二重購読防止
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        /// <summary>戦略/会戦シーンにオブザーバが無ければ生成する（重複生成ガード）。</summary>
        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Strategy" && scene.name != "Battle") return;
            if (Object.FindAnyObjectByType<EconomyObserverOverlay>() != null) return;
            new GameObject("EconomyObserverOverlay").AddComponent<EconomyObserverOverlay>();
        }

        // ===== Unity ライフサイクル =====

        private void Awake()
        {
            BuildUI();
            SetVisible(false); // 初期は非表示
        }

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.経済観測切替))
                Toggle();

            if (panel != null && panel.activeSelf && bodyLabel != null)
                bodyLabel.text = BuildDump();
        }

        // ===== 公開API =====

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

            sb.Append("<b>経済オブザーバ</b>　国庫・税率・課税ベース・税収・版図一体化　(E で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            if (c == null)
            {
                sb.Append("\n<color=#ffcc66>戦役データ（StrategySession.Campaign）がありません。</color>\n");
                sb.Append("戦略マップ（GalaxyView）を一度起動すると CampaignState が生成され、\n");
                sb.Append("ここに各勢力の経済（税収→国庫・高税→民心の綱引き）がライブ表示されます。");
                return sb.ToString();
            }

            int count = c.states != null ? c.states.Count : 0;
            if (count == 0)
            {
                sb.Append("\n<color=#ffcc66>勢力の国家状態がまだありません（states: 0）。</color>");
                return sb.ToString();
            }

            for (int i = 0; i < c.states.Count; i++)
            {
                FactionState s = c.states[i];
                if (s == null) continue;
                AppendFaction(sb, c, s);
            }

            sb.Append("\n<color=#6f8a9a>※ 課税ベース＝人口 × 経済係数 × 安定度。高税ほど税収↑だが民心(希望)↓の綱引き。</color>");
            return sb.ToString();
        }

        private void AppendFaction(StringBuilder sb, CampaignState c, FactionState s)
        {
            float taxRate    = Mathf.Clamp01(s.taxRate);
            float burden     = FiscalRules.TaxBurdenPenalty(taxRate);
            float economyBase = CampaignRules.EconomyBase(s);
            float revenue    = FiscalRules.TaxRevenue(economyBase, taxRate); // /game秒
            float cohesion   = (c.map != null) ? LogisticsRules.CohesionFactor(c.map, s.faction) : 1f;
            float hope       = s.community != null ? s.community.hope : 0f;
            float stability  = FactionStateRules.Stability(s);
            float pop        = s.polity != null ? s.polity.population : 0f;

            sb.Append('\n').Append("<color=#e7e0b0>◤ ").Append(s.faction).Append("</color>\n");

            // 国庫・フロー（生数値）
            sb.Append("  <color=#9fb0c0>国庫 treasury</color> ＝ <color=#ffe08a>")
              .Append(s.treasury.ToString("#,0")).Append("</color>");
            sb.Append("　<color=#9fb0c0>税収/秒</color> ＝ <color=#a0e0a0>")
              .Append(revenue.ToString("#,0.##")).Append("</color>\n");

            sb.Append("  <color=#9fb0c0>課税ベース</color> ＝ <color=#ffd28a>")
              .Append(economyBase.ToString("#,0.##")).Append("</color>")
              .Append("　<color=#6f8a9a>(人口 ").Append(pop.ToString("#,0"))
              .Append(" × 係数 ").Append(CampaignRules.EconomyPerCapita.ToString("0.######"))
              .Append(" × 安定度 ").Append(stability.ToString("0.00")).Append(")</color>\n");

            // レバーと帰結（0..1 バー）
            AppendBar(sb, "  税率", taxRate, "#ffd28a");
            AppendBar(sb, "  高税の不満", burden, "#ff9a8a");
            AppendBar(sb, "  版図一体化度", cohesion, "#7fd4ff");
            AppendBar(sb, "  民心(希望)", hope, "#a0e0a0");
            sb.Append("    <color=#6f8a9a>↑ 民心は高税の不満で蝕まれる（税収↔支持のトレードオフ）</color>\n");
        }

        /// <summary>「ラベル ████░░░░ 0.42」形式の1行を追加する（0..1可視化）。</summary>
        private void AppendBar(StringBuilder sb, string label, float v01, string colorHex)
        {
            v01 = Mathf.Clamp01(v01);
            int filled = Mathf.RoundToInt(v01 * barWidth);
            sb.Append(label).Append("  <color=").Append(colorHex).Append('>');
            for (int i = 0; i < barWidth; i++) sb.Append(i < filled ? '█' : '░');
            sb.Append("</color> ").Append(v01.ToString("0.00")).Append('\n');
        }

        // ===== UI 構築（CampaignObserverOverlay と同型・単一スクロールラベル版） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("EconomyObserverCanvas");
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
            WindowChrome.MakeNonModal(dimImage); // ウィンドウ化＝非モーダル（盤面を塞がない）

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

            WindowChrome.AddTitleBarLayout(frameRT, "経済", () => SetVisible(false));
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
