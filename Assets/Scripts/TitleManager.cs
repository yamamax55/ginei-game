using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// タイトル画面のUI入力を管理するクラス。
    /// </summary>
    public class TitleManager : MonoBehaviour
    {
        [Header("UI参照")]
        public UnityEngine.UI.Button continueButton;
        public GameObject settingsPanel;

        private void Start()
        {
            // セーブデータがある場合のみ「続きから」ボタンを有効化
            if (continueButton != null)
            {
                continueButton.interactable = SaveManager.HasSave();
            }

            if (settingsPanel != null) settingsPanel.SetActive(false);

            AudioManager.Instance.PlayBGM(AudioManager.Instance.bgmTitle);
        }

        /// <summary>
        /// 会戦を開始します。
        /// </summary>
        public void StartBattle()
        {
            Debug.Log("TitleManager: StartBattle called.");
            
            // 現在の設定を保存
            SaveCurrentSetup();

            GameSettings.Instance.ResetStats();
            Debug.Log("TitleManager: Stats reset. Loading scene 'Battle'...");
            SceneLoader.Instance.LoadScene("Battle");
        }

        /// <summary>
        /// 直近のセットアップを復元して会戦を開始します。
        /// </summary>
        public void ContinueBattle()
        {
            SaveData data = SaveManager.Load();
            if (data != null)
            {
                Debug.Log("TitleManager: Restoring previous setup...");
                GameSettings settings = GameSettings.Instance;
                settings.playerFaction = (Faction)data.playerFaction;
                settings.scenarioName = data.scenarioName;
                settings.selectedAdmiral = data.selectedAdmiral;

                settings.ResetStats();
                SceneLoader.Instance.LoadScene("Battle");
            }
            else
            {
                Debug.LogWarning("TitleManager: No save data found to continue.");
            }
        }

        private void SaveCurrentSetup()
        {
            GameSettings settings = GameSettings.Instance;
            SaveData data = new SaveData
            {
                playerFaction = (int)settings.playerFaction,
                scenarioName = settings.scenarioName,
                selectedAdmiral = settings.selectedAdmiral
            };
            SaveManager.Save(data);
        }

        /// <summary>
        /// 設定画面を開く。
        /// </summary>
        public void OpenSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(true);
        }

        public void CloseSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        public void SetVolume(float volume)
        {
            GameSettings.Instance.masterVolume = volume;
            AudioListener.volume = volume;
        }

        /// <summary>
        /// ゲームを終了します。
/// </summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
