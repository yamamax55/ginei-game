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
    /// 国家状態オブザーバ（汎用 観測オーバーレイ）。G キーで開閉し、<see cref="StrategySession.Campaign"/> 配下の
    /// 全 <see cref="FactionState"/>（王朝/統治体/組織/共同体＋統治スタイル/税）を**そのままダンプ**する。
    /// 操作はさせない＝Core 純ロジック（社会・政治シミュ層）が盤面で何を計算しているかを「見える模型」にするだけ。
    /// 第1層＝観測化のパイロット：未配線の Core 群を、まず眺められるようにする土台。
    /// 戦略/会戦の両シーンへ自動生成（<see cref="HelpOverlay"/> / <see cref="TimeDisplay"/> と同じ型）。
    /// </summary>
    public class CampaignObserverOverlay : MonoBehaviour
    {
        // ===== 調整可能なパラメーター =====

        [Header("外観")]
        [Tooltip("オーバーレイ Canvas の描画順（他UIより手前）")]
        public int canvasSortingOrder = 1090;

        [Tooltip("背景ディマーの不透明度（0〜1）")]
        public float dimAlpha = 0.55f;

        [Tooltip("パネルの幅（ピクセル）")]
        public float panelWidth = 960f;

        [Tooltip("パネルの最大高さ（ピクセル）")]
        public float panelMaxHeight = 880f;

        [Tooltip("パネル背景色")]
        public Color panelColor = new Color(0.03f, 0.05f, 0.09f, 0.96f);

        [Tooltip("本文のフォントサイズ")]
        public float bodyFontSize = 20f;

        [Tooltip("バー（0..1可視化）の桁数")]
        public int barWidth = 14;

        // ===== 内部状態 =====

        private GameObject overlayRoot;
        private GameObject panel;
        // 毎フレーム更新する1枚の本文ラベル（行ごとに組み立てず文字列を差し替える＝ダンプ表示）
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
            if (Object.FindAnyObjectByType<CampaignObserverOverlay>() != null) return;
            GameObject go = new GameObject("CampaignObserverOverlay");
            go.AddComponent<CampaignObserverOverlay>();
        }

        // ===== Unity ライフサイクル =====

        private object escWindowToken; // UIWindowStack 登録トークン（#ウィンドウESC）

        private void Awake()
        {
            BuildUI();
            SetVisible(false); // 初期は非表示
            // ESC は UIWindowStack 経由で「手前から閉じる」（G は従来どおり開閉トグル）。
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "国家状態");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            // 開閉（入力は GameInput に集約・#107／共通アクションで戦略/会戦どちらでも）
            if (GameInput.WasPressed(GameAction.観測オーバーレイ切替))
                Toggle();

            // 表示中だけ毎フレーム最新の状態を流し込む（Core の Tick 結果をライブで眺める）
            if (panel != null && panel.activeSelf && bodyLabel != null)
                bodyLabel.text = BuildDump();
        }

        // ===== 公開API =====

        public void Toggle()
        {
            bool next = panel != null && !panel.activeSelf;
            SetVisible(next);
        }

        public void SetVisible(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        // ===== ダンプ本体 =====

        /// <summary>
        /// <see cref="StrategySession.Campaign"/> 配下の全 <see cref="FactionState"/> を文字列にダンプする。
        /// 合成API（<see cref="FactionStateRules"/> / <see cref="CampaignRules"/>）の派生値も併記し、
        /// 生フィールド（regime/polity/organization/community）はバー付きで並べる。観測のみ＝状態は一切変えない。
        /// </summary>
        private string BuildDump()
        {
            var sb = new StringBuilder(2048);
            CampaignState c = StrategySession.Campaign;

            if (c == null)
            {
                sb.Append("<color=#ffcc66>戦役データ（StrategySession.Campaign）がありません。</color>\n\n");
                sb.Append("戦略マップ（GalaxyView）を一度起動すると CampaignState が生成され、\n");
                sb.Append("ここに各勢力の国家状態（腐敗→正統性→合意→希望）がライブ表示されます。");
                return sb.ToString();
            }

            int count = c.states != null ? c.states.Count : 0;
            Faction leading = CampaignRules.LeadingFaction(c);
            sb.Append($"<b>国家状態オブザーバ</b>　勢力数: {count}　暫定優勢: <color=#9fe0ff>{leading}</color>　(G で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────</color>\n");

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
            return sb.ToString();
        }

        private void AppendFaction(StringBuilder sb, CampaignState c, FactionState s)
        {
            float stability = FactionStateRules.Stability(s);
            float effective = CampaignRules.EffectiveStability(c, s.faction);
            bool collapsing = FactionStateRules.IsCollapsing(s);

            string head = collapsing ? "<color=#ff6b6b>" : "<color=#bfe9c0>";
            sb.Append('\n').Append(head).Append("◤ ").Append(s.faction).Append("</color>");
            if (collapsing) sb.Append("　<color=#ff6b6b>▲ 崩壊中</color>");
            sb.Append('\n');

            // 合成（派生値）
            AppendBar(sb, "  総合安定度", stability, "#7fd4ff");
            AppendBar(sb, "  実効安定度", effective, "#7fd4ff");
            sb.Append($"    <color=#8aa0b0>(実効 = 安定度 × 版図一体化度 / 国庫 {s.treasury:0.#})</color>\n");

            // 統治スタイル・税レバー
            AppendBar(sb, "  包摂度", s.inclusiveness, "#c9b3ff");
            AppendBar(sb, "  税率", s.taxRate, "#ffd28a");

            // 王朝（天命/腐敗/徳）
            sb.Append("  <color=#9aa7b3>― 王朝 Regime</color>\n");
            AppendBar(sb, "    正統性(天命)", s.regime.legitimacy, "#ffe08a");
            AppendBar(sb, "    腐敗", s.regime.corruption, "#ff9a8a");
            AppendBar(sb, "    徳", s.regime.virtue, "#a0e0a0");

            // 統治体（合意/抑圧）
            sb.Append("  <color=#9aa7b3>― 統治体 Polity</color>\n");
            AppendBar(sb, "    協力(合意)", s.polity.cooperation, "#8ad0ff");
            AppendBar(sb, "    抑圧", s.polity.oppression, "#ff9a8a");
            sb.Append($"    <color=#8aa0b0>(人口 {s.polity.population:#,0} / 支配戦力 {s.polity.rulerForce:#,0})</color>\n");

            // 組織（結束/制度化/カリスマ）
            sb.Append("  <color=#9aa7b3>― 組織 Organization</color>\n");
            AppendBar(sb, "    結束", s.organization.cohesion, "#8ad0ff");
            AppendBar(sb, "    制度化", s.organization.institutionalization, "#a0e0a0");
            AppendBar(sb, "    個人カリスマ", s.organization.leaderCharisma, "#ffd28a");
            if (s.organization.fragmented) sb.Append("    <color=#ff6b6b>※ 組織崩壊</color>\n");

            // 共同体（希望/抑圧/末人）
            sb.Append("  <color=#9aa7b3>― 共同体 Community</color>\n");
            AppendBar(sb, "    希望", s.community.hope, "#a0e0a0");
            AppendBar(sb, "    抑圧(秩序)", s.community.repression, "#ff9a8a");
            if (s.community.dissent) sb.Append("    <color=#ff6b6b>※ 末人（ロンドン派）発生</color>\n");
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

        // ===== UI 構築（HelpOverlay と同型・単一スクロールラベル版） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("CampaignObserverCanvas");
            overlayRoot.transform.SetParent(transform);
            Canvas canvas = overlayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            CanvasScaler scaler = overlayRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            overlayRoot.AddComponent<GraphicRaycaster>();

            // 全画面ディマー兼パネル root
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
            // 外枠フレーム（固定サイズ・左上寄せ＝盤面を見ながら横で眺められるよう左側に）
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

            WindowChrome.AddTitleBarLayout(frameRT, "国家状態（勢力）", () => SetVisible(false));
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
            // ドラッグ受け用の透明 Image＋RectMask2D（HelpOverlay と同じ＝Mask+alpha0 は使わない）
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

            // 本文（1枚のマルチラインラベル・毎フレーム差し替え）
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

        // ===== ヘルパー（HelpOverlay と同一の呼び方） =====

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
