using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 人物動態オブザーバ（観測層・read-only）。<b>Alt+L キー</b>で開閉し、キャンペーンの人物ロスター
    /// （提督=<see cref="GalaxyView.CommanderRoster"/>＋文民=<see cref="GalaxyView.CivilianRoster"/>）を勢力ごとに集計して
    /// 毎フレームライブダンプする：年齢/死亡（<see cref="LifecycleRules"/>）・捕虜/行方不明/在野（<see cref="CaptiveStatus"/>/`isFreeAgent`）・
    /// 職分内訳（<see cref="PersonVocationRules.VocationOf"/>）。
    /// 観測専用ゆえ既存フィールドのみ読む。操作はさせない＝**状態は変えない**。`HelpOverlay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class PersonnelDynamicsObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1106;
        public float dimAlpha = 0.55f;
        public float panelWidth = 980f;
        public float panelMaxHeight = 900f;
        public Color panelColor = new Color(0.05f, 0.05f, 0.04f, 0.96f);
        public float bodyFontSize = 20f;
        public int barWidth = 14;

        private GameObject overlayRoot;
        private GameObject panel;
        private TextMeshProUGUI bodyLabel;
        private object escWindowToken;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Strategy" && scene.name != "Battle") return;
            if (Object.FindAnyObjectByType<PersonnelDynamicsObserverOverlay>() != null) return;
            new GameObject("PersonnelDynamicsObserverOverlay").AddComponent<PersonnelDynamicsObserverOverlay>();
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "人物動態");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.人物動態観測切替))
                Toggle();

            if (panel != null && panel.activeSelf && bodyLabel != null)
                bodyLabel.text = BuildDump();
        }

        public void Toggle() => SetVisible(panel != null && !panel.activeSelf);

        public void SetVisible(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        // ===== ダンプ本体 =====

        private string BuildDump()
        {
            var sb = new StringBuilder(2048);

            sb.Append("<b>人物動態オブザーバ</b>　年齢/死亡・捕虜・在野・職分　(Alt+L で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            GalaxyView gv = GalaxyView.Active;
            if (gv == null)
            {
                sb.Append("\n<color=#ffcc66>キャンペーンの人物ロスターは戦略マップ（GalaxyView）でのみ観測できます。</color>");
                return sb.ToString();
            }

            int year = gv.CampaignYear;

            var all = new List<Person>();
            if (gv.CommanderRoster != null) all.AddRange(gv.CommanderRoster);
            if (gv.CivilianRoster != null) all.AddRange(gv.CivilianRoster);

            var factions = new List<Faction>();
            for (int i = 0; i < all.Count; i++)
            {
                Person p = all[i];
                if (p == null) continue;
                if (!factions.Contains(p.faction)) factions.Add(p.faction);
            }

            for (int i = 0; i < factions.Count; i++)
                AppendFaction(sb, all, year, factions[i]);

            sb.Append("\n<color=#6f8a9a>※ ロスターは年次に老衰（LifecycleRules）。捕虜/在野/行方不明は会戦・人事で増減。職分は PersonVocationRules で判定。</color>");
            return sb.ToString();
        }

        private void AppendFaction(StringBuilder sb, List<Person> all, int year, Faction faction)
        {
            int total = 0, alive = 0, dead = 0, captive = 0, mia = 0, free = 0;
            int ageCount = 0; long ageSum = 0;
            var vocCount = new Dictionary<PersonVocation, int>();

            for (int i = 0; i < all.Count; i++)
            {
                Person p = all[i];
                if (p == null || p.faction != faction) continue;
                total++;

                if (p.IsDeceased) dead++; else alive++;
                if (p.captiveStatus == CaptiveStatus.捕虜) captive++;
                if (p.captiveStatus == CaptiveStatus.行方不明) mia++;
                if (p.isFreeAgent) free++;

                if (!p.IsDeceased && p.birthYear > 0)
                {
                    ageSum += LifecycleRules.Age(p.birthYear, year);
                    ageCount++;
                }

                PersonVocation voc = PersonVocationRules.VocationOf(p);
                vocCount.TryGetValue(voc, out int n);
                vocCount[voc] = n + 1;
            }

            sb.Append('\n').Append("<color=#e7e0b0>◤ ").Append(faction).Append("</color>");
            sb.Append("　<color=#6f8a9a>(人物 ").Append(total).Append(")</color>\n");

            sb.Append("  <color=#9fb0c0>状態</color>　存命 ").Append(alive)
              .Append("　死亡 ").Append(dead)
              .Append("　捕虜 ").Append(captive)
              .Append("　行方不明 ").Append(mia)
              .Append("　在野 ").Append(free).Append('\n');

            sb.Append("  <color=#9fb0c0>平均年齢</color> ");
            if (ageCount > 0) sb.Append((ageSum / (double)ageCount).ToString("0"));
            else sb.Append("—");
            sb.Append("　歳\n");

            sb.Append("  <color=#9fb0c0>職分</color>　");
            foreach (PersonVocation v in System.Enum.GetValues(typeof(PersonVocation)))
            {
                if (vocCount.TryGetValue(v, out int n) && n > 0)
                    sb.Append(v).Append(' ').Append(n).Append("　");
            }
            sb.Append('\n');
        }

        private void AppendBar(StringBuilder sb, string label, float v01, string colorHex)
        {
            v01 = Mathf.Clamp01(v01);
            int filled = Mathf.RoundToInt(v01 * barWidth);
            sb.Append(label).Append("  <color=").Append(colorHex).Append('>');
            for (int i = 0; i < barWidth; i++) sb.Append(i < filled ? '█' : '░');
            sb.Append("</color> ").Append(v01.ToString("0.00")).Append('\n');
        }

        // ===== UI 構築（EconomyObserverOverlay と同型・単一スクロールラベル版） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("PersonnelDynamicsObserverCanvas");
            overlayRoot.transform.SetParent(transform);
            Canvas canvas = overlayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            CanvasScaler scaler = overlayRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            overlayRoot.AddComponent<GraphicRaycaster>();

            panel = new GameObject("ObserverPanel");
            panel.transform.SetParent(overlayRoot.transform, false);
            RectTransform panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.sizeDelta = Vector2.zero;
            panelRT.anchoredPosition = Vector2.zero;
            Image dimImage = panel.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, dimAlpha);
            WindowChrome.MakeNonModal(dimImage);

            BuildContentPanel(panel.transform);
        }

        private void BuildContentPanel(Transform parent)
        {
            GameObject frame = new GameObject("ObserverFrame");
            frame.transform.SetParent(parent, false);
            RectTransform frameRT = frame.AddComponent<RectTransform>();
            frameRT.anchorMin = new Vector2(0f, 0.5f);
            frameRT.anchorMax = new Vector2(0f, 0.5f);
            frameRT.pivot = new Vector2(0f, 0.5f);
            frameRT.anchoredPosition = new Vector2(24f, 0f);
            frameRT.sizeDelta = new Vector2(panelWidth, panelMaxHeight);

            Image frameImg = frame.AddComponent<Image>();
            frameImg.color = panelColor;

            VerticalLayoutGroup vlg = frame.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 12, 12);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            WindowChrome.AddTitleBarLayout(frameRT, "人物動態", () => SetVisible(false));
            BuildScrollBody(frame.transform);
        }

        private void BuildScrollBody(Transform parent)
        {
            GameObject scrollObj = new GameObject("ObserverScrollRect");
            scrollObj.transform.SetParent(parent, false);
            scrollObj.AddComponent<RectTransform>();
            LayoutElement scrollLE = scrollObj.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;

            ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            RectTransform viewportRT = viewport.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.sizeDelta = Vector2.zero;
            viewportRT.anchoredPosition = Vector2.zero;
            viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRT;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRT = content.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentVlg = content.AddComponent<VerticalLayoutGroup>();
            contentVlg.padding = new RectOffset(8, 8, 4, 4);
            contentVlg.childAlignment = TextAnchor.UpperLeft;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = true;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;

            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRT;

            GameObject bodyObj = new GameObject("Body");
            bodyObj.transform.SetParent(content.transform, false);
            bodyLabel = bodyObj.AddComponent<TextMeshProUGUI>();
            bodyLabel.text = "";
            bodyLabel.fontSize = bodyFontSize;
            bodyLabel.color = new Color(0.9f, 0.93f, 0.96f);
            bodyLabel.alignment = TextAlignmentOptions.TopLeft;
            bodyLabel.richText = true;
            bodyLabel.raycastTarget = false;
            ApplyJapaneseFont(bodyLabel);
        }

        private static void ApplyJapaneseFont(TextMeshProUGUI tmp)
        {
            TMP_FontAsset jaFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (jaFont != null) tmp.font = jaFont;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }
    }
}
