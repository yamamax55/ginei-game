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
    /// 人事オーバーレイ（観測層・read-only）。<b>P キー</b>または上メニュー「人事」で開閉し、<b>タブ（指導者／軍人／文民）</b>で
    /// 人物を切り替えて一覧する。指導者＝君主/元首・政治家、軍人＝提督（<see cref="AdmiralData"/>）＋戦略の武官ロスター、
    /// 文民＝文官/官僚/技術者（<see cref="GalaxyView.CivilianRoster"/>）。職分の振り分けは <see cref="PersonVocationRules"/>。
    /// 観測専用＝状態は変えない。Strategy/Battle へ自動生成（`HelpOverlay`/`TimeDisplay` と同型）。
    /// </summary>
    public class PersonObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1092;
        public float dimAlpha = 0.92f;
        public float bodyFontSize = 18f;
        [Tooltip("一覧に出す最大人数（超過分は『他N名』と表示）")]
        public int maxPersons = 40;

        public Color accentColor = new Color(1f, 0.84f, 0.36f, 1f);

        private static readonly string[] TabLabels = { "指導者", "軍人", "文民" };

        private GameObject root;
        private TextMeshProUGUI bodyLabel;
        private TMP_FontAsset jpFont;
        private int activeTab = 1; // 既定＝軍人（従来の表示に近い）
        private readonly List<Image> tabBgs = new List<Image>();
        private readonly List<TextMeshProUGUI> tabTexts = new List<TextMeshProUGUI>();

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
            if (UnityEngine.Object.FindAnyObjectByType<PersonObserverOverlay>() != null) return;
            new GameObject("PersonObserverOverlay").AddComponent<PersonObserverOverlay>();
        }

        private object escWindowToken; // UIWindowStack 登録トークン（#ウィンドウESC）

        private void Awake()
        {
            jpFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            EnsureEventSystem();
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => root != null && root.activeSelf, () => SetVisible(false), canvasSortingOrder, "人事");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.人物名鑑切替)) Toggle();
            if (root != null && root.activeSelf && bodyLabel != null)
                bodyLabel.text = BuildDump();
        }

        public void Toggle() { SetVisible(root != null && !root.activeSelf); }
        public void SetVisible(bool v) { if (root != null) root.SetActive(v); }

        private void SetTab(int i)
        {
            activeTab = Mathf.Clamp(i, 0, TabLabels.Length - 1);
            UpdateTabVisuals();
        }

        // ===== 集約＋整形 =====

        private string BuildDump()
        {
            var sb = new StringBuilder(4096);
            sb.Append("<b>人事</b>　").Append(TabLabels[activeTab]).Append("　(P で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            switch (activeTab)
            {
                case 0: BuildLeaders(sb); break;
                case 1: BuildMilitary(sb); break;
                default: BuildCivilians(sb); break;
            }
            return sb.ToString();
        }

        // ----- 指導者（君主/元首・政治家） -----

        private void BuildLeaders(StringBuilder sb)
        {
            var gv = UnityEngine.Object.FindAnyObjectByType<GalaxyView>();
            if (gv == null || gv.CivilianRoster == null)
            {
                sb.Append("\n<color=#ffcc66>指導者データは戦略マップ（GalaxyView）でのみ表示されます。</color>");
                return;
            }
            int shown = 0, total = 0;
            for (int i = 0; i < gv.CivilianRoster.Count; i++)
            {
                Person p = gv.CivilianRoster[i];
                if (p == null) continue;
                var v = PersonVocationRules.VocationOf(p);
                if (v != PersonVocation.君主 && v != PersonVocation.政治家) continue;
                total++;
                if (shown < maxPersons) { AppendLeader(sb, p, v); shown++; }
            }
            if (total == 0) sb.Append("\n<color=#ffcc66>指導者（君主/元首・政治家）が居ません。</color>");
            else { if (total > shown) sb.Append($"\n<color=#8aa0b0>…他 {total - shown} 名</color>"); sb.Append($"\n\n<color=#8aa0b0>指導者 計 {total} 名</color>"); }
        }

        private void AppendLeader(StringBuilder sb, Person p, PersonVocation v)
        {
            bool ruler = v == PersonVocation.君主;
            string label = ruler ? "君主/元首" : "政治家";
            string col = ruler ? "#ffd54a" : "#bfe9c0";
            sb.Append($"\n<color={col}>◆ [{label}] {p.name}</color>　<color=#9fb0c0>[{p.faction}]</color>\n");
            sb.Append($"  統率 {p.leadership} ／ 運営 {p.operation} ／ 情報 {p.intelligence}\n");
        }

        // ----- 軍人（提督 AdmiralData ＋ 戦略の武官ロスター Person） -----

        private void BuildMilitary(StringBuilder sb)
        {
            var gv = UnityEngine.Object.FindAnyObjectByType<GalaxyView>();
            bool any = false;

            if (gv != null && gv.CommanderRoster != null && gv.CommanderRoster.Count > 0)
            {
                sb.Append("\n<color=#5b6b7a>── 武官（戦略ロスター）──</color>\n");
                int shown = 0;
                for (int i = 0; i < gv.CommanderRoster.Count && shown < maxPersons; i++)
                {
                    Person p = gv.CommanderRoster[i];
                    if (p == null) continue;
                    AppendMilitaryPerson(sb, p); shown++; any = true;
                }
                if (gv.CommanderRoster.Count > shown) sb.Append($"\n<color=#8aa0b0>…他 {gv.CommanderRoster.Count - shown} 名</color>\n");
            }

            var admirals = ContentDatabase.AllAdmirals();
            if (admirals != null && admirals.Count > 0)
            {
                sb.Append("\n<color=#5b6b7a>── 提督（シナリオ）──</color>\n");
                int shown = Mathf.Min(admirals.Count, maxPersons);
                for (int i = 0; i < shown; i++) AppendAdmiral(sb, admirals[i]);
                if (admirals.Count > shown) sb.Append($"\n<color=#8aa0b0>…他 {admirals.Count - shown} 名</color>");
                any = true;
            }

            if (!any) sb.Append("\n<color=#ffcc66>軍人データがありません。</color>");
        }

        private void AppendMilitaryPerson(StringBuilder sb, Person p)
        {
            string rank = RankSystem.ResolveRankNameOrDefault(null, p.rankTier);
            string rankPart = string.IsNullOrEmpty(rank) ? "" : rank + " ";
            sb.Append($"\n<color=#bfe9c0>◆ {rankPart}{p.name}</color>　<color=#9fb0c0>[{p.faction}]</color>　<color=#8aa0b0>{p.serviceStatus}</color>\n");
            sb.Append($"  統率 {p.leadership} ／ 攻撃 {p.attack} ／ 防御 {p.defense} ／ 機動 {p.mobility} ／ 運営 {p.operation} ／ 情報 {p.intelligence}\n");
        }

        private void AppendAdmiral(StringBuilder sb, AdmiralData a)
        {
            if (a == null) return;
            string rank = RankSystem.ResolveRankNameOrDefault(null, a.rankTier);
            string rankPart = string.IsNullOrEmpty(rank) ? "" : rank + " ";
            string proto = a.isProtagonist ? "　<color=#ffd54a>★主人公</color>" : "";
            sb.Append($"\n<color=#bfe9c0>◆ {rankPart}{a.EpithetName}</color>　<color=#9fb0c0>[{a.faction}]</color>{proto}\n");
            sb.Append($"  統率 {a.EffectiveLeadership} ／ 攻撃 {a.EffectiveAttack} ／ 防御 {a.EffectiveDefense}");
            sb.Append($" ／ 機動 {a.EffectiveMobility} ／ 運営 {a.EffectiveOperation} ／ 情報 {a.EffectiveIntelligence}\n");
            string extra = $"  指揮可能規模 〜{CommandCapacityRules.MaxStrengthForTier(a.rankTier):#,0}隻";
            if (a.HasStaff) extra += $"　参謀: {a.GetStaffNames()}";
            if (a.hasPreferredFormation) extra += $"　得意陣形: {a.preferredFormation}";
            sb.Append(extra).Append('\n');
        }

        // ----- 文民（文官・官僚・技術者） -----

        private void BuildCivilians(StringBuilder sb)
        {
            var gv = UnityEngine.Object.FindAnyObjectByType<GalaxyView>();
            if (gv == null || gv.CivilianRoster == null)
            {
                sb.Append("\n<color=#ffcc66>文民データは戦略マップ（GalaxyView）でのみ表示されます。</color>");
                return;
            }
            float authority = gv.Court != null ? gv.Court.authority : 0f;
            sb.Append($"<color=#8aa0b0>朝廷の権威 {authority:0.00}（{RitsuryoFormalizationRules.PhaseOf(authority)}）＝官位の実権はこの権威で減衰</color>\n");

            int shown = 0, total = 0;
            for (int i = 0; i < gv.CivilianRoster.Count; i++)
            {
                Person p = gv.CivilianRoster[i];
                if (p == null) continue;
                var v = PersonVocationRules.VocationOf(p);
                if (v == PersonVocation.君主 || v == PersonVocation.政治家) continue; // 指導者タブへ
                total++;
                if (shown < maxPersons) { AppendCivil(sb, p, v, gv); shown++; }
            }
            if (total == 0) sb.Append("\n<color=#ffcc66>文官・官僚・技術者が居ません。</color>");
            else { if (total > shown) sb.Append($"\n<color=#8aa0b0>…他 {total - shown} 名</color>"); sb.Append($"\n<color=#8aa0b0>文民 計 {total} 名</color>"); }
        }

        private void AppendCivil(StringBuilder sb, Person p, PersonVocation v, GalaxyView gv)
        {
            string voc = v == PersonVocation.技術者 ? "技術者" : "文官";
            string ikai = JapaneseCourtRankRules.Name(p.courtRank);
            string kou = p.merit != null ? p.merit.lastRating.ToString() : "未評定";
            string noble = JapaneseCourtRankRules.IsNobility(p.courtRank) ? "　<color=#ffd54a>貴族</color>" : "";
            string post = gv != null ? gv.CivilPostOf(p) : "";
            string postPart = string.IsNullOrEmpty(post) ? "" : $"　<color=#ffd54a>在任:{post}</color>";
            sb.Append($"\n<color=#bfe9c0>◆ [{voc}] {ikai} {p.name}</color>　<color=#9fb0c0>[{p.faction}]</color>　考第:{kou}{noble}{postPart}\n");
            if (v == PersonVocation.技術者)
                sb.Append($"  運営 {p.operation} ／ 情報 {p.intelligence}　<color=#9aa7b3>研究 {p.research} ／ 技術 {p.engineering}</color>\n");
            else
                sb.Append($"  運営 {p.operation} ／ 情報 {p.intelligence}\n");
        }

        // ===== UI =====

        private void BuildUI()
        {
            var canvasObj = new GameObject("PersonObserverCanvas");
            canvasObj.transform.SetParent(transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();

            root = new GameObject("Root");
            root.transform.SetParent(canvasObj.transform, false);
            var rrt = root.AddComponent<RectTransform>();
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;
            var dimImage = root.AddComponent<Image>();
            dimImage.color = new Color(0.02f, 0.03f, 0.06f, dimAlpha);
            WindowChrome.MakeNonModal(dimImage);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(root.transform, false);
            var prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.06f, 0.06f); prt.anchorMax = new Vector2(0.94f, 0.94f);
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.11f, 0.96f);
            panel.AddComponent<RectMask2D>();

            WindowChrome.AddTitleBarAnchored(prt, "人事", () => SetVisible(false));
            BuildTabBar(panel.transform);

            float topReserve = WindowChrome.TitleBarHeight + 34f; // タイトルバー＋タブバー

            var labelGo = new GameObject("Body");
            labelGo.transform.SetParent(panel.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(20f, 20f); lrt.offsetMax = new Vector2(-20f, -(12f + topReserve));
            bodyLabel = labelGo.AddComponent<TextMeshProUGUI>();
            bodyLabel.fontSize = bodyFontSize;
            bodyLabel.color = new Color(0.92f, 0.94f, 0.97f);
            bodyLabel.alignment = TextAlignmentOptions.TopLeft;
            bodyLabel.enableWordWrapping = true;
            bodyLabel.raycastTarget = false;
            if (jpFont != null) bodyLabel.font = jpFont;

            UpdateTabVisuals();
            root.SetActive(false);
        }

        /// <summary>タイトルバー直下のタブ行（指導者／軍人／文民）。押すとその種別だけに切り替える。</summary>
        private void BuildTabBar(Transform panel)
        {
            var bar = new GameObject("TabBar");
            bar.transform.SetParent(panel, false);
            var brt = bar.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.sizeDelta = new Vector2(0f, 32f);
            brt.anchoredPosition = new Vector2(0f, -WindowChrome.TitleBarHeight);

            var hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 8, 2, 2);
            hlg.spacing = 4f;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

            for (int i = 0; i < TabLabels.Length; i++) BuildTabButton(bar.transform, i, TabLabels[i]);
        }

        private void BuildTabButton(Transform parent, int index, string text)
        {
            var go = new GameObject("Tab_" + text);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.13f, 0.16f, 0.22f, 1f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            int idx = index;
            btn.onClick.AddListener(() => SetTab(idx));

            var t = new GameObject("Text").AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(go.transform, false);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            t.text = text; t.fontSize = 16f; t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.7f, 0.76f, 0.84f); t.raycastTarget = false;
            if (jpFont != null) t.font = jpFont;

            tabBgs.Add(img);
            tabTexts.Add(t);
        }

        private void UpdateTabVisuals()
        {
            for (int i = 0; i < tabBgs.Count; i++)
            {
                bool active = i == activeTab;
                if (tabBgs[i] != null) tabBgs[i].color = active ? accentColor : new Color(0.13f, 0.16f, 0.22f, 1f);
                if (tabTexts[i] != null) tabTexts[i].color = active ? new Color(0.08f, 0.09f, 0.12f) : new Color(0.7f, 0.76f, 0.84f);
            }
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
    }
}
