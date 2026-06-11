using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 戦略（星系）マップを Windows 風のウィンドウに見せるクローム（#UI統一）。
    /// カメラのビューポート <see cref="Camera.rect"/> を上のタイトルバー＋細い枠のぶん内側へ詰め、
    /// その周囲に不透明な枠バー＋タイトルバーをスクリーン空間で描く。マップ本体（ワールド描画）には触れない。
    /// <b>当たり判定は維持される</b>：<see cref="GalaxyView"/> は <see cref="Camera.ScreenToWorldPoint"/> で
    /// クリックを拾い、これはビューポート rect を尊重するため、ビューポートを詰めても選択/進軍は正しく動く。
    /// 非描画マージンは不透明な枠バーで覆うため残像（clear されない領域）も出ない。Strategy シーンのみ自動生成。
    /// </summary>
    public class StrategyMapWindow : MonoBehaviour
    {
        [Header("枠の寸法（画面比・0〜1）")]
        [Tooltip("タイトルバーの高さ（画面高に対する比）。時計2段を収めるため少し高め")]
        public float titleBarFrac = 0.058f;
        [Tooltip("左右マージン（画面幅に対する比）")]
        public float sideFrac = 0.010f;
        [Tooltip("下マージン（画面高に対する比）")]
        public float bottomFrac = 0.014f;

        [Header("配色（ゲーム意匠）")]
        [Tooltip("枠バーの色（非描画マージンを覆う）")]
        public Color frameColor = new Color(0.09f, 0.12f, 0.18f, 1f);
        [Tooltip("タイトルバーの色")]
        public Color titleBarColor = new Color(0.11f, 0.15f, 0.22f, 1f);
        [Tooltip("金色アクセント（タイトル・ルール線）")]
        public Color accentColor = new Color(1f, 0.84f, 0.36f, 1f);

        private Camera cam;
        private Rect originalRect;
        private bool rectApplied;
        private TextMeshProUGUI clockLabel; // 上メニューに含める時刻/暦/速度

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
            ApplyViewport();
            BuildChrome();
        }

        private void OnDestroy()
        {
            // ビューポートを元へ戻す（カメラが他シーンへ持ち越される場合の保険）
            if (cam != null && rectApplied) cam.rect = originalRect;
        }

        private void Update()
        {
            // 上メニューに時刻を含めたので、速度の +/- もここで受ける（戦略では浮きHUDを出さないため）。
            TimeDisplay.StepSpeedInput();
            if (clockLabel != null && TimeDisplay.TryFormatNow(out string text, out Color color))
            {
                clockLabel.text = text;
                clockLabel.color = color;
            }
        }

        /// <summary>カメラのビューポートをタイトルバー＋枠のぶん内側へ詰める。</summary>
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

            float mapTop = 1f - titleBarFrac; // マップ領域の上端（タイトルバー下端）

            // --- 非描画マージンを覆う不透明な枠バー（残像防止＋窓枠の見た目）---
            AddBar(root, "FrameLeft", new Vector2(0f, 0f), new Vector2(sideFrac, mapTop), frameColor);
            AddBar(root, "FrameRight", new Vector2(1f - sideFrac, 0f), new Vector2(1f, mapTop), frameColor);
            AddBar(root, "FrameBottom", new Vector2(sideFrac, 0f), new Vector2(1f - sideFrac, bottomFrac), frameColor);

            // --- タイトルバー（上端・全幅）---
            var bar = AddBar(root, "TitleBar", new Vector2(0f, mapTop), new Vector2(1f, 1f), titleBarColor);

            var caption = new GameObject("Caption").AddComponent<TextMeshProUGUI>();
            caption.transform.SetParent(bar.transform, false);
            var crt = caption.rectTransform;
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(20f, 0f); crt.offsetMax = new Vector2(-380f, 0f); // 右は時計用に空ける
            caption.text = "≡ 星系マップ ・ 戦略";
            caption.fontSize = 22f;
            caption.fontStyle = FontStyles.Bold;
            caption.color = accentColor;
            caption.alignment = TextAlignmentOptions.Left;
            caption.raycastTarget = false;
            ApplyJapaneseFont(caption);

            // 時刻/二重暦/速度（上メニューに含める＝右寄せ・2段）。整形は TimeDisplay の static を再利用。
            clockLabel = new GameObject("Clock").AddComponent<TextMeshProUGUI>();
            clockLabel.transform.SetParent(bar.transform, false);
            var krt = clockLabel.rectTransform;
            krt.anchorMin = new Vector2(1f, 0f); krt.anchorMax = new Vector2(1f, 1f);
            krt.pivot = new Vector2(1f, 0.5f);
            krt.sizeDelta = new Vector2(360f, 0f);
            krt.anchoredPosition = new Vector2(-20f, 0f);
            clockLabel.fontSize = 17f;
            clockLabel.alignment = TextAlignmentOptions.Right;
            clockLabel.color = new Color(0.95f, 0.92f, 0.7f);
            clockLabel.raycastTarget = false;
            ApplyJapaneseFont(clockLabel);

            // タイトルバー下端の金色ルール線
            var rule = AddBar(bar.transform, "Rule", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Color(accentColor.r, accentColor.g, accentColor.b, 0.6f));
            var rrt = (RectTransform)rule.transform;
            rrt.pivot = new Vector2(0.5f, 0f);
            rrt.sizeDelta = new Vector2(0f, 2f);
        }

        /// <summary>アンカー(min,max)で配置する不透明バーを生成して返す。</summary>
        private static Image AddBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false; // マップのクリックを妨げない
            return img;
        }

        private static void ApplyJapaneseFont(TextMeshProUGUI tmp)
        {
            TMP_FontAsset ja = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (ja != null) tmp.font = ja;
        }
    }
}
