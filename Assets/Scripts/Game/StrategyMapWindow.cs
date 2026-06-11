using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 戦略（星系）マップの Windows 風UI（銀英伝の古典UI意匠・#UI統一）。
    /// 構成は2つ：①画面上部の<b>固定コマンドメニューバー</b>（国家ステータス・二重暦/速度・各パネルを開くボタン列）と、
    /// ②<b>ドラッグで動かせる星系マップ窓</b>。マップ窓はカメラのビューポート <see cref="Camera.rect"/> を窓の中身領域に合わせ、
    /// タイトルバーをつかむと窓ごと移動できる（<see cref="UIDragMove"/>＋毎フレーム rect 同期）。
    /// <b>当たり判定維持</b>：<see cref="GalaxyView"/> は <see cref="Camera.ScreenToWorldPoint"/> でクリックを拾い、
    /// これはビューポート rect を尊重するため窓を動かしても選択/進軍は正しく動く。
    /// 窓の外（デスクトップ領域）は<b>背景カメラ</b>が毎フレーム黒でクリアするため残像が出ない。
    /// 浮いていた税率行/操作ヒントは <see cref="GalaxyView.HideWorldHud"/> で抑制し上メニューへ集約。Strategy シーンのみ自動生成。
    /// </summary>
    public class StrategyMapWindow : MonoBehaviour
    {
        [Header("上部メニューバー")]
        [Tooltip("メニューバーの高さ（画面高に対する比）")]
        public float menuBarFrac = 0.088f;

        [Header("マップ窓の初期配置（参照解像度 1920x1080 ピクセル）")]
        public float windowMargin = 30f;     // 左右マージン
        public float windowGapTop = 12f;      // メニューバーとの隙間
        public float windowMarginBottom = 34f;
        public float mapTitleHeight = 30f;    // 窓タイトルバーの高さ

        [Header("配色（ゲーム意匠）")]
        public Color frameColor = new Color(0.09f, 0.12f, 0.18f, 1f);
        public Color menuBarColor = new Color(0.11f, 0.15f, 0.22f, 1f);
        public Color titleBarColor = new Color(0.13f, 0.18f, 0.26f, 1f);
        public Color buttonColor = new Color(0.16f, 0.21f, 0.30f, 1f);
        public Color accentColor = new Color(1f, 0.84f, 0.36f, 1f);
        public Color desktopColor = new Color(0.02f, 0.02f, 0.05f, 1f);

        private Camera cam;
        private Camera bgCam;            // 窓の外を黒でクリア（残像防止）
        private Rect originalRect;
        private bool rectApplied;
        private RectTransform mapContent; // マップ窓の中身領域（= camera.rect の元）
        private TextMeshProUGUI clockLabel;
        private TextMeshProUGUI statsLabel;

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
            if (scene.name != "Strategy") return;
            if (UnityEngine.Object.FindAnyObjectByType<StrategyMapWindow>() != null) return;
            new GameObject("StrategyMapWindow").AddComponent<StrategyMapWindow>();
        }

        private void Awake()
        {
            cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            GalaxyView.HideWorldHud = true;
            SetupBackgroundCamera();
            BuildUI();
            SyncCameraRect(); // 初期反映
        }

        private void OnDestroy()
        {
            if (cam != null && rectApplied) cam.rect = originalRect;
            if (bgCam != null) Destroy(bgCam.gameObject);
            GalaxyView.HideWorldHud = false;
        }

        private void Update()
        {
            TimeDisplay.StepSpeedInput();
            if (clockLabel != null && TimeDisplay.TryFormatNow(out string text, out Color color))
            {
                clockLabel.text = text;
                clockLabel.color = color;
            }
            UpdateStatsLabel();
        }

        private void LateUpdate()
        {
            // 窓のドラッグ後に rect を合わせる（レイアウト確定後）
            SyncCameraRect();
        }

        // ===== カメラ =====

        private void SetupBackgroundCamera()
        {
            if (cam == null) return;
            originalRect = cam.rect;
            rectApplied = true;

            var go = new GameObject("StrategyDesktopCamera");
            bgCam = go.AddComponent<Camera>();
            bgCam.orthographic = true;
            bgCam.depth = cam.depth - 1f;          // 先に描いて全画面を黒クリア
            bgCam.clearFlags = CameraClearFlags.SolidColor;
            bgCam.backgroundColor = desktopColor;
            bgCam.cullingMask = 0;                 // 何も描かず塗るだけ
            bgCam.rect = new Rect(0f, 0f, 1f, 1f);
        }

        /// <summary>マップ窓の中身領域のスクリーン矩形を camera.rect（正規化）へ反映する。</summary>
        private void SyncCameraRect()
        {
            if (cam == null || mapContent == null) return;
            Vector3[] c = new Vector3[4];
            mapContent.GetWorldCorners(c); // ScreenSpaceOverlay では画面ピクセル座標
            float sw = Screen.width, sh = Screen.height;
            if (sw <= 0f || sh <= 0f) return;

            float x = c[0].x / sw;
            float y = c[0].y / sh;
            float w = (c[2].x - c[0].x) / sw;
            float h = (c[2].y - c[0].y) / sh;

            // 画面内に収め、退化（幅/高さ≈0）を弾く
            x = Mathf.Clamp01(x); y = Mathf.Clamp01(y);
            w = Mathf.Clamp(w, 0.05f, 1f - x);
            h = Mathf.Clamp(h, 0.05f, 1f - y);
            cam.rect = new Rect(x, y, w, h);
        }

        // ===== UI =====

        private void BuildUI()
        {
            var canvasObj = new GameObject("StrategyMapWindowCanvas");
            canvasObj.transform.SetParent(transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 860;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();

            BuildMenuBar(canvasObj.transform);
            BuildMapWindow(canvasObj.transform);
        }

        /// <summary>画面上部の固定コマンドメニューバー（ステータス＋時計＋コマンド列）。</summary>
        private void BuildMenuBar(Transform root)
        {
            var bar = AddBar(root, "MenuBar", new Vector2(0f, 1f - menuBarFrac), new Vector2(1f, 1f), menuBarColor);

            // 上段：ステータス（中央）＋時計（右）
            var top = new GameObject("TopRow").AddComponent<RectTransform>();
            top.transform.SetParent(bar.transform, false);
            top.anchorMin = new Vector2(0f, 0.48f); top.anchorMax = new Vector2(1f, 1f);
            top.offsetMin = Vector2.zero; top.offsetMax = Vector2.zero;

            var title = AddText(top, "≡ 戦略", 20f, accentColor, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            SetAnchors(title.rectTransform, new Vector2(0f, 0f), new Vector2(0.20f, 1f), new Vector2(20f, 0f), new Vector2(-8f, 0f));

            statsLabel = AddText(top, "", 16f, new Color(0.85f, 0.9f, 0.7f), TextAlignmentOptions.Center);
            SetAnchors(statsLabel.rectTransform, new Vector2(0.18f, 0f), new Vector2(0.74f, 1f), Vector2.zero, Vector2.zero);

            clockLabel = AddText(top, "", 16f, new Color(0.95f, 0.92f, 0.7f), TextAlignmentOptions.Right);
            SetAnchors(clockLabel.rectTransform, new Vector2(0.74f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-20f, 0f));

            // 下段：コマンドボタン列
            var cmd = new GameObject("CommandRow").AddComponent<RectTransform>();
            cmd.transform.SetParent(bar.transform, false);
            cmd.anchorMin = new Vector2(0f, 0f); cmd.anchorMax = new Vector2(1f, 0.48f);
            cmd.offsetMin = new Vector2(16f, 4f); cmd.offsetMax = new Vector2(-16f, -2f);
            var hlg = cmd.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;

            AddCommand(cmd.transform, "勢力", () => UnityEngine.Object.FindAnyObjectByType<CampaignObserverOverlay>()?.Toggle());
            AddCommand(cmd.transform, "財政", () => UnityEngine.Object.FindAnyObjectByType<EconomyObserverOverlay>()?.Toggle());
            AddCommand(cmd.transform, "軍事", () => UnityEngine.Object.FindAnyObjectByType<MilitaryObserverOverlay>()?.Toggle());
            AddCommand(cmd.transform, "人事", () => UnityEngine.Object.FindAnyObjectByType<PersonObserverOverlay>()?.Toggle());
            AddCommand(cmd.transform, "解決", () => UnityEngine.Object.FindAnyObjectByType<DecisionBoardPanel>()?.Toggle());
            AddCommand(cmd.transform, "情報", () => UnityEngine.Object.FindAnyObjectByType<CoreStateInspector>()?.Toggle());
            AddCommand(cmd.transform, "通知", () => UnityEngine.Object.FindAnyObjectByType<NotificationLogOverlay>()?.Toggle());
            AddCommand(cmd.transform, "ヘルプ", () => UnityEngine.Object.FindAnyObjectByType<HelpOverlay>()?.Toggle());

            // メニューバー下端の金色ルール線
            var rule = AddBar(bar.transform, "Rule", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Color(accentColor.r, accentColor.g, accentColor.b, 0.6f));
            var rrt = (RectTransform)rule.transform; rrt.pivot = new Vector2(0.5f, 0f); rrt.sizeDelta = new Vector2(0f, 2f);
        }

        /// <summary>ドラッグで動かせる星系マップ窓（タイトルバー＋透明な中身領域＝camera.rect）。</summary>
        private void BuildMapWindow(Transform root)
        {
            // 窓root（左上アンカー・ドラッグで anchoredPosition を動かす）
            var win = new GameObject("MapWindow").AddComponent<RectTransform>();
            win.transform.SetParent(root, false);
            win.anchorMin = new Vector2(0f, 1f); win.anchorMax = new Vector2(0f, 1f);
            win.pivot = new Vector2(0f, 1f);
            float topPx = menuBarFrac * 1080f + windowGapTop;
            float h = 1080f - topPx - windowMarginBottom;
            float w = 1920f - 2f * windowMargin;
            win.anchoredPosition = new Vector2(windowMargin, -topPx);
            win.sizeDelta = new Vector2(w, h);
            // 窓root には Image を付けない（中身は透明＝マップを見せる・クリックを塞がない）。
            // 縁取りは中身領域の周囲に細い枠バーで描く（マップの上に不透明UIを重ねない）。

            // タイトルバー（上端・つかんで移動）
            var titleBar = new GameObject("MapTitleBar").AddComponent<RectTransform>();
            titleBar.transform.SetParent(win.transform, false);
            titleBar.anchorMin = new Vector2(0f, 1f); titleBar.anchorMax = new Vector2(1f, 1f);
            titleBar.pivot = new Vector2(0.5f, 1f);
            titleBar.sizeDelta = new Vector2(0f, mapTitleHeight);
            titleBar.anchoredPosition = Vector2.zero;
            var tImg = titleBar.gameObject.AddComponent<Image>();
            tImg.color = titleBarColor;
            var drag = titleBar.gameObject.AddComponent<UIDragMove>();
            drag.target = win;

            var cap = AddText(titleBar, "≡ 星系マップ　（ドラッグで移動）", 15f, accentColor, TextAlignmentOptions.Left);
            SetAnchors(cap.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));

            // 中身領域（透明＝マップが見える。ここが camera.rect になる）
            mapContent = new GameObject("MapContent").AddComponent<RectTransform>();
            mapContent.transform.SetParent(win.transform, false);
            mapContent.anchorMin = new Vector2(0f, 0f); mapContent.anchorMax = new Vector2(1f, 1f);
            mapContent.offsetMin = new Vector2(2f, 2f);
            mapContent.offsetMax = new Vector2(-2f, -mapTitleHeight);
            // Image は付けない（透明）＝マップのクリックを妨げない＆カメラが透けて見える

            // 窓の縁取り（細い金色バー・クリックは塞がない）
            Color edge = new Color(accentColor.r, accentColor.g, accentColor.b, 0.5f);
            AddEdgeBar(win, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, 0f), Vector2.zero, edge);          // 左
            AddEdgeBar(win, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(2f, 0f), Vector2.zero, edge);          // 右
            AddEdgeBar(win, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), Vector2.zero, edge);          // 下
            AddEdgeBar(win, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 2f), new Vector2(0f, -mapTitleHeight), edge); // タイトル下のルール
        }

        /// <summary>窓の縁に細いバーを置く（raycastTarget=false＝マップ操作を妨げない）。</summary>
        private static void AddEdgeBar(Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot,
            Vector2 sizeDelta, Vector2 anchoredPos, Color color)
        {
            var go = new GameObject("Edge");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.sizeDelta = sizeDelta; rt.anchoredPosition = anchoredPos;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private void UpdateStatsLabel()
        {
            if (statsLabel == null) return;
            CampaignState campaign = StrategySession.Campaign;
            if (campaign == null) { statsLabel.text = ""; return; }
            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            FactionState s = CampaignRules.GetState(campaign, pf);
            if (s == null) { statsLabel.text = ""; return; }
            float hope = s.community != null ? s.community.hope : 0f;
            float stab = CampaignRules.EffectiveStability(campaign, pf);
            statsLabel.text = $"税率 {s.taxRate * 100f:0}%　国庫 {s.treasury:0}　民心 {hope * 100f:0}%　安定度 {stab * 100f:0}%";
            statsLabel.color = hope < 0.2f ? new Color(1f, 0.55f, 0.45f) : new Color(0.85f, 0.9f, 0.7f);
        }

        // ===== 部品 =====

        private void AddCommand(Transform parent, string label, System.Action onClick)
        {
            var go = new GameObject("Cmd_" + label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = Color.white;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = buttonColor;
            cb.highlightedColor = new Color(0.27f, 0.45f, 0.68f, 1f);
            cb.pressedColor = new Color(0.20f, 0.36f, 0.58f, 1f);
            cb.selectedColor = buttonColor;
            cb.fadeDuration = 0.05f;
            btn.colors = cb;
            btn.onClick.AddListener(() => onClick());
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = 92f; le.preferredWidth = 92f; le.flexibleWidth = 0f;

            var t = AddText(go.transform, label, 16f, new Color(0.92f, 0.95f, 1f), TextAlignmentOptions.Center);
            SetAnchors(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private static Image AddBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
        }

        private static TextMeshProUGUI AddText(Transform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            TMP_FontAsset ja = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (ja != null) tmp.font = ja;
            return tmp;
        }
    }
}
