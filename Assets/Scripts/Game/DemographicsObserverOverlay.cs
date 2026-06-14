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
    /// 人口オブザーバ（観測層・read-only）。<b>Z キー</b>で開閉し、勢力ごとに所有惑星の**配線済みの人口動態**を集約して
    /// 毎フレームライブダンプする：年齢コホート（年少/生産年齢/高齢・<see cref="Population"/>）と従属指数・人口局面
    /// （<see cref="DemographicsRules.Phase"/>＝ボーナス/中立/オーナス）・産出係数、職業構成（<see cref="OccupationRules.Workers"/>・#110）と
    /// 就業率・徴募源（軍属）、定住魅力（<see cref="PopulationMigrationRules.Attractiveness"/>＝移住/亡命の引力・#194）。
    /// `GalaxyView.RunAnnualLifecycleTick`（年次）が出生死亡・移住を回す＝荒れた星系は人口が減る、その背景を映す。
    /// 観測専用ゆえ既存フィールドのみ読み（demographics を生成しない）。操作はさせない＝**状態は変えない**。
    /// `SystemView` が惑星ごとに出す内容の勢力横断・マクロ俯瞰版。`HelpOverlay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class DemographicsObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1099;
        public float dimAlpha = 0.55f;
        public float panelWidth = 980f;
        public float panelMaxHeight = 900f;
        public Color panelColor = new Color(0.05f, 0.05f, 0.04f, 0.96f);
        public float bodyFontSize = 20f;
        public int barWidth = 14;

        private static readonly Occupation[] Occupations =
            { Occupation.農民, Occupation.工員, Occupation.鉱員, Occupation.官吏, Occupation.軍属, Occupation.無職 };

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
            if (Object.FindAnyObjectByType<DemographicsObserverOverlay>() != null) return;
            new GameObject("DemographicsObserverOverlay").AddComponent<DemographicsObserverOverlay>();
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "人口");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.人口観測切替))
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
            var provinces = StrategySession.Provinces;

            sb.Append("<b>人口オブザーバ</b>　年齢コホート/局面・職業・徴募源・定住魅力　(Z で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            if (c == null || c.states == null || c.states.Count == 0 || map == null || provinces == null)
            {
                sb.Append("\n<color=#ffcc66>戦役データ（StrategySession.Campaign / Map / Provinces）がありません。</color>\n");
                sb.Append("戦略マップ（GalaxyView）を起動すると、各勢力の人口動態がライブ表示されます。");
                return sb.ToString();
            }

            for (int i = 0; i < c.states.Count; i++)
            {
                FactionState s = c.states[i];
                if (s == null) continue;
                AppendFaction(sb, map, provinces, s.faction);
            }

            sb.Append("\n<color=#6f8a9a>※ 出生死亡（増減）と移住（移動）は別軸。荒れた星系は出生↓死亡↑＋住みよい星系へ流出（亡命/難民）。</color>");
            return sb.ToString();
        }

        private void AppendFaction(StringBuilder sb, GalaxyMap map,
            System.Collections.Generic.Dictionary<int, Province> provinces, Faction fac)
        {
            // 所有惑星を集約。
            float youth = 0f, working = 0f, elderly = 0f, females = 0f, cohortTotal = 0f;
            float plainPop = 0f;           // demographics 未設定惑星の人口
            float empWeighted = 0f, popForEmp = 0f;
            float recruit = 0f, attractSum = 0f;
            var workers = new float[Occupations.Length];
            int ownedCount = 0;

            foreach (var kv in provinces)
            {
                Province prov = kv.Value;
                if (prov == null) continue;
                StarSystem sys = map.GetSystem(prov.systemId);
                if (sys == null || sys.owner != fac) continue;
                ownedCount++;

                Population pop = prov.demographics; // 観測専用：生成しない（null はそのまま）
                if (pop != null)
                {
                    youth += pop.youth; working += pop.working; elderly += pop.elderly;
                    females += pop.Females; cohortTotal += pop.Total;
                }
                else plainPop += prov.population;

                float pw = prov.population;
                empWeighted += OccupationRules.EmploymentRate(prov) * pw;
                popForEmp += pw;
                recruit += OccupationRules.RecruitablePool(prov);
                attractSum += PopulationMigrationRules.Attractiveness(prov);

                for (int o = 0; o < Occupations.Length; o++)
                    workers[o] += OccupationRules.Workers(prov, Occupations[o]);
            }

            sb.Append('\n').Append("<color=#e7e0b0>◤ ").Append(fac).Append("</color>");
            sb.Append("　<color=#6f8a9a>(惑星 ").Append(ownedCount).Append(")</color>\n");
            if (ownedCount == 0)
            {
                sb.Append("  <color=#9aa7b2>（所有惑星なし）</color>\n");
                return;
            }

            // --- 年齢コホート（集約 Population で局面を解く） ---
            if (cohortTotal > 0f)
            {
                var agg = new Population
                {
                    youth = youth, working = working, elderly = elderly,
                    femaleShare = cohortTotal > 0f ? females / cohortTotal : 0.5f
                };
                float dep = DemographicsRules.DependencyRatio(agg);
                PopulationPhase phase = DemographicsRules.Phase(agg, DemographicsRules.DemographicsParams.Default);
                float outF = DemographicsRules.OutputFactor(agg, DemographicsRules.DemographicsParams.Default);

                sb.Append("  <color=#9fb0c0>人口</color> ").Append(agg.Total.ToString("#,0"))
                  .Append("　年少 ").Append(Pct(youth, agg.Total))
                  .Append("／生産 ").Append(Pct(working, agg.Total))
                  .Append("／高齢 ").Append(Pct(elderly, agg.Total))
                  .Append("　女性 ").Append((agg.femaleShare * 100f).ToString("0")).Append("%\n");
                sb.Append("  <color=#9fb0c0>従属指数</color> ").Append(dep.ToString("0.00"))
                  .Append("　<color=").Append(PhaseColor(phase)).Append('>').Append('【').Append(phase).Append('】').Append("</color>")
                  .Append("　産出係数 ").Append(outF.ToString("0.00")).Append('\n');
            }
            if (plainPop > 0f)
                sb.Append("  <color=#9aa7b2>未細分人口 ").Append(plainPop.ToString("#,0")).Append("（コホート未設定）</color>\n");

            // --- 職業構成（実数→シェア） ---
            float wTotal = 0f;
            for (int o = 0; o < workers.Length; o++) wTotal += workers[o];
            if (wTotal > 0f)
            {
                sb.Append("  <color=#9fb0c0>職業</color>　");
                for (int o = 0; o < Occupations.Length; o++)
                {
                    if (workers[o] <= 0f) continue;
                    sb.Append(Occupations[o]).Append(' ').Append((workers[o] / wTotal * 100f).ToString("0")).Append("%　");
                }
                sb.Append('\n');
            }

            // --- 就業率・徴募源・定住魅力 ---
            float emp = popForEmp > 0f ? empWeighted / popForEmp : 0f;
            float attract = attractSum / ownedCount;
            AppendBar(sb, "  就業率", Mathf.Clamp01(emp), emp < 0.7f ? "#ffd28a" : "#a0e0a0");
            sb.Append("  <color=#9fb0c0>徴募源(軍属)</color> ＝ <color=#ffe08a>").Append(recruit.ToString("#,0")).Append("</color>\n");
            AppendBar(sb, "  定住魅力(平均)", Mathf.Clamp01(attract), "#7fd4ff");
        }

        private static string Pct(float part, float total)
            => total > 0f ? (part / total * 100f).ToString("0") + "%" : "0%";

        private static string PhaseColor(PopulationPhase phase)
        {
            switch (phase)
            {
                case PopulationPhase.人口ボーナス: return "#8ce08c";
                case PopulationPhase.人口オーナス: return "#ff9a8a";
                default: return "#ffd28a";
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

            overlayRoot = new GameObject("DemographicsObserverCanvas");
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

            WindowChrome.AddTitleBarLayout(frameRT, "人口", () => SetVisible(false));
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
