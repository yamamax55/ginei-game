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
    /// 政治オブザーバ（観測層・read-only）。<b>O キー</b>で開閉し、勢力ごとの**配線済みの政治状態**
    /// （<see cref="FactionState.politics"/>＝政党リスト・衆参の選挙日程・分断危機）と、そこから導かれる
    /// 民主主義成熟度（<see cref="PartySystemRules.MaturityFrom"/>）・有効政党数（Laakso–Taagepera）・
    /// 分極化（<see cref="PartySystemRules.Polarization"/>）・与党/野党（<see cref="PartyMembershipRules.RoleOf"/>＝組閣と確定議席から）を
    /// 毎フレームライブダンプする。`GalaxyView.RunPoliticsTick`（年次）が回している分＝盤面で実際に動く政治だけを映す。
    /// 操作はさせない＝**観測専用＝状態は変えない**。`CampaignObserverOverlay`（G）の政治版。
    /// `HelpOverlay`/`TimeDisplay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class PoliticsObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1097;
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
            if (Object.FindAnyObjectByType<PoliticsObserverOverlay>() != null) return;
            new GameObject("PoliticsObserverOverlay").AddComponent<PoliticsObserverOverlay>();
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "政治");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.政治観測切替))
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

            sb.Append("<b>政治オブザーバ</b>　政党・有効政党数・分極化・国政選挙（議席/政府）・地方選挙（星系知事）　(O で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            if (c == null || c.states == null || c.states.Count == 0)
            {
                sb.Append("\n<color=#ffcc66>戦役データ（StrategySession.Campaign）がありません。</color>\n");
                sb.Append("戦略マップ（GalaxyView）を一度起動すると、民主政治の勢力に政党がシードされ、\n");
                sb.Append("ここに有効政党数・分極化・衆参選挙日程がライブ表示されます。");
                return sb.ToString();
            }

            for (int i = 0; i < c.states.Count; i++)
            {
                FactionState s = c.states[i];
                if (s == null) continue;
                AppendFaction(sb, s);
            }

            sb.Append("\n<color=#6f8a9a>※ 成熟するほど二大政党へ収束（デュヴェルジェ）、だが二大政党で成熟するほど分極化（分断危機）。</color>");
            return sb.ToString();
        }

        /// <summary>試験用：いま表示する本文（観測専用＝状態は変えない）。</summary>
        public string DumpTextForTest => BuildDump();

        private void AppendFaction(StringBuilder sb, FactionState s)
        {
            sb.Append('\n').Append("<color=#e7e0b0>◤ ").Append(s.faction).Append("</color>\n");

            PoliticsState pol = s.politics;
            bool electoral = ElectoralSystemRules.IsElectoral(s.governmentForm);
            if (pol == null || pol.parties == null || pol.parties.Count == 0)
            {
                sb.Append("  <color=#9aa7b2>（非民主／政党なし＝選挙政治の対象外）</color>\n");
                AppendElections(sb, s, electoral);
                return;
            }

            float maturity = PartySystemRules.MaturityFrom(s);
            float enp      = PartySystemRules.EffectiveNumberOfParties(pol.parties);
            float polar    = PartySystemRules.Polarization(maturity, enp);
            bool  twoParty = PartySystemRules.IsTwoPartySystem(pol.parties);
            bool  crisis   = pol.dividedCrisisActive || PartySystemRules.IsDividedCrisis(maturity, enp, 0.6f);

            AppendBar(sb, "  民主成熟度", maturity, "#7fd4ff");
            sb.Append("  <color=#9fb0c0>有効政党数</color> ＝ <color=#ffe08a>").Append(enp.ToString("0.00"))
              .Append("</color>").Append(twoParty ? "　<color=#8ce08c>(二大政党制)</color>" : "").Append('\n');
            AppendBar(sb, "  分極化", polar, crisis ? "#ff7a6a" : "#ffd28a");
            if (crisis) sb.Append("    <color=#ff7a6a>⚠ 分断危機（二大政党の対立が深刻）</color>\n");

            // 政党一覧：与党/野党は組閣と確定議席から（支持率では決めない）。支持率・議席・ネームド政治家・国政議員・一般党員を分けて出す。
            if (galaxy == null) galaxy = Object.FindAnyObjectByType<GalaxyView>();
            for (int i = 0; i < pol.parties.Count; i++)
            {
                Party p = pol.parties[i];
                if (p == null) continue;
                PartyStatusSummary sum = PartyMembershipRules.Summarize(pol, p);
                bool isRuling = sum.role == PartyGovernmentRole.与党;
                sb.Append(isRuling ? "  <color=#ffd700>★</color>" : "   ");
                sb.Append(' ').Append(p.partyName).Append(' ').Append(RoleTag(sum.role));
                sb.Append("  <color=#9fb0c0>支持率</color>");
                AppendBarInline(sb, p.support, isRuling ? "#ffd700" : "#a0e0a0");
                sb.Append(' ').Append((p.support * 100f).ToString("0")).Append('%');
                sb.Append('\n');
                AppendPartyBreakdown(sb, p, sum);
            }

            AppendElections(sb, s, electoral);
        }

        private static string RoleTag(PartyGovernmentRole role)
        {
            switch (role)
            {
                case PartyGovernmentRole.与党: return "<color=#ffd700>[与党]</color>";
                case PartyGovernmentRole.野党: return "<color=#a0c8ff>[野党]</color>";
                case PartyGovernmentRole.議席なし: return "<color=#9aa7b2>[議席なし]</color>";
                default: return "<color=#9aa7b2>[与野党未確定＝組閣なし]</color>";
            }
        }

        /// <summary>政党の数字の内訳（確定議席・ネームド政治家・国政議員・一般党員＝単位と出所つき・不明は不明と書く）。</summary>
        private void AppendPartyBreakdown(StringBuilder sb, Party p, PartyStatusSummary sum)
        {
            sb.Append("      <color=#9fb0c0>確定議席</color> 下院 ").Append(sum.lowerSeats).Append("・上院 ").Append(sum.upperSeats)
              .Append("　<color=#9fb0c0>党首</color> ")
              .Append(p.HasLeader ? PersonName(p.leaderId) : "<color=#9aa7b2>空席</color>")
              .Append("　<color=#9fb0c0>ネームド政治家</color> ").Append(sum.namedPoliticians).Append("名")
              .Append("　<color=#9fb0c0>国政議員(実在)</color> 下").Append(sum.namedLowerLegislators)
              .Append("・上").Append(sum.namedUpperLegislators).Append("名\n");

            sb.Append("      <color=#9fb0c0>一般党員(全国)</color> ");
            PartyMembershipTally n = p.nationalMembership;
            if (sum.nationalMembershipKnown && n != null)
            {
                sb.Append(sum.nationalMembership.ToString("#,0")).Append("人");
                if (!string.IsNullOrEmpty(n.source)) sb.Append("（出所 ").Append(n.source);
                else sb.Append("（出所 未記載");
                if (n.asOfYear > 0) sb.Append("・SE").Append(n.asOfYear);
                sb.Append('）');
            }
            else sb.Append("<color=#9aa7b2>不明（未設定）</color>");
            sb.Append("　<color=#9fb0c0>地方集計</color> ");
            if (sum.regionalTalliesKnown > 0) sb.Append(sum.regionalTalliesKnown).Append("星系");
            else sb.Append("<color=#9aa7b2>不明（未設定）</color>");
            sb.Append('\n');
        }

        // ===== 国政選挙・地方選挙 =====

        private GalaxyView galaxy; // 人物名の解決に使う（無ければ ID 表示）

        private void AppendElections(StringBuilder sb, FactionState s, bool electoral)
        {
            PoliticsState pol = s.politics;
            if (galaxy == null) galaxy = Object.FindAnyObjectByType<GalaxyView>();

            // --- 国政選挙 ---
            sb.Append("  <color=#ffe08a>■ 国政選挙</color>\n");
            if (!electoral)
                sb.Append("    <color=#9aa7b2>対象外（政体 ").Append(s.governmentForm).Append("＝選挙なし・首相は任命/世襲の経路）</color>\n");
            if (pol == null)
            {
                if (electoral) sb.Append("    <color=#9aa7b2>未実施（次の年次で政党と両院を編成して初回選挙）</color>\n");
            }
            else
            {
                AppendChamber(sb, pol, pol.lowerSeats, pol.lowerHouse, "下院", "任期4年・全議席改選");
                AppendChamber(sb, pol, pol.upperSeats, pol.upperHouse, "上院", "任期6年・3年ごと半数改選");
                AppendGovernment(sb, pol);
                AppendRecentResults(sb, pol);
            }

            // --- 地方選挙 ---
            sb.Append("  <color=#ffe08a>■ 地方選挙（星系知事・任期4年）</color>\n");
            if (!electoral)
                sb.Append("    <color=#9aa7b2>対象外（知事は官位による任命制）</color>\n");
            if (pol == null || pol.locals == null || pol.locals.Count == 0)
            {
                if (electoral) sb.Append("    <color=#9aa7b2>未実施（次の年次で所有星系ごとに知事選の日程を組む）</color>\n");
                return;
            }
            for (int i = 0; i < pol.locals.Count; i++)
            {
                LocalElectionState rec = pol.locals[i];
                if (rec == null) continue;
                sb.Append("    ").Append(SystemName(rec.systemId)).Append("：");
                if (rec.governorPersonId >= 0)
                    sb.Append("知事 <color=#a0e0a0>").Append(PersonName(rec.governorPersonId)).Append("</color>（")
                      .Append(PartyName(pol, rec.governorPartyId)).Append("）任期〜SE").Append(rec.termEndYear);
                else
                    sb.Append("<color=#9aa7b2>知事 空席</color>");
                sb.Append("　[").Append(rec.status).Append(']');
                if (rec.nextElectionYear > 0) sb.Append("　次回 SE").Append(rec.nextElectionYear);
                sb.Append('\n');

                if (rec.lastResults != null && rec.lastResults.Count > 0 && rec.lastElectionYear > 0)
                {
                    sb.Append("      <color=#9fb0c0>直近 SE").Append(rec.lastElectionYear).Append("</color> ");
                    int shown = System.Math.Min(3, rec.lastResults.Count);
                    for (int k = 0; k < shown; k++)
                    {
                        LocalCandidateResult c = rec.lastResults[k];
                        if (k > 0) sb.Append(" / ");
                        sb.Append(PersonName(c.personId)).Append(' ').Append((c.voteShare * 100f).ToString("0")).Append('%');
                    }
                    if (rec.lastResults.Count > shown) sb.Append(" ほか").Append(rec.lastResults.Count - shown).Append('名');
                    sb.Append('\n');
                }
                if (!string.IsNullOrEmpty(rec.reason))
                    sb.Append("      <color=#ffb070>理由：").Append(rec.reason).Append("</color>\n");
            }
        }

        private void AppendChamber(StringBuilder sb, PoliticsState pol, ChamberSeats seats, ChamberSchedule schedule, string label, string rule)
        {
            sb.Append("    <color=#9fb0c0>").Append(label).Append("</color>");
            if (seats != null && seats.seated)
            {
                sb.Append(' ').Append(seats.TotalSeats).Append("議席（過半数 ")
                  .Append(ElectionCycleRules.MajorityLine(seats.TotalSeats)).Append('）');
            }
            else sb.Append(" <color=#9aa7b2>未構成</color>");
            sb.Append("　次回 ").Append(schedule != null ? "SE" + schedule.nextElectionYear : "—");
            if (schedule != null && schedule.chamber == LegislativeChamber.上院 && seats != null && seats.seated)
                sb.Append("（改選区分 ").Append(schedule.currentClass == 0 ? "A" : "B").Append('）');
            sb.Append("　<color=#6f8a9a>").Append(rule).Append("</color>\n");

            if (seats == null || !seats.seated || seats.parties == null) return;
            bool upper = seats.chamber == LegislativeChamber.上院;
            sb.Append("      ");
            int n = 0;
            for (int i = 0; i < seats.parties.Count; i++)
            {
                PartySeatCount e = seats.parties[i];
                if (e == null || e.Total <= 0) continue;
                if (n++ > 0) sb.Append(" / ");
                sb.Append(PartyName(pol, e.partyId)).Append(' ').Append(e.Total);
                if (upper) sb.Append("（A").Append(e.classA).Append("+B").Append(e.classB).Append('）');
            }
            if (n == 0) sb.Append("<color=#9aa7b2>議席なし</color>");
            sb.Append('\n');
            AppendLegislators(sb, pol, seats);
        }

        [Header("議員名簿")]
        [Tooltip("議院ごとに一覧表示する実在議員の上限（超えた分は件数だけ表示）")]
        public int maxLegislatorsShown = 40;

        /// <summary>党ごとの実在議員数と集計議席の内訳、実在議員の一覧（院・区分・当選回数・記録開始）。</summary>
        private void AppendLegislators(StringBuilder sb, PoliticsState pol, ChamberSeats seats)
        {
            LegislativeChamber chamber = seats.chamber;
            sb.Append("      <color=#9fb0c0>内訳</color> ");
            int n = 0;
            for (int i = 0; i < seats.parties.Count; i++)
            {
                PartySeatCount e = seats.parties[i];
                if (e == null || e.Total <= 0) continue;
                if (n++ > 0) sb.Append(" / ");
                sb.Append(PartyName(pol, e.partyId)).Append(" 人物 <color=#a0e0a0>")
                  .Append(LegislatorRosterRules.NamedSeats(pol, chamber, e.partyId)).Append("</color>・集計 ")
                  .Append(LegislatorRosterRules.AggregateSeats(pol, chamber, e.partyId));
            }
            sb.Append('\n');

            System.Collections.Generic.List<LegislatorRecord> members = LegislatorRosterRules.SeatedMembers(pol, chamber);
            if (members.Count == 0)
            {
                sb.Append("      <color=#9aa7b2>実在の議員なし（全議席が集計議席）</color>\n");
                return;
            }
            int shown = Mathf.Min(members.Count, Mathf.Max(0, maxLegislatorsShown));
            for (int i = 0; i < shown; i++)
            {
                LegislatorRecord r = members[i];
                sb.Append("      ・").Append(PersonName(r.personId)).Append("（").Append(PartyName(pol, r.seatPartyId)).Append("）");
                if (chamber == LegislativeChamber.上院) sb.Append(" 区分").Append(r.seatClass == 0 ? "A" : "B");
                sb.Append(" 当選").Append(r.TotalWins).Append("回（下").Append(r.TotalLowerWins).Append("・上").Append(r.TotalUpperWins)
                  .Append("）連続").Append(r.consecutiveWins).Append("回");
                if (r.firstWinYear > 0) sb.Append(" 初当選SE").Append(r.firstWinYear);
                if (r.lastWinYear > 0) sb.Append(" 直近SE").Append(r.lastWinYear);
                sb.Append(r.priorKnown
                    ? "　<color=#6f8a9a>開始前の経歴はシナリオ明示</color>"
                    : "　<color=#6f8a9a>記録開始SE" + r.recordStartYear + "（それ以前は不明）</color>");
                sb.Append('\n');
            }
            if (members.Count > shown)
                sb.Append("      <color=#9aa7b2>ほか ").Append(members.Count - shown).Append("名（表示上限）</color>\n");
        }

        private void AppendGovernment(StringBuilder sb, PoliticsState pol)
        {
            GovernmentFormation g = pol.government;
            sb.Append("    <color=#9fb0c0>政府</color> ＝ ");
            if (g == null) { sb.Append("<color=#9aa7b2>未組閣</color>\n"); return; }
            if (g.premierPersonId >= 0)
                sb.Append("首相 <color=#ffd700>").Append(PersonName(g.premierPersonId)).Append("</color>（")
                  .Append(PartyName(pol, g.partyId)).Append(' ').Append(g.partySeats).Append('/').Append(g.totalSeats).Append("議席）");
            else
                sb.Append("<color=#ff7a6a>首相 空席</color>");
            sb.Append("　[").Append(g.status).Append(']');
            if (g.formedYear > 0) sb.Append("　SE").Append(g.formedYear);
            sb.Append('\n');
            if (!string.IsNullOrEmpty(g.reason))
                sb.Append("      <color=#ffb070>理由：").Append(g.reason).Append("</color>\n");
        }

        private void AppendRecentResults(StringBuilder sb, PoliticsState pol)
        {
            if (pol.recentResults == null || pol.recentResults.Count == 0)
            {
                sb.Append("    <color=#9fb0c0>直近開票</color> ＝ <color=#9aa7b2>なし</color>\n");
                return;
            }
            const int maxShown = 2; // 新しい順に2件
            for (int i = pol.recentResults.Count - 1, shown = 0; i >= 0 && shown < maxShown; i--, shown++)
            {
                NationalElectionRecord r = pol.recentResults[i];
                if (r == null) continue;
                sb.Append("    <color=#9fb0c0>直近開票</color> SE").Append(r.year).Append(' ').Append(r.chamber)
                  .Append(r.inaugural ? "（初回・" : "（").Append(r.classUp < 0 ? "全" : (r.classUp == 0 ? "区分A " : "区分B "))
                  .Append(r.seatsUp).Append("議席）\n      ");
                for (int k = 0; k < r.results.Count; k++)
                {
                    PartyVoteResult v = r.results[k];
                    if (v == null) continue;
                    if (k > 0) sb.Append(" / ");
                    sb.Append(v.partyName).Append(' ').Append((v.voteShare * 100f).ToString("0")).Append("%→")
                      .Append(v.seatsWon).Append("議席");
                }
                sb.Append('\n');
            }
        }

        private string PersonName(int personId)
        {
            Person p = galaxy != null ? galaxy.FindPersonById(personId) : null;
            return p != null ? p.name : "人物#" + personId;
        }

        private static string SystemName(int systemId)
        {
            StarSystem s = StrategySession.Map != null ? StrategySession.Map.GetSystem(systemId) : null;
            return s != null ? s.systemName : "星系#" + systemId;
        }

        private static string PartyName(PoliticsState pol, int partyId)
        {
            Party p = ElectionCycleRules.FindParty(pol != null ? pol.parties : null, partyId);
            return p != null ? p.partyName : "無所属";
        }

        private void AppendBar(StringBuilder sb, string label, float v01, string colorHex)
        {
            v01 = Mathf.Clamp01(v01);
            int filled = Mathf.RoundToInt(v01 * barWidth);
            sb.Append(label).Append("  <color=").Append(colorHex).Append('>');
            for (int i = 0; i < barWidth; i++) sb.Append(i < filled ? '█' : '░');
            sb.Append("</color> ").Append(v01.ToString("0.00")).Append('\n');
        }

        private void AppendBarInline(StringBuilder sb, float v01, string colorHex)
        {
            v01 = Mathf.Clamp01(v01);
            int filled = Mathf.RoundToInt(v01 * barWidth);
            sb.Append("  <color=").Append(colorHex).Append('>');
            for (int i = 0; i < barWidth; i++) sb.Append(i < filled ? '█' : '░');
            sb.Append("</color>");
        }

        // ===== UI 構築（EconomyObserverOverlay と同型・単一スクロールラベル版） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("PoliticsObserverCanvas");
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

            WindowChrome.AddTitleBarLayout(frameRT, "政治", () => SetVisible(false));
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
            UiScrollbars.Attach(scrollRect);   // #H スクロールできることを画面で示す（見えて掴めるバー）

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
