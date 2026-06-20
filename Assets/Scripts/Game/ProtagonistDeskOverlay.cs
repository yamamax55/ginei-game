using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 主人公の執務机（軍人立身伝の一人称UIシェル・TKO-8 #2485・<b>Alt+J</b>）。god-view から「一人の軍人」へ視点を落とし、
    /// 自分の<b>階級・拝命中の主命（君主→指揮系統→自分のカスケード）・武勲と次の昇進・君主との人脈・一代記</b>を一望する。
    /// 状態は <see cref="ProtagonistCareerDirector"/> が回す立身出世ループから読むだけ（観測）。加えて<b>上官へ具申する</b>ボタン（TKO-4）を持ち、
    /// 押すと <see cref="ProtagonistCareerDirector.SubmitPetition"/> が稟議を起案＝稟議オブザーバ（Alt+I）に起案者=自分・決裁者=上官として現れる。
    /// `RingiObserverOverlay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class ProtagonistDeskOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1115;
        public float dimAlpha = 0.55f;
        public float panelWidth = 1000f;
        public float panelMaxHeight = 900f;
        public Color panelColor = new Color(0.05f, 0.06f, 0.08f, 0.96f);
        public float bodyFontSize = 20f;
        public int barWidth = 14;

        private GameObject overlayRoot;
        private GameObject panel;
        private TextMeshProUGUI bodyLabel;
        private object escWindowToken;

        private static readonly MeritRecordRules.MeritRecordParams MeritP = MeritRecordRules.MeritRecordParams.Default;

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
            if (Object.FindAnyObjectByType<ProtagonistDeskOverlay>() != null) return;
            new GameObject("ProtagonistDeskOverlay").AddComponent<ProtagonistDeskOverlay>();
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "執務机");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.執務机切替)) Toggle();
            if (panel != null && panel.activeSelf && bodyLabel != null)
                bodyLabel.text = BuildDump();
        }

        public void Toggle() => SetVisible(panel != null && !panel.activeSelf);
        public void SetVisible(bool visible) { if (panel != null) panel.SetActive(visible); }

        // ===== ダンプ本体 =====

        private string BuildDump()
        {
            var sb = new StringBuilder(2048);
            sb.Append("<b>執務机</b>　階級・主命・武勲・人脈・一代記　(Alt+J で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            var d = ProtagonistCareerDirector.Instance;
            if (d == null || d.Protagonist == null)
            {
                sb.Append("\n<color=#ffcc66>立身出世ループはまだ起動していません。</color>\n");
                sb.Append("戦略マップ（Strategy）で ProtagonistCareerDirector が主人公を士官学校から任官させると、\n");
                sb.Append("ここに階級・拝命中の主命・武勲・人脈・一代記がライブ表示されます。");
                return sb.ToString();
            }

            Person me = d.Protagonist;
            sb.Append("\n<color=#e7e0b0>◤ 人物</color>\n");
            sb.Append("  <color=#ffe08a>").Append(me.name).Append("</color>　")
              .Append("<color=#9ad0ff>").Append(d.RankName(me.rankTier)).Append("</color>\n");
            sb.Append("  <color=#9aa7b2>").Append(OriginRules.Title(d.Origin)).Append("</color>\n");
            sb.Append("  <color=#9aa7b2>武名</color> ");
            AppendBar(sb, Mathf.Clamp01(d.Fame / 100f), "#d0b060");
            sb.Append("　<color=#6f8a9a>(政界転身の資本)</color>\n");

            // 能力（会戦成長 P1-b を反映した実効値＝基準＋GrowthRegistry）
            Growth g = d.HeroGrowth;
            sb.Append("\n<color=#e7e0b0>◤ 能力（会戦成長を反映）</color>\n");
            AppendStat(sb, "統率", me.leadership, g);
            AppendStat(sb, "攻撃", me.attack, g);
            AppendStat(sb, "防御", me.defense, g);
            AppendStat(sb, "機動", me.mobility, g);

            // 主命（カスケード）
            sb.Append("\n<color=#e7e0b0>◤ 主命（君主 → 指揮系統 → あなた）</color>\n");
            SovereignMandate m = d.ActiveMandate;
            if (m == null)
                sb.Append("  <color=#9aa7b2>（拝命中の主命なし）</color>\n");
            else
            {
                sb.Append("  発令 <color=#ffcc66>").Append(d.ResolveName(m.issuerId)).Append("</color>")
                  .Append("　→　拝命 <color=#ffe08a>").Append(me.name).Append("</color>\n");
                sb.Append("  <color=#9ad0ff>").Append(m.kind).Append("</color>　状態 ").Append(m.status)
                  .Append("　期限(通算月) ").Append(m.dueMonth).Append('\n');
                AppendCascade(sb, d);
            }

            // 武勲
            sb.Append("\n<color=#e7e0b0>◤ 武勲</color>\n");
            MeritRecord mr = d.Merit;
            if (mr != null)
            {
                float toNext = MeritP.pointsPerPromotion * (mr.meritPromotionsApplied + 1) - mr.points;
                if (toNext < 0f) toNext = 0f;
                sb.Append("  累積 <color=#ffe08a>").Append(Mathf.RoundToInt(mr.points)).Append("</color> 点");
                sb.Append("　次の昇進まで ").Append(Mathf.CeilToInt(toNext)).Append(" 点\n  実力 ");
                AppendBar(sb, MeritRecordRules.MeritScore01(mr, MeritP), "#8ce08c");
                sb.Append('\n');
            }

            // 階級ピラミッド（自勢力）＝定員が昇進を律速（上ほど狭き門）
            AppendPyramid(sb, d, me);

            // 人脈（君主との縁）
            sb.Append("\n<color=#e7e0b0>◤ 人脈（君主との縁）</color>\n");
            if (d.Relations != null && d.Sovereign != null)
            {
                float favor = PersonRelationRules.NetAffinity(d.Relations, me.id, d.Sovereign.id);
                sb.Append("  ").Append(me.name).Append(" → ").Append(d.Sovereign.CharacterName)
                  .Append("　正味の親密度 <color=").Append(favor >= 0f ? "#8ce08c" : "#ff9a8a").Append('>')
                  .Append(favor.ToString("+0.00;-0.00;0.00")).Append("</color>\n");
            }
            else sb.Append("  <color=#9aa7b2>（記録なし）</color>\n");

            // 岐路（開かれた進路・TKO-7・政界転身を含む）
            sb.Append("\n<color=#e7e0b0>◤ 岐路（開かれた進路）</color>\n  ");
            List<CareerFork> forks = d.AvailableForks();
            for (int i = 0; i < forks.Count; i++)
            {
                if (i > 0) sb.Append("　/　");
                sb.Append(ForkColor(forks[i])).Append(forks[i].ToString()).Append("</color>");
            }
            sb.Append("\n  <color=#9aa7b2>不満 </color>");
            AppendBar(sb, d.Grievance, "#e0a06a");
            sb.Append('\n');

            // 一代記
            sb.Append("\n<color=#e7e0b0>◤ 一代記（新しい順）</color>\n");
            var recent = ProtagonistChronicleRules.Recent(d.Chronicle, 8);
            if (recent.Count == 0) sb.Append("  <color=#9aa7b2>（記録なし）</color>\n");
            for (int i = 0; i < recent.Count; i++)
            {
                ChronicleEntry e = recent[i];
                sb.Append("  <color=#6f8a9a>第").Append(e.monthIndex).Append("月</color> 【")
                  .Append(e.kind).Append("】 ").Append(e.note).Append('\n');
            }

            sb.Append("\n<color=#6f8a9a>※ 主命は君主の大方針が指揮系統で噛み砕かれた末端目標（MBO）。達成で武勲と恩義、月次評定で昇進。</color>");
            return sb.ToString();
        }

        private void AppendCascade(StringBuilder sb, ProtagonistCareerDirector d)
        {
            var c = d.Cascade;
            if (c == null || c.Count == 0) return;
            sb.Append("  <color=#9fb0c0>◇ 主命の落とし込み（MBO）</color>\n");
            for (int i = 0; i < c.Count; i++)
            {
                CascadeLevel lv = c[i];
                sb.Append("    ");
                for (int t = 0; t < i; t++) sb.Append("　");
                sb.Append("└ <color=#9ad0ff>").Append(d.RankName(lv.tier)).Append("</color> ")
                  .Append(d.ResolveName(lv.holderId)).Append("  規模 ").Append(Mathf.RoundToInt(lv.scope));
                if (lv.parentIndex >= 0 && lv.parentIndex < c.Count)
                {
                    float contrib = MandateCascadeRules.Contribution(lv, c[lv.parentIndex]);
                    sb.Append("  <color=#6f8a9a>(寄与 ").Append(Mathf.RoundToInt(contrib * 100f)).Append("%)</color>");
                }
                sb.Append('\n');
            }
        }

        private void AppendPyramid(StringBuilder sb, ProtagonistCareerDirector d, Person me)
        {
            sb.Append("\n<color=#e7e0b0>◤ 階級ピラミッド（自勢力）</color>\n");
            if (me == null || !MilitaryRankRegistry.Has(me.faction))
            {
                sb.Append("  <color=#9aa7b2>（未整備）</color>\n");
                return;
            }
            RankDistribution dist = MilitaryRankRegistry.Get(me.faction);
            int total = RankDistributionRules.TotalForce(dist);
            if (total <= 0) { sb.Append("  <color=#9aa7b2>（未整備）</color>\n"); return; }

            MilitaryGrade myGrade = RankDistributionRules.GradeForOfficerTier(me.rankTier);
            MilitaryGrade[] show =
            {
                MilitaryGrade.元帥, MilitaryGrade.大将, MilitaryGrade.准将, MilitaryGrade.大佐,
                MilitaryGrade.少尉, MilitaryGrade.准尉, MilitaryGrade.曹長, MilitaryGrade.二等兵,
            };
            for (int i = 0; i < show.Length; i++)
            {
                MilitaryGrade g = show[i];
                bool mine = g == myGrade;
                sb.Append("  ");
                sb.Append(mine ? "<color=#ffd700>★ " : "　 ");
                sb.Append(g.ToString()).Append("  ").Append(dist.Get(g)).Append(" 名");
                if (mine) sb.Append("（現在地）</color>");
                sb.Append('\n');
            }

            int nextTier = Mathf.Min(10, me.rankTier + 1);
            int[] target = RankDistributionRules.PyramidTarget(total, RankDistributionRules.PyramidParams.Default);
            int vac = RankDistributionRules.Vacancy(dist, RankDistributionRules.GradeForOfficerTier(nextTier), target);
            bool can = RankPyramidDirector.Instance != null && RankPyramidDirector.Instance.CanPromote(me.faction, nextTier);
            sb.Append("  <color=#6f8a9a>次階級 ").Append(d.RankName(nextTier)).Append(" の定員空き ").Append(vac)
              .Append(can ? "（昇進余地あり）" : "（狭き門＝空き待ち）").Append("</color>\n");
        }

        // 会戦成長（GrowthRegistry）を反映した実効能力を1行表示（基準→実効・成長分を+表示）。
        private void AppendStat(StringBuilder sb, string label, int baseStat, Growth g)
        {
            int grown = AdmiralGrowthRules.GrownStat(baseStat, g);
            sb.Append("  ").Append(label).Append("  <color=#9ad0ff>").Append(grown).Append("</color>");
            if (grown > baseStat) sb.Append(" <color=#8ce08c>(+").Append(grown - baseStat).Append(")</color>");
            sb.Append('\n');
        }

        private static string ForkColor(CareerFork f)
        {
            switch (f)
            {
                case CareerFork.忠勤: return "<color=#9ad0ff>";
                case CareerFork.政界転身: return "<color=#c9a0ff>";
                case CareerFork.独立: return "<color=#ffd700>";
                case CareerFork.亡命: return "<color=#ff9a8a>";
                default: return "<color=#e0c060>"; // 下野
            }
        }

        private void AppendBar(StringBuilder sb, float v01, string colorHex)
        {
            v01 = Mathf.Clamp01(v01);
            int filled = Mathf.RoundToInt(v01 * barWidth);
            sb.Append("<color=").Append(colorHex).Append('>');
            for (int i = 0; i < barWidth; i++) sb.Append(i < filled ? '█' : '░');
            sb.Append("</color> ").Append(v01.ToString("0.00"));
        }

        private void OnSubmitPetition()
        {
            var d = ProtagonistCareerDirector.Instance;
            if (d != null) d.SubmitPetition();
            if (bodyLabel != null) bodyLabel.text = BuildDump();
        }

        // ===== UI 構築（RingiObserverOverlay と同型＋具申ボタン） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("ProtagonistDeskCanvas");
            overlayRoot.transform.SetParent(transform);
            Canvas canvas = overlayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            CanvasScaler scaler = overlayRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            overlayRoot.AddComponent<GraphicRaycaster>();

            panel = new GameObject("DeskPanel");
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
            GameObject frame = new GameObject("DeskFrame");
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

            WindowChrome.AddTitleBarLayout(frameRT, "執務机", () => SetVisible(false));
            BuildPetitionButton(frame.transform);
            BuildScrollBody(frame.transform);
        }

        private void BuildPetitionButton(Transform parent)
        {
            GameObject go = new GameObject("SubmitPetitionButton");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = 42f; le.preferredHeight = 42f;
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.30f, 0.46f, 1f);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(OnSubmitPetition);

            GameObject lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            RectTransform lrt = lblGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.sizeDelta = Vector2.zero; lrt.anchoredPosition = Vector2.zero;
            TextMeshProUGUI lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = "上官へ具申する（建白を起案）";
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.fontSize = 18f;
            lbl.color = new Color(0.92f, 0.95f, 1f);
            lbl.raycastTarget = false;
            ApplyJapaneseFont(lbl);
        }

        private void BuildScrollBody(Transform parent)
        {
            GameObject scrollObj = new GameObject("DeskScrollRect");
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
