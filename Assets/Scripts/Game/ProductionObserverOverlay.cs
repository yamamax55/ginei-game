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
    /// 生産・流通オブザーバ（観測層・read-only）。<b>Alt+P キー</b>で開閉し、勢力ごとに所有惑星の**生産チェーン・消費財BOM在庫**を集約して
    /// 毎フレームライブダンプする：生産チェーン（森林→木材→建材→住宅・#2091）／消費財BOM（食品/衣類等・#2098）。
    /// `GalaxyView` の年次Tickが生産・消費を回す＝そのループの帰結（在庫）を映す。
    /// 観測専用ゆえ既存フィールドのみ読む。操作はさせない＝**状態は変えない**。`HelpOverlay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class ProductionObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1103;
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
            if (Object.FindAnyObjectByType<ProductionObserverOverlay>() != null) return;
            new GameObject("ProductionObserverOverlay").AddComponent<ProductionObserverOverlay>();
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "生産");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.生産観測切替))
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
            GalaxyView gv = GalaxyView.Active;

            sb.Append("<b>生産・流通オブザーバ</b>　生産チェーン・消費財BOM在庫　(Alt+P で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            if (gv == null)
            {
                sb.Append("\n<color=#ffcc66>生産・流通の在庫は戦略マップ（GalaxyView）でのみ観測できます（会戦中は非公開）。</color>");
                return sb.ToString();
            }

            if (c == null || c.states == null || c.states.Count == 0 || map == null)
            {
                sb.Append("\n<color=#ffcc66>生産・流通の在庫は戦略マップ（GalaxyView）でのみ観測できます（会戦中は非公開）。</color>");
                return sb.ToString();
            }

            for (int i = 0; i < c.states.Count; i++)
            {
                FactionState s = c.states[i];
                if (s == null) continue;
                AppendFaction(sb, map, gv, s.faction);
            }

            sb.Append("\n<color=#6f8a9a>※ 森林→木材→建材→住宅の連鎖（#2091）と食品/衣類等の消費財BOM（#2098）と市場価格（#179＝供給/需要で創発）を年次Tickで回す。</color>");
            return sb.ToString();
        }

        private void AppendFaction(StringBuilder sb, GalaxyMap map, GalaxyView gv, Faction fac)
        {
            var provinces = StrategySession.Provinces;
            int ownedCount = 0;
            float forest = 0f, wood = 0f, materials = 0f, housing = 0f;

            // 消費財BOM(#2098) の品目集計
            List<Commodity> consumerGoods = CommodityCatalog.ByCategory(CommodityCategory.消費財);
            float[] consumerTotals = consumerGoods != null ? new float[consumerGoods.Count] : null;

            if (provinces != null)
            {
                foreach (var kv in provinces)
                {
                    Province prov = kv.Value;
                    if (prov == null) continue;
                    StarSystem sys = map.GetSystem(prov.systemId);
                    if (sys == null || sys.owner != fac) continue;
                    ownedCount++;

                    ChainStock cs = gv.GetChainStock(prov.systemId);
                    if (cs != null)
                    {
                        forest    += cs.forest;
                        wood      += cs.wood;
                        materials += cs.materials;
                        housing   += cs.housing;
                    }

                    if (consumerGoods != null)
                    {
                        CommodityStock stock = gv.GetCommodityStock(prov.systemId);
                        if (stock != null)
                        {
                            for (int g = 0; g < consumerGoods.Count; g++)
                                consumerTotals[g] += stock.Get(consumerGoods[g].id);
                        }
                    }
                }
            }

            sb.Append('\n').Append("<color=#e7e0b0>◤ ").Append(fac).Append("</color>");
            sb.Append("　<color=#6f8a9a>(惑星 ").Append(ownedCount).Append(")</color>\n");
            if (ownedCount == 0)
            {
                sb.Append("  <color=#9aa7b2>（所有惑星なし）</color>\n");
                return;
            }

            sb.Append("  <color=#9fb0c0>生産チェーン</color>　森林 ").Append(forest.ToString("#,0"))
              .Append("　木材 ").Append(wood.ToString("#,0"))
              .Append("　建材 ").Append(materials.ToString("#,0"))
              .Append("　住宅 ").Append(housing.ToString("#,0")).Append('\n');

            if (consumerGoods != null && consumerGoods.Count > 0)
            {
                sb.Append("  <color=#9fb0c0>消費財</color>　");
                for (int g = 0; g < consumerGoods.Count; g++)
                {
                    if (g > 0) sb.Append("　");
                    sb.Append(consumerGoods[g].name).Append(' ').Append(consumerTotals[g].ToString("#,0"));
                }
                sb.Append('\n');
            }
            else
            {
                sb.Append("  <color=#9aa7b2>消費財BOM 未生成</color>\n");
            }

            // --- 市場（M-1 #179 配線・需給で動く価格） ---
            sb.Append("  <color=#9fb0c0>市場</color>　");
            bool anyMarket = false;
            foreach (GoodType t in System.Enum.GetValues(typeof(GoodType)))
            {
                Market m = gv.GetMarket(fac, t);
                if (m == null) continue;
                anyMarket = true;
                string col = m.demand > m.supply * 1.05f ? "#ff9a8a" : (m.supply > m.demand * 1.05f ? "#a0e0a0" : "#ffe08a");
                sb.Append(t).Append(" <color=").Append(col).Append('>').Append(m.price.ToString("0.00")).Append("</color>")
                  .Append("<color=#6f8a9a>(供").Append(m.supply.ToString("0.#")).Append("/需").Append(m.demand.ToString("0.#")).Append(")</color>　");
            }
            if (!anyMarket) sb.Append("<color=#9aa7b2>未生成</color>");
            sb.Append('\n');

            // --- 企業（#1022 配線・市場価格で生産・雇用・利潤） ---
            var firms = gv.GetEnterprises(fac);
            if (firms != null && firms.Count > 0)
            {
                sb.Append("  <color=#9fb0c0>企業</color>\n");
                for (int e = 0; e < firms.Count; e++)
                {
                    Enterprise firm = firms[e];
                    if (firm == null) continue;
                    GoodType good = GoodForSector(firm.sector);
                    Market m = gv.GetMarket(fac, good);
                    float price = m != null ? m.price : 1f;
                    float profit = EnterpriseRules.Profit(firm, price);
                    string pcol = profit >= 0f ? "#a0e0a0" : "#ff9a8a";
                    sb.Append("   ").Append(firm.name).Append("（").Append(firm.ownership).Append("）")
                      .Append(" 雇用 ").Append(firm.employees.ToString("#,0"))
                      .Append("　資本 ").Append(firm.capital.ToString("#,0"))
                      .Append("　産出 ").Append(EnterpriseRules.Output(firm).ToString("#,0"))
                      .Append("　利潤 <color=").Append(pcol).Append('>').Append(profit.ToString("+#,0;-#,0;0")).Append("</color>\n");
                }
            }

            // --- 株式市場（#185 配線・私有企業の上場銘柄＝利潤→株価） ---
            var stocks = gv.GetListings(fac);
            if (stocks != null && stocks.Count > 0)
            {
                float index = StockMarketSystemRules.MarketIndex(stocks);
                float senti = StockMarketSystemRules.MarketSentiment(stocks);
                string scol = senti >= 0.6f ? "#a0e0a0" : (senti <= 0.4f ? "#ff9a8a" : "#ffe08a");
                sb.Append("  <color=#9fb0c0>株式市場</color>　指数 ").Append(index.ToString("#,0"))
                  .Append("　心理 <color=").Append(scol).Append('>').Append((senti * 100f).ToString("0")).Append("%</color>\n");
                for (int i = 0; i < stocks.Count; i++)
                {
                    Listing l = stocks[i];
                    if (l == null || l.stock == null) continue;
                    float yld = StockMarketRules.DividendYield(l.stock.dividend, l.stock.sharePrice);
                    sb.Append("   ").Append(l.name).Append(" 株価 ").Append(l.stock.sharePrice.ToString("0.00"))
                      .Append("　配当利回り ").Append((yld * 100f).ToString("0.0")).Append("%\n");
                }
            }

            // --- 商社＝総合商社（FRM-5 #1027 配線・仲介/裁定/与信/権益で稼ぐ独立エージェント・フェザーン #160） ---
            TradingHouse th = gv.GetTradingHouse(fac);
            if (th != null)
            {
                string ccol = th.capital < 0f ? "#ff7a6a" : "#a0e0a0";
                sb.Append("  <color=#9fb0c0>商社</color> ").Append(th.name)
                  .Append("　自己資本 <color=").Append(ccol).Append('>').Append(th.capital.ToString("#,0")).Append("</color>")
                  .Append("　在庫 ").Append(th.inventory.ToString("#,0"))
                  .Append("　与信 ").Append(th.tradeCredit.ToString("#,0"))
                  .Append("　権益 ").Append(th.resourceStakes.ToString("#,0"))
                  .Append("　事業投資 ").Append(th.businessStakes.ToString("#,0"));
                if (th.capital < 0f) sb.Append("　<color=#ff7a6a>⚠ 破綻</color>");
                sb.Append('\n');
            }

            // --- 不動産会社（#2019 配線・賃貸/売買/開発・地価バブル RE-5） ---
            RealEstateCompany re = gv.GetRealEstateCompany(fac);
            if (re != null)
            {
                float effRent = RealEstateRules.EffectiveRent(re.grossRent, re.vacancyRate);
                float noi = RealEstateRules.NetOperatingIncome(effRent, effRent * 0.3f);
                float p2r = RealEstateRules.PriceToRentRatio(re.propertyValue, re.grossRent);
                bool bubble = RealEstateRules.IsBubble(p2r, RealEstateRules.DefaultBubbleThreshold);
                sb.Append("  <color=#9fb0c0>不動産</color> ").Append(re.name)
                  .Append("　地価 ").Append(re.landValue.ToString("#,0"))
                  .Append("　物件価値 ").Append(re.propertyValue.ToString("#,0"))
                  .Append("　NOI ").Append(noi.ToString("#,0"))
                  .Append("　空室 ").Append((re.vacancyRate * 100f).ToString("0")).Append('%')
                  .Append("　価格/賃料 <color=").Append(bubble ? "#ff7a6a" : (p2r > 20f ? "#ffd28a" : "#a0e0a0")).Append('>')
                  .Append(p2r.ToString("0")).Append("倍</color>");
                if (bubble) sb.Append("　<color=#ff7a6a>⚠ バブル</color>");
                sb.Append('\n');
            }
        }

        /// <summary>セクター（産業）が売る市場財（観測表示用・GalaxyView の対応と一致させる）。</summary>
        private static GoodType GoodForSector(SystemType sector)
        {
            switch (sector)
            {
                case SystemType.鉱業: return GoodType.燃料;
                case SystemType.居住: return GoodType.奢侈品;
                default: return GoodType.物資;
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

            overlayRoot = new GameObject("ProductionObserverCanvas");
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

            WindowChrome.AddTitleBarLayout(frameRT, "生産", () => SetVisible(false));
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
