using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
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

        /// <summary>選択艦隊の陣営色を決定する。FactionData があればその color、無ければ enum の既定色。</summary>
        private Color ResolveFactionColor(FleetStrength fs)
        {
            if (fs != null && fs.factionData != null) return fs.factionData.color;
            Faction f = (fs != null) ? fs.faction : Faction.帝国;
            return (f == Faction.帝国) ? empireColor : allianceColor;
        }

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
                if (admiralText != null)
                {
                    // 提督名（正式名 FullName）＋異名行＋参謀名（参謀がいれば実効能力が補完される）
                    AdmiralData ad = strength.admiralData;
                    string fullName = (ad != null) ? ad.FullName : strength.admiralName;
                    string mark = ProtagonistRules.IsProtagonist(ad) ? "★ " : ""; // 主人公（GON-6）
                    // 階級（#14）：所属勢力の階級表から名称を解決（未設定/不明なら空＝出さない）
                    string rankName = RankSystem.ResolveRankName(strength.factionData, (ad != null) ? ad.rankTier : 0);
                    string rankPrefix = string.IsNullOrEmpty(rankName) ? "" : rankName + " ";
                    string admiralLine = $"提督: {mark}{rankPrefix}{fullName}";
                    if (ad != null && !string.IsNullOrEmpty(ad.epithet)) admiralLine += $"\n異名: {ad.EpithetName}";
                    if (ad != null && ad.HasStaff) admiralLine += $"\n参謀: {ad.GetStaffNames()}";
                    admiralText.text = admiralLine;
                }
                if (factionText != null)
                {
                    string fname = (strength.factionData != null) ? strength.factionData.factionName : strength.faction.ToString();
                    factionText.text = $"陣営: {fname}";
                    factionText.color = ResolveFactionColor(strength);
                }

                if (strengthBar != null)
                {
                    strengthBar.maxValue = strength.maxStrength;
                    strengthBar.value = strength.strength;
                }

                if (strengthBarFill != null)
                {
                    strengthBarFill.color = ResolveFactionColor(strength);
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

                    // ミサイル残弾（あれば表示）
                    FleetWeapon weapon = firstSelected.GetComponent<FleetWeapon>();
                    if (weapon != null) shipCountText.text += $"\nミサイル: {weapon.MissileAmmo}発";
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
                AdmiralData ad = (strength != null) ? strength.admiralData : null;
                if (ad != null && ad.hasPreferredFormation)
                {
                    bool match = ad.IsPreferredFormation(squadron.currentFormation);
                    // 得意陣形は同一行に併記（FormationText は高さ約1行ぶんのため、改行2行目が
                    // 見切れないよう1行に収める）。一致時は★＋金色で強調。
                    string star = match ? " ★" : "";
                    formationText.text = $"現在陣形: {squadron.currentFormation}　得意陣形: {ad.preferredFormation}{star}";
                    formationText.color = match ? new Color(1f, 0.85f, 0.3f) : Color.white;
                }
                else
                {
                    formationText.text = $"現在陣形: {squadron.currentFormation}";
                    formationText.color = Color.white;
                }
            }
        }

        // ===== 画面メッセージ（攻撃対象通知など、一定時間表示）=====

        private TextMeshProUGUI messageText;
        private Coroutine messageRoutine;

        /// <summary>
        /// 画面上部にメッセージを duration 秒間表示します（攻撃対象通知など）。
        /// </summary>
        public void ShowMessage(string text, float duration)
        {
            EnsureMessageText();
            if (messageText == null) return;

            messageText.text = text;
            messageText.gameObject.SetActive(true);

            if (messageRoutine != null) StopCoroutine(messageRoutine);
            messageRoutine = StartCoroutine(HideMessageAfter(duration));
        }

        private IEnumerator HideMessageAfter(float duration)
        {
            // timeScale に依存しない実時間で消す（ポーズ・倍速でも一定時間表示）
            yield return new WaitForSecondsRealtime(duration);
            if (messageText != null) messageText.gameObject.SetActive(false);
            messageRoutine = null;
        }

        private void EnsureMessageText()
        {
            if (messageText != null) return;

            // HUD と同じ Canvas に作る（永続のロード用 Canvas を掴まないよう優先順位をつける）
            Canvas canvas = (infoPanel != null) ? infoPanel.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
            if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            GameObject go = new GameObject("AttackTargetMessage", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);

            messageText = go.AddComponent<TextMeshProUGUI>();
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.fontSize = 34f;
            messageText.fontStyle = FontStyles.Bold;
            messageText.color = new Color(1f, 0.85f, 0.3f);
            messageText.raycastTarget = false;
            TMP_FontAsset jaFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (jaFont != null) messageText.font = jaFont;

            RectTransform rt = messageText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -80f);
            rt.sizeDelta = new Vector2(700f, 50f);

            go.SetActive(false);
        }

        /// <summary>
        /// 選択中の艦隊の陣形を変更します（UIボタンから呼ばれる想定）。
        /// </summary>
        /// <param name="formationIdx">Formation enumのインデックス</param>
        public void ChangeFormation(int formationIdx)
        {
            // 陣形変更の実体は FleetCommander に集約（重複排除）。ここは委譲のみ。
            if (commander != null) commander.ChangeFormation(formationIdx);
        }
    }
}
