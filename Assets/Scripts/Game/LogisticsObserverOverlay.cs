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
    /// 兵站オブザーバ（観測層・read-only）。<b>Q キー</b>で開閉し、勢力ごとの**配線済みの兵站**を毎フレームライブダンプする：
    /// 資源備蓄（<see cref="GalaxyView.GetStateStockpile"/>＝物資/弾薬/燃料・#2077）／所有惑星からの産出
    /// （<see cref="ResourceProductionRules.ProvinceRate"/>＋希少資源 <see cref="StrategicResourceRules.ProvinceRate"/>）／
    /// 軍需要（<see cref="MilitaryDemandRules.AggregateDemand"/>・#2049）／艦隊の補給レディネス（<see cref="StrategicFleet.supply"/>）と
    /// 補給線の途絶（後背が自勢力領か）。`GalaxyView` の日次/年次 Tick が回している分＝盤面で実際に動く兵站だけを映す。
    /// 経済(E)は国庫/税率だけ＝3資源備蓄・補給線の途絶・前線艦隊の枯渇は本オブザーバが担う（「滅びの時計」#94 の可視化）。
    /// 操作はさせない＝**観測専用＝状態は変えない**。`HelpOverlay`/`TimeDisplay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class LogisticsObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1098;
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
            if (Object.FindAnyObjectByType<LogisticsObserverOverlay>() != null) return;
            new GameObject("LogisticsObserverOverlay").AddComponent<LogisticsObserverOverlay>();
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "兵站");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.兵站観測切替))
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
            CampaignState c = StrategySession.Campaign;
            GalaxyMap map = StrategySession.Map ?? (c != null ? c.map : null);

            sb.Append("<b>兵站オブザーバ</b>　資源備蓄/産出・軍需要・艦隊補給/枯渇　(Q で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            if (c == null || c.states == null || c.states.Count == 0 || map == null)
            {
                sb.Append("\n<color=#ffcc66>戦役データ（StrategySession.Campaign / Map）がありません。</color>\n");
                sb.Append("戦略マップ（GalaxyView）を起動すると、各勢力の資源産出・軍需要・艦隊補給がライブ表示されます。");
                return sb.ToString();
            }

            for (int i = 0; i < c.states.Count; i++)
            {
                FactionState s = c.states[i];
                if (s == null) continue;
                AppendFaction(sb, map, s.faction);
            }

            sb.Append("\n<color=#6f8a9a>※ 補給は後背が自勢力領なら回復・敵に後背を取られると枯渇＋損耗（滅びの時計 #94）。備蓄は Strategy でのみ公開。</color>");
            return sb.ToString();
        }

        private void AppendFaction(StringBuilder sb, GalaxyMap map, Faction fac)
        {
            sb.Append('\n').Append("<color=#e7e0b0>◤ ").Append(fac).Append("</color>\n");

            // --- 資源備蓄（国庫・#2077。GalaxyView の private 実体を read-only アクセサ経由で） ---
            GalaxyView gv = GalaxyView.Active;
            ResourceStockpile stock = gv != null ? gv.GetStateStockpile(fac) : null;
            if (stock != null)
            {
                sb.Append("  <color=#9fb0c0>備蓄</color>　物資 <color=#a0e0a0>").Append(stock.supplies.ToString("#,0"))
                  .Append("</color>　弾薬 <color=#ffd28a>").Append(stock.ammo.ToString("#,0"))
                  .Append("</color>　燃料 <color=#7fd4ff>").Append(stock.fuel.ToString("#,0")).Append("</color>");
                if (stock.IsDepleted) sb.Append("　<color=#ff7a6a>⚠ 枯渇</color>");
                sb.Append('\n');
            }
            else
            {
                sb.Append("  <color=#9aa7b2>備蓄＝（会戦中 or 未生成＝非公開。Strategy で日次産出が回ると表示）</color>\n");
            }

            // --- 所有惑星からの産出（産出/秒・希少資源含む） ---
            float prodSup = 0f, prodAmmo = 0f, prodFuel = 0f, prodRare = 0f;
            int ownedCount = 0;
            var provinces = StrategySession.Provinces;
            if (provinces != null)
            {
                foreach (var kv in provinces)
                {
                    Province prov = kv.Value;
                    if (prov == null) continue;
                    StarSystem sys = map.GetSystem(prov.systemId);
                    if (sys == null || sys.owner != fac) continue;
                    ownedCount++;
                    prodSup  += ResourceProductionRules.ProvinceRate(prov, ResourceType.物資);
                    prodAmmo += ResourceProductionRules.ProvinceRate(prov, ResourceType.弾薬);
                    prodFuel += ResourceProductionRules.ProvinceRate(prov, ResourceType.燃料);
                    prodRare += StrategicResourceRules.ProvinceRate(prov);
                }
            }
            sb.Append("  <color=#9fb0c0>産出/秒</color>　物資 ").Append(prodSup.ToString("0.#"))
              .Append("　弾薬 ").Append(prodAmmo.ToString("0.#"))
              .Append("　燃料 ").Append(prodFuel.ToString("0.#"))
              .Append("　希少 ").Append(prodRare.ToString("0.##"))
              .Append("　<color=#6f8a9a>(惑星 ").Append(ownedCount).Append(")</color>\n");

            // --- 希少資源の備蓄（#178 配線・種類別＝鉱床のある惑星から偏って貯まる） ---
            StrategicResourceStockpile rare = gv != null ? gv.GetStrategicStockpile(fac) : null;
            if (rare != null && !rare.IsEmpty)
            {
                sb.Append("  <color=#c9a0ff>希少資源</color>　");
                foreach (StrategicResourceType t in StrategicResourceRules.All)
                {
                    float v = rare.Get(t);
                    if (v <= 0f) continue;
                    sb.Append(t).Append(' ').Append(v.ToString("#,0.#")).Append("　");
                }
                sb.Append('\n');
            }
            else if (gv != null)
            {
                sb.Append("  <color=#9aa7b2>希少資源＝まだ備蓄なし（鉱床のある所有惑星が無い／産出待ち）</color>\n");
            }

            // --- 軍需要＋艦隊補給（StrategicFleet・#2049） ---
            var fleets = new List<StrategicFleet>();
            var reg = StrategySession.Reg;
            float supplySum = 0f; int cutOff = 0;
            if (reg != null && reg.fleets != null)
            {
                for (int i = 0; i < reg.fleets.Count; i++)
                {
                    StrategicFleet f = reg.fleets[i];
                    if (f == null || f.faction != fac || f.strength <= 0f) continue;
                    fleets.Add(f);
                    supplySum += Mathf.Clamp01(f.supply);
                    StarSystem sys = map.GetSystem(f.currentSystemId);
                    if (sys == null || sys.owner != fac) cutOff++;
                }
            }
            float demSup  = MilitaryDemandRules.AggregateDemand(fleets, ResourceType.物資);
            float demAmmo = MilitaryDemandRules.AggregateDemand(fleets, ResourceType.弾薬);
            float demFuel = MilitaryDemandRules.AggregateDemand(fleets, ResourceType.燃料);
            sb.Append("  <color=#9fb0c0>軍需要</color>　物資 ").Append(demSup.ToString("0.#"))
              .Append("　弾薬 ").Append(demAmmo.ToString("0.#"))
              .Append("　燃料 ").Append(demFuel.ToString("0.#")).Append('\n');

            if (fleets.Count == 0)
            {
                sb.Append("  <color=#9aa7b2>艦隊なし</color>\n");
            }
            else
            {
                float avg = supplySum / fleets.Count;
                AppendBar(sb, "  艦隊補給(平均)", avg, avg < 0.34f ? "#ff7a6a" : (avg < 0.7f ? "#ffd28a" : "#a0e0a0"));
                sb.Append("    <color=#6f8a9a>艦隊 ").Append(fleets.Count);
                if (cutOff > 0) sb.Append("　<color=#ff7a6a>補給線途絶 ").Append(cutOff).Append("</color>");
                sb.Append("</color>\n");
            }
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

            overlayRoot = new GameObject("LogisticsObserverCanvas");
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

            WindowChrome.AddTitleBarLayout(frameRT, "兵站", () => SetVisible(false));
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
