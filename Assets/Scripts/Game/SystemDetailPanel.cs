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
    /// 戦略マップで星系/惑星をクリック（I キー）した時に、その内政・攻城・所有を表示するモーダル（#759）。
    /// 艦隊の <see cref="FleetDetailPanel"/> の星系版。表示中は <see cref="Time.timeScale"/>=0 でポーズし、
    /// 閉じる/Esc/背景クリックで復帰する。UIは実行時にコード生成。数値ロジックは持たず
    /// <see cref="GovernanceRules"/>/<see cref="Planet"/> など static 窓口を読むだけ。
    /// </summary>
    public class SystemDetailPanel : MonoBehaviour
    {
        private static SystemDetailPanel instance;

        /// <summary>パネルが開いているか（GalaxyView 等が入力を譲るために参照）。</summary>
        public static bool IsOpen => instance != null && instance.isOpen;

        /// <summary>
        /// 描画順（Esc の閉じる順も同じ値）。星系図 <see cref="SystemMapWindow"/>（950）より手前・観測窓（1090）より後ろ。
        /// </summary>
        public const int SortingOrder = 960;

        private bool isOpen;
        private object escWindowToken; // UIWindowStack 登録トークン（#ウィンドウESC）
        private GameObject root;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI bodyText;
        private TextMeshProUGUI stabilityLabel;
        private RectTransform stabilityFill;

        private const float PanelWidth = 560f;
        private const float PanelHeight = 700f;
        private const int ContentPaddingX = 26;

        // 開いたまま本文・安定度を最新化するための表示中データ（GalaxyView が無い時は最後に受け取った参照を読み直す）。
        private StarSystem shownSystem;
        private Province shownProvince;
        private int shownNeighborCount;
        private string shownFleetSummary;

        // 統治政策の上申（#67/#109）：Alt+T と同じ入口（RingiDirector.ProposeNextGovernancePolicy）をマウスから押す。
        [Header("統治政策の上申")]
        [Tooltip("表示中の上申見込み（所有・決裁待ち・権限）を読み直す間隔（実時間秒）")]
        public float governanceRefreshInterval = 0.5f;
        private const float MinGovernanceRefreshInterval = 0.05f; // 0 以下を入れても毎フレーム全再計算しない
        private int shownSystemId = -1;
        private float governanceRefreshTimer;
        private TextMeshProUGUI governanceText;
        private TextMeshProUGUI governanceResultText;
        private LayoutElement governanceTextLE;
        private LayoutElement governanceResultLE;
        private Button governanceButton;
        private Image governanceButtonImage;
        private static readonly Color ButtonEnabledColor = new Color(0.2f, 0.25f, 0.4f, 1f);
        private static readonly Color ButtonDisabledColor = new Color(0.16f, 0.16f, 0.18f, 1f);

        /// <summary>星系の詳細を表示する（必要なら生成）。prov/planet は無くても可（後方互換表示）。</summary>
        public static void Show(StarSystem s, Province prov, int neighborCount, string fleetSummary)
        {
            if (s == null) return;
            if (instance == null)
            {
                GameObject go = new GameObject("SystemDetailPanel");
                instance = go.AddComponent<SystemDetailPanel>();
                instance.Build();
            }
            instance.Display(s, prov, neighborCount, fleetSummary);
        }

        private void Build()
        {
            EnsureEventSystem();

            GameObject canvasObj = new GameObject("SystemDetailCanvas");
            canvasObj.transform.SetParent(transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 星系図（950）の入口ボタンから開くので、星系図より必ず手前に出す（同順位だと背面に隠れた＝実画面の指摘）。
            canvas.sortingOrder = SortingOrder;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // root：全画面の透明コンテナ（★ディマー無し＝非モーダル＝背後のマップ操作を塞がない）。
            root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(canvasObj.transform, false);
            StretchFull(root.GetComponent<RectTransform>());

            // 枠ウィンドウ（中央・タイトルバーをつかんでドラッグ移動）。
            GameObject panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            RectTransform pRT = panel.GetComponent<RectTransform>();
            pRT.anchorMin = pRT.anchorMax = pRT.pivot = new Vector2(0.5f, 0.5f);
            pRT.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            pRT.anchoredPosition = Vector2.zero;
            Image pImg = panel.AddComponent<Image>();
            pImg.color = new Color(0.06f, 0.07f, 0.12f, 0.97f);
            Outline border = panel.AddComponent<Outline>();
            border.effectColor = new Color(1f, 0.84f, 0.36f, 0.5f);
            border.effectDistance = new Vector2(2f, -2f);

            VerticalLayoutGroup outer = panel.AddComponent<VerticalLayoutGroup>();
            outer.padding = new RectOffset(0, 0, 0, 0);
            outer.spacing = 0f;
            outer.childControlWidth = true; outer.childForceExpandWidth = true;
            outer.childControlHeight = true; outer.childForceExpandHeight = false;

            BuildTitleBar(panel.transform, pRT);

            // 内容コンテナ（内側パディング・残り高さを埋める）
            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(panel.transform, false);
            LayoutElement contentLE = content.AddComponent<LayoutElement>();
            contentLE.flexibleHeight = 1f;
            VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(ContentPaddingX, ContentPaddingX, 16, 18);
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;

            titleText = CreateText(content.transform, "星系情報", 24f, FontStyles.Bold, TextAlignmentOptions.Center);

            // 安定度バー（ラベル＋色付きフィル）
            BuildStabilityBar(content.transform);

            BuildGovernanceBlock(content.transform);

            bodyText = CreateText(content.transform, "", 21f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            // 上申欄を足したぶん本文が枠からはみ出さないよう、入りきらなければ文字を縮める。
            bodyText.enableAutoSizing = true;
            bodyText.fontSizeMin = 12f;
            bodyText.fontSizeMax = 21f;
            LayoutElement bodyLE = bodyText.gameObject.AddComponent<LayoutElement>();
            bodyLE.flexibleHeight = 1f;
            // 本文は「残りの高さ」だけを使う（長い本文の必要高さで上申欄が押し潰され、見込みの最終行がボタンに重なった）。
            bodyLE.minHeight = 0f;
            bodyLE.preferredHeight = 0f;

            root.SetActive(false);

            // ESC は UIWindowStack 経由で「手前から閉じる」（#ウィンドウESC）。
            escWindowToken = UIWindowStack.Register(() => isOpen, Close, SortingOrder, "星系情報");
        }

        /// <summary>タイトルバー（Windows 風・つかんでドラッグ移動＋×で閉じる）。観測窓と同型。</summary>
        private void BuildTitleBar(Transform parent, RectTransform windowRT)
        {
            GameObject bar = new GameObject("TitleBar", typeof(RectTransform));
            bar.transform.SetParent(parent, false);
            Image img = bar.AddComponent<Image>();
            img.color = new Color(0.13f, 0.18f, 0.26f, 1f);
            LayoutElement le = bar.AddComponent<LayoutElement>();
            le.minHeight = 30f; le.preferredHeight = 30f;
            UIDragMove drag = bar.AddComponent<UIDragMove>();
            drag.target = windowRT;

            TextMeshProUGUI cap = CreateText(bar.transform, "≡ 星系情報　（ドラッグで移動）", 15f, FontStyles.Normal, TextAlignmentOptions.Left);
            cap.color = new Color(1f, 0.84f, 0.36f);
            RectTransform crt = cap.rectTransform;
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(12f, 0f); crt.offsetMax = new Vector2(-42f, 0f);

            GameObject cb = new GameObject("Close", typeof(RectTransform));
            cb.transform.SetParent(bar.transform, false);
            RectTransform cbrt = cb.GetComponent<RectTransform>();
            cbrt.anchorMin = new Vector2(1f, 0f); cbrt.anchorMax = new Vector2(1f, 1f);
            cbrt.pivot = new Vector2(1f, 0.5f); cbrt.sizeDelta = new Vector2(34f, 0f);
            cbrt.anchoredPosition = new Vector2(-3f, 0f);
            Image cimg = cb.AddComponent<Image>();
            cimg.color = new Color(0.13f, 0.18f, 0.26f, 1f);
            Button cbtn = cb.AddComponent<Button>();
            cbtn.transition = UnityEngine.UI.Selectable.Transition.None;
            cbtn.onClick.AddListener(Close);
            TextMeshProUGUI glyph = CreateText(cb.transform, "×", 18f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchFull(glyph.rectTransform);
        }

        private void BuildStabilityBar(Transform parent)
        {
            stabilityLabel = CreateText(parent, "安定度 —", 19f, FontStyles.Bold, TextAlignmentOptions.Left);

            GameObject bg = new GameObject("StabilityBarBg", typeof(RectTransform));
            bg.transform.SetParent(parent, false);
            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.5f);
            bgImg.raycastTarget = false;
            LayoutElement le = bg.AddComponent<LayoutElement>();
            le.preferredHeight = 22f;

            GameObject fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(bg.transform, false);
            stabilityFill = fill.GetComponent<RectTransform>();
            stabilityFill.anchorMin = new Vector2(0f, 0f);
            stabilityFill.anchorMax = new Vector2(1f, 1f); // Display で anchorMax.x を割合に
            stabilityFill.offsetMin = stabilityFill.offsetMax = Vector2.zero;
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = Color.green;
            fillImg.raycastTarget = false;
        }

        /// <summary>統治政策の上申欄（現在→次の政策・上申できない理由・決裁の見込み＋上申ボタン＋結果）。</summary>
        private void BuildGovernanceBlock(Transform parent)
        {
            governanceText = CreateText(parent, "", 17f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            governanceTextLE = governanceText.gameObject.AddComponent<LayoutElement>();
            governanceButton = CreateButton(parent, "統治政策の変更を上申", OnGovernanceButton, 38f, 19f);
            governanceButtonImage = governanceButton != null ? governanceButton.GetComponent<Image>() : null;
            governanceResultText = CreateText(parent, "", 16f, FontStyles.Italic, TextAlignmentOptions.TopLeft);
            governanceResultText.color = new Color(1f, 0.84f, 0.36f);
            governanceResultLE = governanceResultText.gameObject.AddComponent<LayoutElement>();
        }

        /// <summary>
        /// 折り返し後の必要高さを最小高さとして確保する（詰まっても次の行＝ボタン/結果に重ならない）。
        /// 初回レイアウト前は幅が 0 なので、枠幅−内側パディングで見積もる。
        /// </summary>
        private static void ReserveTextHeight(TextMeshProUGUI t, LayoutElement le)
        {
            if (t == null || le == null) return;
            float width = t.rectTransform.rect.width;
            if (width <= 0f) width = PanelWidth - ContentPaddingX * 2;
            le.minHeight = string.IsNullOrEmpty(t.text) ? 0f : t.GetPreferredValues(t.text, width, 0f).y;
        }

        /// <summary>上申の結果文を設定し、その高さを確保する。</summary>
        private void SetGovernanceResult(string message)
        {
            if (governanceResultText == null) return;
            governanceResultText.text = message ?? "";
            ReserveTextHeight(governanceResultText, governanceResultLE);
        }

        /// <summary>
        /// 上申欄を読み直す。判定は Alt+T と同じ <see cref="RingiDirector.PreviewGovernanceProposal"/>、
        /// 決裁の見込みは裁可時と同じ <see cref="DecisionAuthorityDirector.TryPreviewAuthority"/>（どちらも読み取りのみ）。
        /// </summary>
        private void RefreshGovernance()
        {
            if (governanceText == null || shownSystemId < 0) return;
            GovernanceProposalPreview p = RingiDirector.PreviewGovernanceProposal(shownSystemId);

            var sb = new StringBuilder();
            sb.Append("― 統治政策の上申 ―\n");
            if (p.rejection == GovernanceProposalRejection.星系なし || p.rejection == GovernanceProposalRejection.内政データなし)
                sb.Append("対象: ").Append(string.IsNullOrEmpty(p.systemName) ? $"星系 #{shownSystemId}" : p.systemName).Append('\n');
            else
                sb.Append("対象: ").Append(p.systemName)
                  .Append("　現在「").Append(p.current).Append("」→ 次「").Append(p.next).Append("」\n");

            if (p.CanSubmit)
            {
                sb.Append("上申: できます（").Append(GameInput.KeyLabel(GameAction.統治政策上申))
                  .Append(" でも同じ上申になります）\n");
                string key = GovernanceRules.PolicyPetitionKey(shownSystemId, p.next);
                bool enforced = DecisionAuthorityDirector.TryPreviewAuthority(key, out DecisionAuthorityResult auth);
                sb.Append("決裁の見込み: ");
                if (!enforced) sb.Append(auth.basis);
                else if (auth.CanDecide) sb.Append("あなたが裁可できます（").Append(auth.basis).Append("）");
                else if (auth.authority == DecisionAuthority.上申)
                    sb.Append("裁可すると ").Append(string.IsNullOrEmpty(auth.addresseeName) ? "所管" : auth.addresseeName)
                      .Append(" へ上申されます（").Append(auth.basis).Append("）");
                else sb.Append("いまは裁可できません（").Append(auth.basis).Append("）");
            }
            else
            {
                sb.Append("上申できません: ").Append(GovernanceProposalRules.RejectionText(p.rejection, p.systemName));
            }
            governanceText.text = sb.ToString();
            ReserveTextHeight(governanceText, governanceTextLE);

            if (governanceButton != null) governanceButton.interactable = p.CanSubmit;
            if (governanceButtonImage != null) governanceButtonImage.color = p.CanSubmit ? ButtonEnabledColor : ButtonDisabledColor;
        }

        /// <summary>上申ボタン：Alt+T と同じ入口へ渡すだけ（政策を直接変えない）。</summary>
        private void OnGovernanceButton()
        {
            // ボタンに選択が残ると、Space/Enter（UI の決定）で押し直されて意図しない再上申になる＝選択を外す。
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (shownSystemId < 0) return;
            // 盤面を止めるモーダル（イベント提示・システムメニュー・終了画面）の表示中は上申しない（Alt+T と同じ扱い）。
            // 判定は GalaxyView.Update の早期 return と同じ（艦隊編成画面も含む）。
            if (GalaxyView.IsBoardModalOpen)
            {
                SetGovernanceResult("いまは上申できません（画面中央の窓を先に閉じてください）");
                return;
            }

            RingiDirector.ProposeNextGovernancePolicy(shownSystemId, out _, out string message);
            SetGovernanceResult(message);
            RefreshGovernance();
        }

        private void Update()
        {
            if (!isOpen) return;
            governanceRefreshTimer -= Time.unscaledDeltaTime; // ポーズ中も所有・決裁待ちの変化を映す
            if (governanceRefreshTimer > 0f) return;
            governanceRefreshTimer = Mathf.Max(MinGovernanceRefreshInterval, governanceRefreshInterval);
            RefreshGovernance();
            RefreshShownSystem(); // 上段（上申欄）と同じ間隔で本文・安定度も読み直す＝上下で政策が食い違わない
        }

        /// <summary>
        /// 表示中星系の最新データを読み直して本文・安定度・見出しへ反映する（開閉・ポーズの副作用なし）。
        /// 取得経路は <see cref="GalaxyView.OpenSystemInfo"/> と同じ <see cref="GalaxyView.TryGetSystemInfo"/>。
        /// GalaxyView が無い時は上申欄と同じ <see cref="StrategySession"/> の地図/内政を、それも無ければ最後の参照を読む。
        /// </summary>
        private void RefreshShownSystem()
        {
            if (shownSystemId < 0) return;
            GalaxyView gv = GalaxyView.Active;
            if (gv != null && gv.TryGetSystemInfo(shownSystemId, out StarSystem s, out Province prov, out int n, out string fleets))
            {
                shownSystem = s; shownProvince = prov; shownNeighborCount = n; shownFleetSummary = fleets;
            }
            else
            {
                StarSystem ss = StrategySession.Map != null ? StrategySession.Map.GetSystem(shownSystemId) : null;
                if (ss != null)
                {
                    shownSystem = ss;
                    if (StrategySession.Provinces != null && StrategySession.Provinces.TryGetValue(shownSystemId, out Province sp))
                        shownProvince = sp;
                }
            }
            if (shownSystem != null) ApplySystemInfo();
        }

        private void Display(StarSystem s, Province prov, int neighborCount, string fleetSummary)
        {
            if (shownSystemId != s.id) SetGovernanceResult(""); // 別の星系の結果を残さない
            shownSystemId = s.id;
            shownSystem = s; shownProvince = prov; shownNeighborCount = neighborCount; shownFleetSummary = fleetSummary;
            RefreshGovernance();
            governanceRefreshTimer = governanceRefreshInterval;
            ApplySystemInfo();

            isOpen = true; // 非モーダル（ポーズしない）＝開いたままマップ操作・進行が続く
            if (root != null) root.SetActive(true);
        }

        /// <summary>表示中データ（shownXxx）を見出し・安定度バー・本文へ書く。</summary>
        private void ApplySystemInfo()
        {
            StarSystem s = shownSystem;
            Province prov = shownProvince;
            if (s == null) return;
            if (titleText != null) titleText.text = $"{s.systemName}（星系 #{s.id}）";

            // 安定度バー
            float stability = prov != null ? prov.stability : -1f;
            if (stabilityFill != null)
            {
                if (prov != null)
                {
                    float frac = Mathf.Clamp01(stability / GovernanceRules.MaxStability);
                    stabilityFill.anchorMax = new Vector2(frac, 1f);
                    // 緑(高)→黄→赤(低)
                    Image fi = stabilityFill.GetComponent<Image>();
                    if (fi != null) fi.color = StabilityColor(frac);
                }
                else stabilityFill.anchorMax = new Vector2(0f, 1f);
            }
            if (stabilityLabel != null)
            {
                stabilityLabel.text = prov != null
                    ? $"安定度 {Mathf.RoundToInt(stability)}%{(GovernanceRules.IsUnrest(prov) ? "　▲反乱リスク" : "")}"
                    : "安定度 —（未統治）";
            }

            if (bodyText != null)
            {
                string info = BuildInfo(s, prov, shownNeighborCount, shownFleetSummary);
                if (bodyText.text != info) bodyText.text = info; // 変化が無ければ TMP の再構築を避ける
            }
        }

        /// <summary>表示中の本文（試験・診断用の読み取り）。</summary>
        public static string BodyTextForTest => instance != null && instance.bodyText != null ? instance.bodyText.text : null;

        /// <summary>表示中の上申欄の文（試験・診断用の読み取り）。</summary>
        public static string GovernanceTextForTest => instance != null && instance.governanceText != null ? instance.governanceText.text : null;

        /// <summary>ウィンドウを閉じる。</summary>
        public void Close()
        {
            isOpen = false;
            if (root != null) root.SetActive(false);
        }

        // Esc は UIWindowStack 経由（GalaxyView）で「手前から閉じる」＝自前で読まない。

        private string BuildInfo(StarSystem s, Province prov, int neighborCount, string fleetSummary)
        {
            var sb = new StringBuilder();

            string ownerName = (s.ownerData != null) ? s.ownerData.factionName : s.owner.ToString();
            sb.AppendLine($"所有: {ownerName}");
            sb.AppendLine($"座標: ({s.position.x:0.0}, {s.position.y:0.0})　回廊: {neighborCount}本");

            sb.AppendLine();
            sb.AppendLine("― 内政 ―");
            if (prov != null)
            {
                string ideo = string.IsNullOrEmpty(prov.nativeIdeology) ? "（不明）" : prov.nativeIdeology;
                sb.AppendLine($"住民の思想: {ideo}　人口: {Mathf.RoundToInt(prov.population)}");
                sb.AppendLine($"統治政策: {prov.governancePolicy}（変更は上の上申ボタン、または星系にカーソルを合わせて {GameInput.KeyLabel(GameAction.統治政策上申)}）");
                sb.AppendLine($"統合度: {Mathf.RoundToInt(Mathf.Clamp01(prov.integration) * 100f)}%　産出: ×{GovernanceRules.OutputFactor(prov):0.00}");

                // 経済（#93 を惑星層へ #767）＝SystemView と同じ Core 窓口を読むだけ（数式は二重実装しない）。
                sb.AppendLine($"経済類型: {prov.systemType}");
                float sup = ResourceProductionRules.ProvinceRate(prov, ResourceType.物資);
                float amm = ResourceProductionRules.ProvinceRate(prov, ResourceType.弾薬);
                float fue = ResourceProductionRules.ProvinceRate(prov, ResourceType.燃料);
                sb.AppendLine($"資源産出/秒: 物資 {sup:0.#} / 弾薬 {amm:0.#} / 燃料 {fue:0.#}");
                if (prov.hasStrategicResource) // 希少資源の鉱床（#178・偏在＝一部の惑星のみ）
                {
                    StrategicResourceInfo info = StrategicResourceRules.Info(prov.strategicResource);
                    float srate = StrategicResourceRules.ProvinceRate(prov);
                    sb.AppendLine($"希少資源: {info.displayName}（豊富さ {Mathf.RoundToInt(prov.strategicAbundance * 100f)}%・/秒 {srate:0.##}）");
                }

                if (GovernanceRules.IsUnrest(prov))
                    sb.AppendLine($"反乱圧: {Mathf.RoundToInt(GovernanceRules.RebelPressure(prov) * 100f)}%（安定度が低い）");
            }
            else sb.AppendLine("（未統治＝内政データなし）");

            // 不動産・土地（#2019/#2070）：惑星の地価/賃料（不動産基盤）＋土地の所有（国家＝残余／個人＝私有持分）。
            sb.AppendLine();
            sb.AppendLine("― 不動産・土地 ―");
            if (prov != null && prov.realEstate != null)
            {
                PlanetRealEstate re = prov.realEstate;
                int p2r = Mathf.RoundToInt(PlanetRealEstateRules.PriceToRent(re));
                bool bubble = PlanetRealEstateRules.IsBubble(re, PlanetRealEstateRules.Params.Default);
                sb.AppendLine($"地価: {Mathf.RoundToInt(re.landValue)}　賃料: {Mathf.RoundToInt(re.rentLevel)}　価格/賃料: {p2r}倍{(bubble ? "　▲バブル" : "")}");
            }
            else sb.AppendLine("地価データなし（年次で形成）");
            SystemType landType = prov != null ? prov.systemType : SystemType.居住;
            int statePct = Mathf.RoundToInt(PlanetLandRules.StateShare(s.id, landType) * 100f);
            int personPct = Mathf.RoundToInt(PlanetLandRules.PersonShare(s.id, landType) * 100f);
            int firmPct = Mathf.RoundToInt(PlanetLandRules.EnterpriseShare(s.id, landType) * 100f);
            int deedCount = PropertyDeedRegistry.CountDeedsOnSystem(s.id);
            sb.AppendLine($"土地所有: 国家 {statePct}%　個人 {personPct}%　企業 {firmPct}%（権利証 {deedCount}枚）");
            // 土地分割（地目・#2019）：用途ゾーンと私有区画。惑星の大きさで分割可能数が決まる。
            if (prov != null)
                sb.AppendLine($"区画(最大{LandZoningRules.MaxSubdivisions(prov)})　住宅地 {Mathf.RoundToInt(LandZoningRules.ZoneWeight(prov, LandUseType.住宅地) * 100f)}%／農地 {Mathf.RoundToInt(LandZoningRules.ZoneWeight(prov, LandUseType.農地) * 100f)}%／鉱山 {Mathf.RoundToInt(LandZoningRules.ZoneWeight(prov, LandUseType.鉱山) * 100f)}%");
            int leaseCount = LandLeaseRegistry.OnSystem(s.id).Count;
            if (leaseCount > 0) sb.AppendLine($"賃貸借: {leaseCount}件（貸出中）");

            sb.AppendLine();
            sb.AppendLine("― 惑星防衛 ―");
            Planet p = s.planet;
            if (p != null)
            {
                int ipct = Mathf.FloorToInt(100f * p.invasionProgress / Mathf.Max(1f, p.invasionThreshold));
                sb.AppendLine($"種別: {p.KindName}"); // 惑星/要塞/コロニー（PB-6）
                if (p.maxOrbitalDefense > 0f)
                {
                    int dpct = Mathf.CeilToInt(100f * p.orbitalDefense / Mathf.Max(1f, p.maxOrbitalDefense));
                    sb.AppendLine($"制空権: {dpct}%　{(p.DomainDown ? "（ドメイン・ダウン）" : "（健在＝接近限界）")}");
                }
                else sb.AppendLine("制空権: なし（軌道超兵器なし＝接近限界なし）"); // コロニー
                sb.AppendLine($"侵略値: {ipct}%　{(p.Captured ? "（占領済み）" : "")}");
            }
            else sb.AppendLine("防衛惑星なし（停泊で占領）");

            sb.AppendLine();
            sb.AppendLine("― 在席艦隊 ―");
            sb.AppendLine(string.IsNullOrEmpty(fleetSummary) ? "（なし）" : fleetSummary);

            return sb.ToString();
        }

        private static Color StabilityColor(float frac)
        {
            // 0=赤 / 0.5=黄 / 1=緑
            return frac < 0.5f
                ? Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.95f, 0.85f, 0.2f), frac * 2f)
                : Color.Lerp(new Color(0.95f, 0.85f, 0.2f), new Color(0.35f, 0.9f, 0.45f), (frac - 0.5f) * 2f);
        }

        // ===== UI生成ヘルパ（FleetDetailPanel と同作法） =====

        private TextMeshProUGUI CreateText(Transform parent, string text, float size, FontStyles style, TextAlignmentOptions align)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.fontStyle = style; t.alignment = align;
            t.color = Color.white; t.raycastTarget = false;
            ApplyJapaneseFont(t);
            return t;
        }

        private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick,
                                    float height = 50f, float fontSize = 24f)
        {
            GameObject go = new GameObject("Button_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = ButtonEnabledColor;
            Button btn = go.AddComponent<Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            btn.onClick.AddListener(onClick);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            TextMeshProUGUI txt = CreateText(go.transform, label, fontSize, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchFull(txt.rectTransform);
            return btn;
        }

        private void ApplyJapaneseFont(TextMeshProUGUI tmp)
        {
            TMP_FontAsset jaFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (jaFont != null) tmp.font = jaFont;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
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
            if (instance == this) instance = null;
        }
    }
}
