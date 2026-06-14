using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
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

        // 人物名のクリック（TMP リンク）→ 詳細カード（士官情報）
        private readonly Dictionary<string, object> linkTargets = new Dictionary<string, object>();
        private int linkSeq;
        private GameObject detailRoot;
        private Image detailPortrait;
        private TextMeshProUGUI detailPortraitInitial;
        private TextMeshProUGUI detailTitle;
        private TextMeshProUGUI detailBody;
        private object escDetailToken;

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

        private void OnDestroy()
        {
            UIWindowStack.Unregister(escWindowToken);
            UIWindowStack.Unregister(escDetailToken);
        }

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.人物名鑑切替)) Toggle();
            if (root != null && root.activeSelf && bodyLabel != null)
            {
                bodyLabel.text = BuildDump();
                HandleLinkClick();
            }
        }

        /// <summary>本文の人物名（TMP リンク）をクリックしたら詳細カードを開く。</summary>
        private void HandleLinkClick()
        {
            if (detailRoot != null && detailRoot.activeSelf) return; // 詳細表示中は本文クリックを無視
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            Vector2 mp = Mouse.current.position.ReadValue();
            int li = TMP_TextUtilities.FindIntersectingLink(bodyLabel, mp, null); // overlay＝camera null
            if (li < 0) return;
            string id = bodyLabel.textInfo.linkInfo[li].GetLinkID();
            if (linkTargets.TryGetValue(id, out object obj)) OpenDetail(obj);
        }

        /// <summary>クリック対象をリンクID へ登録し、`&lt;link&gt;` のIDを返す（BuildDump 毎に採番リセット）。</summary>
        private string Link(object o)
        {
            string id = "L" + (linkSeq++);
            linkTargets[id] = o;
            return id;
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
            linkTargets.Clear(); linkSeq = 0; // 人物名リンクを毎回採番し直す
            sb.Append("<b>人事</b>　").Append(TabLabels[activeTab]).Append("　<color=#8aa0b0>(名前クリックで詳細／P で閉じる)</color>\n");
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
            sb.Append($"\n<color={col}>◆ [{label}] <link=\"{Link(p)}\">{p.name}</link></color>　<color=#9fb0c0>[{p.faction}]</color>\n");
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
            sb.Append($"\n<color=#bfe9c0>◆ {rankPart}<link=\"{Link(p)}\">{p.name}</link></color>　<color=#9fb0c0>[{p.faction}]</color>　<color=#8aa0b0>{p.serviceStatus}</color>\n");
            sb.Append($"  統率 {p.leadership} ／ 攻撃 {p.attack} ／ 防御 {p.defense} ／ 機動 {p.mobility} ／ 運営 {p.operation} ／ 情報 {p.intelligence}\n");
        }

        private void AppendAdmiral(StringBuilder sb, AdmiralData a)
        {
            if (a == null) return;
            string rank = RankSystem.ResolveRankNameOrDefault(null, a.rankTier);
            string rankPart = string.IsNullOrEmpty(rank) ? "" : rank + " ";
            string proto = a.isProtagonist ? "　<color=#ffd54a>★主人公</color>" : "";
            sb.Append($"\n<color=#bfe9c0>◆ {rankPart}<link=\"{Link(a)}\">{a.EpithetName}</link></color>　<color=#9fb0c0>[{a.faction}]</color>{proto}\n");
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
            sb.Append($"\n<color=#bfe9c0>◆ [{voc}] {ikai} <link=\"{Link(p)}\">{p.name}</link></color>　<color=#9fb0c0>[{p.faction}]</color>　考第:{kou}{noble}{postPart}\n");
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

            BuildDetail(canvasObj.transform);

            UpdateTabVisuals();
            root.SetActive(false);
        }

        // ===== 詳細カード（士官情報・人物名クリックで開く） =====

        private void BuildDetail(Transform canvasTr)
        {
            detailRoot = new GameObject("Detail");
            detailRoot.transform.SetParent(canvasTr, false);
            var drt = detailRoot.AddComponent<RectTransform>();
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            var dim = detailRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            var dimBtn = detailRoot.AddComponent<Button>(); // 外側クリックで閉じる
            dimBtn.transition = UnityEngine.UI.Selectable.Transition.None;
            dimBtn.onClick.AddListener(CloseDetail);

            // フレーム（中央・士官情報カード）
            var frame = new GameObject("DetailFrame");
            frame.transform.SetParent(detailRoot.transform, false);
            var frt = frame.AddComponent<RectTransform>();
            frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
            frt.pivot = new Vector2(0.5f, 0.5f);
            frt.sizeDelta = new Vector2(660f, 380f);
            frame.AddComponent<Image>().color = new Color(0.10f, 0.13f, 0.20f, 0.99f);

            // 肖像（プレースホルダ＝陣営色＋頭文字。専用立ち絵アセットは未整備のため簡易表示）
            var portGo = new GameObject("Portrait");
            portGo.transform.SetParent(frame.transform, false);
            var portRt = portGo.AddComponent<RectTransform>();
            portRt.anchorMin = new Vector2(0f, 1f); portRt.anchorMax = new Vector2(0f, 1f);
            portRt.pivot = new Vector2(0f, 1f);
            portRt.sizeDelta = new Vector2(150f, 170f);
            portRt.anchoredPosition = new Vector2(22f, -22f);
            detailPortrait = portGo.AddComponent<Image>();
            detailPortrait.color = new Color(0.2f, 0.25f, 0.35f, 1f);
            detailPortraitInitial = MakeLabel(portGo.transform, "", 64f, Color.white);
            detailPortraitInitial.alignment = TextAlignmentOptions.Center;
            var pirt = detailPortraitInitial.rectTransform;
            pirt.anchorMin = Vector2.zero; pirt.anchorMax = Vector2.one;
            pirt.offsetMin = Vector2.zero; pirt.offsetMax = Vector2.zero;

            // 氏名＋階級（上部・肖像の右）
            detailTitle = MakeLabel(frame.transform, "", 24f, new Color(1f, 0.85f, 0.4f));
            var trt = detailTitle.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0f, 1f);
            trt.offsetMin = new Vector2(190f, -52f); trt.offsetMax = new Vector2(-16f, -16f);
            detailTitle.alignment = TextAlignmentOptions.TopLeft;

            // 諸元（肖像の右・本文）
            detailBody = MakeLabel(frame.transform, "", 18f, new Color(0.92f, 0.94f, 0.97f));
            var brt = detailBody.rectTransform;
            brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 1f);
            brt.offsetMin = new Vector2(190f, 54f); brt.offsetMax = new Vector2(-16f, -56f);
            detailBody.alignment = TextAlignmentOptions.TopLeft;
            detailBody.enableWordWrapping = true;

            // 閉じる
            var btnObj = new GameObject("Close");
            btnObj.transform.SetParent(frame.transform, false);
            var crt = btnObj.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(1f, 0f); crt.anchorMax = new Vector2(1f, 0f);
            crt.pivot = new Vector2(1f, 0f);
            crt.sizeDelta = new Vector2(120f, 38f);
            crt.anchoredPosition = new Vector2(-16f, 14f);
            btnObj.AddComponent<Image>().color = new Color(0.26f, 0.40f, 0.56f, 1f);
            var cbtn = btnObj.AddComponent<Button>();
            cbtn.transition = UnityEngine.UI.Selectable.Transition.None;
            cbtn.onClick.AddListener(CloseDetail);
            var clabel = MakeLabel(btnObj.transform, "閉じる", 18f, Color.white);
            clabel.alignment = TextAlignmentOptions.Center;
            var clrt = clabel.rectTransform;
            clrt.anchorMin = Vector2.zero; clrt.anchorMax = Vector2.one;
            clrt.offsetMin = Vector2.zero; clrt.offsetMax = Vector2.zero;

            // ESC は詳細カードを先に閉じる（人事本体より手前）。
            escDetailToken = UIWindowStack.Register(() => detailRoot != null && detailRoot.activeSelf, CloseDetail, canvasSortingOrder + 1, "士官情報");

            detailRoot.SetActive(false);
        }

        private void OpenDetail(object obj)
        {
            if (obj == null || detailRoot == null) return;
            if (obj is AdmiralData a) FillAdmiral(a);
            else if (obj is Person p) FillPerson(p);
            else return;
            detailRoot.transform.SetAsLastSibling();
            detailRoot.SetActive(true);
        }

        private void CloseDetail() { if (detailRoot != null) detailRoot.SetActive(false); }

        private void FillAdmiral(AdmiralData a)
        {
            string rank = RankSystem.ResolveRankNameOrDefault(null, a.rankTier);
            SetPortrait(a.faction, a.ShortName);
            detailTitle.text = $"{(string.IsNullOrEmpty(rank) ? "" : rank + " ")}{a.EpithetName}";
            var sb = new StringBuilder(512);
            sb.Append($"<color=#9fb0c0>所属</color> {a.faction}　<color=#9fb0c0>職分</color> 武官（提督）\n");
            if (a.isProtagonist) sb.Append("<color=#ffd54a>★主人公</color>\n");
            sb.Append('\n');
            sb.Append($"統率 <b>{a.EffectiveLeadership}</b>　　攻撃 <b>{a.EffectiveAttack}</b>　　防御 <b>{a.EffectiveDefense}</b>\n");
            sb.Append($"機動 <b>{a.EffectiveMobility}</b>　　運営 <b>{a.EffectiveOperation}</b>　　情報 <b>{a.EffectiveIntelligence}</b>\n\n");
            sb.Append($"<color=#9fb0c0>指揮可能規模</color> 〜{CommandCapacityRules.MaxStrengthForTier(a.rankTier):#,0} 隻\n");
            if (a.HasStaff) sb.Append($"<color=#9fb0c0>参謀</color> {a.GetStaffNames()}\n");
            if (a.hasPreferredFormation) sb.Append($"<color=#9fb0c0>得意陣形</color> {a.preferredFormation}\n");
            detailBody.text = sb.ToString();
        }

        private void FillPerson(Person p)
        {
            var voc = PersonVocationRules.VocationOf(p);
            string vlabel = voc.ToString();
            string rank = p.role == PersonRole.軍人 ? RankSystem.ResolveRankNameOrDefault(null, p.rankTier) : "";
            string ikai = JapaneseCourtRankRules.Name(p.courtRank);
            SetPortrait(p.faction, p.name);
            detailTitle.text = $"{(string.IsNullOrEmpty(rank) ? "" : rank + " ")}{p.name}";

            var sb = new StringBuilder(512);
            sb.Append($"<color=#9fb0c0>所属</color> {p.faction}　<color=#9fb0c0>職分</color> {vlabel}　<color=#9fb0c0>状態</color> {p.serviceStatus}\n");
            if (p.role == PersonRole.文民) sb.Append($"<color=#9fb0c0>位階</color> {ikai}　<color=#9fb0c0>考第</color> {(p.merit != null ? p.merit.lastRating.ToString() : "未評定")}\n");
            sb.Append('\n');
            sb.Append($"統率 <b>{p.leadership}</b>　　攻撃 <b>{p.attack}</b>　　防御 <b>{p.defense}</b>\n");
            sb.Append($"機動 <b>{p.mobility}</b>　　運営 <b>{p.operation}</b>　　情報 <b>{p.intelligence}</b>\n");
            if (voc == PersonVocation.技術者 || p.research > 0 || p.engineering > 0)
                sb.Append($"\n<color=#9aa7b3>研究 {p.research}　技術 {p.engineering}　計画 {p.planning}　生産 {p.production}</color>\n");
            if (p.role == PersonRole.軍人)
                sb.Append($"\n<color=#9fb0c0>指揮可能規模</color> 〜{CommandCapacityRules.MaxStrengthForTier(p.rankTier):#,0} 隻\n");

            AppendPrivateAssets(sb, p);
            detailBody.text = sb.ToString();
        }

        /// <summary>人物の私有財産（流動資産＋ネームド資産/金融資産/不動産）を詳細カードへ追記する（#2056/#2063/#2070）。</summary>
        private void AppendPrivateAssets(StringBuilder sb, Person p)
        {
            int pid = p.id;
            float liquid = p.wealth;
            float netWorth = NamedAssetEffectRules.TotalNetWorthOfPerson(pid, liquid);
            float income = NamedAssetEffectRules.PersonAnnualIncome(pid);
            var named = NamedAssetRegistry.OwnedByPerson(pid);
            var holdings = FinancialHoldingRegistry.OwnedByPerson(pid);
            var deeds = PropertyDeedRegistry.OwnedByPerson(pid);

            sb.Append("\n<color=#5b6b7a>──── 私有財産 ────</color>\n");
            sb.Append($"<color=#9fb0c0>流動資産</color> {liquid:#,0}　<color=#9fb0c0>特性</color> {p.financialTrait}　<color=#9fb0c0>年収</color> {income:#,0}\n");
            sb.Append($"<color=#9fb0c0>総資産</color> <b><color=#ffe08a>{netWorth:#,0}</color></b>\n");

            const int Cap = 5;
            if (named != null && named.Count > 0)
            {
                sb.Append($"<color=#9fb0c0>ネームド資産</color>（{named.Count}件・計 {NamedAssetRegistry.TotalValueOfPerson(pid):#,0}）\n");
                for (int i = 0; i < named.Count && i < Cap; i++)
                    sb.Append($"  ・[{named[i].category}] {named[i].name}　{named[i].value:#,0}\n");
            }
            if (holdings != null && holdings.Count > 0)
            {
                float mv = 0f; for (int i = 0; i < holdings.Count; i++) mv += FinancialAssetRules.MarketValue(holdings[i]);
                sb.Append($"<color=#9fb0c0>金融資産</color>（{holdings.Count}件・時価 {mv:#,0}）\n");
                for (int i = 0; i < holdings.Count && i < Cap; i++)
                    sb.Append($"  ・{holdings[i].instrument} {holdings[i].units:#,0}口　時価 {FinancialAssetRules.MarketValue(holdings[i]):#,0}\n");
            }
            if (deeds != null && deeds.Count > 0)
            {
                float dv = 0f; for (int i = 0; i < deeds.Count; i++) dv += PropertyValuationRules.DeedValue(deeds[i]);
                sb.Append($"<color=#9fb0c0>不動産</color>（{deeds.Count}件・評価 {dv:#,0}）\n");
                for (int i = 0; i < deeds.Count && i < Cap; i++)
                    sb.Append($"  ・星系{deeds[i].systemId}　持分 {deeds[i].share:0.##}　評価 {PropertyValuationRules.DeedValue(deeds[i]):#,0}\n");
            }
            bool noAssets = (named == null || named.Count == 0) && (holdings == null || holdings.Count == 0) && (deeds == null || deeds.Count == 0);
            if (noAssets) sb.Append("<color=#7a8694>（固有資産なし＝流動資産のみ。年次で資産が形成される）</color>\n");
        }

        /// <summary>肖像プレースホルダを陣営色＋頭文字で設定する（専用立ち絵は未整備）。</summary>
        private void SetPortrait(Faction fac, string name)
        {
            Color c = fac == Faction.帝国 ? new Color(0.45f, 0.18f, 0.18f, 1f) : new Color(0.16f, 0.24f, 0.42f, 1f);
            if (detailPortrait != null) detailPortrait.color = c;
            if (detailPortraitInitial != null)
                detailPortraitInitial.text = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1);
        }

        /// <summary>詳細カード用の TMP ラベル生成（子・フォント適用）。</summary>
        private TextMeshProUGUI MakeLabel(Transform parent, string text, float size, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color;
            t.raycastTarget = false; t.richText = true;
            if (jpFont != null) t.font = jpFont;
            return t;
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
