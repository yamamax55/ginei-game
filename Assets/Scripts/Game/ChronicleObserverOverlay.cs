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
    /// イベントオブザーバ（観測層・read-only）。<b>Alt+D キー</b>で開閉し、内政イベントエンジン
    /// （<see cref="EventEngine"/>＝<see cref="GalaxyView.PolicyEngine"/>）の提示中/保留を映す。
    /// 開示エンジン（秘史/真相/予言/エンディング＝<c>DisclosureLedger</c>）は現状未配線のためその旨を表示する。
    /// 観測専用ゆえ既存フィールドのみ読む。操作はさせない＝**状態は変えない**。`HelpOverlay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class ChronicleObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1107;
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
            if (Object.FindAnyObjectByType<ChronicleObserverOverlay>() != null) return;
            new GameObject("ChronicleObserverOverlay").AddComponent<ChronicleObserverOverlay>();
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "イベント");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.イベント観測切替))
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
            var sb = new StringBuilder(1024);

            sb.Append("<b>イベントオブザーバ</b>　内政イベントの提示中/保留　(Alt+D で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            GalaxyView gv = GalaxyView.Active;
            if (gv == null)
            {
                sb.Append("\n<color=#ffcc66>内政イベントは戦略マップ（GalaxyView）でのみ観測できます（会戦のイベントは別エンジン）。</color>");
                return sb.ToString();
            }

            EventEngine eng = gv.PolicyEngine;
            if (eng == null)
            {
                sb.Append("\n<color=#9aa7b2>イベントエンジン未初期化。</color>");
                return sb.ToString();
            }

            sb.Append("\n<color=#e7e0b0>◤ 内政イベントエンジン</color>\n");
            sb.Append("  <color=#9fb0c0>提示中</color> ＝ ").Append(eng.Current != null ? eng.Current.title : "（なし）").Append('\n');
            sb.Append("  <color=#9fb0c0>保留件数</color> ＝ ").Append(eng.PendingCount).Append('\n');
            if (eng.Current != null)
                sb.Append("  id: ").Append(eng.Current.id).Append('\n');

            sb.Append("\n<color=#5b6b7a>──────────────────────────────────────────────</color>\n");
            sb.Append("<color=#e7e0b0>◤ 開示エンジン（秘史/真相/予言/エンディング）</color>\n");
            sb.Append("  <color=#9fb0c0>開示進捗</color> ＝ <color=#ffe08a>").Append((gv.DisclosureProgress() * 100f).ToString("0")).Append("%</color>");
            sb.Append("　断片 ").Append(gv.IsDisclosureRevealed("secret_fragment") ? "◎" : "—")
              .Append("／真相 ").Append(gv.IsDisclosureRevealed("ancient_truth") ? "◎" : "—")
              .Append("／結末 ").Append(gv.IsDisclosureRevealed("ending_unlocked") ? "◎" : "—").Append('\n');

            sb.Append("\n<color=#6f8a9a>※ 提示中＝プレイヤーへ提示すべきイベント。保留＝発火待ちキュー。履歴の列挙APIは無いため現状値のみ。</color>");
            return sb.ToString();
        }

        // ===== UI 構築（EconomyObserverOverlay と同型・単一スクロールラベル版） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("ChronicleObserverCanvas");
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

            WindowChrome.AddTitleBarLayout(frameRT, "イベント", () => SetVisible(false));
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
