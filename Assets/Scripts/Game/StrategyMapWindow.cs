using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 戦略（星系）マップの Windows 風ウィンドウ＋上部コマンドメニューバー（銀英伝の古典UI意匠・#UI統一）。
    /// カメラのビューポート <see cref="Camera.rect"/> をメニューバー＋枠のぶん内側へ詰め、周囲に枠・タイトル・
    /// 国家ステータス・時計・コマンドボタン列をスクリーン空間で描く。マップ本体（ワールド描画）には触れない。
    /// <b>当たり判定維持</b>：<see cref="GalaxyView"/> は <see cref="Camera.ScreenToWorldPoint"/> でクリックを拾い、
    /// これはビューポート rect を尊重するためビューポートを詰めても選択/進軍は正しく動く。非描画マージンは不透明枠で覆い残像も防ぐ。
    /// コマンドボタンは既存の各パネル（観測オーバーレイ等）の <c>Toggle()</c> を呼ぶ＝並行実装を増やさない。
    /// 浮いていた <see cref="GalaxyView"/> の税率行・操作ヒントは <see cref="GalaxyView.HideWorldHud"/> で抑制し上メニューへ集約。
    /// Strategy シーンのみ自動生成。
    /// </summary>
    public class StrategyMapWindow : MonoBehaviour
    {
        [Header("枠の寸法（画面比・0〜1）")]
        [Tooltip("上部メニューバーの高さ（画面高に対する比）。タイトル＋ステータス行とコマンドボタン行の2段ぶん")]
        public float titleBarFrac = 0.088f;
        [Tooltip("左右マージン（画面幅に対する比）")]
        public float sideFrac = 0.010f;
        [Tooltip("下マージン（画面高に対する比）")]
        public float bottomFrac = 0.014f;

        [Header("配色（ゲーム意匠）")]
        public Color frameColor = new Color(0.09f, 0.12f, 0.18f, 1f);
        public Color menuBarColor = new Color(0.11f, 0.15f, 0.22f, 1f);
        public Color buttonColor = new Color(0.16f, 0.21f, 0.30f, 1f);
        public Color accentColor = new Color(1f, 0.84f, 0.36f, 1f);

        private Camera cam;
        private Rect originalRect;
        private bool rectApplied;
        private TextMeshProUGUI clockLabel;  // 二重暦＋時刻＋速度
        private TextMeshProUGUI statsLabel;  // 税率/国庫/民心/安定度

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
            if (scene.name != "Strategy") return; // 戦略マップ専用
            if (UnityEngine.Object.FindAnyObjectByType<StrategyMapWindow>() != null) return;
            new GameObject("StrategyMapWindow").AddComponent<StrategyMapWindow>();
        }

        private void Awake()
        {
            cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            GalaxyView.HideWorldHud = true; // 浮いていた税率行/操作ヒントは上メニューへ集約
            ApplyViewport();
            BuildChrome();
        }

        private void OnDestroy()
        {
            if (cam != null && rectApplied) cam.rect = originalRect; // ビューポート復元
            GalaxyView.HideWorldHud = false;                         // 他シーンに持ち越さない
        }

        private void Update()
        {
            TimeDisplay.StepSpeedInput(); // 上メニューに時刻を含めたので +/- 速度入力もここで
            if (clockLabel != null && TimeDisplay.TryFormatNow(out string text, out Color color))
            {
                clockLabel.text = text;
                clockLabel.color = color;
            }
            UpdateStatsLabel();
        }

        /// <summary>プレイヤー勢力の税率/国庫/民心/安定度を上メニューに表示（読み取りのみ）。</summary>
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

        /// <summary>カメラのビューポートをメニューバー＋枠のぶん内側へ詰める。</summary>
        private void ApplyViewport()
        {
            if (cam == null) return;
            originalRect = cam.rect;
            cam.rect = new Rect(sideFrac, bottomFrac,
                1f - 2f * sideFrac, 1f - titleBarFrac - bottomFrac);
            rectApplied = true;
        }

        private void BuildChrome()
        {
            var canvasObj = new GameObject("StrategyMapWindowCanvas");
            canvasObj.transform.SetParent(transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 860; // マップの前・各パネル(880+)の後ろ
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();
            Transform root = canvasObj.transform;

            float mapTop = 1f - titleBarFrac;

            // 非描画マージンを覆う不透明な枠バー（残像防止＋窓枠の見た目）
            AddBar(root, "FrameLeft", new Vector2(0f, 0f), new Vector2(sideFrac, mapTop), frameColor);
            AddBar(root, "FrameRight", new Vector2(1f - sideFrac, 0f), new Vector2(1f, mapTop), frameColor);
            AddBar(root, "FrameBottom", new Vector2(sideFrac, 0f), new Vector2(1f - sideFrac, bottomFrac), frameColor);

            // 上部メニューバー（タイトル＋ステータス＋時計 / コマンドボタン）
            var bar = AddBar(root, "MenuBar", new Vector2(0f, mapTop), new Vector2(1f, 1f), menuBarColor);
            BuildTopRow(bar.transform);
            BuildCommandRow(bar.transform);

            // メニューバー下端の金色ルール線
            var rule = AddBar(bar.transform, "Rule", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Color(accentColor.r, accentColor.g, accentColor.b, 0.6f));
            var rrt = (RectTransform)rule.transform;
            rrt.pivot = new Vector2(0.5f, 0f);
            rrt.sizeDelta = new Vector2(0f, 2f);
        }

        /// <summary>上段：タイトル（左）／国家ステータス（中央）／時計（右）。</summary>
        private void BuildTopRow(Transform bar)
        {
            var row = new GameObject("TopRow");
            row.transform.SetParent(bar, false);
            var rrt = row.AddComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0f, 0.48f); rrt.anchorMax = new Vector2(1f, 1f);
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;

            var title = AddText(row.transform, "≡ 星系マップ ・ 戦略", 20f, accentColor, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(0.34f, 1f);
            trt.offsetMin = new Vector2(20f, 0f); trt.offsetMax = new Vector2(-8f, 0f);

            statsLabel = AddText(row.transform, "", 16f, new Color(0.85f, 0.9f, 0.7f), TextAlignmentOptions.Center);
            var srt = statsLabel.rectTransform;
            srt.anchorMin = new Vector2(0.30f, 0f); srt.anchorMax = new Vector2(0.74f, 1f);
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;

            clockLabel = AddText(row.transform, "", 16f, new Color(0.95f, 0.92f, 0.7f), TextAlignmentOptions.Right);
            var krt = clockLabel.rectTransform;
            krt.anchorMin = new Vector2(0.74f, 0f); krt.anchorMax = new Vector2(1f, 1f);
            krt.offsetMin = new Vector2(8f, 0f); krt.offsetMax = new Vector2(-20f, 0f);
        }

        /// <summary>下段：コマンドボタン列（既存パネルの Toggle を呼ぶ）。</summary>
        private void BuildCommandRow(Transform bar)
        {
            var row = new GameObject("CommandRow");
            row.transform.SetParent(bar, false);
            var rrt = row.AddComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0f, 0f); rrt.anchorMax = new Vector2(1f, 0.48f);
            rrt.offsetMin = new Vector2(16f, 4f); rrt.offsetMax = new Vector2(-16f, -2f);

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;

            // 既存パネルへ配線（並行実装を増やさない）。クリックで該当パネルの Toggle() を呼ぶ。
            AddCommand(row.transform, "勢力", () => UnityEngine.Object.FindAnyObjectByType<CampaignObserverOverlay>()?.Toggle());
            AddCommand(row.transform, "財政", () => UnityEngine.Object.FindAnyObjectByType<EconomyObserverOverlay>()?.Toggle());
            AddCommand(row.transform, "軍事", () => UnityEngine.Object.FindAnyObjectByType<MilitaryObserverOverlay>()?.Toggle());
            AddCommand(row.transform, "人事", () => UnityEngine.Object.FindAnyObjectByType<PersonObserverOverlay>()?.Toggle());
            AddCommand(row.transform, "解決", () => UnityEngine.Object.FindAnyObjectByType<DecisionBoardPanel>()?.Toggle());
            AddCommand(row.transform, "情報", () => UnityEngine.Object.FindAnyObjectByType<CoreStateInspector>()?.Toggle());
            AddCommand(row.transform, "通知", () => UnityEngine.Object.FindAnyObjectByType<NotificationLogOverlay>()?.Toggle());
            AddCommand(row.transform, "ヘルプ", () => UnityEngine.Object.FindAnyObjectByType<HelpOverlay>()?.Toggle());
        }

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
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        }

        /// <summary>アンカー(min,max)で配置する不透明バーを生成して返す。</summary>
        private static Image AddBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false; // 既定は素通し（ボタンの Image だけ raycast する）
            return img;
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
