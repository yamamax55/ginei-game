using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 結果画面の表示と遷移を管理するクラス。
    /// </summary>
    public class ResultManager : MonoBehaviour
    {
        [Header("UI参照")]
        public TextMeshProUGUI winnerText;
        public TextMeshProUGUI statsText;

        private void Start()
        {
            DisplayResults();
        }

        /// <summary>
        /// GameSettingsから戦績を取得して表示します。
        /// </summary>
        private void DisplayResults()
        {
            GameSettings settings = GameSettings.Instance;
            
            if (winnerText != null)
            {
                winnerText.text = $"{settings.winner}軍の勝利";
                winnerText.color = (settings.winner == Faction.帝国) ? Color.red : Color.cyan;
            }

            if (statsText != null)
            {
                statsText.text = $"【戦績】\n" +
                                 $"帝国軍喪失数: {settings.imperialSunkCount}\n" +
                                 $"同盟軍喪失数: {settings.allianceSunkCount}\n" +
                                 $"残存兵力: {settings.remainingStrength}";
            }
        }

        /// <summary>
        /// タイトル画面に戻ります。
        /// </summary>
        public void BackToTitle()
        {
            SceneLoader.Instance.LoadScene("Title");
        }
}
}
