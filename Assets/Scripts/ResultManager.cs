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
                // 多勢力対応：winnerName があればそれを表示（無ければ enum の「◯◯軍」）
                string wname = string.IsNullOrEmpty(settings.winnerName) ? $"{settings.winner}軍" : settings.winnerName;
                winnerText.text = (settings.winnerName == "引き分け") ? "引き分け" : $"{wname}の勝利";
                winnerText.color = (settings.winner == Faction.帝国) ? Color.red : Color.cyan;
            }

            if (statsText != null)
            {
                string mvp = string.IsNullOrEmpty(settings.mvpAdmiral) ? "—" : settings.mvpAdmiral;
                string reason = string.IsNullOrEmpty(settings.victoryReason) ? "—" : settings.victoryReason;
                statsText.text = $"【戦果】\n" +
                                 $"帝国軍  喪失: {settings.imperialSunkCount}　残存兵力: {settings.imperialRemainingStrength}\n" +
                                 $"同盟軍  喪失: {settings.allianceSunkCount}　残存兵力: {settings.allianceRemainingStrength}\n" +
                                 $"\n" +
                                 $"勝因: {reason}\n" +
                                 $"殊勲提督(MVP): {mvp}";
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
