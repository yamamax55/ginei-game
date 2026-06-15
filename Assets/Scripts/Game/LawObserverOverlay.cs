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
    /// 法令オブザーバ（観測層・read-only・LAW #2126）。<b>L キー</b>または上メニュー「法令」で開閉し、勢力ごとの
    /// <b>法の支配</b>（<see cref="LegalSystem"/> の4要素＝司法独立/法の前の平等/権力制約/予測可能性、合成 <see cref="RuleOfLawRules.RuleOfLawIndex"/>・
    /// 「法治どまり」判定 <see cref="RuleOfLawRules.IsRuleByLawOnly"/>）と、<b>治安</b>（所有惑星の失業#110・貧困#181 から
    /// <see cref="LawTickRules.TickProvince"/> で犯罪圧力→公共秩序→抑圧度を集約）を毎フレームライブダンプする。
    /// `GalaxyView.RunLawTick`（年次）が実際に回しているデモ法体系（同盟＝法の支配／帝国＝法治）と同じ計算を映す。
    /// 操作はさせない＝<b>観測専用＝状態は変えない</b>。<see cref="EconomyObserverOverlay"/>（E・経済）の法令版。
    /// `HelpOverlay`/`TimeDisplay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class LawObserverOverlay : MonoBehaviour
    {
        // ===== 調整可能なパラメーター =====

        [Header("外観")]
        [Tooltip("オーバーレイ Canvas の描画順（他UIより手前）")]
        public int canvasSortingOrder = 1093;

        [Tooltip("背景ディマーの不透明度（0〜1）")]
        public float dimAlpha = 0.55f;

        [Tooltip("パネルの幅（ピクセル）")]
        public float panelWidth = 980f;

        [Tooltip("パネルの最大高さ（ピクセル）")]
        public float panelMaxHeight = 900f;

        [Tooltip("パネル背景色")]
        public Color panelColor = new Color(0.05f, 0.05f, 0.04f, 0.96f);

        [Tooltip("本文のフォントサイズ")]
        public float bodyFontSize = 20f;

        [Tooltip("バー（0..1可視化）の桁数")]
        public int barWidth = 14;

        // デモ警察力／格差（RunLawTick と同じ＝観測が実挙動と一致する）。
        private const float DemoEnforcement = 0.6f;
        private const float DemoInequality = 0.3f;

        // ===== 内部状態 =====

        private GameObject overlayRoot;
        private GameObject panel;
        private TextMeshProUGUI bodyLabel;
        private object escWindowToken; // UIWindowStack 登録トークン（#ウィンドウESC）

        // タブ（概況／憲法／法律／政令）。概況＝法の支配・治安の数値、他＝法令カタログの条文一覧。
        private enum LawTab { 概況, 憲法, 法律, 政令 }
        private LawTab activeTab = LawTab.概況;
        private readonly System.Collections.Generic.Dictionary<LawTab, Image> tabBgs = new System.Collections.Generic.Dictionary<LawTab, Image>();

        // ===== 自動生成エントリーポイント（HelpOverlay と同型） =====

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; // 二重購読防止
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        /// <summary>戦略/会戦シーンにオブザーバが無ければ生成する（重複生成ガード）。</summary>
        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Strategy" && scene.name != "Battle") return;
            if (Object.FindAnyObjectByType<LawObserverOverlay>() != null) return;
            new GameObject("LawObserverOverlay").AddComponent<LawObserverOverlay>();
        }

        // ===== Unity ライフサイクル =====

        private void Awake()
        {
            BuildUI();
            SetVisible(false); // 初期は非表示
            // ESC は UIWindowStack 経由で「手前から閉じる」（L は従来どおり開閉トグル）。
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "法令");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.法令観測切替))
                Toggle();

            if (panel != null && panel.activeSelf && bodyLabel != null)
                bodyLabel.text = activeTab == LawTab.概況 ? BuildDump() : BuildLawList(ToCategory(activeTab));
        }

        private static LawCategory ToCategory(LawTab t) => t switch
        {
            LawTab.憲法 => LawCategory.憲法,
            LawTab.法律 => LawCategory.法律,
            _ => LawCategory.政令,
        };

        // ===== 公開API =====

        public void Toggle() => SetVisible(panel != null && !panel.activeSelf);

        public void SetVisible(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        // ===== ダンプ本体 =====

        private string BuildDump()
        {
            var sb = new StringBuilder(2048);

            sb.Append("<b>法令オブザーバ</b>　法の支配 / 法治・治安（犯罪→秩序→抑圧）　(L で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            CampaignState c = StrategySession.Campaign;
            if (c == null || c.states == null || c.states.Count == 0)
            {
                sb.Append("\n<color=#ffcc66>戦役データ（StrategySession.Campaign）がまだありません。</color>\n");
                sb.Append("戦略マップ（GalaxyView）を起動すると各勢力の法の支配・治安がここに表示されます。");
                return sb.ToString();
            }

            for (int i = 0; i < c.states.Count; i++)
            {
                FactionState s = c.states[i];
                if (s == null) continue;
                AppendFaction(sb, s.faction);
            }

            sb.Append("\n<color=#6f8a9a>※ 権力制約が低いと「法治どまり」＝法はあるが権力を縛らない。法の支配なき取締りは抑圧化し正統性を蝕む。</color>");
            return sb.ToString();
        }

        /// <summary>憲法／法律／政令タブ：勢力ごとに、その政体に適用される法令（共通＋固有）を一覧する（<see cref="LawCatalog"/>）。</summary>
        private string BuildLawList(LawCategory category)
        {
            var sb = new StringBuilder(2048);
            sb.Append("<b>法令オブザーバ ― ").Append(category).Append("</b>　(L で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            CampaignState c = StrategySession.Campaign;
            if (c == null || c.states == null || c.states.Count == 0)
            {
                // 戦役が無くても法令カタログ自体は見せる（民主/専制の両体系）。
                AppendLawSection(sb, "民主政体（自由惑星同盟 型）", category, "民主");
                AppendLawSection(sb, "専制政体（銀河帝国 型）", category, "専制");
                sb.Append("\n<color=#6f8a9a>※ 戦役（GalaxyView）起動中は勢力ごとに表示されます。</color>");
                return sb.ToString();
            }

            for (int i = 0; i < c.states.Count; i++)
            {
                FactionState s = c.states[i];
                if (s == null) continue;
                string ideology = s.faction == Faction.同盟 ? "民主" : "専制";
                AppendLawSection(sb, s.faction.ToString(), category, ideology);
            }

            sb.Append("\n<color=#6f8a9a>※ 憲法＞法律＞政令 の効力順。共通法はどの政体にも適用され、固有法は政体（民主/専制）で異なる。</color>");
            return sb.ToString();
        }

        private void AppendLawSection(StringBuilder sb, string header, LawCategory category, string ideology)
        {
            sb.Append('\n').Append("<color=#e7e0b0>◤ ").Append(header).Append("</color>\n");
            var list = LawCatalog.For(category, ideology);
            if (list.Count == 0)
            {
                sb.Append("  <color=#9aa7b2>（該当する法令なし）</color>\n");
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                LawArticle a = list[i];
                string tag = a.IsCommon ? "<color=#7f8a96>[共通]</color> " : "";
                sb.Append("  ").Append(tag).Append("<color=#cfe0f0>").Append(a.title).Append("</color>　")
                  .Append("<color=#9aa7b2>").Append(a.summary).Append("</color>\n");
            }
        }

        private void AppendFaction(StringBuilder sb, Faction fac)
        {
            // デモ法体系（GalaxyView.RunLawTick と同一）：同盟＝法の支配／それ以外＝法治どまり。
            LegalSystem legal = fac == Faction.同盟
                ? new LegalSystem(0.7f, 0.7f, 0.7f, 0.7f)
                : new LegalSystem(0.7f, 0.4f, 0.25f, 0.6f);
            float rol = RuleOfLawRules.RuleOfLawIndex(legal);
            bool ruleByLawOnly = RuleOfLawRules.IsRuleByLawOnly(legal);

            sb.Append('\n').Append("<color=#e7e0b0>◤ ").Append(fac).Append("</color>　");
            sb.Append(ruleByLawOnly
                ? "<color=#ff9a8a>法治どまり（権力の道具）</color>\n"
                : "<color=#a0e0a0>法の支配（権力も法に従う）</color>\n");

            // 法の支配：合成指数＋4要素
            AppendBar(sb, "  法の支配指数", rol, "#7fd4ff");
            AppendBar(sb, "  司法の独立", legal.judicialIndependence, "#bcd2e0");
            AppendBar(sb, "  法の前の平等", legal.equalityBeforeLaw, "#bcd2e0");
            AppendBar(sb, "  権力の制約", legal.powerConstraint, "#ffd28a");
            AppendBar(sb, "  予測可能性", legal.legalPredictability, "#bcd2e0");

            // 派生効果（LAW-2）
            float legitDelta = RuleOfLawEffectRules.LegitimacyDelta(rol, 1f);
            float econConf   = RuleOfLawEffectRules.EconomicConfidence(rol, 0.5f);
            float arbitrary  = RuleOfLawEffectRules.ArbitraryPowerFactor(rol);
            sb.Append("    <color=#6f8a9a>正統性 ").Append(legitDelta >= 0f ? "+" : "").Append(legitDelta.ToString("0.00"))
              .Append("／経済信頼 ").Append(econConf.ToString("0.00"))
              .Append("／恣意的権力の余地 ").Append(arbitrary.ToString("0.00")).Append("</color>\n");

            // 治安：所有惑星を集約（犯罪圧力→公共秩序→抑圧）
            AppendOrder(sb, fac, rol);
        }

        /// <summary>所有惑星の治安を集約表示（RunLawTick と同じ計算を平均）。</summary>
        private void AppendOrder(StringBuilder sb, Faction fac, float rol)
        {
            GalaxyMap map = StrategySession.Map;
            var provinces = StrategySession.Provinces;
            if (map == null || provinces == null)
            {
                sb.Append("    <color=#6f8a9a>治安：惑星データなし</color>\n");
                return;
            }

            var cp = CrimeRules.CrimeParams.Default;
            int n = 0, repressed = 0;
            float sumCrime = 0f, sumOrder = 0f, sumRepr = 0f;
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null || s.owner != fac) continue;
                if (!provinces.TryGetValue(s.id, out var prov) || prov == null) continue;
                float unemployment = Mathf.Clamp01(OccupationRules.UnemploymentPressure(prov));
                float poverty = Mathf.Clamp01(1f - prov.livingStandard);
                var r = LawTickRules.TickProvince(rol, unemployment, poverty, DemoInequality, DemoEnforcement, cp);
                sumCrime += r.crimePressure; sumOrder += r.orderLevel; sumRepr += r.repression;
                if (r.repression > 0.4f) repressed++;
                n++;
            }

            if (n == 0)
            {
                sb.Append("    <color=#6f8a9a>治安：所有惑星なし</color>\n");
                return;
            }

            AppendBar(sb, "  犯罪圧力(平均)", sumCrime / n, "#ff9a8a");
            AppendBar(sb, "  公共秩序(平均)", sumOrder / n, "#a0e0a0");
            AppendBar(sb, "  抑圧度(平均)", sumRepr / n, "#d08ad0");
            sb.Append("    <color=#6f8a9a>取締り力 ").Append(DemoEnforcement.ToString("0.00"))
              .Append("／抑圧化している惑星 ").Append(repressed).Append("/").Append(n).Append("</color>\n");
        }

        /// <summary>「ラベル ████░░░░ 0.42」形式の1行を追加する（0..1可視化）。</summary>
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

            overlayRoot = new GameObject("LawObserverCanvas");
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
            WindowChrome.MakeNonModal(dimImage); // ウィンドウ化＝非モーダル（盤面を塞がない）

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

            WindowChrome.AddTitleBarLayout(frameRT, "法令", () => SetVisible(false));
            BuildTabBar(frame.transform);
            BuildScrollBody(frame.transform);
        }

        private void BuildTabBar(Transform parent)
        {
            GameObject bar = new GameObject("TabBar");
            bar.transform.SetParent(parent, false);
            bar.AddComponent<RectTransform>();
            LayoutElement le = bar.AddComponent<LayoutElement>();
            // flexibleHeight=0 を明示＝HorizontalLayoutGroup(childForceExpandHeight) が親へ報告する
            // flexibleHeight を LayoutElement(優先度高)で打ち消し、タブ帯がスクロール本文と高さを取り合わない。
            le.minHeight = 32f; le.preferredHeight = 32f; le.flexibleHeight = 0f;
            HorizontalLayoutGroup h = bar.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 4f;
            h.childControlWidth = true; h.childForceExpandWidth = true;
            h.childControlHeight = true; h.childForceExpandHeight = true;

            tabBgs.Clear();
            MakeTab(bar.transform, "概況", LawTab.概況);
            MakeTab(bar.transform, "憲法", LawTab.憲法);
            MakeTab(bar.transform, "法律", LawTab.法律);
            MakeTab(bar.transform, "政令", LawTab.政令);
            UpdateTabVisuals();
        }

        private void MakeTab(Transform parent, string label, LawTab tab)
        {
            GameObject go = new GameObject("Tab_" + tab);
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.13f, 0.18f, 0.26f, 1f);
            Button b = go.AddComponent<Button>();
            b.transition = UnityEngine.UI.Selectable.Transition.None;
            b.onClick.AddListener(() => SetTab(tab));

            GameObject txt = new GameObject("Label");
            txt.transform.SetParent(go.transform, false);
            RectTransform trt = txt.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.sizeDelta = Vector2.zero; trt.anchoredPosition = Vector2.zero;
            TextMeshProUGUI t = txt.AddComponent<TextMeshProUGUI>();
            t.text = label; t.fontSize = 17f; t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.9f, 0.92f, 0.96f); t.raycastTarget = false;
            ApplyJapaneseFont(t);

            tabBgs[tab] = img;
        }

        private void SetTab(LawTab tab)
        {
            activeTab = tab;
            UpdateTabVisuals();
            if (bodyLabel != null)
                bodyLabel.text = tab == LawTab.概況 ? BuildDump() : BuildLawList(ToCategory(tab));
        }

        private void UpdateTabVisuals()
        {
            foreach (var kv in tabBgs)
                kv.Value.color = kv.Key == activeTab
                    ? new Color(0.22f, 0.30f, 0.42f, 1f)
                    : new Color(0.13f, 0.18f, 0.26f, 1f);
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
