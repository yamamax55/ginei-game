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
    /// 軍動態オブザーバ（観測層・read-only）。<b>Alt+V キー</b>で開閉し、勢力ごとに<b>艦隊の練度</b>（#練度・`VeterancyRules`）／
    /// <b>軍需補給</b>（#2049・`MilitarySupplyRules`）／<b>兵器産業</b>（#2020・`WeaponsRules`）を集約してライブダンプする。
    /// 歴戦の艦隊が育ち、備蓄が払底すれば前線が干上がり、兵器メーカーが戦力を供給する――軍の年次Tickの帰結を映す。
    /// 集計は `GalaxyView.Active` の read-only アクセサ（`StrategicFleets`/`FleetExperience`/`SupplyReadinessOf`/`GetWeaponsManufacturer`）。
    /// **観測専用＝状態は変えない**。`WealthObserverOverlay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class MilitaryDynamicsObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1113;
        public float dimAlpha = 0.55f;
        public float panelWidth = 1000f;
        public float panelMaxHeight = 900f;
        public Color panelColor = new Color(0.05f, 0.05f, 0.04f, 0.96f);
        public float bodyFontSize = 20f;

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
            if (Object.FindAnyObjectByType<MilitaryDynamicsObserverOverlay>() != null) return;
            new GameObject("MilitaryDynamicsObserverOverlay").AddComponent<MilitaryDynamicsObserverOverlay>();
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "軍動態");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.軍動態観測切替)) Toggle();
            if (panel != null && panel.activeSelf && bodyLabel != null)
                bodyLabel.text = BuildDump();
        }

        public void Toggle() => SetVisible(panel != null && !panel.activeSelf);
        public void SetVisible(bool visible) { if (panel != null) panel.SetActive(visible); }

        // ===== ダンプ本体 =====

        private string BuildDump()
        {
            var sb = new StringBuilder(2048);
            sb.Append("<b>軍動態オブザーバ</b>　艦隊の練度・軍需補給・兵器産業　(Alt+V で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            CampaignState c = StrategySession.Campaign;
            GalaxyView gv = GalaxyView.Active;
            if (gv == null)
            {
                sb.Append("\n<color=#ffcc66>軍動態は戦略マップ（GalaxyView）でのみ観測できます（会戦中は非公開）。</color>");
                return sb.ToString();
            }
            if (c == null || c.states == null || c.states.Count == 0)
            {
                sb.Append("\n<color=#ffcc66>戦役データ（StrategySession.Campaign）がありません。</color>");
                return sb.ToString();
            }

            var fleets = gv.StrategicFleets;
            for (int i = 0; i < c.states.Count; i++)
            {
                FactionState s = c.states[i];
                if (s == null) continue;
                AppendFaction(sb, gv, s.faction, fleets);
            }
            sb.Append("\n<color=#6f8a9a>※ 練度＝歴戦の艦隊が精強化（会戦#106 へ倍率）。補給＝備蓄から軍需を消費し払底で損耗。兵器産業＝FleetPool#148 へ戦力供給。</color>");
            return sb.ToString();
        }

        private void AppendFaction(StringBuilder sb, GalaxyView gv, Faction fac, IReadOnlyList<StrategicFleet> fleets)
        {
            sb.Append('\n').Append("<color=#e7e0b0>◤ ").Append(fac).Append("</color>\n");

            // 練度：勢力の艦隊を段階別に集計＋平均XP。
            int rookie = 0, regular = 0, elite = 0, veteran = 0, n = 0;
            float sumXp = 0f;
            if (fleets != null)
            {
                for (int i = 0; i < fleets.Count; i++)
                {
                    StrategicFleet f = fleets[i];
                    if (f == null || f.faction != fac) continue;
                    n++;
                    sumXp += gv.FleetXp(f.id);
                    switch (gv.FleetExperience(f.id))
                    {
                        case ExperienceLevel.新兵: rookie++; break;
                        case ExperienceLevel.一般: regular++; break;
                        case ExperienceLevel.精鋭: elite++; break;
                        case ExperienceLevel.古参: veteran++; break;
                    }
                }
            }
            sb.Append("  <color=#9fb0c0>練度</color> 艦隊 ").Append(n)
              .Append("　新兵 ").Append(rookie).Append("／一般 ").Append(regular)
              .Append("／<color=#7fd4ff>精鋭 ").Append(elite).Append("</color>／<color=#ffe08a>古参 ").Append(veteran).Append("</color>")
              .Append("　平均XP ").Append((n > 0 ? sumXp / n : 0f).ToString("0")).Append('\n');

            // 補給：勢力の平均補給レディネス。
            float readiness = gv.SupplyReadinessOf(fac);
            string rcol = readiness < 0.5f ? "#ff7a6a" : (readiness < 0.8f ? "#ffd28a" : "#a0e0a0");
            sb.Append("  <color=#9fb0c0>軍需補給</color> <color=").Append(rcol).Append('>')
              .Append(Mathf.RoundToInt(readiness * 100f)).Append("%</color>")
              .Append(readiness < 0.5f ? "　<color=#ff7a6a>⚠ 払底（前線が干上がる）</color>" : "").Append('\n');

            // 兵器産業：兵器メーカーの R&D / 性能。
            WeaponsManufacturer maker = gv.GetWeaponsManufacturer(fac);
            if (maker != null)
            {
                float perf = WeaponsRules.WeaponPerformance(maker.baseWeaponSpec, maker.rdLevel, 0.1f);
                sb.Append("  <color=#9fb0c0>兵器産業</color> ").Append(maker.name)
                  .Append("　R&D ").Append(maker.rdLevel.ToString("0.0"))
                  .Append("　性能 ").Append(perf.ToString("0"))
                  .Append("　生産能力 ").Append(maker.productionCapacity.ToString("0")).Append('\n');
            }
        }

        // ===== UI 構築（WealthObserverOverlay と同型・単一スクロールラベル版） =====

        private void BuildUI()
        {
            EnsureEventSystem();
            overlayRoot = new GameObject("MilitaryDynamicsObserverCanvas");
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
            panelRT.anchorMin = Vector2.zero; panelRT.anchorMax = Vector2.one;
            panelRT.sizeDelta = Vector2.zero; panelRT.anchoredPosition = Vector2.zero;
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
            frame.AddComponent<Image>().color = panelColor;

            VerticalLayoutGroup vlg = frame.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 12, 12);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            WindowChrome.AddTitleBarLayout(frameRT, "軍動態", () => SetVisible(false));
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
            scrollRect.horizontal = false; scrollRect.vertical = true; scrollRect.scrollSensitivity = 30f;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            RectTransform viewportRT = viewport.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero; viewportRT.anchorMax = Vector2.one;
            viewportRT.sizeDelta = Vector2.zero; viewportRT.anchoredPosition = Vector2.zero;
            viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRT;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRT = content.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f); contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero; contentRT.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentVlg = content.AddComponent<VerticalLayoutGroup>();
            contentVlg.padding = new RectOffset(8, 8, 4, 4);
            contentVlg.childAlignment = TextAnchor.UpperLeft;
            contentVlg.childControlWidth = true; contentVlg.childControlHeight = true;
            contentVlg.childForceExpandWidth = true; contentVlg.childForceExpandHeight = false;

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
            bodyLabel.richText = true; bodyLabel.raycastTarget = false;
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
