using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 戦術画面（会戦）の上端に置く、控えめな<b>停止／再開ボタン</b>。
    ///
    /// <b>時間を動かす経路は増やさない</b>のが要点＝押すと <see cref="PauseManager.TogglePause"/> を呼ぶだけで、
    /// Space と<b>同じ状態・同じ窓口</b>を通る。<c>Time.timeScale</c> をここから直接触ることはしない
    /// （再開時に直前の速度へ戻るのも <see cref="PauseManager.Resume"/> の <c>savedTimeScale</c> 任せ）。
    ///
    /// <b>約束</b>
    /// <list type="bullet">
    ///   <item>表示は毎フレーム <see cref="PauseManager.IsPaused"/> に同期する
    ///         ＝Space やシステムメニューで状態が変わってもラベルがずれない。</item>
    ///   <item>艦隊詳細・編制パネル、システムメニュー／設定が手前にある間は<b>隠れて押せない</b>
    ///         ＝その停止をボタンが上から解除しない（既存のポーズ維持仕様を守る）。</item>
    ///   <item>自前の Canvas に <see cref="GraphicRaycaster"/> を持ち、
    ///         クリックは盤面の選択・移動命令へ<b>透過しない</b>
    ///         （<c>FleetCommander</c> は <c>IsPointerOverGameObject()</c> で弾く）。</item>
    ///   <item>会戦シーンに1つだけ生成し、シーンと一緒に破棄される
    ///         ＝二重生成・リスナー重複・再入場時の残留がない。</item>
    /// </list>
    ///
    /// ウィンドウ化会戦（戦略マップに additive で載る会戦）では作らない＝
    /// そこでは時間制御を統一クロックが担い <see cref="PauseManager"/> 自身も止まっているため
    /// （<see cref="TimeDisplay"/> の浮きHUDと同じ判定）。
    /// </summary>
    public class BattlePauseButton : MonoBehaviour
    {
        // ===== 見た目の調整値（控えめ＝盤面の邪魔をしない） =====

        [Header("配置")]
        [Tooltip("ボタンの大きさ（クリック範囲が小さすぎないよう 96x28 を目安にする）")]
        public Vector2 buttonSize = new Vector2(96f, 28f);
        [Tooltip("既存の PAUSE/SPEED 表示の左端からどれだけ離すか（画面ピクセル）")]
        public float gapFromTimeLabel = 12f;
        [Tooltip("PAUSE/SPEED 表示が見つからないときの位置（上端中央から左・下へのオフセット）")]
        public Vector2 fallbackOffset = new Vector2(-120f, -30f);

        [Header("配色")]
        [Tooltip("通常時の背景（濃い半透明）")]
        public Color normalColor = new Color(0.08f, 0.09f, 0.12f, 0.62f);
        [Tooltip("ホバー時の背景（少しだけ明るく）")]
        public Color hoverColor = new Color(0.18f, 0.20f, 0.26f, 0.85f);
        [Tooltip("押下時の背景")]
        public Color pressedColor = new Color(0.26f, 0.29f, 0.36f, 0.92f);
        [Tooltip("文字色（落ち着いたトーン。小さくても読める明度は残す）")]
        public Color textColor = new Color(0.82f, 0.84f, 0.88f, 0.95f);
        [Tooltip("文字の大きさ")]
        public float fontSize = 15f;

        /// <summary>実行中に出す文字。</summary>
        public const string LabelPause = "停止";
        /// <summary>一時停止中に出す文字。</summary>
        public const string LabelResume = "再開";

        /// <summary>この HUD の描画順。時刻HUD(900)より手前・ポーズメニュー(1000)より後ろ。</summary>
        private const int SortingOrder = 950;

        private Button button;
        private TextMeshProUGUI label;
        private GameObject root;
        private RectTransform buttonRT;
        private Canvas canvas;
        private PauseManager pause;
        private bool lastPaused;
        private bool lastShown;
        private Rect lastLabelRect;
        private Vector2 lastScreen;

        /// <summary>この会戦シーンのボタン（診断と盤面のUI判定が引く）。</summary>
        private static BattlePauseButton instance;

        /// <summary>通常経路へ与える猶予フレーム数（Update の実行順は保証されないため）。</summary>
        private const int FallbackGraceFrames = 2;

        private int eventSystemClickFrame = -10;
        private int pendingFallbackFrame = -1;
        private int pressedInsideFrame = -1;
        private int fallbackClickCount;

        // ===== 生成（会戦シーンに1つだけ） =====

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;   // 二重購読防止
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Battle") return;
            // ウィンドウ会戦（アクティブシーンにならない additive ロード）では作らない。
            // 全画面 Overlay なので窓の外へ漏れるうえ、そこでは PauseManager 自体が止まっている。
            if (scene != SceneManager.GetActiveScene()) return;
            if (FindAnyObjectByType<BattlePauseButton>() != null) return;   // ★二重生成しない
            new GameObject("BattlePauseButton").AddComponent<BattlePauseButton>();
        }

        private void Awake()
        {
            // TryCreate と同じ判定の二重防御（別経路で紛れ込んだ個体は何も作らない）。
            if (gameObject.scene.IsValid() && gameObject.scene != SceneManager.GetActiveScene())
            {
                enabled = false;
                return;
            }
        }

        private void Start()
        {
            pause = FindAnyObjectByType<PauseManager>();
            if (pause == null || !pause.isActiveAndEnabled)
            {
                // 時間制御の持ち主が居ない／止まっている会戦ではボタンを出さない
                // （押しても何も起きないボタンを置かない）。
                enabled = false;
                return;
            }
            BuildUI();
        }

        private void Update()
        {
            if (button == null || pause == null) return;

            // ★表示するか：手前にモーダルが無いときだけ。
            //   居るあいだは隠す＝ボタンからその停止を解除できない（既存のポーズ維持仕様）。
            bool shown = !PauseManager.IsTimeInputDeferred && !pause.IsSystemUiShown;
            if (shown != lastShown)
            {
                lastShown = shown;
                if (root != null) root.SetActive(shown);
            }
            if (!shown) return;

            LayoutBesideTimeLabel();
            PollDirectClickFallback();

            // ★状態は PauseManager が唯一の出所。Space でも右クリックメニューでも同じ値を見る。
            bool paused = pause.IsPaused;
            if (paused != lastPaused || label.text.Length == 0)
            {
                lastPaused = paused;
                label.text = paused ? LabelResume : LabelPause;
            }
        }

        // ===== 直接判定のフォールバック（通常経路が届かなかったときだけ） =====

        /// <summary>
        /// <b>本筋は EventSystem → <see cref="Button.onClick"/></b>。ここはそれが届かなかったときの保険。
        ///
        /// 実機で「ホバーの変色は出るのに <c>onClick</c> が来ない」報告があったため、
        /// EventSystem に依存しない経路を1本だけ足す。<see cref="FleetCommander"/> が
        /// 盤面のクリックを <c>Mouse.current</c> で直接読んでいるのと同じ作法。
        ///
        /// <b>二重に効かせない</b>ため、離した瞬間ではなく<b>数フレーム待って</b>、
        /// その間に通常経路が来ていなければ初めて実行する（どちらの経路で効いたかは
        /// <see cref="LastClickPath"/> に残して診断できるようにする）。
        /// </summary>
        private void PollDirectClickFallback()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // 押して離したのがボタンの内側なら、保険の判定を予約する。
            if (mouse.leftButton.wasPressedThisFrame && PointerInsideButton(mouse.position.ReadValue()))
                pressedInsideFrame = Time.frameCount;

            if (mouse.leftButton.wasReleasedThisFrame
                && pressedInsideFrame >= 0
                && PointerInsideButton(mouse.position.ReadValue()))
            {
                pendingFallbackFrame = Time.frameCount;
                pressedInsideFrame = -1;
            }

            if (pendingFallbackFrame < 0) return;

            // 通常経路（EventSystem）へ猶予を与える。Update の実行順は保証されないため2フレーム待つ。
            if (Time.frameCount <= pendingFallbackFrame + FallbackGraceFrames) return;

            int pending = pendingFallbackFrame;
            pendingFallbackFrame = -1;
            if (eventSystemClickFrame >= pending) return;   // 通常経路が処理済み＝何もしない

            fallbackClickCount++;
            LastClickPath = "直接判定（EventSystem からクリックが来なかった）";
            Toggle();
        }

        /// <summary>画面座標がボタンの内側か（Overlay Canvas なのでカメラは null）。</summary>
        private bool PointerInsideButton(Vector2 screenPos)
        {
            if (buttonRT == null || root == null || !root.activeInHierarchy) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(buttonRT, screenPos, null);
        }

        /// <summary>
        /// いまポインタが停止ボタンの上にあるか（<see cref="FleetCommander"/> のUI判定が参照する）。
        /// EventSystem が不調でも<b>盤面へクリックが透過しない</b>ようにするための保険。
        /// </summary>
        public static bool IsPointerOverButton()
        {
            if (instance == null) return false;
            Mouse mouse = Mouse.current;
            if (mouse == null) return false;
            return instance.PointerInsideButton(mouse.position.ReadValue());
        }

        // ===== 診断用（読み取り専用・状態は変えない） =====

        /// <summary>直近のクリックがどの経路で届いたか（入力診断メニューが読む）。</summary>
        public static string LastClickPath { get; private set; } = "（まだ押されていません）";
        /// <summary>通常経路（EventSystem → onClick）で届いた回数。</summary>
        public static int EventSystemClickCount { get; private set; }
        /// <summary>保険の直接判定で届いた回数。</summary>
        public static int FallbackClickCount => instance != null ? instance.fallbackClickCount : 0;
        /// <summary>この会戦シーンのボタン（無ければ null）。</summary>
        public static BattlePauseButton Instance => instance;
        /// <summary>いまボタンが表示されているか（モーダル中は隠れる）。</summary>
        public bool IsShown => root != null && root.activeInHierarchy;
        /// <summary>ボタンが押せる状態か。</summary>
        public bool IsInteractable => button != null && button.IsInteractable();

        /// <summary>
        /// 既存の PAUSE/SPEED 表示の<b>実際の画面矩形を測って</b>、そのすぐ左へ置く。
        ///
        /// 決め打ちのオフセットにしないのは、あの表示が <c>Battle.unity</c> の Canvas（参照解像度 800x600）
        /// に載っていて、この HUD の Canvas（1920x1080）とは<b>拡大率が違う</b>ため
        /// ＝画面上では約2.4倍の大きさで出る。座標を直接そろえると隠れてしまう。
        /// 実測なら解像度・スケーラ設定が変わっても隠れない。
        /// </summary>
        private void LayoutBesideTimeLabel()
        {
            if (buttonRT == null || canvas == null) return;

            var screen = new Vector2(Screen.width, Screen.height);
            TextMeshProUGUI timeLabel = pause != null ? pause.timeScaleText : null;
            if (timeLabel == null || !timeLabel.isActiveAndEnabled)
            {
                if (lastScreen != screen) { lastScreen = screen; buttonRT.anchoredPosition = fallbackOffset; }
                return;
            }

            // ScreenSpaceOverlay の Canvas ではワールド座標＝画面ピクセル。
            timeLabel.rectTransform.GetWorldCorners(corners);
            float minX = corners[0].x, maxX = corners[0].x;
            float minY = corners[0].y, maxY = corners[0].y;
            for (int i = 1; i < 4; i++)
            {
                minX = Mathf.Min(minX, corners[i].x); maxX = Mathf.Max(maxX, corners[i].x);
                minY = Mathf.Min(minY, corners[i].y); maxY = Mathf.Max(maxY, corners[i].y);
            }
            var labelRect = new Rect(minX, minY, maxX - minX, maxY - minY);

            // 変化していなければ触らない（毎フレーム RectTransform を書き換えない）。
            if (labelRect == lastLabelRect && lastScreen == screen) return;
            lastLabelRect = labelRect;
            lastScreen = screen;

            float sf = canvas.scaleFactor > 0.001f ? canvas.scaleFactor : 1f;
            // pivot＝右中央なので、ラベル左端から gap ぶん左が pivot の画面座標になる。
            float pivotScreenX = labelRect.xMin - gapFromTimeLabel;
            float pivotScreenY = labelRect.center.y;

            // アンカー（上端中央）＝画面 (幅/2, 高さ) からの差を Canvas 単位へ直す。
            var pos = new Vector2((pivotScreenX - screen.x * 0.5f) / sf,
                                  (pivotScreenY - screen.y) / sf);

            // 画面外へ出さない（左端に寄りすぎたら中へ戻す）。
            float halfWidth = screen.x * 0.5f / sf;
            pos.x = Mathf.Max(pos.x, -halfWidth + buttonSize.x + 8f);
            buttonRT.anchoredPosition = pos;
        }

        /// <summary>ワールド4隅の受け皿（毎フレームの確保を避ける）。</summary>
        private readonly Vector3[] corners = new Vector3[4];

        /// <summary>通常経路（EventSystem → <see cref="Button.onClick"/>）で押されたとき。</summary>
        private void OnClicked()
        {
            eventSystemClickFrame = Time.frameCount;
            pendingFallbackFrame = -1;              // 保険は要らない
            EventSystemClickCount++;
            LastClickPath = "EventSystem（通常経路）";
            Toggle();
        }

        /// <summary>
        /// 実際に切り替える。どちらの経路から来ても<b>ここ1か所</b>だけを通る
        /// ＝Space と同じ <see cref="PauseManager.TogglePause"/> しか呼ばない。
        /// </summary>
        private void Toggle()
        {
            if (pause == null) return;
            // 表示条件と同じ門番をもう一度通す（クリックとフレームの隙間で状態が変わっても解除しない）。
            if (PauseManager.IsTimeInputDeferred || pause.IsSystemUiShown)
            {
                LastClickPath += "／手前にモーダルがあるため見送り";
                return;
            }
            pause.TogglePause();
        }

        // ===== UI 生成 =====

        private void BuildUI()
        {
            // クリックを拾うために EventSystem を保証する（PauseManager の窓口を再利用＝二重実装しない）。
            PauseManager.EnsureEventSystem();

            var canvasGo = new GameObject("PauseButtonCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // ★これがあるからクリックが盤面へ透過しない（FleetCommander の IsPointerOverGameObject が true になる）。
            canvasGo.AddComponent<GraphicRaycaster>();

            root = new GameObject("PauseButton");
            root.transform.SetParent(canvasGo.transform, false);
            buttonRT = root.AddComponent<RectTransform>();
            // 画面上端中央を基準に、既存の PAUSE/SPEED 表示の<b>すぐ左</b>へ置く（位置は毎フレーム実測）。
            // 上端中央は縦に混んでいる（戦況バー・PAUSE表示・戦況メッセージ）ので、
            // 下へ逃がさず横へ避ける。pivot を右中央にして「ラベルの左端から左へ」置けるようにする。
            buttonRT.anchorMin = new Vector2(0.5f, 1f);
            buttonRT.anchorMax = new Vector2(0.5f, 1f);
            buttonRT.pivot = new Vector2(1f, 0.5f);
            buttonRT.anchoredPosition = fallbackOffset;
            buttonRT.sizeDelta = buttonSize;

            Image bg = root.AddComponent<Image>();
            bg.color = normalColor;
            bg.raycastTarget = true;      // ★クリックをここで受け止める

            button = root.AddComponent<Button>();
            button.targetGraphic = bg;
            // ★Ginei.Selectable（艦隊の選択コンポーネント）と名前がぶつかるので完全修飾する。
            button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;                 // 乗算なので白＝bg.color そのまま
            colors.highlightedColor = Divide(hoverColor, normalColor);
            colors.pressedColor = Divide(pressedColor, normalColor);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            // ★リスナーはここで一度だけ張る（生成が1回きりなので重複しない）。
            button.onClick.AddListener(OnClicked);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(root.transform, false);
            RectTransform lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.sizeDelta = Vector2.zero;
            lrt.anchoredPosition = Vector2.zero;

            label = labelGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = textColor;
            label.raycastTarget = false;   // 文字はクリック判定を持たない（背景が受ける）
            TMP_FontAsset ja = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (ja != null) label.font = ja;

            lastPaused = pause.IsPaused;
            label.text = lastPaused ? LabelResume : LabelPause;
            lastShown = true;
            instance = this;
        }

        /// <summary>
        /// ColorTint は <c>targetGraphic</c> の色に<b>乗算</b>される。
        /// 「この見た目にしたい色」から必要な倍率を逆算する（0 除算は 1 に寄せる）。
        /// </summary>
        private static Color Divide(Color want, Color baseColor)
        {
            return new Color(
                baseColor.r > 0.001f ? Mathf.Clamp(want.r / baseColor.r, 0f, 4f) : 1f,
                baseColor.g > 0.001f ? Mathf.Clamp(want.g / baseColor.g, 0f, 4f) : 1f,
                baseColor.b > 0.001f ? Mathf.Clamp(want.b / baseColor.b, 0f, 4f) : 1f,
                baseColor.a > 0.001f ? Mathf.Clamp(want.a / baseColor.a, 0f, 4f) : 1f);
        }

        private void OnDestroy()
        {
            // ★シーンを抜けるときにリスナーを外す（残留させない）。
            if (button != null) button.onClick.RemoveListener(OnClicked);
            if (instance == this) instance = null;
        }
    }
}
