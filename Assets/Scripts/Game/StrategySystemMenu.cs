using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 戦略マップのシステムメニュー（#ウィンドウESC）。重ねたウィンドウを ESC で全部閉じたあと、
    /// もう一度 ESC を押すと開くフォールバックメニュー＝<b>再開／セーブ／タイトルへ戻る</b>。
    /// 会戦の <see cref="PauseManager"/> のポーズメニューに相当する戦略版。表示中は統一クロックを
    /// ポーズし（<see cref="GameClock.Pause"/>）、<see cref="GalaxyView"/> は <see cref="IsOpen"/> の間
    /// 盤面入力を譲る。ESC 制御は <see cref="GalaxyView"/> が <see cref="UIWindowStack"/> 経由で行うため
    /// 自前でキー直読みしない。Strategy シーンに自動生成（<see cref="HelpOverlay"/> と同方式）。
    /// </summary>
    public class StrategySystemMenu : MonoBehaviour
    {
        private static StrategySystemMenu instance;

        /// <summary>メニューが開いているか（GalaxyView が入力を譲るために参照）。</summary>
        public static bool IsOpen => instance != null && instance.isOpen;

        private bool isOpen;
        private bool pausedClock;
        private GameObject root;

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
            if (Object.FindAnyObjectByType<StrategySystemMenu>() != null) return;
            new GameObject("StrategySystemMenu").AddComponent<StrategySystemMenu>();
        }

        private void Awake()
        {
            instance = this;
            BuildUI();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ===== 公開API（GalaxyView の ESC フォールバックが呼ぶ） =====

        /// <summary>メニューを開閉する。</summary>
        public void Toggle()
        {
            if (isOpen) Close();
            else Open();
        }

        /// <summary>開いて統一クロックをポーズする。</summary>
        public void Open()
        {
            if (root == null) return;
            isOpen = true;
            GameClock clock = StrategySession.Clock;
            if (clock != null && !clock.paused) { clock.Pause(); pausedClock = true; }
            root.SetActive(true);
        }

        /// <summary>閉じてポーズを解除する（再開）。</summary>
        public void Close()
        {
            isOpen = false;
            GameClock clock = StrategySession.Clock;
            if (clock != null && pausedClock) { clock.Resume(); pausedClock = false; }
            if (root != null) root.SetActive(false);
        }

        private void SetVisible(bool v) { if (root != null) root.SetActive(v); }

        // ===== ボタン挙動 =====

        private void OnSave()
        {
            // F5 と同じ全永続化（GalaxyView が世界状態を保存）。メニューは開いたまま。
            GalaxyView gv = Object.FindAnyObjectByType<GalaxyView>();
            if (gv != null) gv.SaveCampaign();
        }

        private void OnBackToTitle()
        {
            // 戦役を畳んでタイトルへ（次の戦役を新規に始められるよう状態をクリア＝CampaignEndOverlay と同手順）。
            isOpen = false;
            StrategySession.Clear();
            BattleHandoff.Clear();
            GalaxyView.ResetCampaignStatics();
            SceneManager.LoadScene("Title");
        }

        // ===== UI 構築（PauseManager / CampaignEndOverlay と同作法・コード生成） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            GameObject canvasObj = new GameObject("StrategySystemMenuCanvas");
            canvasObj.transform.SetParent(transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // 観測窓(1090台)とは別流れ。フォールバック時は単独で開くため十分手前
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();

            // 全画面の暗いディマー（モーダル＝背後を塞ぐ）
            root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(canvasObj.transform, false);
            StretchFull(root.GetComponent<RectTransform>());
            Image dim = root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.7f);

            // 中央の縦並びパネル
            GameObject panel = new GameObject("MenuPanel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            RectTransform pRT = panel.GetComponent<RectTransform>();
            pRT.anchorMin = new Vector2(0.5f, 0.5f);
            pRT.anchorMax = new Vector2(0.5f, 0.5f);
            pRT.pivot = new Vector2(0.5f, 0.5f);
            pRT.anchoredPosition = Vector2.zero;
            pRT.sizeDelta = new Vector2(360f, 360f);
            Image pbg = panel.AddComponent<Image>();
            pbg.color = new Color(0.06f, 0.08f, 0.13f, 0.97f);
            VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 18f;
            vlg.padding = new RectOffset(24, 24, 24, 24);
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            CreateLabel(panel.transform, "メニュー", 38f);
            CreateButton(panel.transform, "再開", Close, new Color(0.2f, 0.28f, 0.4f, 1f));
            CreateButton(panel.transform, "セーブ", OnSave, new Color(0.2f, 0.32f, 0.26f, 1f));
            CreateButton(panel.transform, "タイトルへ戻る", OnBackToTitle, new Color(0.36f, 0.18f, 0.2f, 1f));
        }

        private void CreateLabel(Transform parent, string text, float fontSize)
        {
            GameObject obj = new GameObject("Label", typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = new Color(1f, 0.84f, 0.36f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            ApplyJapaneseFont(tmp);
            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.minHeight = 56f; le.preferredHeight = 56f;
        }

        private void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action, Color color)
        {
            GameObject btnObj = new GameObject("Button_" + label, typeof(RectTransform));
            btnObj.transform.SetParent(parent, false);
            Image img = btnObj.AddComponent<Image>();
            img.color = color;
            Button btn = btnObj.AddComponent<Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.None; // Ginei.Selectable と衝突回避
            btn.targetGraphic = img;
            if (AudioManager.Instance != null) btn.onClick.AddListener(() => AudioManager.Instance.PlayUIClick());
            btn.onClick.AddListener(action);
            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.minHeight = 54f; le.preferredHeight = 54f;

            GameObject txtObj = new GameObject("Text", typeof(RectTransform));
            txtObj.transform.SetParent(btnObj.transform, false);
            StretchFull(txtObj.GetComponent<RectTransform>());
            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 26f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            ApplyJapaneseFont(tmp);
        }

        private static void ApplyJapaneseFont(TextMeshProUGUI tmp)
        {
            TMP_FontAsset jaFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (jaFont != null) tmp.font = jaFont;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
    }
}
