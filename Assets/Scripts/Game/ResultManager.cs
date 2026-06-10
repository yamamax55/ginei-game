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

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append("【戦果】\n");

                // 多勢力対応：勢力名キーの戦績があれば勢力数可変で表示。
                // 無ければ従来の帝国/同盟2勢力にフォールバック。
                if (settings.factionStats != null && settings.factionStats.Count > 0)
                {
                    foreach (var fsStat in settings.factionStats)
                    {
                        if (fsStat == null) continue;
                        sb.Append($"{fsStat.factionName}  喪失: {fsStat.sunkCount}　残存兵力: {fsStat.remainingStrength}\n");
                    }
                }
                else
                {
                    sb.Append($"帝国軍  喪失: {settings.imperialSunkCount}　残存兵力: {settings.imperialRemainingStrength}\n");
                    sb.Append($"同盟軍  喪失: {settings.allianceSunkCount}　残存兵力: {settings.allianceRemainingStrength}\n");
                }

                sb.Append($"\n勝因: {reason}\n殊勲提督(MVP): {mvp}");
                statsText.text = sb.ToString();
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
