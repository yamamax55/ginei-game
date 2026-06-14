using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 選択中の艦隊の詳細情報を表示するモーダルパネル（HUDとは別）。
    /// コマンドメニューの「情報」から開く。表示中は <see cref="Time.timeScale"/>=0 でポーズし、
    /// 閉じる/Esc/背景クリックで元の速度へ復帰する。UIは実行時にコード生成する。
    /// </summary>
    public class FleetDetailPanel : MonoBehaviour
    {
        private static FleetDetailPanel instance;

        /// <summary>詳細パネルが開いているか（PauseManager 等が入力を譲るために参照）。</summary>
        public static bool IsOpen => instance != null && instance.isOpen;

        private bool isOpen;
        private float savedTimeScale = 1f;
        private GameObject root;
        private TextMeshProUGUI bodyText;
        private object escWindowToken; // UIWindowStack 登録トークン（#ウィンドウESC）

        /// <summary>指定艦隊の詳細を表示する（必要なら生成）。</summary>
        public static void Show(Selectable fleet)
        {
            if (fleet == null) return;
            if (instance == null)
            {
                GameObject go = new GameObject("FleetDetailPanel");
                instance = go.AddComponent<FleetDetailPanel>();
                instance.Build();
            }
            instance.Display(fleet);
        }

        private void Build()
        {
            EnsureEventSystem();

            GameObject canvasObj = new GameObject("FleetDetailCanvas");
            canvasObj.transform.SetParent(transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90; // HUD/コマンドメニューより前面
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(canvasObj.transform, false);
            StretchFull(root.GetComponent<RectTransform>());

            // 背景ディマー（クリックで閉じる）
            GameObject dim = new GameObject("Dimmer", typeof(RectTransform));
            dim.transform.SetParent(root.transform, false);
            StretchFull(dim.GetComponent<RectTransform>());
            Image dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.6f);
            Button dimBtn = dim.AddComponent<Button>();
            dimBtn.transition = UnityEngine.UI.Selectable.Transition.None; // Ginei.Selectable と衝突するため完全修飾
            dimBtn.onClick.AddListener(Close);

            // パネル（中央）
            GameObject panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            RectTransform pRT = panel.GetComponent<RectTransform>();
            pRT.anchorMin = new Vector2(0.5f, 0.5f);
            pRT.anchorMax = new Vector2(0.5f, 0.5f);
            pRT.pivot = new Vector2(0.5f, 0.5f);
            pRT.sizeDelta = new Vector2(620f, 720f);
            pRT.anchoredPosition = Vector2.zero;
            Image pImg = panel.AddComponent<Image>();
            pImg.color = new Color(0.06f, 0.07f, 0.12f, 0.97f);

            VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(26, 26, 22, 22);
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;

            CreateText(panel.transform, "艦隊詳細", 30f, FontStyles.Bold, TextAlignmentOptions.Center);

            bodyText = CreateText(panel.transform, "", 22f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            LayoutElement bodyLE = bodyText.gameObject.AddComponent<LayoutElement>();
            bodyLE.flexibleHeight = 1f;

            CreateButton(panel.transform, "閉じる (Esc)", Close);

            root.SetActive(false);

            // ESC は UIWindowStack 経由で「手前から閉じる」（自前でキー直読みしない）。
            escWindowToken = UIWindowStack.Register(() => isOpen, Close, 90, "艦隊詳細");
        }

        private void Display(Selectable fleet)
        {
            if (bodyText != null) bodyText.text = BuildInfo(fleet);
            if (!isOpen)
            {
                savedTimeScale = Time.timeScale;
                Time.timeScale = 0f; // 情報確認中はポーズ
                isOpen = true;
            }
            if (root != null) root.SetActive(true);
        }

        /// <summary>パネルを閉じて元の速度へ復帰する。</summary>
        public void Close()
        {
            if (isOpen)
            {
                Time.timeScale = savedTimeScale;
                isOpen = false;
            }
            if (root != null) root.SetActive(false);
        }

        private void Update()
        {
            if (!isOpen) return;
            // 倍速キー等で解除されてもポーズを維持する。Esc は UIWindowStack 経由（PauseManager）で閉じる。
            Time.timeScale = 0f;
        }

        private string BuildInfo(Selectable fleet)
        {
            FleetStrength str = fleet.GetComponent<FleetStrength>();
            Squadron sq = fleet.GetComponent<Squadron>();
            FleetMorale mor = fleet.GetComponent<FleetMorale>();
            FleetWeapon wpn = fleet.GetComponent<FleetWeapon>();
            AdmiralData ad = (str != null) ? str.admiralData : null;

            var sb = new StringBuilder();

            string fullName = (ad != null) ? ad.FullName : (str != null ? str.admiralName : "—");
            string rank = (str != null) ? RankSystem.ResolveRankNameOrDefault(str.factionData, ad != null ? ad.rankTier : 0) : "";
            string mark = ProtagonistRules.IsProtagonist(ad) ? "★" : "";
            string rankPrefix = string.IsNullOrEmpty(rank) ? "" : rank + " ";
            sb.AppendLine($"提督: {mark}{rankPrefix}{fullName}");
            if (ad != null && !string.IsNullOrEmpty(ad.epithet)) sb.AppendLine($"異名: {ad.EpithetName}");
            if (ad != null && !string.IsNullOrEmpty(ad.callName)) sb.AppendLine($"呼称: {ad.callName}");
            string fname = (str != null && str.factionData != null) ? str.factionData.factionName
                : (str != null ? str.faction.ToString() : "—");
            sb.AppendLine($"陣営: {fname}");
            if (str != null && str.HasFleetNumber)
            {
                sb.AppendLine($"艦隊: {str.FleetLabel}");
                if (str.HasEchelon) sb.AppendLine($"編制: {str.EchelonPath}");
            }
            if (ProtagonistRules.IsProtagonist(ad)) sb.AppendLine("主人公: ★アンカー（プレイヤー操作固定）");

            sb.AppendLine();
            sb.AppendLine("― 能力（実効値）―");
            if (ad != null)
            {
                sb.AppendLine($"統率 {ad.EffectiveLeadership}　　攻撃 {ad.EffectiveAttack}　　防御 {ad.EffectiveDefense}");
                sb.AppendLine($"機動 {ad.EffectiveMobility}　　運営 {ad.EffectiveOperation}　　情報 {ad.EffectiveIntelligence}");
                if (ad.HasStaff) sb.AppendLine($"参謀: {ad.GetStaffNames()}");
            }
            else sb.AppendLine("（提督データなし）");

            sb.AppendLine();
            sb.AppendLine("― 戦力 ―");
            if (str != null)
            {
                string retreat = str.IsRetreating ? "　（退却中）" : "";
                sb.AppendLine($"兵力: {Mathf.Max(0, str.strength)} / {str.maxStrength}{retreat}");
            }
            if (mor != null)
            {
                string rout = mor.IsRouted ? "　（敗走）" : "";
                sb.AppendLine($"士気: {Mathf.RoundToInt(mor.morale)} / {Mathf.RoundToInt(mor.maxMorale)}{rout}");
            }
            if (sq != null)
            {
                sq.GetEscortStatus(out int alive, out int total);
                sb.AppendLine($"配下艦: {alive}隻 / 計{total}");
            }
            if (wpn != null) sb.AppendLine($"ミサイル残弾: {wpn.MissileAmmo}発");

            sb.AppendLine();
            sb.AppendLine("― 陣形 ―");
            if (sq != null)
            {
                sb.AppendLine($"現在陣形: {sq.currentFormation}");
                if (ad != null && ad.hasPreferredFormation)
                {
                    bool m = ad.IsPreferredFormation(sq.currentFormation);
                    sb.AppendLine($"得意陣形: {ad.preferredFormation}{(m ? "　★一致中" : "")}");
                }
            }

            return sb.ToString();
        }

        // ===== UI生成ヘルパ =====

        private TextMeshProUGUI CreateText(Transform parent, string text, float size, FontStyles style, TextAlignmentOptions align)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            ApplyJapaneseFont(t);
            return t;
        }

        private void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject("Button_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.25f, 0.4f, 1f);
            Button btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 50f;

            TextMeshProUGUI txt = CreateText(go.transform, label, 24f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchFull(txt.rectTransform);
        }

        private void ApplyJapaneseFont(TextMeshProUGUI tmp)
        {
            TMP_FontAsset jaFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (jaFont != null) tmp.font = jaFont;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private void OnDestroy()
        {
            UIWindowStack.Unregister(escWindowToken);
            if (isOpen) Time.timeScale = savedTimeScale;
            if (instance == this) instance = null;
        }
    }
}
