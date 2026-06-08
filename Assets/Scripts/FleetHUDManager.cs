using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace Ginei
{
    /// <summary>
    /// 戦術画面のHUD（UI）を管理するクラス。
    /// 選択中の艦隊情報を表示し、陣形変更命令を送ります。
    ///
    /// HUDは実行時にコード生成する（#745 恒久対応）：右上に VerticalLayoutGroup ＋ ContentSizeFitter の
    /// パネルを置き、行（提督/陣営/兵力/士気/配下艦/陣形）を縦積みする。行数が増えても（★主人公/階級/異名/
    /// 参謀）自動でパネルが伸び、要素同士が重ならない。旧来のシーン手配線フィールドは後方互換のため残すが、
    /// コード生成HUDでは使用しない（割り当てがあれば起動時に隠す）。
    /// </summary>
    public class FleetHUDManager : MonoBehaviour
    {
        [Header("参照")]
        public FleetCommander commander;

        [Header("（旧）シーン手配線 ※コード生成HUDでは未使用・割当があれば非表示にする")]
        public GameObject infoPanel;
        public TextMeshProUGUI admiralText;
        public TextMeshProUGUI factionText;
        public TextMeshProUGUI formationText;
        public TextMeshProUGUI shipCountText;
        public Slider strengthBar;
        public Image strengthBarFill;
        public Slider moraleBar;
        public Image moraleBarFill;

        [Header("配色")]
        public Color empireColor = new Color(0.8f, 0.2f, 0.2f);   // 赤系
        public Color allianceColor = new Color(0.2f, 0.4f, 0.8f); // 青系
        public Color panelBgColor = new Color(0.05f, 0.05f, 0.1f, 0.9f); // 濃紺

        [Header("レイアウト")]
        [Tooltip("HUDパネルの横幅(px)。長い正式名はこの幅で折り返す")]
        public float panelWidth = 380f;

        // ===== コード生成HUDの要素 =====
        private Canvas hudCanvas;
        private GameObject hudPanel;
        private TextMeshProUGUI hudAdmiral, hudFaction, hudStrengthLabel, hudMoraleLabel, hudShips, hudFormation;
        private GameObject hudStrengthRow, hudMoraleRow, hudShipsObj;
        private Image hudStrengthFill, hudMoraleFill;
        private bool hudBuilt;

        /// <summary>選択艦隊の陣営色を決定する。FactionData があればその color、無ければ enum の既定色。</summary>
        private Color ResolveFactionColor(FleetStrength fs)
        {
            if (fs != null && fs.factionData != null) return fs.factionData.color;
            Faction f = (fs != null) ? fs.faction : Faction.帝国;
            return (f == Faction.帝国) ? empireColor : allianceColor;
        }

        private void Start()
        {
            if (commander == null) commander = Object.FindAnyObjectByType<FleetCommander>();

            // 旧シーンHUDがあれば隠す（コード生成HUDに一本化）
            if (infoPanel != null) infoPanel.SetActive(false);

            BuildHud();
            SetHudVisible(false);
        }

        private void Update()
        {
            UpdateUI();
        }

        // ===== HUD生成 =====

        private void BuildHud()
        {
            if (hudBuilt) return;

            GameObject canvasObj = new GameObject("FleetHUDCanvas");
            hudCanvas = canvasObj.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = 10; // ワールドより前面・モーダル（ポーズ/コマンド）より背面寄り
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            hudPanel = new GameObject("HUDPanel", typeof(RectTransform));
            hudPanel.transform.SetParent(canvasObj.transform, false);
            RectTransform pRT = hudPanel.GetComponent<RectTransform>();
            pRT.anchorMin = new Vector2(1f, 1f);
            pRT.anchorMax = new Vector2(1f, 1f);
            pRT.pivot = new Vector2(1f, 1f);
            pRT.anchoredPosition = new Vector2(-16f, -16f);
            pRT.sizeDelta = new Vector2(panelWidth, 0f); // 横幅固定・高さは Fitter が決める

            Image bg = hudPanel.AddComponent<Image>();
            bg.color = panelBgColor;
            bg.raycastTarget = false;

            VerticalLayoutGroup vlg = hudPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 12, 12);
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter fitter = hudPanel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; // 幅は sizeDelta 固定
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;   // 高さは中身ぶん

            hudAdmiral = CreateText("Admiral", 26f, FontStyles.Bold);
            hudFaction = CreateText("Faction", 22f, FontStyles.Normal);
            hudStrengthLabel = CreateText("StrengthLabel", 18f, FontStyles.Normal);
            hudStrengthFill = CreateBar("StrengthBar", out hudStrengthRow);
            hudMoraleLabel = CreateText("MoraleLabel", 18f, FontStyles.Normal);
            hudMoraleFill = CreateBar("MoraleBar", out hudMoraleRow);
            hudShips = CreateText("Ships", 18f, FontStyles.Normal);
            hudShipsObj = hudShips.gameObject;
            hudFormation = CreateText("Formation", 20f, FontStyles.Normal);

            hudBuilt = true;
        }

        private TextMeshProUGUI CreateText(string name, float size, FontStyles style)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(hudPanel.transform, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.fontStyle = style;
            t.color = Color.white;
            t.raycastTarget = false;
            ApplyJapaneseFont(t);
            return t;
        }

        /// <summary>横バー（背景＋左詰めの塗り）を生成し、塗りの Image を返す。塗り幅は anchorMax.x で表す。</summary>
        private Image CreateBar(string name, out GameObject row)
        {
            row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(hudPanel.transform, false);
            LayoutElement le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 12f;
            le.flexibleWidth = 1f;
            Image bgImg = row.AddComponent<Image>();
            bgImg.color = new Color(1f, 1f, 1f, 0.15f);
            bgImg.raycastTarget = false;

            GameObject fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(row.transform, false);
            RectTransform fRT = fill.GetComponent<RectTransform>();
            fRT.anchorMin = new Vector2(0f, 0f);
            fRT.anchorMax = new Vector2(1f, 1f);
            fRT.offsetMin = Vector2.zero;
            fRT.offsetMax = Vector2.zero;
            Image fImg = fill.AddComponent<Image>();
            fImg.color = Color.white;
            fImg.raycastTarget = false;
            return fImg;
        }

        private void SetHudVisible(bool visible)
        {
            if (hudPanel != null && hudPanel.activeSelf != visible) hudPanel.SetActive(visible);
        }

        // ===== 表示更新 =====

        private void UpdateUI()
        {
            if (commander == null || commander.SelectedFleets.Count == 0)
            {
                SetHudVisible(false);
                return;
            }
            SetHudVisible(true);

            Selectable firstSelected = commander.SelectedFleets[0];
            FleetStrength strength = firstSelected.GetComponent<FleetStrength>();
            Squadron squadron = firstSelected.GetComponent<Squadron>();
            FleetMorale morale = firstSelected.GetComponent<FleetMorale>();

            if (strength != null)
            {
                AdmiralData ad = strength.admiralData;

                // 提督行（★主人公／階級／正式名）＋異名行＋参謀行
                string fullName = (ad != null) ? ad.FullName : strength.admiralName;
                string mark = ProtagonistRules.IsProtagonist(ad) ? "★ " : "";
                string rankName = RankSystem.ResolveRankNameOrDefault(strength.factionData, (ad != null) ? ad.rankTier : 0);
                string rankPrefix = string.IsNullOrEmpty(rankName) ? "" : rankName + " ";
                // 艦隊番号（#146）があれば提督行の上に「[軍集団 ⊃ 軍団 ⊃] 第N艦隊」を出す（梯団⊃艦隊⊃提督の分離を表示・#147）。
                string line = "";
                if (strength.HasFleetNumber)
                {
                    string fleetLine = strength.HasEchelon ? $"{strength.EchelonPath} ⊃ {strength.FleetLabel}" : strength.FleetLabel;
                    line = fleetLine + "\n";
                }
                line += $"提督: {mark}{rankPrefix}{fullName}";
                if (ad != null && !string.IsNullOrEmpty(ad.epithet)) line += $"\n異名: {ad.EpithetName}";
                if (ad != null && ad.HasStaff) line += $"\n参謀: {ad.GetStaffNames()}";
                hudAdmiral.text = line;

                // 陣営
                string fname = (strength.factionData != null) ? strength.factionData.factionName : strength.faction.ToString();
                hudFaction.text = $"陣営: {fname}";
                hudFaction.color = ResolveFactionColor(strength);

                // 兵力バー
                int curS = Mathf.Max(0, strength.strength);
                int maxS = Mathf.Max(1, strength.maxStrength);
                hudStrengthLabel.text = strength.IsRetreating ? "兵力: 退却" : $"兵力: {curS} / {maxS}";
                SetBarFill(hudStrengthFill, (float)curS / maxS, ResolveFactionColor(strength));

                // 配下艦＋ミサイル
                if (squadron != null)
                {
                    squadron.GetEscortStatus(out int alive, out int total);
                    string ships = $"配下艦: {alive}隻 (計{total})";
                    FleetWeapon weapon = firstSelected.GetComponent<FleetWeapon>();
                    if (weapon != null) ships += $"　ミサイル: {weapon.MissileAmmo}発";
                    hudShips.text = ships;
                    if (hudShipsObj != null) hudShipsObj.SetActive(true);
                }
                else if (hudShipsObj != null) hudShipsObj.SetActive(false);
            }

            // 士気バー
            if (morale != null)
            {
                float maxM = Mathf.Max(1f, morale.maxMorale);
                float frac = Mathf.Clamp01(morale.morale / maxM);
                hudMoraleLabel.text = morale.IsRouted
                    ? "士気: 敗走"
                    : $"士気: {Mathf.RoundToInt(morale.morale)} / {Mathf.RoundToInt(morale.maxMorale)}";
                SetBarFill(hudMoraleFill, frac, morale.IsRouted ? Color.gray : new Color(1f, 0.8f, 0.2f));
                hudMoraleLabel.gameObject.SetActive(true);
                if (hudMoraleRow != null) hudMoraleRow.SetActive(true);
            }
            else
            {
                hudMoraleLabel.gameObject.SetActive(false);
                if (hudMoraleRow != null) hudMoraleRow.SetActive(false);
            }

            // 陣形（得意陣形一致で★金色）
            if (squadron != null)
            {
                AdmiralData ad = (strength != null) ? strength.admiralData : null;
                if (ad != null && ad.hasPreferredFormation)
                {
                    bool match = ad.IsPreferredFormation(squadron.currentFormation);
                    string star = match ? " ★" : "";
                    hudFormation.text = $"現在陣形: {squadron.currentFormation}　得意陣形: {ad.preferredFormation}{star}";
                    hudFormation.color = match ? new Color(1f, 0.85f, 0.3f) : Color.white;
                }
                else
                {
                    hudFormation.text = $"現在陣形: {squadron.currentFormation}";
                    hudFormation.color = Color.white;
                }
                hudFormation.gameObject.SetActive(true);
            }
            else hudFormation.gameObject.SetActive(false);
        }

        /// <summary>塗りの幅（0..1）と色を設定する。</summary>
        private static void SetBarFill(Image fill, float fraction, Color color)
        {
            if (fill == null) return;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            fill.color = color;
        }

        private void ApplyJapaneseFont(TextMeshProUGUI tmp)
        {
            TMP_FontAsset jaFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (jaFont != null) tmp.font = jaFont;
        }

        // ===== 画面メッセージ（攻撃対象通知など、一定時間表示）=====

        private TextMeshProUGUI messageText;
        private Coroutine messageRoutine;

        /// <summary>画面上部にメッセージを duration 秒間表示します（攻撃対象通知など）。</summary>
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
            // timeScale 非依存の実時間で消す（ポーズ・倍速でも一定時間表示）
            yield return new WaitForSecondsRealtime(duration);
            if (messageText != null) messageText.gameObject.SetActive(false);
            messageRoutine = null;
        }

        private void EnsureMessageText()
        {
            if (messageText != null) return;

            Canvas canvas = hudCanvas != null ? hudCanvas : Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            GameObject go = new GameObject("AttackTargetMessage", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);

            messageText = go.AddComponent<TextMeshProUGUI>();
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.fontSize = 34f;
            messageText.fontStyle = FontStyles.Bold;
            messageText.color = new Color(1f, 0.85f, 0.3f);
            messageText.raycastTarget = false;
            ApplyJapaneseFont(messageText);

            RectTransform rt = messageText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -80f);
            rt.sizeDelta = new Vector2(700f, 50f);

            go.SetActive(false);
        }

        /// <summary>選択中の艦隊の陣形を変更します（UIボタンから呼ばれる想定）。</summary>
        /// <param name="formationIdx">Formation enumのインデックス</param>
        public void ChangeFormation(int formationIdx)
        {
            // 陣形変更の実体は FleetCommander に集約（重複排除）。ここは委譲のみ。
            if (commander != null) commander.ChangeFormation(formationIdx);
        }
    }
}
