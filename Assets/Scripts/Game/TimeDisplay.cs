using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 統一ゲーム時間の表示HUD（TIME-3 #949）。<see cref="StrategySession.Clock"/> の累積秒を <see cref="GameDate"/> で
    /// 宇宙暦/帝国暦へ写し、速度/ポーズと共に画面上端中央に表示する。戦略/会戦の両シーンへ自動生成し、
    /// 全シーンで一貫した日付バーを出す（Paradox の日付バー風）。`HelpOverlay` と同じ自動生成パターン。
    /// </summary>
    public class TimeDisplay : MonoBehaviour
    {
        /// <summary>開始暦（宇宙暦SE）。銀英伝風に 796 を既定とする。</summary>
        public const int StartYear = 796;

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
            // 帝国暦オフセット＝宇宙暦−309（SE796 ⇒ IC487。銀英伝の対応に倣う）。1日=60秒の既定暦。
            dateParams = new GameDate.DateParams(60d, 30, 12, 309);
            BuildUI();
        }

        private void Update()
        {
            if (label == null) return;
            GameClock clock = StrategySession.Clock;
            if (clock == null) { label.text = ""; return; }

            GameDate date = GameDate.FromSeconds(clock.ElapsedSeconds, StartYear, dateParams);
            string speed = clock.paused ? "⏸ 停止" : $"× {clock.speed:0.#}";
            label.text = $"{date.ToDualString(dateParams)}　｜　{speed}";
            label.color = clock.paused ? new Color(0.8f, 0.8f, 0.85f) : new Color(0.95f, 0.92f, 0.7f);
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
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -8f);
            rt.sizeDelta = new Vector2(720f, 40f);

            label = txt.AddComponent<TextMeshProUGUI>();
            label.fontSize = 24f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            TMP_FontAsset ja = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (ja != null) label.font = ja;
        }
    }
}
