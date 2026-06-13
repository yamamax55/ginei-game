using System;
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
    /// 通知ログ観測オーバーレイ（観測層・read-only）。<b>N キー</b>で開閉し、<see cref="NotificationCenter"/> の
    /// 有界リングバッファ（<see cref="NotificationCenter.Capacity"/> 件）を**新しい順に履歴ダンプ**する。
    /// 左下フィード（<see cref="NotificationFeed"/>）は流れて消えるトーストだけだが、ここは履歴を遡れる＝
    /// 「いつ何が起きたか」を後から確認できる。カテゴリ別の件数サマリも併記。操作はさせない＝状態は変えない。
    /// `CampaignObserverOverlay`（G）/ `CoreStateInspector`（J）/ `MilitaryObserverOverlay`（M）と同じ観測層の家族。
    /// 戦略/会戦の両シーンへ自動生成（<see cref="HelpOverlay"/> / <see cref="TimeDisplay"/> と同じ型）。
    /// </summary>
    public class NotificationLogOverlay : MonoBehaviour
    {
        // ===== 調整可能なパラメーター =====

        [Header("外観")]
        [Tooltip("オーバーレイ Canvas の描画順（他UIより手前）")]
        public int canvasSortingOrder = 1093;

        [Tooltip("背景ディマーの不透明度（0〜1）")]
        public float dimAlpha = 0.55f;

        [Tooltip("パネルの幅（ピクセル）")]
        public float panelWidth = 1000f;

        [Tooltip("パネルの最大高さ（ピクセル）")]
        public float panelMaxHeight = 920f;

        [Tooltip("パネル背景色")]
        public Color panelColor = new Color(0.04f, 0.05f, 0.08f, 0.96f);

        [Tooltip("本文のフォントサイズ")]
        public float bodyFontSize = 19f;

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
            if (UnityEngine.Object.FindAnyObjectByType<NotificationLogOverlay>() != null) return;
            new GameObject("NotificationLogOverlay").AddComponent<NotificationLogOverlay>();
        }

        // ===== Unity ライフサイクル =====

        private void Awake()
        {
            BuildUI();
            SetVisible(false); // 初期は非表示
        }

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.通知ログ切替))
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
            var sb = new StringBuilder(4096);
            IReadOnlyList<Notification> all = NotificationCenter.All;
            int total = all != null ? all.Count : 0;

            sb.Append("<b>通知ログ</b>　保持 ").Append(total).Append('/').Append(NotificationCenter.Capacity)
              .Append(" 件　(N で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            if (total == 0)
            {
                sb.Append("\n<color=#ffcc66>通知はまだありません。</color>\n");
                sb.Append("会戦結果・占領・造船完成・提督死去などが NotificationCenter 経由で\n");
                sb.Append("ここに履歴として積まれます（左下フィードは流れて消える分のみ）。");
                return sb.ToString();
            }

            // カテゴリ別の件数サマリ
            AppendCategorySummary(sb, all);

            // 本体（新しい順）
            sb.Append("<color=#9aa7b3>― 履歴（新しい順）</color>\n");
            List<Notification> recent = NotificationCenter.Recent(NotificationCenter.Capacity);
            for (int i = 0; i < recent.Count; i++)
                AppendNotification(sb, recent[i]);

            return sb.ToString();
        }

        private void AppendCategorySummary(StringBuilder sb, IReadOnlyList<Notification> all)
        {
            var counts = new Dictionary<NotificationCategory, int>();
            for (int i = 0; i < all.Count; i++)
            {
                counts.TryGetValue(all[i].category, out int c);
                counts[all[i].category] = c + 1;
            }

            sb.Append("  ");
            bool first = true;
            foreach (NotificationCategory cat in Enum.GetValues(typeof(NotificationCategory)))
            {
                if (!counts.TryGetValue(cat, out int c) || c <= 0) continue;
                if (!first) sb.Append("　");
                first = false;
                sb.Append("<color=").Append(CategoryColor(cat)).Append('>').Append(cat).Append("</color> ").Append(c);
            }
            sb.Append("\n\n");
        }

        private void AppendNotification(StringBuilder sb, Notification n)
        {
            string sevMark = n.severity == NotificationSeverity.警告 ? "<color=#ff6b6b>● 警告</color>"
                           : n.severity == NotificationSeverity.注意 ? "<color=#ffd28a>● 注意</color>"
                           : "<color=#8aa0b0>○ 情報</color>";
            sb.Append("  <color=#5b6b7a>#").Append(n.seq).Append("</color> ")
              .Append("<color=").Append(CategoryColor(n.category)).Append(">[").Append(n.category).Append("]</color> ")
              .Append(sevMark).Append("  ")
              .Append("<color=#e6edf3>").Append(n.message).Append("</color>\n");
        }

        /// <summary>カテゴリごとの色（フィードの色分け方針に揃えた観測用の淡色）。</summary>
        private static string CategoryColor(NotificationCategory cat)
        {
            switch (cat)
            {
                case NotificationCategory.戦闘:   return "#ff9a8a";
                case NotificationCategory.建艦:   return "#9fe0ff";
                case NotificationCategory.占領:   return "#ffd28a";
                case NotificationCategory.政治:   return "#c9b3ff";
                case NotificationCategory.人事:   return "#a0e0a0";
                case NotificationCategory.内政:   return "#8ad0ff";
                case NotificationCategory.外交:   return "#e0a0e0";
                default:                          return "#9aa7b3"; // システム
            }
        }

        // ===== UI 構築（CampaignObserverOverlay と同型・単一スクロールラベル版） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("NotificationLogCanvas");
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
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }
    }
}
