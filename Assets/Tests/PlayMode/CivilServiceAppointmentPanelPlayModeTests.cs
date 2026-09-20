using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 官僚人事メニュー（#141）の接続試験。実 GalaxyView（政府シード＋内閣＋年次人事で台帳を初期化）に対し、
    /// 操作者を実在の人物に固定し、画面の選択から <see cref="GalaxyView.PreviewPlayerCivilServicePost"/> へ渡る引数
    /// （省・行為・人物・段）と、<see cref="GalaxyView.SubmitPlayerCivilServicePost"/> の結果表示だけを見る。
    /// <para>Core の判定を写す試験ではない＝可否そのものは人事の共通入口が決める。画面は
    /// ①窓が開閉し Esc スタックに載る ②3つの一覧に見えるスクロールバーがある ③5種の操作で段の導出が正しい
    /// ④直接実行で台帳が動く ⑤上申で決裁 id と決裁者が出て台帳は動かない ⑥組めない組合せは確定不能で理由が出る
    /// ⑦選択・理由を変えると確認が解除される ⑧再描画は読み取り専用（台帳・決裁を変えない）ことだけを担保する。</para>
    /// <para>GalaxyView は無効な GameObject に載せて Start を走らせない。セーブファイルは読み書きしない。
    /// TearDown で static・Registry・Clock・Active を戻す。</para>
    /// </summary>
    public class CivilServiceAppointmentPanelPlayModeTests
    {
        private const int YearSeconds = 60 * 30 * 12; // GameDate.DateParams.Default の1年（game-秒）
        private const int FirstYear = 797;
        private const int CapitalId = 1, FrontierId = 2;
        private const int PremierId = 11;             // 政治家 11..20（11=首相・12〜=各省の大臣）
        private const int PoliticianCount = 10;
        private const int BureaucratId = 31;          // 既存の職業官僚 31..36
        private const int BureaucratCount = 6;
        private const int Newcomer = 40;              // 未配属の入省候補
        private const Faction F = Faction.同盟;
        private static readonly CabinetParams CabPrm = CabinetParams.Default;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private CampaignState savedCampaign;
        private GalaxyMap savedMap;
        private Dictionary<int, Province> savedProvinces;
        private GameClock savedClock;
        private DecisionQueue savedDecisions;
        private PetitionLedger savedPetitions;
        private System.Func<PendingDecision, DecisionAuthorityResult> savedAuthority;
        private List<GovernmentRegistry.Appointment> savedAppointments;
        private GalaxyView savedActive;

        private GalaxyView view;
        private FactionState alliance;
        private List<Person> civilians;

        [SetUp]
        public void SetUp()
        {
            savedActive = GalaxyView.Active;
            savedCampaign = StrategySession.Campaign;
            savedMap = StrategySession.Map;
            savedProvinces = StrategySession.Provinces;
            savedClock = StrategySession.Clock;
            savedDecisions = StrategySession.Decisions;
            savedPetitions = StrategySession.Petitions;
            savedAuthority = DecisionDeck.AuthorityCheck;
            savedAppointments = new List<GovernmentRegistry.Appointment>(GovernmentRegistry.Appointments);

            StrategySession.Decisions = new DecisionQueue();
            StrategySession.Petitions = new PetitionLedger();
        }

        [TearDown]
        public void TearDown()
        {
            CivilServiceAppointmentPanel panel = CivilServiceAppointmentPanel.InstanceForTest;
            if (panel != null) Object.DestroyImmediate(panel.gameObject);
            if (view != null) view.BindPlayerCharacterForQa(null);
            view = null;
            alliance = null;
            civilians = null;

            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            spawned.Clear();

            GalaxyView.SwapActiveForQa(savedActive);
            DecisionDeck.AuthorityCheck = savedAuthority;
            StrategySession.Decisions = savedDecisions;
            StrategySession.Petitions = savedPetitions;
            StrategySession.Campaign = savedCampaign;
            StrategySession.Map = savedMap;
            StrategySession.Provinces = savedProvinces;
            StrategySession.Clock = savedClock;
            GovernmentRegistry.Clear();
            for (int i = 0; i < savedAppointments.Count; i++)
            {
                GovernmentRegistry.Appointment a = savedAppointments[i];
                GovernmentRegistry.TryAppoint(a.faction, a.office, a.holder, a.scopeKey);
            }
        }

        // ===== 盤面（CivilServiceRingiPlayModeTests と同じ固定条件の同盟） =====

        private static GalaxyMap NewMap()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(CapitalId, "人事画面試験首都星", Vector2.zero, F));
            map.AddSystem(new StarSystem(FrontierId, "人事画面試験辺境星", new Vector2(1f, 0f), F));
            return map;
        }

        private static Dictionary<int, Province> NewProvinces()
            => new Dictionary<int, Province>
            {
                { CapitalId, new Province(CapitalId, "", 1000f) },
                { FrontierId, new Province(FrontierId, "", 600f) },
            };

        /// <summary>職業官僚（政治家でない文民）：入省・課長級の資格（正七位上・考課6.0）を満たす。</summary>
        private static Person NewBureaucrat(int id, int aptitude)
        {
            var p = new Person(id, "試験官僚" + id, F, PersonRole.文民)
            { birthYear = 750, courtRank = CourtRank.正七位上, operation = aptitude, intelligence = aptitude };
            p.merit = new OfficialMerit(id) { evaluations = 4, cumulativeScore = 24f }; // 平均6.0
            return p;
        }

        private static List<Person> NewCivilians()
        {
            var list = new List<Person>();
            for (int i = 0; i < PoliticianCount; i++)
                list.Add(new Person(PremierId + i, "試験政治家" + (PremierId + i), F, PersonRole.文民)
                { isPolitician = true, birthYear = 760, charisma = 60, operation = 90 - i, intelligence = 90 });
            for (int i = 0; i < BureaucratCount; i++) list.Add(NewBureaucrat(BureaucratId + i, 30 - i));
            return list;
        }

        private void SpawnDirectors()
        {
            var ringiGo = new GameObject("RingiDirector_CivilServicePanelQa");
            spawned.Add(ringiGo);
            RingiDirector ringi = ringiGo.AddComponent<RingiDirector>();
            ringi.raiseInterval = 1e9f;      // 試験中に自発的な建白を起こさせない
            ringi.agendaCooldownSeconds = 1e9f;

            var authGo = new GameObject("DecisionAuthorityDirector_CivilServicePanelQa");
            spawned.Add(authGo);
            authGo.AddComponent<DecisionAuthorityDirector>();
        }

        /// <summary>同盟（共和制）＋政府シード＋内閣＋人事台帳（既存配属の移行済み）まで整えた盤面。</summary>
        private void BuildLiveWorld()
        {
            GalaxyMap map = NewMap();
            Dictionary<int, Province> provinces = NewProvinces();
            civilians = NewCivilians();
            var campaign = new CampaignState(map);
            alliance = new FactionState(F) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            campaign.states.Add(alliance);
            StrategySession.Map = map;
            StrategySession.Campaign = campaign;
            StrategySession.Provinces = provinces;
            StrategySession.Clock = new GameClock { elapsedSeconds = YearSeconds + 1d };

            var go = new GameObject("GalaxyView_CivilServicePanelQa");
            go.SetActive(false); // Start を走らせない
            spawned.Add(go);
            view = go.AddComponent<GalaxyView>();
            view.BindElectionQaWorld(map, provinces, new List<Person>(), civilians);
            view.SeedGovernmentForQa();
            FormCabinet(FirstYear);
            GalaxyView.SwapActiveForQa(view);
            SpawnDirectors();
            view.RunCivilServiceAnnualTickForQa(); // 台帳を作り既存の配属を移行する
            Assert.IsNotNull(alliance.civilService, "前提：人事台帳が作られない");
        }

        private void FormCabinet(int year)
        {
            PoliticsState pol = alliance.politics;
            var ruling = new Party(1, "人事画面試験民政党", F) { leaderId = PremierId, support = 1f };
            for (int i = 0; i < PoliticianCount; i++) ruling.memberIds.Add(PremierId + i);
            pol.parties.Add(ruling);
            pol.government = new GovernmentFormation
            {
                status = CabinetStatus.単独過半, premierPersonId = PremierId, partyId = 1, formedYear = year, sourceElectionId = "qa",
            };

            List<Ministry> tree = Tree();
            int top = view.TopMinistryIdOf(F);
            CabinetAppointmentRules.Reconcile(pol, F, tree, top, civilians, year, CabPrm);
            int next = PremierId + 1;
            for (int i = 0; i < tree.Count; i++)
            {
                if (tree[i].id == top) continue;
                AppointmentResult r = CabinetAppointmentRules.TryAppoint(pol, F, PremierId, tree, top,
                    tree[i].id, CabinetPostKind.大臣, next, civilians, year, "試験：承認権者を置く", CabPrm);
                Assert.IsTrue(r.ok, tree[i].ministryName + " に大臣を置けない：" + r.reason);
                next++;
            }
        }

        // ===== 参照 =====

        private List<Ministry> Tree() => new List<Ministry>(view.MinistriesOf(F));

        private Ministry MinistryNamed(string suffix)
        {
            List<Ministry> tree = Tree();
            for (int i = 0; i < tree.Count; i++)
                if (tree[i] != null && tree[i].ministryName != null && tree[i].ministryName.EndsWith(suffix)) return tree[i];
            return null;
        }

        private int MinisterOf(int ministryId)
        {
            CabinetPost post = CabinetAppointmentRules.FindPost(alliance.politics.cabinet, ministryId, CabinetPostKind.大臣);
            return post != null ? post.holderId : -1;
        }

        private Person Find(int id) => civilians.Find(p => p.id == id);

        /// <summary>台帳の在任者を1人取る（年次人事が既存配属から移行したもの）。</summary>
        private CivilServiceRecord AnyServing()
        {
            List<CivilServiceRecord> recs = alliance.civilService.records;
            Assert.IsNotNull(recs);
            Assert.Greater(recs.Count, 0, "前提：台帳に在任者がいない（既存配属の移行が走っていない）");
            return recs[0];
        }

        /// <summary>選択省と違う省（異動の行き先／在籍していない省の指定に使う）。</summary>
        private Ministry OtherMinistryThan(int ministryId)
        {
            List<Ministry> tree = Tree();
            for (int i = 0; i < tree.Count; i++)
                if (tree[i] != null && tree[i].id != ministryId) return tree[i];
            return null;
        }

        private CivilServiceAppointmentPanel OpenPanel()
        {
            CivilServiceAppointmentPanel.Show();
            CivilServiceAppointmentPanel panel = CivilServiceAppointmentPanel.InstanceForTest;
            Assert.IsNotNull(panel, "官僚人事メニューが生成されない");
            Assert.IsTrue(CivilServiceAppointmentPanel.IsOpen);
            return panel;
        }

        private static int ActiveCards() => DecisionDeck.Queue != null ? DecisionDeck.Queue.ActiveCount() : 0;

        // ===== 1. 窓の生成・スクロールバー・Esc =====

        [Test]
        public void Panel_BuildsScrollableListsAndClosesThroughEscStack()
        {
            BuildLiveWorld();
            view.BindPlayerCharacterForQa(Find(PremierId));
            CivilServiceAppointmentPanel panel = OpenPanel();

            Assert.IsTrue(panel.AllListsHaveScrollbarsForTest, "省庁・対象・履歴の一覧に見えるスクロールバーがない");
            Assert.IsTrue(panel.ReasonAndFilterFieldsExistForTest, "絞り込み・理由の入力欄がない");
            Assert.AreEqual(5, panel.ActionButtonCountForTest, "配属/異動/昇任/降任/解任の5種がない");
            Assert.Greater(panel.MinistryRowCountForTest, 0, "省庁一覧が空");
            Assert.IsFalse(panel.ConfirmedForTest, "開いた直後に確認済みになっている");
            Assert.IsFalse(panel.FinalButtonInteractableForTest, "対象を選ぶ前に確定できる");
            StringAssert.Contains("操作者", panel.HeaderTextForTest);

            Assert.IsTrue(panel.EscRegisteredForTest, "Esc スタックに登録していない");
            Assert.IsTrue(UIWindowStack.CloseTopmost(), "Esc で閉じられる窓として登録されていない");
            Assert.IsFalse(CivilServiceAppointmentPanel.IsOpen, "Esc（CloseTopmost）で閉じない");

            CivilServiceAppointmentPanel.Toggle();
            Assert.IsTrue(CivilServiceAppointmentPanel.IsOpen);
            CivilServiceAppointmentPanel.Toggle();
            Assert.IsFalse(CivilServiceAppointmentPanel.IsOpen);
        }

        // ===== 2. 5種の操作から Preview へ渡る引数（省・行為・人物・段） =====

        [Test]
        public void Preview_ArgumentsFollowSelectionRules_ForAllFiveActions()
        {
            BuildLiveWorld();
            view.BindPlayerCharacterForQa(Find(PremierId));
            CivilServiceAppointmentPanel panel = OpenPanel();

            CivilServiceRecord rec = AnyServing();
            Ministry other = OtherMinistryThan(rec.ministryId);
            Assert.IsNotNull(other, "前提：異動の行き先になる別の省がない");
            civilians.Add(NewBureaucrat(Newcomer, 20));

            // --- 配属：未在籍の人物を対象の省の「一般官僚」へ ---
            panel.SelectForTest(CivilServiceAction.配属, other.id, Newcomer, "試験：配属");
            Assert.IsNull(panel.LastPlanProblemForTest, panel.ConfirmTextForTest);
            Assert.IsTrue(panel.LastPreviewQueriedForTest, "配属で見込みを引いていない");
            Assert.AreEqual(other.id, panel.LastPreviewMinistryIdForTest);
            Assert.AreEqual(CivilServiceAction.配属, panel.LastPreviewActionForTest);
            Assert.AreEqual(Newcomer, panel.LastPreviewPersonIdForTest);
            Assert.AreEqual(BureaucratGrade.一般官僚, panel.LastPreviewGradeForTest, "配属は一般官僚から");

            // 以後は台帳側の段を試験の材料として動かす（画面は段を書き換えない＝導出だけを見る）
            rec.grade = BureaucratGrade.局長級;

            // --- 異動：段は現職のまま・行き先は選んだ省 ---
            panel.SelectForTest(CivilServiceAction.異動, other.id, rec.personId, "試験：異動");
            Assert.IsNull(panel.LastPlanProblemForTest, panel.ConfirmTextForTest);
            Assert.AreEqual(other.id, panel.LastPreviewMinistryIdForTest, "異動の行き先が選んだ省でない");
            Assert.AreEqual(BureaucratGrade.局長級, panel.LastPreviewGradeForTest, "異動で段が変わっている");

            // --- 昇任：1段上を自動設定 ---
            panel.SelectForTest(CivilServiceAction.昇任, rec.ministryId, rec.personId, "試験：昇任");
            Assert.IsNull(panel.LastPlanProblemForTest, panel.ConfirmTextForTest);
            Assert.AreEqual(BureaucratGrade.事務次官級, panel.LastPreviewGradeForTest, "昇任が1段上でない");

            // --- 降任：1段下を自動設定 ---
            panel.SelectForTest(CivilServiceAction.降任, rec.ministryId, rec.personId, "試験：降任");
            Assert.IsNull(panel.LastPlanProblemForTest, panel.ConfirmTextForTest);
            Assert.AreEqual(BureaucratGrade.課長級, panel.LastPreviewGradeForTest, "降任が1段下でない");

            // --- 解任：現在の段が対象 ---
            panel.SelectForTest(CivilServiceAction.解任, rec.ministryId, rec.personId, "試験：解任");
            Assert.IsNull(panel.LastPlanProblemForTest, panel.ConfirmTextForTest);
            Assert.AreEqual(BureaucratGrade.局長級, panel.LastPreviewGradeForTest, "解任は現在の段を対象にする");
            Assert.AreEqual(rec.ministryId, panel.LastPreviewMinistryIdForTest);
        }

        // ===== 3. 成立しない組合せ（最上段の昇任・最下段の降任）は確定不能 =====

        [Test]
        public void ImpossibleSteps_AreNotConfirmable_AndShowReason()
        {
            BuildLiveWorld();
            view.BindPlayerCharacterForQa(Find(PremierId));
            CivilServiceAppointmentPanel panel = OpenPanel();

            CivilServiceRecord rec = AnyServing();

            // 最下段（一般官僚）の降任
            rec.grade = BureaucratGrade.一般官僚;
            panel.SelectForTest(CivilServiceAction.降任, rec.ministryId, rec.personId, "試験：降任できない");
            Assert.IsNotNull(panel.LastPlanProblemForTest, "最下段の降任が組めてしまう");
            Assert.IsFalse(panel.LastPreviewQueriedForTest, "組めない要求で見込みを引いている");
            Assert.IsFalse(panel.ConfirmButtonInteractableForTest, "最下段の降任を確認できる");
            Assert.IsFalse(panel.FinalButtonInteractableForTest, "最下段の降任を確定できる");
            StringAssert.Contains("降任できません", panel.ConfirmTextForTest);

            // 最上段（事務次官級）の昇任
            rec.grade = BureaucratGrade.事務次官級;
            panel.SelectForTest(CivilServiceAction.昇任, rec.ministryId, rec.personId, "試験：昇任できない");
            Assert.IsNotNull(panel.LastPlanProblemForTest, "最上段の昇任が組めてしまう");
            Assert.IsFalse(panel.ConfirmButtonInteractableForTest, "最上段の昇任を確認できる");
            Assert.IsFalse(panel.FinalButtonInteractableForTest, "最上段の昇任を確定できる");
            StringAssert.Contains("昇任できません", panel.ConfirmTextForTest);

            // 対象を選んでいない
            panel.SelectForTest(CivilServiceAction.配属, rec.ministryId, -1, "");
            Assert.IsFalse(panel.FinalButtonInteractableForTest, "対象なしで確定できる");
            StringAssert.Contains("確定できません", panel.ConfirmTextForTest);
        }

        // ===== 4. 権限があれば直接実行（台帳が動く・決裁カードを作らない） =====

        [Test]
        public void DirectExecute_ThroughPanel_UpdatesLedger()
        {
            BuildLiveWorld();
            Ministry hyobu = MinistryNamed("兵部省");
            Assert.IsNotNull(hyobu, "前提：兵部省がない");
            int minister = MinisterOf(hyobu.id);
            Assert.Greater(minister, 0, "前提：兵部省に所管大臣がいない");

            hyobu.staffSlots++;                 // 入省の空きを1つ作る
            civilians.Add(NewBureaucrat(Newcomer, 20));
            view.BindPlayerCharacterForQa(Find(minister));
            int cardsBefore = ActiveCards();

            CivilServiceAppointmentPanel panel = OpenPanel();
            panel.SelectForTest(CivilServiceAction.配属, hyobu.id, Newcomer, "試験：画面からの直接任用");
            Assert.IsTrue(panel.LastPreviewOkForTest, "所管大臣の配属が直接実行にならない：" + panel.ConfirmTextForTest);
            Assert.IsTrue(panel.ConfirmButtonInteractableForTest, "確認を押せない");
            Assert.IsFalse(panel.FinalButtonInteractableForTest, "確認前に確定できる（二段階確認になっていない）");

            panel.ConfirmForTest();
            Assert.IsTrue(panel.ConfirmedForTest);
            Assert.IsTrue(panel.FinalButtonInteractableForTest, "確認後に確定できない");
            Assert.AreEqual("直接実行する", panel.FinalButtonCaptionForTest, "直接実行だと明示していない");
            StringAssert.Contains("【確認】", panel.ConfirmTextForTest, "確認内容が固定表示されない");
            StringAssert.Contains("試験：画面からの直接任用", panel.ConfirmTextForTest, "理由が確認欄に出ない");

            Assert.IsNull(CivilServicePostRules.FindServing(alliance.civilService, Newcomer), "確認だけで台帳が動いた");
            panel.ExecuteForTest();

            Assert.AreEqual(CivilServiceRequestOutcome.実行, panel.LastResultForTest.outcome, panel.MessageTextForTest);
            StringAssert.Contains("直接実行", panel.MessageTextForTest);
            Assert.AreEqual(cardsBefore, ActiveCards(), "直接実行で決裁カードが増えた");

            CivilServiceRecord made = CivilServicePostRules.FindServing(alliance.civilService, Newcomer);
            Assert.IsNotNull(made, "台帳へ反映されない");
            Assert.AreEqual(hyobu.id, made.ministryId);
            Assert.AreEqual(BureaucratGrade.一般官僚, made.grade);
            Assert.AreEqual(minister, made.appointedById, "承認したのが所管大臣でない");
            StringAssert.Contains("試験：画面からの直接任用", made.reason, "理由が人事履歴へ残らない");
            Assert.IsTrue(hyobu.staffIds.Contains(Newcomer), "省庁の配属と食い違う");
            Assert.IsFalse(panel.ConfirmedForTest, "実行後も確認が残っている");
        }

        /// <summary>理由が空でも受け付ける（Core の既定理由が入る）。画面は既定理由を案内する。</summary>
        [Test]
        public void DirectExecute_WithBlankReason_UsesDefaultReason()
        {
            BuildLiveWorld();
            Ministry hyobu = MinistryNamed("兵部省");
            int minister = MinisterOf(hyobu.id);
            hyobu.staffSlots++;
            civilians.Add(NewBureaucrat(Newcomer, 20));
            view.BindPlayerCharacterForQa(Find(minister));

            CivilServiceAppointmentPanel panel = OpenPanel();
            panel.SelectForTest(CivilServiceAction.配属, hyobu.id, Newcomer, "   ");
            StringAssert.Contains("既定理由", panel.ConfirmTextForTest, "未入力時に既定理由を案内していない");
            panel.ConfirmForTest();
            Assert.IsTrue(panel.FinalButtonInteractableForTest, "理由未入力で確定できない");
            panel.ExecuteForTest();

            Assert.IsTrue(panel.LastResultForTest.DidExecute, panel.MessageTextForTest);
            CivilServiceRecord made = CivilServicePostRules.FindServing(alliance.civilService, Newcomer);
            Assert.IsNotNull(made);
            StringAssert.Contains(CivilServiceRingiRules.DefaultReason(CivilServiceAction.配属), made.reason,
                "理由が空のとき既定文を残さない");
        }

        // ===== 5. 権限外は上申（決裁 id と決裁者を出し、台帳はまだ動かない） =====

        [Test]
        public void Petition_ThroughPanel_ShowsDecisionIdAndLeavesLedgerUntouched()
        {
            BuildLiveWorld();
            Ministry hyobu = MinistryNamed("兵部省");
            int minister = MinisterOf(hyobu.id);
            hyobu.staffSlots++;
            civilians.Add(NewBureaucrat(Newcomer, 20));

            Person clerk = Find(BureaucratId);        // 権限を持たない職業官僚が起案する
            Assert.IsNotNull(clerk);
            view.BindPlayerCharacterForQa(clerk);

            CivilServiceAppointmentPanel panel = OpenPanel();
            panel.SelectForTest(CivilServiceAction.配属, hyobu.id, Newcomer, "試験：画面からの上申");
            Assert.IsFalse(panel.LastPreviewOkForTest, "権限のない操作者が直接実行できてしまう");
            Assert.IsTrue(panel.LastPreviewCanPetitionForTest, "上申にならない：" + panel.ConfirmTextForTest);
            Assert.AreEqual(minister, panel.LastPreviewPetitionToIdForTest, "上申先が所管大臣でない");

            panel.ConfirmForTest();
            Assert.AreEqual("上申する", panel.FinalButtonCaptionForTest, "上申だと明示していない");
            StringAssert.Contains("上申", panel.ConfirmTextForTest);
            StringAssert.Contains("上申先", panel.ConfirmTextForTest, "上申先を出していない");

            int recordsBefore = alliance.civilService.records.Count;
            panel.ExecuteForTest();

            CivilServiceRequestResult r = panel.LastResultForTest;
            Assert.AreEqual(CivilServiceRequestOutcome.上申, r.outcome, panel.MessageTextForTest);
            Assert.GreaterOrEqual(r.decisionId, GalaxyView.CivilServiceDecisionIdBand, "決裁 id の番号帯が違う");
            Assert.AreEqual(minister, r.addresseeId, "決裁者が所管大臣でない");
            StringAssert.Contains("決裁#" + r.decisionId, panel.MessageTextForTest, "決裁 id を画面に出していない");
            StringAssert.Contains("人物#" + r.addresseeId, panel.MessageTextForTest, "決裁者を画面に出していない");
            StringAssert.Contains("台帳はまだ変わっていません", panel.MessageTextForTest, "未反映であることを明示していない");

            Assert.IsNull(CivilServicePostRules.FindServing(alliance.civilService, Newcomer), "上申の時点で台帳が動いた");
            Assert.AreEqual(recordsBefore, alliance.civilService.records.Count, "上申で台帳が増えた");
            Assert.IsFalse(hyobu.staffIds.Contains(Newcomer), "上申で省庁の配属が動いた");
        }

        // ===== 6. 選択・理由を変えたら確認は解除される =====

        [Test]
        public void ChangingSelectionOrReason_ClearsConfirmation()
        {
            BuildLiveWorld();
            Ministry hyobu = MinistryNamed("兵部省");
            int minister = MinisterOf(hyobu.id);
            hyobu.staffSlots += 2;
            civilians.Add(NewBureaucrat(Newcomer, 20));
            civilians.Add(NewBureaucrat(Newcomer + 1, 20));
            view.BindPlayerCharacterForQa(Find(minister));

            CivilServiceAppointmentPanel panel = OpenPanel();
            panel.SelectForTest(CivilServiceAction.配属, hyobu.id, Newcomer, "試験：確認の解除");
            panel.ConfirmForTest();
            Assert.IsTrue(panel.ConfirmedForTest);

            panel.SetReasonForTest("試験：理由を書き換えた");
            Assert.IsFalse(panel.ConfirmedForTest, "理由を変えても確認が残る");
            Assert.IsFalse(panel.FinalButtonInteractableForTest, "確認が解除されたのに確定できる");

            panel.ConfirmForTest();
            panel.SelectPersonForTest(Newcomer + 1);
            Assert.IsFalse(panel.ConfirmedForTest, "対象を変えても確認が残る");

            panel.ConfirmForTest();
            panel.SelectActionForTest(CivilServiceAction.解任);
            Assert.IsFalse(panel.ConfirmedForTest, "操作種別を変えても確認が残る");

            // 確認していない状態で確定を呼んでも実行されない（二段階確認）
            panel.SelectForTest(CivilServiceAction.配属, hyobu.id, Newcomer, "試験：確認せずに実行");
            int recordsBefore = alliance.civilService.records.Count;
            panel.ExecuteForTest();
            Assert.AreEqual(recordsBefore, alliance.civilService.records.Count, "確認前に台帳が動いた");
            StringAssert.Contains("内容を確認", panel.MessageTextForTest);
        }

        // ===== 7. 再描画は読み取り専用（台帳・省庁の配属・決裁を変えない） =====

        [Test]
        public void Rebuild_IsReadOnly()
        {
            BuildLiveWorld();
            view.BindPlayerCharacterForQa(Find(PremierId));
            CivilServiceAppointmentPanel panel = OpenPanel();

            CivilServiceRecord rec = AnyServing();
            panel.SelectForTest(CivilServiceAction.昇任, rec.ministryId, rec.personId, "試験：再描画");

            int records = alliance.civilService.records.Count;
            int history = alliance.civilService.history.Count;
            int cards = ActiveCards();
            int petitions = RingiDirector.Ledger != null ? RingiDirector.Ledger.ActiveCount() : 0;
            List<Ministry> tree = Tree();
            var staffCounts = new List<int>();
            for (int i = 0; i < tree.Count; i++) staffCounts.Add(tree[i].staffIds.Count);

            for (int i = 0; i < 3; i++) panel.RebuildForTest();
            panel.SetEligibleOnlyForTest(false);
            panel.SetFilterForTest("試験");
            panel.SetFilterForTest("");

            Assert.AreEqual(records, alliance.civilService.records.Count, "再描画で在任記録が変わった");
            Assert.AreEqual(history, alliance.civilService.history.Count, "再描画で履歴が変わった");
            Assert.AreEqual(cards, ActiveCards(), "再描画で決裁カードが増えた");
            Assert.AreEqual(petitions, RingiDirector.Ledger != null ? RingiDirector.Ledger.ActiveCount() : 0,
                "再描画で稟議が増えた");
            for (int i = 0; i < tree.Count; i++)
                Assert.AreEqual(staffCounts[i], tree[i].staffIds.Count, tree[i].ministryName + " の配属が再描画で変わった");
        }

        // ===== 8. 材料が無くても壊さない（台帳 null でも理由つきで操作不能） =====

        [Test]
        public void MissingLedger_DisablesOperationsWithReason()
        {
            BuildLiveWorld();
            view.BindPlayerCharacterForQa(Find(PremierId));
            alliance.civilService = null;   // 旧セーブ・年次人事の前などを模す

            CivilServiceAppointmentPanel panel = OpenPanel();
            Assert.IsFalse(panel.ConfirmButtonInteractableForTest, "台帳が無いのに確認できる");
            Assert.IsFalse(panel.FinalButtonInteractableForTest, "台帳が無いのに確定できる");
            Assert.AreEqual("実行できません", panel.FinalButtonCaptionForTest);
            Assert.IsNotEmpty(panel.ConfirmTextForTest, "操作不能の理由が出ない");
            StringAssert.Contains("人事台帳", panel.HeaderTextForTest);
            Assert.Greater(panel.MinistryRowCountForTest, 0, "理由の行すら出ていない");
        }
    }
}
