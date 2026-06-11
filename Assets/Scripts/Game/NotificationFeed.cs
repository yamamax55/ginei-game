using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 画面左下の通知パネル（#964 NOTIF-2）。<see cref="NotificationCenter"/> を単一ソースに、直近 <see cref="maxRows"/> 件を
    /// <b>枠付き（Windows 風・タイトルバー＋ドラッグ移動 <see cref="UIDragMove"/>）で常時表示</b>する。
    /// <b>枠は最初から maxRows 件ぶんの高さを確保</b>（件数が少なくてもサイズが安定）し、<b>タブ（全般／重要／決裁）で絞り込み</b>できる。
    /// 旧仕様（時間で消えるトースト）と違い、新着が来たら古いものを押し出すだけで4〜5件は残る＝見逃さない。
    /// 各行は1行（省略表示）。全文・全履歴は N キーの <see cref="NotificationLogOverlay"/> で遡れる。
    /// Strategy/Battle 両シーンへ自動生成。クリックはタイトルバー/タブのみ（行はクリックスルー）。
    /// </summary>
    public class NotificationFeed : MonoBehaviour
    {
        [Header("表示")]
        [Tooltip("常時残す通知の最大件数＝枠が確保する行数")]
        public int maxRows = 5;
        [Tooltip("パネルの幅（ピクセル）")]
        public float panelWidth = 600f;
        [Tooltip("1行の高さ（ピクセル）。枠は maxRows×この高さを最初から確保する")]
        public float rowHeight = 26f;
        [Tooltip("画面左下からのマージン（X, Y）。下端で見切れないよう Y は余裕を取る")]
        public Vector2 margin = new Vector2(24f, 40f);

        [Header("配色（ゲーム意匠）")]
        public Color panelColor = new Color(0.05f, 0.07f, 0.12f, 0.94f);
        public Color borderColor = new Color(0.45f, 0.40f, 0.22f, 0.9f);
        public Color titleBarColor = new Color(0.09f, 0.12f, 0.18f, 1f);
        public Color accentColor = new Color(1f, 0.84f, 0.36f, 1f);

        // タブ定義（ラベルと <see cref="Match"/> のインデックスが対応）。「など」は Match に分岐を足すだけで増やせる。
        private static readonly string[] TabLabels = { "全般", "重要", "決裁" };

        private const float RowSpacing = 5f;
        private const float RowPadTop = 8f;
        private const float RowPadBottom = 10f;

        private long lastSeq;
        private int activeTab;
        private RectTransform window;     // 枠ウィンドウ本体（ドラッグ対象）
        private Transform rowsParent;     // 通知行の親（固定高）
        private TMP_FontAsset jpFont;
        private readonly List<GameObject> rows = new List<GameObject>();
        private readonly List<Image> tabBgs = new List<Image>();
        private readonly List<TextMeshProUGUI> tabTexts = new List<TextMeshProUGUI>();

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
            if (UnityEngine.Object.FindAnyObjectByType<NotificationFeed>() != null) return;
            new GameObject("NotificationFeed").AddComponent<NotificationFeed>();
        }

        private void Awake()
        {
            jpFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            BuildUI();
            UpdateTabVisuals();
            lastSeq = NotificationCenter.LastSeq; // 以降の新着を検知する基準
            RebuildRows();                        // 直近履歴でパネルを満たす（開いた瞬間から見える）
        }

        private void Update()
        {
            // 新着が来たら（時間では消さず）行を作り直す＝最新 maxRows 件を常時表示。
            var fresh = NotificationCenter.Since(lastSeq);
            if (fresh.Count == 0) return;
            lastSeq = fresh[fresh.Count - 1].seq;
            RebuildRows();
        }

        // ===== タブ・フィルタ =====

        /// <summary>タブを切り替えて行を再描画する。</summary>
        private void SetActiveTab(int index)
        {
            if (index == activeTab) return;
            activeTab = index;
            UpdateTabVisuals();
            RebuildRows();
        }

        /// <summary>通知が現在のタブに該当するか。タブ追加は分岐を足すだけ。</summary>
        private static bool Match(int tab, Notification n)
        {
            switch (tab)
            {
                case 1: // 重要＝注意・警告
                    return n.severity == NotificationSeverity.注意 || n.severity == NotificationSeverity.警告;
                case 2: // 決裁＝政治カテゴリ（裁可/諮問/自動処理 等）
                    return n.category == NotificationCategory.政治;
                default: // 0=全般
                    return true;
            }
        }

        // ===== 行の再構築 =====

        /// <summary>現在のタブで直近 maxRows 件を抽出し、古い→新しい順で行を作り直す。</summary>
        private void RebuildRows()
        {
            for (int i = 0; i < rows.Count; i++) if (rows[i] != null) Destroy(rows[i]);
            rows.Clear();

            // Recent は新しい順。フィルタしながら maxRows 件集め、表示は古い→新しいへ反転。
            var recent = NotificationCenter.Recent(NotificationCenter.Capacity);
            var matched = new List<Notification>(maxRows);
            for (int i = 0; i < recent.Count && matched.Count < maxRows; i++)
                if (Match(activeTab, recent[i])) matched.Add(recent[i]);

            for (int i = matched.Count - 1; i >= 0; i--) AddRow(matched[i]);
        }

        /// <summary>通知1件を1行（省略表示・固定高）として追加する。</summary>
        private void AddRow(Notification n)
        {
            var go = new GameObject("Row");
            go.transform.SetParent(rowsParent, false);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = $"▸ {n.message}";
            label.fontSize = 18f;
            label.color = SeverityColor(n.severity);
            label.raycastTarget = false;                       // クリックスルー
            label.alignment = TextAlignmentOptions.Left;
            label.enableWordWrapping = false;                  // 1行固定（枠の高さを安定させる）
            label.overflowMode = TextOverflowModes.Ellipsis;   // 長文は…で省略（全文は N で）
            if (jpFont != null) label.font = jpFont;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = rowHeight;
            le.preferredHeight = rowHeight;

            go.transform.SetAsLastSibling(); // 新しいものを下に
            rows.Add(go);
        }

        private void UpdateTabVisuals()
        {
            for (int i = 0; i < tabBgs.Count; i++)
            {
                bool active = i == activeTab;
                if (tabBgs[i] != null)
                    tabBgs[i].color = active ? accentColor : new Color(0.13f, 0.16f, 0.22f, 1f);
                if (tabTexts[i] != null)
                    tabTexts[i].color = active ? new Color(0.08f, 0.09f, 0.12f) : new Color(0.7f, 0.76f, 0.84f);
            }
        }

        private static Color SeverityColor(NotificationSeverity s)
        {
            switch (s)
            {
                case NotificationSeverity.警告: return new Color(1f, 0.5f, 0.4f);
                case NotificationSeverity.注意: return new Color(1f, 0.85f, 0.4f);
                default: return new Color(0.9f, 0.93f, 0.96f);
            }
        }

        // ===== UI 構築 =====

        private void BuildUI()
        {
            var canvasObj = new GameObject("NotificationCanvas");
            canvasObj.transform.SetParent(transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 880; // ゲームUIより前・モーダル(900+)より後ろ
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();

            // 枠ウィンドウ（左下・下端から余裕を取って見切れ防止）。子は固定高なので全体サイズは安定。
            var win = new GameObject("NotificationWindow");
            win.transform.SetParent(canvasObj.transform, false);
            window = win.AddComponent<RectTransform>();
            window.anchorMin = new Vector2(0f, 0f);
            window.anchorMax = new Vector2(0f, 0f);
            window.pivot = new Vector2(0f, 0f);
            window.anchoredPosition = margin;
            window.sizeDelta = new Vector2(panelWidth, 0f);

            var bg = win.AddComponent<Image>();
            bg.color = panelColor;
            var border = win.AddComponent<Outline>();
            border.effectColor = borderColor;
            border.effectDistance = new Vector2(2f, -2f);

            var winVlg = win.AddComponent<VerticalLayoutGroup>();
            winVlg.padding = new RectOffset(0, 0, 0, 0);
            winVlg.spacing = 0f;
            winVlg.childControlWidth = true;
            winVlg.childControlHeight = true;
            winVlg.childForceExpandWidth = true;
            winVlg.childForceExpandHeight = false;
            var winFitter = win.AddComponent<ContentSizeFitter>();
            winFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildTitleBar(win.transform);
            BuildTabBar(win.transform);
            BuildRowsArea(win.transform);
        }

        /// <summary>タイトルバー（Windows 風・つかんでドラッグ移動）。</summary>
        private void BuildTitleBar(Transform parent)
        {
            var bar = new GameObject("TitleBar");
            bar.transform.SetParent(parent, false);
            var img = bar.AddComponent<Image>();
            img.color = titleBarColor;
            var le = bar.AddComponent<LayoutElement>();
            le.minHeight = 30f;
            le.preferredHeight = 30f;

            var drag = bar.AddComponent<UIDragMove>(); // 決裁デスクと同じ移動コンポーネントを再利用
            drag.target = window;

            var label = new GameObject("Caption").AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(bar.transform, false);
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(12f, 0f); lrt.offsetMax = new Vector2(-12f, 0f);
            label.text = "≡ 通知　（ドラッグで移動／N で履歴）";
            label.fontSize = 15f;
            label.color = accentColor;
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;
            if (jpFont != null) label.font = jpFont;

            var rule = new GameObject("Rule");
            rule.transform.SetParent(bar.transform, false);
            var rrt = rule.AddComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0f, 0f); rrt.anchorMax = new Vector2(1f, 0f);
            rrt.pivot = new Vector2(0.5f, 0f);
            rrt.sizeDelta = new Vector2(0f, 2f); rrt.anchoredPosition = Vector2.zero;
            var ri = rule.AddComponent<Image>();
            ri.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.5f);
            ri.raycastTarget = false;
        }

        /// <summary>タブバー（全般／重要／決裁…）。押すと該当通知だけに絞る。</summary>
        private void BuildTabBar(Transform parent)
        {
            var bar = new GameObject("TabBar");
            bar.transform.SetParent(parent, false);
            bar.AddComponent<RectTransform>();
            var le = bar.AddComponent<LayoutElement>();
            le.minHeight = 28f;
            le.preferredHeight = 28f;

            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(6, 6, 4, 2);
            hlg.spacing = 4f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;  // タブを等幅に
            hlg.childForceExpandHeight = true;

            for (int i = 0; i < TabLabels.Length; i++) BuildTabButton(bar.transform, i, TabLabels[i]);
        }

        private void BuildTabButton(Transform parent, int index, string text)
        {
            var go = new GameObject("Tab_" + text);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.13f, 0.16f, 0.22f, 1f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            int idx = index;
            btn.onClick.AddListener(() => SetActiveTab(idx));
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;

            var t = new GameObject("Text").AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(go.transform, false);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            t.text = text;
            t.fontSize = 15f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.7f, 0.76f, 0.84f);
            t.raycastTarget = false;
            if (jpFont != null) t.font = jpFont;

            tabBgs.Add(img);
            tabTexts.Add(t);
        }

        /// <summary>通知行を縦積みする領域（新しいものが下・最初から maxRows 行ぶんの高さを確保）。</summary>
        private void BuildRowsArea(Transform parent)
        {
            var area = new GameObject("Rows");
            area.transform.SetParent(parent, false);
            area.AddComponent<RectTransform>();

            var vlg = area.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, (int)RowPadTop, (int)RowPadBottom);
            vlg.spacing = RowSpacing;
            vlg.childAlignment = TextAnchor.LowerLeft; // 新しいものが下・空きは上に
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 最初から maxRows 行ぶんの高さを確保（件数が少なくても枠サイズが安定）
            float reserved = maxRows * rowHeight + (maxRows - 1) * RowSpacing + RowPadTop + RowPadBottom;
            var le = area.AddComponent<LayoutElement>();
            le.minHeight = reserved;
            le.preferredHeight = reserved;

            rowsParent = area.transform;
        }
    }
}
