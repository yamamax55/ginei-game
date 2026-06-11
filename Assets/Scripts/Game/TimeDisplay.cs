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
            if (UnityEngine.Object.FindAnyObjectByType<TimeDisplay>() != null) return;
            GameObject go = new GameObject("TimeDisplay");
            go.AddComponent<TimeDisplay>();
        }

        private void Awake()
        {
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
        public static void StepSpeedInput()
        {
            // イベントモーダル表示中は速度操作を受けない（誤操作防止）。
            if (StrategyEventPanel.IsOpen) return;
            Keyboard kb = Keyboard.current;
            GameClock clock = StrategySession.Clock;
            if (kb == null || clock == null) return;

            if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame)
            {
                clock.SetSpeed(NextSpeed(clock.speed, +1));
                clock.Resume();
            }
            if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame)
            {
                clock.SetSpeed(NextSpeed(clock.speed, -1));
                clock.Resume();
            }
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
