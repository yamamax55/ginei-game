using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 戦術画面のHUD（UI）を管理するクラス。
    /// 選択中の艦隊情報を表示し、陣形変更命令を送ります。
    /// </summary>
    public class FleetHUDManager : MonoBehaviour
    {
        [Header("参照")]
        public FleetCommander commander;

        [Header("情報パネル")]
        public GameObject infoPanel;
        public TextMeshProUGUI admiralText;
        public TextMeshProUGUI factionText;
        public TextMeshProUGUI formationText;
        [Tooltip("旗艦艦艇数と配下艦残存数を表示（任意。未割当なら非表示）")]
        public TextMeshProUGUI shipCountText;
        public UnityEngine.UI.Slider strengthBar;
        public UnityEngine.UI.Image strengthBarFill;
        public UnityEngine.UI.Slider moraleBar;
        public UnityEngine.UI.Image moraleBarFill;

        [Header("配色")]
public Color empireColor = new Color(0.8f, 0.2f, 0.2f); // 赤系
        public Color allianceColor = new Color(0.2f, 0.4f, 0.8f); // 青系
        public Color panelBgColor = new Color(0.05f, 0.05f, 0.1f, 0.9f); // 濃紺

        private void Start()
        {
            if (commander == null)
            {
                commander = Object.FindAnyObjectByType<FleetCommander>();
            }

            // 初期状態はパネル非表示
            if (infoPanel != null) infoPanel.SetActive(false);
        }

        private void Update()
        {
            UpdateUI();
        }

        /// <summary>
        /// 選択中の艦隊情報をUIに反映します。
        /// </summary>
        private void UpdateUI()
        {
            if (commander == null || commander.SelectedFleets.Count == 0)
            {
                if (infoPanel != null && infoPanel.activeSelf) infoPanel.SetActive(false);
                return;
            }

            if (infoPanel != null && !infoPanel.activeSelf) infoPanel.SetActive(true);

            // 最初に選択された艦隊の情報を表示
            Selectable firstSelected = commander.SelectedFleets[0];
            FleetStrength strength = firstSelected.GetComponent<FleetStrength>();
            Squadron squadron = firstSelected.GetComponent<Squadron>();
            FleetMorale morale = firstSelected.GetComponent<FleetMorale>();

            if (strength != null)
            {
                if (admiralText != null) admiralText.text = $"提督: {strength.admiralName}";
                if (factionText != null)
                {
                    factionText.text = $"陣営: {strength.faction}";
                    factionText.color = (strength.faction == Faction.帝国) ? empireColor : allianceColor;
                }

                if (strengthBar != null)
                {
                    strengthBar.maxValue = strength.maxStrength;
                    strengthBar.value = strength.strength;
                }

                if (strengthBarFill != null)
                {
                    strengthBarFill.color = (strength.faction == Faction.帝国) ? empireColor : allianceColor;
                }

                // 旗艦艦艇数＋配下艦の残存数を表示
                if (shipCountText != null)
                {
                    string flagshipState = strength.IsRetreating
                        ? "退却"
                        : Mathf.Max(0, strength.strength).ToString();

                    int aliveEscorts = 0, totalEscort = 0;
                    if (squadron != null) squadron.GetEscortStatus(out aliveEscorts, out totalEscort);

                    shipCountText.text = $"旗艦艦艇数: {flagshipState}\n配下艦: {aliveEscorts}隻 (計{totalEscort})";
                }
            }

            if (morale != null && moraleBar != null)
            {
                moraleBar.maxValue = morale.maxMorale;
                moraleBar.value = morale.morale;
                if (moraleBarFill != null)
                {
                    // 敗走状態なら灰色、そうでなければ黄色系など
                    moraleBarFill.color = morale.IsRouted ? Color.gray : new Color(1f, 0.8f, 0.2f);
                }
            }

            if (squadron != null && formationText != null)
{
                formationText.text = $"現在陣形: {squadron.currentFormation}";
            }
        }

        /// <summary>
        /// 選択中の艦隊の陣形を変更します（UIボタンから呼ばれる想定）。
        /// </summary>
        /// <param name="formationIdx">Formation enumのインデックス</param>
        public void ChangeFormation(int formationIdx)
        {
            if (commander == null) return;

            Formation newFormation = (Formation)formationIdx;

            foreach (var selected in commander.SelectedFleets)
            {
                Squadron squadron = selected.GetComponent<Squadron>();
                if (squadron != null)
                {
                    squadron.currentFormation = newFormation;
                }
            }
        }
    }
}
