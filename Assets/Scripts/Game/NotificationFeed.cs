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
    /// 旧仕様（時間で消えるトースト）と違い、**新着が来たら古いものを押し出すだけで4〜5件は残る**＝見逃さない。
    /// 生成時に <see cref="NotificationCenter.Recent"/> で直近履歴を seed し、以降の新着は <see cref="NotificationCenter.Since"/>
    /// で全件反映する（取りこぼさない）。全履歴は N キーの <see cref="NotificationLogOverlay"/> で遡れる。
    /// Strategy/Battle 両シーンへ自動生成。クリックはタイトルバーのドラッグのみ受ける（行はクリックスルー）。
    /// </summary>
    public class NotificationFeed : MonoBehaviour
    {
        [Header("表示")]
        [Tooltip("常時残す通知の最大件数（超過は古いものから押し出す）")]
        public int maxRows = 5;
        [Tooltip("パネルの幅（ピクセル）")]
        public float panelWidth = 600f;
        [Tooltip("画面左下からのマージン（X, Y）。下端で見切れないよう Y は余裕を取る")]
        public Vector2 margin = new Vector2(24f, 40f);

        [Header("配色（ゲーム意匠）")]
        public Color panelColor = new Color(0.05f, 0.07f, 0.12f, 0.94f);
        public Color borderColor = new Color(0.45f, 0.40f, 0.22f, 0.9f);
        public Color titleBarColor = new Color(0.09f, 0.12f, 0.18f, 1f);
        public Color accentColor = new Color(1f, 0.84f, 0.36f, 1f);

        private long lastSeq;
        private RectTransform window;     // 枠ウィンドウ本体（ドラッグ対象）
        private Transform rowsParent;     // 通知行の親
        private TMP_FontAsset jpFont;
        private readonly List<GameObject> rows = new List<GameObject>();

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

            // 直近履歴を seed（パネルを空にしない＝開いた瞬間から4〜5件見える）。
            // Recent は新しい順で返すため、古い→新しいの順に積む（新着が下＝Since と同じ並び）。
            var recent = NotificationCenter.Recent(maxRows);
            for (int i = recent.Count - 1; i >= 0; i--) AddRow(recent[i]);
            lastSeq = NotificationCenter.LastSeq; // 以降は新着だけ追記
        }

        private void Update()
        {
            // 新着を全件追記（古い順）。時間では消さず、件数超過で古いものを押し出す＝4〜5件は常に残る。
            var fresh = NotificationCenter.Since(lastSeq);
            for (int i = 0; i < fresh.Count; i++)
            {
                AddRow(fresh[i]);
                lastSeq = fresh[i].seq;
            }
        }

        /// <summary>通知1件を行として追加し、上限を超えたら最古を押し出す。</summary>
        private void AddRow(Notification n)
        {
            var go = new GameObject("Row");
            go.transform.SetParent(rowsParent, false);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = $"▸ {n.message}";
            label.fontSize = 19f;
            label.color = SeverityColor(n.severity);
            label.raycastTarget = false; // クリックスルー
            label.alignment = TextAlignmentOptions.Left;
            label.enableWordWrapping = true;
            if (jpFont != null) label.font = jpFont;

            go.transform.SetAsLastSibling(); // 新しいものを下に
            rows.Add(go);

            while (rows.Count > maxRows)
            {
                if (rows[0] != null) Destroy(rows[0]);
                rows.RemoveAt(0);
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

            // 枠ウィンドウ（左下・下端から余裕を取って見切れ防止・下端アンカーで上へ伸びる）
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
            winFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize; // 内容に応じて上へ伸びる

            BuildTitleBar(win.transform);
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

            // つかんでウィンドウを移動（決裁デスクと同じ UIDragMove を再利用）
            var drag = bar.AddComponent<UIDragMove>();
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
            label.raycastTarget = false; // バー本体にドラッグを通す
            if (jpFont != null) label.font = jpFont;

            // タイトルバー下端の金色ルール線
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

        /// <summary>通知行を縦積みする領域（新しいものが下）。</summary>
        private void BuildRowsArea(Transform parent)
        {
            var area = new GameObject("Rows");
            area.transform.SetParent(parent, false);
            area.AddComponent<RectTransform>();
            var vlg = area.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 8, 10);
            vlg.spacing = 5f;
            vlg.childAlignment = TextAnchor.LowerLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            rowsParent = area.transform;
        }
    }
}
