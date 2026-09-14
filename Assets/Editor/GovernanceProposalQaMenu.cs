using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 統治政策の上申→裁可を<b>実画面で再現する</b>ための QA 条件（Editor 専用・戦略マップの Play 中のみ）。
    ///
    /// <b>通常の経路はそのまま</b>：権限判定（<see cref="DecisionAuthorityDirector"/>）・官僚機構の生存ロール
    /// （<see cref="PetitionFlowRules"/>）・稟議の決裁と執行（<see cref="RingiDirector"/>）を無効化せず、
    /// 政策も直接変えない。変えるのは<b>それらが読む入力</b>だけ：
    /// <list type="bullet">
    ///   <item>対象星系の地方箱の信認と国家の傾聴度（heed）を 1</item>
    ///   <item>勢力の正統性・結束・希望（生存ロールの正統性係数）を 1</item>
    ///   <item>内政の省庁の省益を下限近くへ（摩擦＝握り潰しと執行の骨抜き）</item>
    ///   <item>宰相（内政・国家の正式な文民役職）に、賛意が承認の閾値に届く文民を <see cref="GovernmentRegistry"/> で任命
    ///   （資格は <see cref="CivilAppointmentRules.IsQualified"/>。位階が足りなければ位階だけ官位相当へ上げる）</item>
    /// </list>
    /// ボタンを押すのも、カードの「裁可する」を押すのも人（または ChatGPT）。ファイルへは何も書かない
    /// （ただし QA 中にゲーム内でセーブすると変えた値が保存されるので、戻してから保存する）。
    /// </summary>
    [InitializeOnLoad]
    public static class GovernanceProposalQaMenu
    {
        // Ginei 直下は項目が多く画面外にはみ出すため、既存 QA の親メニュー配下へ置く。
        private const string Root = "Ginei/QA（Play中のみ・保存しない）/統治上申/";
        private const string MenuPrepare = Root + "裁可まで通る条件を整える（戦略・Play中）";
        private const string MenuDump = Root + "いまの条件と見込みを出力（戦略・Play中）";
        private const string MenuRestore = Root + "QA条件を元に戻す（戦略・Play中）";
        private const string Title = "QA: 統治上申";

        /// <summary>QA で内政の省庁に置く省益（0 だと RingiDirector が既定摩擦 0.4 に戻すため、0 より大きい最小値）。</summary>
        private const float QaInstitutionalInterest = 0.001f;
        /// <summary>QA で置く信認・傾聴度・正統性係数の値。</summary>
        private const float QaFull = 1f;

        static GovernanceProposalQaMenu()
        {
            // Play を抜けたら QA 状態の控えを捨てる（戻す相手のオブジェクトはもう無い）。
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
                    snapshot = null;
            };
        }

        // ===== QA 前の値の控え（戻す用・メモリのみ） =====

        private sealed class Snapshot
        {
            public Faction faction;
            public FactionState state;
            public int systemId;
            public string systemName;
            public string credKey;
            public bool hadCredEntry;
            public float credValue;
            public float globalDeference;
            public float legitimacy, cohesion, hope;
            public readonly List<KeyValuePair<Ministry, float>> ministries = new List<KeyValuePair<Ministry, float>>();
            public Office office;
            public Person previousHolder;   // QA が解任した在任者（無ければ null）
            public Person appointed;        // QA が任命した人（在任者をそのまま使ったなら null）
            public Person rankRaised;       // 位階を上げた人（無ければ null）
            public CourtRank originalRank;
        }

        private static Snapshot snapshot;

        // ===== メニューの可否（戦略マップの Play 中だけ） =====

        [MenuItem(MenuPrepare, true)]
        [MenuItem(MenuDump, true)]
        private static bool ValidateStrategyPlay()
            => Application.isPlaying && SceneManager.GetActiveScene().name == "Strategy" && GalaxyView.Active != null;

        [MenuItem(MenuRestore, true)]
        private static bool ValidateRestore() => ValidateStrategyPlay() && snapshot != null;

        // ===== 整える =====

        [MenuItem(MenuPrepare, false, 390)]
        public static void Prepare()
        {
            if (!ValidateStrategyPlay())
            {
                EditorUtility.DisplayDialog(Title, "戦略マップ（Strategy）の Play 中に実行してください。", "OK");
                return;
            }
            if (snapshot != null)
            {
                EditorUtility.DisplayDialog(Title,
                    "すでに QA 条件を適用しています。\n先に「" + MenuRestore + "」で戻してから、もう一度実行してください。", "OK");
                return;
            }

            if (!TryPlan(out Plan plan, out string problem))
            {
                Debug.LogWarning("[QA 統治上申] 条件を整えられません：" + problem);
                EditorUtility.DisplayDialog(Title, "条件を整えられません（何も変更していません）。\n\n" + problem, "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(Title + "（実行前の確認）", plan.Describe(), "QA条件を適用する", "やめる"))
                return;

            Apply(plan);

            GalaxyView gv = GalaxyView.Active;
            bool opened = gv != null && gv.OpenSystemInfo(plan.systemId);
            string report = Report(plan, opened);
            NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.警告,
                $"［QA］統治上申の検証条件を適用中（{plan.systemName}・宰相 {plan.decider.name}）。この間はセーブしないでください");
            Debug.Log("[QA 統治上申] 適用しました\n" + report);
            EditorUtility.DisplayDialog(Title + "（適用しました）", report, "OK");
        }

        /// <summary>適用する内容（まだ何も変えていない）。</summary>
        private sealed class Plan
        {
            public Faction faction;
            public FactionState state;
            public Person actor;
            public int systemId;
            public string systemName;
            public GovernanceProposalPreview preview;
            public string regionKey;
            public List<Ministry> domesticMinistries = new List<Ministry>();
            public Office office;
            public Person currentHolder;
            public Person decider;          // 裁可する宰相（在任者か、QA が任命する人）
            public bool appoint;            // QA が任命するか
            public bool raiseRank;          // 位階を官位相当へ上げるか
            public float threshold;
            public float reviewSeconds;
            public float survivalBefore;
            public float survivalAfter;

            public string Describe()
            {
                var sb = new StringBuilder();
                sb.Append("戦役のメモリ上の値だけを変えます（ファイルへは書きません）。\n");
                sb.Append("権限判定・官僚機構の判定・稟議の決裁と執行はそのまま通ります。政策は直接変えません。\n\n");
                sb.Append("■ 対象：").Append(faction).Append(" の ").Append(systemName).Append("（星系 #").Append(systemId).Append("）")
                  .Append("　現在「").Append(preview.current).Append("」→ 次「").Append(preview.next).Append("」\n");
                sb.Append("■ 操作者：").Append(actor.name).Append("（").Append(actor.role).Append("）\n\n");
                sb.Append("■ 変える値\n");
                sb.Append("  ・地方箱（").Append(systemName).Append("）の信認と国家の傾聴度 → ").Append(QaFull).Append('\n');
                sb.Append("  ・").Append(faction).Append(" の正統性・結束・希望 → ").Append(QaFull).Append('\n');
                sb.Append("  ・内政の省庁 ").Append(domesticMinistries.Count).Append(" 件の省益 → ").Append(QaInstitutionalInterest).Append('\n');
                if (appoint)
                {
                    if (currentHolder != null)
                        sb.Append("  ・宰相 ").Append(currentHolder.name).Append(" を解任（賛意 ")
                          .Append(DecisionAuthorityDirector.Favor(currentHolder).ToString("0.00")).Append(" が閾値未満／資格なし）\n");
                    sb.Append("  ・").Append(decider.name).Append(" を ").Append(office.officeName).Append(" に任命");
                    if (raiseRank)
                        sb.Append("（位階 ").Append(JapaneseCourtRankRules.Name(decider.courtRank)).Append(" → ")
                          .Append(JapaneseCourtRankRules.Name(GalaxyView.PremierRank)).Append("）");
                    sb.Append('\n');
                }
                else sb.Append("  ・宰相は在任の ").Append(decider.name).Append(" のまま（変えない）\n");
                sb.Append("\n■ 見込み\n");
                sb.Append("  ・地方官僚機構を通る確率 ").Append(Pct(survivalBefore)).Append(" → ").Append(Pct(survivalAfter))
                  .Append("（1 回ごとの乱数。100% にはならない）\n");
                sb.Append("  ・宰相の賛意 ").Append(DecisionAuthorityDirector.Favor(decider).ToString("0.00"))
                  .Append("（承認の閾値 ").Append(threshold.ToString("0.00")).Append("）\n");
                sb.Append("  ・上申してから返事まで ").Append(reviewSeconds.ToString("0")).Append(" game-秒（一時停止中は進みません）\n\n");
                sb.Append("★ QA 中はゲーム内でセーブしないでください（変えた値が保存されます）。\n");
                sb.Append("  終わったら「統治上申 QA条件を元に戻す」を実行してください。");
                return sb.ToString();
            }
        }

        /// <summary>何を変えるかを決める（ここでは何も変えない）。できなければ理由を返す。</summary>
        private static bool TryPlan(out Plan plan, out string problem)
        {
            plan = null;
            problem = "";
            GalaxyView gv = GalaxyView.Active;
            CampaignState camp = StrategySession.Campaign;
            GalaxyMap map = StrategySession.Map;
            if (gv == null || camp == null || map == null || StrategySession.Provinces == null)
            { problem = "戦役（盤面・国家状態・地図）がまだ構築されていません。"; return false; }

            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            FactionState fs = CampaignRules.GetState(camp, player);
            if (fs == null) { problem = player + " の国家状態がありません。"; return false; }

            RingiDirector ringi = Object.FindAnyObjectByType<RingiDirector>();
            if (ringi == null) { problem = "RingiDirector（稟議）がありません。"; return false; }

            var authorityDirector = Object.FindAnyObjectByType<DecisionAuthorityDirector>();
            if (authorityDirector == null || DecisionDeck.AuthorityCheck == null)
            { problem = "権限判定（DecisionAuthorityDirector）が有効ではありません。権限を通す検証になりません。"; return false; }

            Person actor = gv.PlayerCharacter();
            if (actor == null)
            { problem = "操作者（主人公）を特定できません。この状態では権限判定が省略されるため、検証になりません。"; return false; }

            // 対象＝自領で、いま上申を受け付ける星系（先に見つかったもの）。
            int systemId = -1;
            GovernanceProposalPreview preview = default;
            var reasons = new List<string>();
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null || s.owner != player) continue;
                GovernanceProposalPreview p = RingiDirector.PreviewGovernanceProposal(s.id);
                if (p.CanSubmit) { systemId = s.id; preview = p; break; }
                if (reasons.Count < 3) reasons.Add(GovernanceProposalRules.RejectionText(p.rejection, p.systemName));
            }
            if (systemId < 0)
            {
                problem = "上申を受け付ける自領の星系がありません。" + (reasons.Count > 0 ? "\n例：" + string.Join("\n例：", reasons) : "");
                return false;
            }

            Office office = gv.PremierOfficeOf(player);
            if (office == null) { problem = player + " の宰相職がまだ編成されていません。"; return false; }

            float threshold = PetitionEscalationParams.Default.approveThreshold;
            CourtRank required = GalaxyView.PremierRank;
            Person holder = GovernmentRegistry.GetHolder(office) as Person;
            bool holderOk = holder != null && CivilAppointmentRules.IsQualified(holder, office, required)
                            && DecisionAuthorityDirector.Favor(holder) >= threshold;

            Person decider = holderOk ? holder : PickCandidate(gv, player, office, threshold, actor);
            if (decider == null)
            {
                problem = $"{player} の文民に、宰相の賛意が承認の閾値 {threshold:0.00} に届く人（運営＋情報が十分）がいません。\n" +
                          "能力値は変えない方針なので、この戦役では再現できません（年を進めて人材を待つ）。";
                return false;
            }

            plan = new Plan
            {
                faction = player,
                state = fs,
                actor = actor,
                systemId = systemId,
                systemName = preview.systemName,
                preview = preview,
                regionKey = systemId.ToString(),
                office = office,
                currentHolder = holder,
                decider = decider,
                appoint = !holderOk,
                raiseRank = !holderOk && JapaneseCourtRankRules.Compare(decider.courtRank, required) < 0,
                threshold = threshold,
                reviewSeconds = authorityDirector.reviewSeconds,
            };
            IReadOnlyList<Ministry> ministries = gv.MinistriesOf(player);
            if (ministries != null)
                for (int i = 0; i < ministries.Count; i++)
                    if (ministries[i] != null && ministries[i].domain == OfficeDomain.内政) plan.domesticMinistries.Add(ministries[i]);

            plan.survivalBefore = PetitionFlowRules.SurvivalChance(
                CredibilityRules.Heed(fs.credibility, BoxKind.地方, plan.regionKey),
                RingiDirector.PreviewMinistryFriction(player, OfficeDomain.内政),
                FactionLoyaltyRules.BaselineLoyalty(fs));
            float frictionAfter = plan.domesticMinistries.Count > 0
                ? QaInstitutionalInterest
                : RingiDirector.PreviewMinistryFriction(player, OfficeDomain.内政); // 省庁が無ければ既定摩擦のまま
            plan.survivalAfter = PetitionFlowRules.SurvivalChance(QaFull, frictionAfter, QaFull);
            return true;
        }

        /// <summary>
        /// 宰相の候補：自勢力の存命の文民で、賛意が閾値に届き、役職の資格（位階を除く）を満たす人。
        /// 位階が官位相当の人を優先し、次に他の役職に就いていない人、賛意の高い人、id の小さい人（決定論）。
        /// </summary>
        private static Person PickCandidate(GalaxyView gv, Faction player, Office office, float threshold, Person actor)
        {
            IReadOnlyList<Person> roster = gv.CivilianRoster;
            if (roster == null) return null;
            Person best = null;
            for (int i = 0; i < roster.Count; i++)
            {
                Person p = roster[i];
                if (p == null || p.faction != player || !p.IsAvailable || p.role != PersonRole.文民) continue;
                if (actor != null && p.id == actor.id) continue;
                if (!OfficeRules.CanHold(p, office)) continue;
                if (DecisionAuthorityDirector.Favor(p) < threshold) continue;
                if (best == null || Better(p, best, office)) best = p;
            }
            return best;
        }

        private static bool Better(Person a, Person b, Office office)
        {
            bool aRank = JapaneseCourtRankRules.Compare(a.courtRank, GalaxyView.PremierRank) >= 0;
            bool bRank = JapaneseCourtRankRules.Compare(b.courtRank, GalaxyView.PremierRank) >= 0;
            if (aRank != bRank) return aRank;
            bool aFree = GovernmentRegistry.GetOffices(a).Count == 0;
            bool bFree = GovernmentRegistry.GetOffices(b).Count == 0;
            if (aFree != bFree) return aFree;
            float fa = DecisionAuthorityDirector.Favor(a), fb = DecisionAuthorityDirector.Favor(b);
            if (!Mathf.Approximately(fa, fb)) return fa > fb;
            return a.id < b.id;
        }

        /// <summary>計画どおりに値を変え、戻すための控えを残す。</summary>
        private static void Apply(Plan plan)
        {
            FactionState fs = plan.state;
            var snap = new Snapshot
            {
                faction = plan.faction,
                state = fs,
                systemId = plan.systemId,
                systemName = plan.systemName,
                credKey = CredibilityRules.Key(BoxKind.地方, plan.regionKey),
                globalDeference = fs.credibility.globalDeference,
                legitimacy = fs.regime != null ? fs.regime.legitimacy : 0f,
                cohesion = fs.organization != null ? fs.organization.cohesion : 0f,
                hope = fs.community != null ? fs.community.hope : 0f,
                office = plan.office,
            };
            snap.hadCredEntry = fs.credibility.entries.TryGetValue(snap.credKey, out snap.credValue);

            // 信認（地方箱）と傾聴度＝既存の窓口で動かす。
            CredibilityRules.Adjust(fs.credibility, BoxKind.地方, QaFull, plan.regionKey);
            CredibilityRules.AdjustGlobal(fs.credibility, QaFull);
            if (fs.regime != null) fs.regime.legitimacy = QaFull;
            if (fs.organization != null) fs.organization.cohesion = QaFull;
            if (fs.community != null) fs.community.hope = QaFull;

            for (int i = 0; i < plan.domesticMinistries.Count; i++)
            {
                Ministry m = plan.domesticMinistries[i];
                snap.ministries.Add(new KeyValuePair<Ministry, float>(m, m.institutionalInterest));
                m.institutionalInterest = QaInstitutionalInterest;
            }

            if (plan.appoint)
            {
                if (plan.currentHolder != null && GovernmentRegistry.Dismiss(plan.office, plan.currentHolder))
                    snap.previousHolder = plan.currentHolder;
                if (plan.raiseRank)
                {
                    snap.rankRaised = plan.decider;
                    snap.originalRank = plan.decider.courtRank;
                    plan.decider.courtRank = GalaxyView.PremierRank;
                }
                if (GovernmentRegistry.TryAppoint(plan.faction, plan.office, plan.decider))
                    snap.appointed = plan.decider;
                else
                    Debug.LogWarning($"[QA 統治上申] {plan.decider.name} を {plan.office.officeName} に任命できませんでした（資格・定員）。");
            }

            snapshot = snap;
        }

        /// <summary>適用後の見込みと、人が行う手順。</summary>
        private static string Report(Plan plan, bool panelOpened)
        {
            var sb = new StringBuilder();
            sb.Append(DumpText()).Append('\n');
            sb.Append("■ 手順（押すのは人）\n");
            sb.Append("  1. ").Append(panelOpened ? "星系情報パネルを開きました。" : "星系図の「星系情報・統治政策の上申…」か I キーで ")
              .Append(plan.systemName).Append(panelOpened ? "" : " の星系情報パネルを開く。").Append('\n');
            sb.Append("  2. 「決裁の見込み」が「裁可すると 〇〇 へ上申されます」か「あなたが裁可できます」であることを見る。\n");
            sb.Append("  3. 「統治政策の変更を上申」を押す → 右下の決裁デスクにカードが出る。\n");
            sb.Append("     （「決裁デスクまで届きませんでした」なら乱数で止まった。もう一度押す）\n");
            sb.Append("  4. カードの「裁可する」を押す → 「［上申］… を 〇〇 へ上げました」。\n");
            sb.Append("  5. 一時停止を解いて ").Append(plan.reviewSeconds.ToString("0"))
              .Append(" game-秒待つ → 「［上申の裁可］…を承認しました」「［執行］…」、パネルの「現在」が「")
              .Append(plan.preview.next).Append("」になる。\n");
            sb.Append("  6. 終わったら「").Append(MenuRestore).Append("」。\n");
            return sb.ToString();
        }

        // ===== 出力（読み取りのみ） =====

        [MenuItem(MenuDump, false, 391)]
        public static void Dump()
        {
            if (!ValidateStrategyPlay())
            {
                EditorUtility.DisplayDialog(Title, "戦略マップ（Strategy）の Play 中に実行してください。", "OK");
                return;
            }
            string text = DumpText();
            Debug.Log("[QA 統治上申] いまの条件\n" + text);
            EditorUtility.DisplayDialog(Title + "（読み取りのみ）", text, "OK");
        }

        private static string DumpText()
        {
            var sb = new StringBuilder();
            GalaxyView gv = GalaxyView.Active;
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            FactionState fs = StrategySession.Campaign != null ? CampaignRules.GetState(StrategySession.Campaign, player) : null;

            sb.Append("■ QA 条件：").Append(snapshot != null ? $"適用中（{snapshot.systemName}）★セーブしないこと" : "未適用").Append('\n');
            Person actor = gv != null ? gv.PlayerCharacter() : null;
            sb.Append("■ 操作者：").Append(actor != null ? $"{actor.name}（{actor.role}）" : "特定できない（権限判定は省略される）").Append('\n');
            sb.Append("■ 権限判定：").Append(DecisionDeck.AuthorityCheck != null ? "有効" : "★無効（誰でも裁可できる）").Append('\n');
            if (gv != null)
                sb.Append("■ 文民統制：").Append(gv.CivilianControlOf(player)).Append('\n');

            Office office = gv != null ? gv.PremierOfficeOf(player) : null;
            Person premier = office != null ? GovernmentRegistry.GetHolder(office) as Person : null;
            float threshold = PetitionEscalationParams.Default.approveThreshold;
            sb.Append("■ 宰相：").Append(premier != null
                ? $"{premier.name}（{JapaneseCourtRankRules.Name(premier.courtRank)}・賛意 {DecisionAuthorityDirector.Favor(premier):0.00}／閾値 {threshold:0.00}）"
                : "空席").Append('\n');
            Person addressee = gv != null && actor != null ? gv.FindOfficeHolder(player, OfficeDomain.内政, actor) : null;
            sb.Append("■ 実際の上申先（内政・国家の役職者）：").Append(addressee != null
                ? $"{addressee.name}（賛意 {DecisionAuthorityDirector.Favor(addressee):0.00}）"
                : "いない").Append('\n');

            var authorityDirector = Object.FindAnyObjectByType<DecisionAuthorityDirector>();
            if (authorityDirector != null) sb.Append("■ 返事まで：").Append(authorityDirector.reviewSeconds.ToString("0")).Append(" game-秒\n");
            GameClock clock = StrategySession.Clock;
            sb.Append("■ 時計：").Append(clock == null ? "なし" : (clock.paused ? "一時停止中（審査が進まない）" : "進行中")).Append('\n');

            int systemId = snapshot != null ? snapshot.systemId : -1;
            if (systemId < 0 && StrategySession.Map != null)
                for (int i = 0; i < StrategySession.Map.systems.Count; i++)
                {
                    StarSystem s = StrategySession.Map.systems[i];
                    if (s != null && s.owner == player) { systemId = s.id; break; }
                }
            if (systemId >= 0)
            {
                GovernanceProposalPreview p = RingiDirector.PreviewGovernanceProposal(systemId);
                sb.Append("■ 対象：").Append(p.systemName).Append("（#").Append(systemId).Append("）現在「").Append(p.current)
                  .Append("」→ 次「").Append(p.next).Append("」　上申: ")
                  .Append(p.CanSubmit ? "できる" : GovernanceProposalRules.RejectionText(p.rejection, p.systemName)).Append('\n');
                if (fs != null)
                {
                    string region = systemId.ToString();
                    float heed = CredibilityRules.Heed(fs.credibility, BoxKind.地方, region);
                    float friction = RingiDirector.PreviewMinistryFriction(player, OfficeDomain.内政);
                    float legitimacy = FactionLoyaltyRules.BaselineLoyalty(fs);
                    sb.Append("■ 地方官僚機構：傾聴 ").Append(heed.ToString("0.00")).Append("　摩擦 ").Append(friction.ToString("0.000"))
                      .Append("　正統性 ").Append(legitimacy.ToString("0.00")).Append(" → 通る確率 ")
                      .Append(Pct(PetitionFlowRules.SurvivalChance(heed, friction, legitimacy))).Append('\n');
                }
                string key = GovernanceRules.PolicyPetitionKey(systemId, p.next);
                bool enforced = DecisionAuthorityDirector.TryPreviewAuthority(key, out DecisionAuthorityResult auth);
                sb.Append("■ 決裁の見込み：").Append(!enforced ? auth.basis
                    : auth.CanDecide ? "操作者が裁可できる（" + auth.basis + "）"
                    : auth.authority == DecisionAuthority.上申 ? $"{auth.addresseeName} へ上申（{auth.basis}）"
                    : "裁可できない（" + auth.basis + "）").Append('\n');
            }
            return sb.ToString();
        }

        // ===== 戻す =====

        [MenuItem(MenuRestore, false, 392)]
        public static void Restore()
        {
            if (snapshot == null)
            {
                EditorUtility.DisplayDialog(Title, "QA 条件は適用されていません。", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog(Title, "QA 前の値に戻します（信認・傾聴度・正統性/結束/希望・省益・宰相の任命・位階）。\n" +
                    "QA 中に上申・裁可した結果（カード・政策の変更）は戻しません。", "戻す", "やめる"))
                return;

            Snapshot s = snapshot;
            var sb = new StringBuilder();

            if (s.appointed != null && GovernmentRegistry.Dismiss(s.office, s.appointed))
                sb.Append("・").Append(s.appointed.name).Append(" を宰相から外しました\n");
            if (s.rankRaised != null)
            {
                s.rankRaised.courtRank = s.originalRank;
                sb.Append("・").Append(s.rankRaised.name).Append(" の位階を ").Append(JapaneseCourtRankRules.Name(s.originalRank)).Append(" に戻しました\n");
            }
            if (s.previousHolder != null)
                sb.Append(GovernmentRegistry.TryAppoint(s.faction, s.office, s.previousHolder)
                    ? $"・{s.previousHolder.name} を宰相に戻しました\n"
                    : $"・{s.previousHolder.name} を宰相に戻せませんでした（資格・定員）。次の年境界の銓衡に任せます\n");

            for (int i = 0; i < s.ministries.Count; i++)
                if (s.ministries[i].Key != null) s.ministries[i].Key.institutionalInterest = s.ministries[i].Value;

            FactionState fs = s.state;
            if (fs != null)
            {
                if (s.hadCredEntry) fs.credibility.entries[s.credKey] = s.credValue;
                else fs.credibility.entries.Remove(s.credKey);
                fs.credibility.globalDeference = s.globalDeference;
                if (fs.regime != null) fs.regime.legitimacy = s.legitimacy;
                if (fs.organization != null) fs.organization.cohesion = s.cohesion;
                if (fs.community != null) fs.community.hope = s.hope;
            }
            sb.Append("・信認・傾聴度・正統性/結束/希望・省益を QA 前の値に戻しました\n");
            sb.Append("（QA 中に時間が進んだ場合、その間の変化は QA 前の値で上書きされます）");

            snapshot = null;
            NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.情報, "［QA］統治上申の検証条件を元に戻しました");
            Debug.Log("[QA 統治上申] 元に戻しました\n" + sb);
            EditorUtility.DisplayDialog(Title + "（戻しました）", sb.ToString(), "OK");
        }

        private static string Pct(float v) => Mathf.RoundToInt(Mathf.Clamp01(v) * 1000f) / 10f + "%";
    }
}
