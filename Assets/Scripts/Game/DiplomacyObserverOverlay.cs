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
    /// 外交オブザーバ（観測層・read-only）。<b>Y キー</b>で開閉し、勢力ペアの**配線済みの外交状態**
    /// （<see cref="DiplomacySession"/> の関係値 opinion／外交状態〔平時/同盟/不可侵/属国/交戦〕）と、
    /// 進行中の戦争（<see cref="WarLedger"/>＝開戦ターン/損害/戦況/厭戦）・締結中の条約（<see cref="TreatyLedger"/>）を
    /// 毎フレームライブダンプする。`GalaxyView.RunDiplomacyTick`（年次）が回している分＝盤面で実際に動く外交だけを映す。
    /// `FactionRelations.IsHostile` を駆動するペア相互作用は J（汎用インスペクタ）の木では読みづらいため、
    /// 関係マトリクスとして並べる。操作はさせない＝**観測専用＝状態は変えない**。
    /// `CampaignObserverOverlay`（G）の外交版。`HelpOverlay`/`TimeDisplay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class DiplomacyObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        [Tooltip("オーバーレイ Canvas の描画順（他UIより手前）")]
        public int canvasSortingOrder = 1096;

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
            if (Object.FindAnyObjectByType<DiplomacyObserverOverlay>() != null) return;
            new GameObject("DiplomacyObserverOverlay").AddComponent<DiplomacyObserverOverlay>();
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "外交");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.外交観測切替))
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
            sb.Append("<b>外交オブザーバ</b>　関係値・外交状態・進行中の戦争・締結条約　(Y で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            if (!DiplomacySession.HasState)
            {
                sb.Append("\n<color=#ffcc66>外交状態（DiplomacySession）がまだ初期化されていません。</color>\n");
                sb.Append("戦略マップ（GalaxyView）が年次の RunDiplomacyTick で勢力ペアを初期化すると、\n");
                sb.Append("ここに関係値・外交状態・進行中の戦争・締結条約がライブ表示されます。");
                return sb.ToString();
            }

            DiplomacyState st = DiplomacySession.State;

            // --- 関係マトリクス（ペア相互作用＝J では読みづらい核） ---
            sb.Append("\n<color=#e7e0b0>◤ 勢力関係（opinion −100〜+100 ／ 外交状態）</color>\n");
            int pairs = st.entries != null ? st.entries.Count : 0;
            if (pairs == 0)
                sb.Append("  <color=#9aa7b2>（登録ペアなし）</color>\n");
            for (int i = 0; i < pairs; i++)
            {
                DiplomacyState.Entry e = st.entries[i];
                if (e == null) continue;
                float n01 = Mathf.Clamp01((e.opinion + 100f) / 200f);
                sb.Append("  ").Append(e.factionA).Append(" ⇔ ").Append(e.factionB).Append("  ");
                sb.Append('<').Append("color=").Append(StatusColor(e.status)).Append('>')
                  .Append('【').Append(e.status).Append('】').Append("</color> ");
                AppendBarInline(sb, n01, OpinionColor(e.opinion));
                sb.Append(' ').Append(e.opinion.ToString("+0;-0;0")).Append('\n');
            }

            // --- 進行中の戦争 ---
            sb.Append("\n<color=#e7e0b0>◤ 進行中の戦争</color>\n");
            var wars = WarLedger.All;
            if (wars == null || wars.Count == 0)
                sb.Append("  <color=#9aa7b2>（戦争なし）</color>\n");
            else
            {
                var p = WarGoalRules.WarGoalParams.Default;
                for (int i = 0; i < wars.Count; i++)
                {
                    WarState w = wars[i];
                    if (w == null) continue;
                    float weariness = WarStateRules.Weariness(w, p);
                    sb.Append("  <color=#ff9a8a>⚔ ").Append(w.factionA).Append(" 対 ").Append(w.factionB)
                      .Append("</color>　経過 ").Append(w.turnsAtWar).Append(" ターン\n");
                    AppendBar(sb, "    損害", Mathf.Clamp01(w.casualties), "#ff9a8a");
                    AppendBar(sb, "    厭戦", Mathf.Clamp01(weariness), "#ffcc66");
                    sb.Append("    <color=#9fb0c0>戦況(A優位+/B優位−)</color> ＝ <color=#ffe08a>")
                      .Append(w.warScore.ToString("+0.00;-0.00;0.00")).Append("</color>\n");
                }
            }

            // --- 締結中の条約 ---
            sb.Append("\n<color=#e7e0b0>◤ 締結中の条約</color>\n");
            var treaties = TreatyLedger.All;
            if (treaties == null || treaties.Count == 0)
                sb.Append("  <color=#9aa7b2>（条約なし）</color>\n");
            else
            {
                for (int i = 0; i < treaties.Count; i++)
                {
                    ActiveTreaty t = treaties[i];
                    if (t == null) continue;
                    sb.Append("  <color=#7fd4ff>").Append(t.type).Append("</color>　")
                      .Append(t.factionA).Append(" − ").Append(t.factionB)
                      .Append("　<color=#6f8a9a>締結 SE").Append(t.signedYear);
                    if (t.durationYears <= 0) sb.Append(" ／ 恒久");
                    else sb.Append(" ／ 失効 SE").Append(t.ExpiresYear);
                    sb.Append("</color>\n");
                }
            }

            sb.Append("\n<color=#6f8a9a>※ opinion と外交状態が FactionRelations.IsHostile を駆動する＝戦前の関係が会戦の敵味方を決める。</color>");
            return sb.ToString();
        }

        private static string StatusColor(DiplomacyState.DiplomaticStatus s)
        {
            switch (s)
            {
                case DiplomacyState.DiplomaticStatus.交戦: return "#ff7a6a";
                case DiplomacyState.DiplomaticStatus.同盟: return "#8ce08c";
                case DiplomacyState.DiplomaticStatus.不可侵: return "#7fd4ff";
                case DiplomacyState.DiplomaticStatus.属国: return "#c9a0ff";
                default: return "#9aa7b2"; // 平時
            }
        }

        private static string OpinionColor(float opinion)
            => opinion >= 25f ? "#8ce08c" : (opinion <= -25f ? "#ff9a8a" : "#ffd28a");

        private void AppendBar(StringBuilder sb, string label, float v01, string colorHex)
        {
            v01 = Mathf.Clamp01(v01);
            int filled = Mathf.RoundToInt(v01 * barWidth);
            sb.Append(label).Append("  <color=").Append(colorHex).Append('>');
            for (int i = 0; i < barWidth; i++) sb.Append(i < filled ? '█' : '░');
            sb.Append("</color> ").Append(v01.ToString("0.00")).Append('\n');
        }

        private void AppendBarInline(StringBuilder sb, float v01, string colorHex)
        {
            v01 = Mathf.Clamp01(v01);
            int filled = Mathf.RoundToInt(v01 * barWidth);
            sb.Append("<color=").Append(colorHex).Append('>');
            for (int i = 0; i < barWidth; i++) sb.Append(i < filled ? '█' : '░');
            sb.Append("</color>");
        }

        // ===== UI 構築（EconomyObserverOverlay と同型・単一スクロールラベル版） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("DiplomacyObserverCanvas");
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

            WindowChrome.AddTitleBarLayout(frameRT, "外交", () => SetVisible(false));
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
