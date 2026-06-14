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
    /// 財政オブザーバ（観測層・read-only）。<b>Alt+F キー</b>で開閉し、勢力ごとに**配線済みの財政**を集約して
    /// 毎フレームライブダンプする：歳入歳出・基礎収支／総合収支（<see cref="FiscalRules"/>）／国債・債務比率・利払い・金利／
    /// 債務スパイラル（利払い>基礎黒字＝複利膨張）／財政健全度／為替係数／予算配分（<see cref="BudgetRules"/>＝分野別シェア）。
    /// `GalaxyView.RunFiscalYearTick`（年次）が歳入→予算配分→歳出→国債→利払いを回す＝そのループの帰結を映す。
    /// 観測専用ゆえ既存フィールドのみ読む。操作はさせない＝**状態は変えない**。`HelpOverlay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class FiscalObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1102;
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
            if (Object.FindAnyObjectByType<FiscalObserverOverlay>() != null) return;
            new GameObject("FiscalObserverOverlay").AddComponent<FiscalObserverOverlay>();
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "財政");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.財政観測切替))
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

            sb.Append("<b>財政オブザーバ</b>　歳入歳出・国債/債務スパイラル・予算配分　(Alt+F で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            if (c == null || c.states == null || c.states.Count == 0)
            {
                sb.Append("\n<color=#ffcc66>戦役データ（StrategySession.Campaign）がありません。</color>\n");
                sb.Append("戦略マップ（GalaxyView）を起動すると、各勢力の財政がライブ表示されます。");
                return sb.ToString();
            }

            for (int i = 0; i < c.states.Count; i++)
            {
                FactionState s = c.states[i];
                if (s == null) continue;
                AppendFaction(sb, s);
            }

            AppendCrossRates(sb, c);

            sb.Append("\n<color=#6f8a9a>※ 歳入(税収)→予算配分→歳出。赤字は国債→利払い→翌年へ／国債は財政悪化で価格↓利回り↑。為替は通貨間の相対的強さ。</color>");
            return sb.ToString();
        }

        private void AppendFaction(StringBuilder sb, FactionState s)
        {
            float economy = CampaignRules.EconomyBase(s);
            FiscalState fs = s.fiscal;
            var p = FiscalRules.FiscalParams.Default;

            float primaryBalance = FiscalRules.PrimaryBalance(fs);
            float interestRate = FiscalRules.InterestRate(fs, economy, p);
            float interestPayment = FiscalRules.InterestPayment(fs, economy, p);
            float overallBalance = FiscalRules.OverallBalance(fs, economy, p);
            float debtRatio = FiscalRules.DebtRatio(fs, economy);
            bool debtSpiral = FiscalRules.IsDebtSpiral(fs, economy, p);
            float fiscalHealth = FiscalRules.FiscalHealthFactor(fs, economy, p);
            float exchangeRate = FiscalRules.ExchangeRateFactor(fs, economy, p);

            sb.Append('\n').Append("<color=#e7e0b0>◤ ").Append(s.faction).Append("</color>\n");

            sb.Append("  <color=#9fb0c0>国庫</color> <color=#ffe08a>").Append(s.treasury.ToString("#,0"))
              .Append("</color>　<color=#9fb0c0>歳入</color> ").Append(fs.revenue.ToString("0"))
              .Append("　<color=#9fb0c0>歳出</color> ").Append(fs.baseExpenditure.ToString("0"))
              .Append("　<color=#9fb0c0>基礎収支</color> ").Append(primaryBalance.ToString("+0;-0;0")).Append('\n');

            sb.Append("  <color=#9fb0c0>債務</color> ").Append(fs.debt.ToString("#,0"))
              .Append("　<color=#9fb0c0>利払い</color> ").Append(interestPayment.ToString("0"))
              .Append("　<color=#9fb0c0>金利</color> ").Append((interestRate * 100f).ToString("0.0")).Append('%')
              .Append("　<color=#9fb0c0>総合収支</color> ").Append(overallBalance.ToString("+0;-0;0")).Append('\n');

            AppendBar(sb, "  債務比率", Mathf.Clamp01(debtRatio), debtSpiral ? "#ff7a6a" : "#ffd28a");
            AppendBar(sb, "  財政健全度", Mathf.Clamp01(fiscalHealth), "#a0e0a0");

            // 通貨（#通貨 配線）：固有名＋為替＋物価/インフレ＋通貨供給（赤字の貨幣化で動く）。
            string curName = s.currency != null && !string.IsNullOrEmpty(s.currency.currencyName)
                ? s.currency.currencyName : CurrencyNames.For(s.faction);
            float fxShown = s.currency != null ? s.currency.exchangeRate : exchangeRate;
            sb.Append("  <color=#9fb0c0>通貨</color> <color=#c9a0ff>").Append(curName).Append("</color>")
              .Append("　<color=#9fb0c0>為替</color> ").Append(fxShown.ToString("0.00"))
              .Append("　<color=#6f8a9a>(1.0=基準・<1=通貨安)</color>\n");
            if (s.currency != null)
            {
                float infl = s.currency.inflationRate;
                sb.Append("  <color=#9fb0c0>物価</color> ").Append(s.currency.priceLevel.ToString("0.00"))
                  .Append("　<color=#9fb0c0>インフレ</color> <color=")
                  .Append(infl >= 0.5f ? "#ff7a6a" : (infl >= 0.1f ? "#ffd28a" : "#a0e0a0")).Append('>')
                  .Append((infl * 100f).ToString("0.0")).Append("%</color>")
                  .Append("　<color=#9fb0c0>通貨供給</color> ").Append(s.currency.moneySupply.ToString("#,0")).Append('\n');
                // 基軸通貨（#ReserveCurrencyRules）＋通貨改鋳（#CoinageRules）。
                sb.Append("  <color=#9fb0c0>基軸度</color> ").Append((s.currency.reserveStatus * 100f).ToString("0")).Append('%')
                  .Append("　<color=#9fb0c0>発行益</color> ").Append(s.currency.seigniorageIncome.ToString("#,0"))
                  .Append("　<color=#9fb0c0>品位</color> ").Append((s.currency.silverContent * 100f).ToString("0")).Append('%')
                  .Append("　<color=#9fb0c0>信認</color> <color=")
                  .Append(s.currency.publicTrust < 0.5f ? "#ff7a6a" : (s.currency.publicTrust < 0.8f ? "#ffd28a" : "#a0e0a0")).Append('>')
                  .Append((s.currency.publicTrust * 100f).ToString("0")).Append("%</color>\n");
                if (InflationRules.IsHyperinflation(infl))
                    sb.Append("  <color=#ff7a6a>⚠ ハイパーインフレ</color>\n");
                if (s.currency.publicTrust < 0.5f)
                    sb.Append("  <color=#ff7a6a>⚠ 改鋳の露見＝通貨信認の崩壊（グレシャム）</color>\n");
            }

            // 国債（#161/#185 配線）：額面=債務／価格・利回りは財政悪化で逆相関に動く。
            Bond bond = s.sovereignBond;
            if (bond != null)
            {
                sb.Append("  <color=#9fb0c0>国債</color> 額面 ").Append(bond.faceValue.ToString("#,0"))
                  .Append("　価格 ").Append(bond.price.ToString("0.00"))
                  .Append("　利回り ").Append((BondMarketRules.CurrentYield(bond) * 100f).ToString("0.0")).Append('%');
                if (bond.defaultRisk > 0.01f)
                    sb.Append("　<color=").Append(bond.defaultRisk >= 0.5f ? "#ff7a6a" : "#ffd28a").Append('>')
                      .Append("信用リスク ").Append((bond.defaultRisk * 100f).ToString("0")).Append("%</color>");
                sb.Append('\n');
            }

            // 金融当局・研究・動員（P0/P1 配線）：中銀政策金利・技術水準・徴兵/年・金融危機。
            GalaxyView gvv = GalaxyView.Active;
            if (gvv != null)
            {
                CentralBank cb = gvv.GetCentralBank(s.faction);
                ResearchState rsx = gvv.GetResearch(s.faction);
                sb.Append("  <color=#9fb0c0>中銀</color> 政策金利 ").Append(cb != null ? (cb.policyRate * 100f).ToString("0.0") : "—").Append('%')
                  .Append("　<color=#9fb0c0>技術</color> ").Append(rsx != null ? rsx.techLevel.ToString("0.0") : "—")
                  .Append("　<color=#9fb0c0>徴兵/年</color> ").Append(gvv.GetLastRecruits(s.faction).ToString("#,0"));
                if (gvv.IsFinancialCrisis(s.faction)) sb.Append("　<color=#ff7a6a>⚠ 金融危機</color>");
                sb.Append('\n');
            }

            if (debtSpiral)
                sb.Append("  <color=#ff7a6a>⚠ 債務スパイラル（利払い>基礎黒字＝複利膨張）</color>\n");

            NationalBudget b = s.budget;
            float total = BudgetRules.Total(b);
            if (total > 0f)
            {
                sb.Append("  <color=#9fb0c0>予算配分</color>\n");
                foreach (BudgetCategory cat in System.Enum.GetValues(typeof(BudgetCategory)))
                    AppendBar(sb, "  " + cat, BudgetRules.Share(b, cat), "#7fd4ff");
            }
        }

        /// <summary>為替クロスレート（#為替）＝通貨ペアの相対的強さ。通貨を持つ勢力の各順序対を1行ずつ。</summary>
        private void AppendCrossRates(StringBuilder sb, CampaignState c)
        {
            var withCur = new System.Collections.Generic.List<FactionState>();
            for (int i = 0; i < c.states.Count; i++)
                if (c.states[i] != null && c.states[i].currency != null) withCur.Add(c.states[i]);
            if (withCur.Count < 2) return;

            sb.Append('\n').Append("<color=#e7e0b0>◤ 為替クロスレート</color>\n");
            for (int a = 0; a < withCur.Count; a++)
                for (int b = 0; b < withCur.Count; b++)
                {
                    if (a == b) continue;
                    CurrencyState ca = withCur[a].currency, cb = withCur[b].currency;
                    float rate = CurrencyRules.CrossRate(ca, cb);
                    sb.Append("  1 <color=#c9a0ff>").Append(ca.currencyName).Append("</color> = <color=#ffe08a>")
                      .Append(rate.ToString("0.000")).Append("</color> ").Append(cb.currencyName).Append('\n');
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

            overlayRoot = new GameObject("FiscalObserverCanvas");
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

            WindowChrome.AddTitleBarLayout(frameRT, "財政", () => SetVisible(false));
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
