using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 統一ゲーム時間の表示HUD（TIME-3 #949）。<see cref="StrategySession.Clock"/> の累積秒を <see cref="GameDate"/> で
    /// 宇宙暦/帝国暦＋時刻(HH:MM)へ写し、速度/ポーズと共に**画面右上**に表示する。戦略/会戦の両シーンへ自動生成。
    /// <b>+/-（=/-キー）で時間速度を変更</b>できる（全シーン共通＝クロックの速度を駆動）。
    /// </summary>
    public class TimeDisplay : MonoBehaviour
    {
        /// <summary>開始暦（宇宙暦SE）。銀英伝風に 796 を既定とする。</summary>
        public const int StartYear = 796;
        /// <summary>速度の段階（+/- で行き来する）。</summary>
        private static readonly float[] SpeedSteps = { 0.5f, 1f, 2f, 3f, 5f };

        /// <summary>暦の既定パラメータ（1日=60秒・帝国暦オフセット309）。表示整形の単一ソース。</summary>
        public static GameDate.DateParams DateParams => new GameDate.DateParams(60d, 30, 12, 309);

        private TextMeshProUGUI label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; // 二重購読防止
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        /// <summary>
        /// 会戦シーンに TimeDisplay が無ければ生成する（重複生成ガード）。
        /// <b>戦略では生成しない</b>＝時刻は <see cref="StrategyMapWindow"/> の上メニュー（タイトルバー）に表示する
        /// （整形/速度入力は下記 static を再利用＝二重実装しない）。
        /// </summary>
        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Battle") return;
            // ウィンドウ会戦（戦略マップに additive で載る会戦＝アクティブシーンにならない）では作らない。
            // この HUD は全画面 Overlay なので、窓の枠外へ日付/SPEED が漏れて戦略 HUD と重なっていた（実機報告）。
            // 窓モードの時刻表示は戦略マップ上メニュー（StrategyMapWindow）が既に担っている＝二重表示でもある。
            if (scene != SceneManager.GetActiveScene()) return;
            if (UnityEngine.Object.FindAnyObjectByType<TimeDisplay>() != null) return;
            GameObject go = new GameObject("TimeDisplay");
            go.AddComponent<TimeDisplay>();
        }

        private void Awake()
        {
            // ウィンドウ化会戦（additive ロード＝アクティブシーンにならない）に紛れ込んだ個体は浮きHUDを作らない。
            // この HUD は全画面 Overlay なので、作ると日付/SPEED が窓の枠外へ出て背後の戦略HUDと重なる（実機報告）。
            // 窓モードの時刻は戦略の上メニュー（StrategyMapWindow）と会戦ウィンドウのタイトルバーが担う
            // （どちらも TryFormatNow の単一窓口を使う＝二重実装しない）。TryCreate と同じ判定の二重防御。
            if (gameObject.scene.IsValid() && gameObject.scene != SceneManager.GetActiveScene())
            {
                enabled = false;
                return;
            }
            BuildUI();
        }

        private void Update()
        {
            StepSpeedInput();
            if (label == null) return;
            if (TryFormatNow(out string text, out Color color)) { label.text = text; label.color = color; }
            else label.text = "";
        }

        /// <summary>
        /// 統一クロックを表示文字列（日付2段＋時刻＋速度）と色へ整形する単一窓口。
        /// 戦略の上メニュー（<see cref="StrategyMapWindow"/>）も会戦の右上HUDもこれを使う。
        /// </summary>
        public static bool TryFormatNow(out string text, out Color color)
        {
            text = ""; color = Color.white;
            GameClock clock = StrategySession.Clock;
            if (clock == null) return false;
            GameDate.DateParams dp = DateParams;
            GameDate date = GameDate.FromSeconds(clock.ElapsedSeconds, StartYear, dp);
            string time = GameDate.TimeString(clock.ElapsedSeconds, dp.secondsPerDay);
            string speed = clock.paused ? "■ 停止" : $"× {clock.speed:0.#}";
            text = $"{date.ToDualString(dp)}\n{time}　{speed}";
            color = clock.paused ? new Color(0.8f, 0.8f, 0.85f) : new Color(0.95f, 0.92f, 0.7f);
            return true;
        }

        /// <summary>+/-（=/-キー）で時間速度を段階変更する（全シーン共通・クロックを駆動）。</summary>
        // このフレームで既に速度入力を処理したか（複数のコンポーネントが同じキーを二重に消費しないため）。
        private static int lastSpeedInputFrame = -1;

        public static void StepSpeedInput()
        {
            // 同一フレームで2回目以降は無視（戦略は StrategyMapWindow、会戦は TimeDisplay が呼ぶ＝将来の重複も防ぐ）。
            if (lastSpeedInputFrame == Time.frameCount) return;
            lastSpeedInputFrame = Time.frameCount;

            // イベントモーダル表示中は速度操作を受けない（誤操作防止）。
            if (StrategyEventPanel.IsOpen) return;
            // 文字入力中（名前入力欄など）はゲームの速度キーとして解釈しない。
            if (IsTextInputFocused()) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // 日本語配列では「＝」が Shift+「ー」だったり、右手前の記号キーの並びが英語配列と違う。
            // 取りこぼしを減らすため、増速/減速それぞれに複数のキーを割り当てる（テンキーも含む）。
            bool up = kb.equalsKey.wasPressedThisFrame
                   || kb.numpadPlusKey.wasPressedThisFrame
                   || kb.semicolonKey.wasPressedThisFrame;       // JIS配列で「＋」が乗るキー
            bool down = kb.minusKey.wasPressedThisFrame
                     || kb.numpadMinusKey.wasPressedThisFrame;

            if (up) SpeedUp();
            if (down) SlowDown();
        }

        /// <summary>入力欄（TMP/uGUI の InputField）にフォーカスがあるか。</summary>
        private static bool IsTextInputFocused()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            GameObject sel = es != null ? es.currentSelectedGameObject : null;
            if (sel == null) return false;
            return sel.GetComponent<TMP_InputField>() != null
                || sel.GetComponent<UnityEngine.UI.InputField>() != null;
        }

        // ===== 速度操作の単一窓口（キーも画面上のボタンもここを呼ぶ＝挙動が分岐しない） =====

        /// <summary>
        /// 速度操作を受け付けてよい状態か。システムメニュー/イベントモーダルが開いている間は
        /// <b>クロックに触らない</b>＝速度変更で意図せずメニューの停止を解除しない（実機報告）。
        /// </summary>
        private static bool SpeedControlAllowed()
            => !StrategySystemMenu.IsOpen && !StrategyEventPanel.IsOpen && !CampaignEndOverlay.IsOpen;

        /// <summary>1段速くする（停止中なら再開する）。メニュー表示中は何もしない。</summary>
        public static void SpeedUp()
        {
            if (!SpeedControlAllowed()) return;
            GameClock clock = StrategySession.Clock;
            if (clock == null) return;
            clock.SetSpeed(NextSpeed(clock.speed, +1));
            clock.Resume();
        }

        /// <summary>1段遅くする（停止中なら再開する）。メニュー表示中は何もしない。</summary>
        public static void SlowDown()
        {
            if (!SpeedControlAllowed()) return;
            GameClock clock = StrategySession.Clock;
            if (clock == null) return;
            clock.SetSpeed(NextSpeed(clock.speed, -1));
            clock.Resume();
        }

        /// <summary>
        /// 停止と再開を切り替える。<b>ポーズメニュー（システムメニュー）が開いている間は触らない</b>
        /// ＝速度操作でメニューのポーズを意図せず解除しない（実機報告）。
        /// </summary>
        public static void TogglePause()
        {
            if (!SpeedControlAllowed()) return;
            GameClock clock = StrategySession.Clock;
            if (clock == null) return;
            clock.TogglePause();
        }

        /// <summary>現在速度に最も近い段階から <paramref name="dir"/> 方向へ1段移動した速度を返す。</summary>
        private static float NextSpeed(float current, int dir)
        {
            int idx = 0;
            float best = float.MaxValue;
            for (int i = 0; i < SpeedSteps.Length; i++)
            {
                float d = Mathf.Abs(SpeedSteps[i] - current);
                if (d < best) { best = d; idx = i; }
            }
            idx = Mathf.Clamp(idx + dir, 0, SpeedSteps.Length - 1);
            return SpeedSteps[idx];
        }

        private void BuildUI()
        {
            GameObject canvasObj = new GameObject("TimeDisplayCanvas");
            canvasObj.transform.SetParent(transform);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900; // ゲームUIより手前・モーダルより後ろ
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject txt = new GameObject("DateLabel");
            txt.transform.SetParent(canvasObj.transform, false);
            RectTransform rt = txt.AddComponent<RectTransform>();
            // 画面右上
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-16f, -10f);
            rt.sizeDelta = new Vector2(420f, 64f);

            label = txt.AddComponent<TextMeshProUGUI>();
            label.fontSize = 22f;
            label.alignment = TextAlignmentOptions.TopRight;
            label.raycastTarget = false;
            TMP_FontAsset ja = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (ja != null) label.font = ja;
        }
    }
}
