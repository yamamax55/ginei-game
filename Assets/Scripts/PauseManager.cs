using UnityEngine;
using UnityEngine.InputSystem;
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
            // 初期状態
            if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            
            // デフォルト速度を適用
            savedTimeScale = GameSettings.Instance.defaultTimeScale;
            ApplyTimeScale(savedTimeScale);
        }

        private void Update()
        {
            HandleInput();
            UpdateUI();
        }

        private void HandleInput()
        {
            if (Keyboard.current == null) return;

            // Space: 一時停止 / 再開
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TogglePause();
            }

            // Esc: ポーズメニュー
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                TogglePauseMenu();
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
            if (pauseMenuRoot == null) return;

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
