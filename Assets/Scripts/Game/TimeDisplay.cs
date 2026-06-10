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

        private TextMeshProUGUI label;
        private GameDate.DateParams dateParams;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; // 二重購読防止
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        /// <summary>戦略/会戦シーンに TimeDisplay が無ければ生成する（重複生成ガード）。</summary>
        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Strategy" && scene.name != "Battle") return;
            if (UnityEngine.Object.FindAnyObjectByType<TimeDisplay>() != null) return;
            GameObject go = new GameObject("TimeDisplay");
            go.AddComponent<TimeDisplay>();
        }

        private void Awake()
        {
            // 帝国暦オフセット＝宇宙暦−309（SE796 ⇒ IC487）。1日=60秒の既定暦。
            dateParams = new GameDate.DateParams(60d, 30, 12, 309);
            BuildUI();
        }

        private void Update()
        {
            HandleSpeedInput();
            if (label == null) return;
            GameClock clock = StrategySession.Clock;
            if (clock == null) { label.text = ""; return; }

            GameDate date = GameDate.FromSeconds(clock.ElapsedSeconds, StartYear, dateParams);
            string time = GameDate.TimeString(clock.ElapsedSeconds, dateParams.secondsPerDay);
            string speed = clock.paused ? "⏸ 停止" : $"× {clock.speed:0.#}";
            // 右上：日付（宇宙暦/帝国暦）＋時刻＋速度（右寄せ・改行で2段）
            label.text = $"{date.ToDualString(dateParams)}\n{time}　{speed}";
            label.color = clock.paused ? new Color(0.8f, 0.8f, 0.85f) : new Color(0.95f, 0.92f, 0.7f);
        }

        /// <summary>+/-（=/-キー）で時間速度を段階変更する（全シーン共通・クロックを駆動）。</summary>
        private void HandleSpeedInput()
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
