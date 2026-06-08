using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// ポーズ、時間制御（倍速）、および設定画面の表示を管理するクラス。
    /// </summary>
    public class PauseManager : MonoBehaviour
    {
        [Header("UI参照")]
        public GameObject pauseMenuRoot;
        public GameObject settingsPanel;
        public TextMeshProUGUI timeScaleText;

        private float savedTimeScale = 1f;
        private bool isPaused = false;

        private void Start()
        {
            // pauseMenuRoot が未割り当てなら、ポーズメニューUIをコードで自動生成
            if (pauseMenuRoot == null)
            {
                BuildPauseMenuUI();
            }
            else
            {
                // シーンで割り当て済みの場合、全画面に収まるよう RectTransform を正規化する。
                // （オーサリング時の anchoredPosition ずれで画面外に出てメニューが見えない不具合を防止）
                NormalizeFullScreenRect(pauseMenuRoot);
                // ボタンが反応するよう EventSystem を保証（無い時のみ生成）
                EnsureEventSystem();
            }

            // 初期状態
            if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            
            // デフォルト速度を適用
            savedTimeScale = GameSettings.Instance.defaultTimeScale;
            ApplyTimeScale(savedTimeScale);

            // #745：PAUSE/SPEED ラベルを画面上端中央へ寄せ、右上の艦隊HUDと重ならないようにする。
            if (timeScaleText != null)
            {
                RectTransform rt = timeScaleText.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -16f);
            }
        }

        private void Update()
        {
            HandleInput();
            UpdateUI();
        }

        private void HandleInput()
        {
            // 艦隊詳細パネル表示中は、そのパネルがポーズ／Esc を握る（入力を譲る）。
            if (FleetDetailPanel.IsOpen) return;

            if (Keyboard.current == null) return;

            // Space: 一時停止 / 再開
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TogglePause();
            }

            // Esc: ポーズメニュー
            // 優先順位「コマンドメニューを閉じる ＞ 移動/攻撃目標指定キャンセル ＞ ポーズ切替」。
            // 前2者が処理する状況ではポーズメニューを開かず、それぞれの処理に任せる。
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CommandMenu commandMenu = Object.FindAnyObjectByType<CommandMenu>();
                FleetCommander commander = Object.FindAnyObjectByType<FleetCommander>();
                bool handledElsewhere = (commandMenu != null && commandMenu.IsOpen)
                                        || (commander != null && commander.IsWaitingForMoveTarget)
                                        || (commander != null && commander.IsWaitingForAttackTarget);
                if (!handledElsewhere)
                {
                    TogglePauseMenu();
                }
            }

            // 数字キー: 倍速切り替え
            if (!isPaused)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) SetTimeScale(1f);
                if (Keyboard.current.digit2Key.wasPressedThisFrame) SetTimeScale(2f);
                if (Keyboard.current.digit3Key.wasPressedThisFrame) SetTimeScale(3f);
            }
        }

        public void TogglePause()
        {
            if (isPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        public void Pause()
        {
            isPaused = true;
            Time.timeScale = 0f;
            Debug.Log("Game Paused");
        }

        public void Resume()
        {
            isPaused = false;
            Time.timeScale = savedTimeScale;
            if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            Debug.Log("Game Resumed. TimeScale: " + savedTimeScale);
        }

        public void TogglePauseMenu()
        {
            // 未生成なら自動生成して、Escで必ずメニューが開くようにする
            if (pauseMenuRoot == null)
            {
                BuildPauseMenuUI();
                if (pauseMenuRoot == null) return; // 生成失敗時のみ何もしない
            }

            if (pauseMenuRoot.activeSelf)
            {
                Resume();
            }
            else
            {
                Pause();
                pauseMenuRoot.SetActive(true);
            }
        }

        public void SetTimeScale(float scale)
        {
            savedTimeScale = scale;
            if (!isPaused)
            {
                ApplyTimeScale(scale);
            }
        }

        private void ApplyTimeScale(float scale)
        {
            Time.timeScale = scale;
        }

        private void UpdateUI()
        {
            if (timeScaleText != null)
            {
                if (Time.timeScale == 0)
                {
                    timeScaleText.text = "PAUSE";
                }
                else
                {
                    timeScaleText.text = $"SPEED: {Time.timeScale:F1}x";
                }
            }
        }

        // --- ポーズメニューUIの自動生成（SceneLoaderのロード画面生成と同方式） ---

        /// <summary>
        /// ポーズメニューUI一式（Canvas/背景/タイトル/ボタン）をコードで生成します。
        /// </summary>
        private void BuildPauseMenuUI()
        {
            // 新Input System用のEventSystemを用意（無いとボタンが反応しない）
            EnsureEventSystem();

            // 1. Canvas（最前面のScreenSpaceOverlay）
            GameObject canvasObj = new GameObject("PauseCanvas");
            canvasObj.transform.SetParent(transform);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // 最前面
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();

            // 2. 半透明の暗い背景パネル（これを pauseMenuRoot にする）
            pauseMenuRoot = new GameObject("PauseMenuRoot");
            pauseMenuRoot.transform.SetParent(canvasObj.transform);
            RectTransform rootRT = pauseMenuRoot.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.sizeDelta = Vector2.zero;
            rootRT.anchoredPosition = Vector2.zero;
            Image bg = pauseMenuRoot.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.7f);

            // 3. 中央の縦並びコンテナ
            GameObject panel = new GameObject("MenuPanel");
            panel.transform.SetParent(pauseMenuRoot.transform);
            RectTransform panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.anchoredPosition = Vector2.zero;
            panelRT.sizeDelta = new Vector2(320, 320);
            VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 20;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(20, 20, 20, 20);

            // 4. タイトル文字
            CreateLabel(panel.transform, "ポーズ", 40f);

            // 5. ボタン（既存メソッドに接続）
            CreateMenuButton(panel.transform, "再開", OnResumeButton);
            CreateMenuButton(panel.transform, "タイトルへ", OnBackToTitleButton);
        }

        /// <summary>
        /// 指定オブジェクトの RectTransform を親いっぱい（全画面）に正規化します。
        /// アンカーを全ストレッチにし、オフセットと anchoredPosition を 0 にして画面外配置を矯正します。
        /// </summary>
        private void NormalizeFullScreenRect(GameObject target)
        {
            if (target == null) return;
            RectTransform rt = target.GetComponent<RectTransform>();
            if (rt == null) return;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// シーンにEventSystemが無ければ、InputSystemUIInputModule付きで生成します。
        /// （StandaloneInputModuleでは新Input System下でボタンが反応しないため）
        /// </summary>
        private void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;

            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>
        /// 縦並びコンテナ用のラベル（TMP）を生成します。日本語フォントを適用。
        /// </summary>
        private void CreateLabel(Transform parent, string text, float fontSize)
        {
            GameObject obj = new GameObject("Label");
            obj.transform.SetParent(parent);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            ApplyJapaneseFont(tmp);

            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.minHeight = 60;
            le.preferredHeight = 60;
        }

        /// <summary>
        /// 縦並びコンテナ用のボタンを生成し、onClickに指定アクションを接続します。
        /// </summary>
        private void CreateMenuButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            GameObject btnObj = new GameObject("Button_" + label);
            btnObj.transform.SetParent(parent);
            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.25f, 0.35f, 1f);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => AudioManager.Instance.PlayUIClick());
            btn.onClick.AddListener(action);

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.minHeight = 50;
            le.preferredHeight = 50;

            // ボタンのラベル（TMP）
            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform);
            RectTransform txtRT = txtObj.AddComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.sizeDelta = Vector2.zero;
            txtRT.anchoredPosition = Vector2.zero;

            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 28f;
            tmp.alignment = TextAlignmentOptions.Center;
            ApplyJapaneseFont(tmp);
        }

        /// <summary>
        /// Resources の "JapaneseFont_TMP" を適用します（文字化け防止）。
        /// </summary>
        private void ApplyJapaneseFont(TextMeshProUGUI tmp)
        {
            TMP_FontAsset jaFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (jaFont != null) tmp.font = jaFont;
        }

        // --- UI Button Hooks ---

        public void OnResumeButton() => Resume();

        public void OnSettingsButton()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);
            }
        }

        public void OnCloseSettingsButton()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }

        public void OnBackToTitleButton()
        {
            Time.timeScale = 1f;
            SceneLoader.Instance.LoadScene("Title");
        }

        // --- Settings Updates ---

        public void SetVolume(float volume)
        {
            GameSettings.Instance.masterVolume = volume;
            AudioListener.volume = volume;
        }

        public void SetAlwaysShowGizmos(bool value)
        {
            GameSettings.Instance.alwaysShowGizmos = value;
        }
    }
}
