using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 内閣人事メニューの接続試験（#2768 #141）。実 GalaxyView（政府シード＋年次の政治 Tick で組閣）に対し、
    /// 操作者を実在の人物に固定して GalaxyView の Player* 入口（＝CabinetAppointmentRules の共通入口）を通す。
    /// Core の判定を写す試験ではなく、メニューの生成（スクロールバー・入力欄・操作ボタン）と、首相の任命成功・無権限の失敗・委任期限の結果だけを見る。
    /// Start・実セーブは走らせない。TearDown で static・Registry・Clock・Active を戻す。
    /// </summary>
    public class CabinetAppointmentPanelPlayModeTests
    {
        private const int YearSeconds = 60 * 30 * 12;
        private const int FirstYear = 797;
        private const int PoliticianCount = 30;
        private const int FirstPoliticianId = 11;
        private const Faction F = Faction.同盟;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private CampaignState savedCampaign;
        private GalaxyMap savedMap;
        private Dictionary<int, Province> savedProvinces;
        private GameClock savedClock;
        private DecisionQueue savedDecisions;
        private PetitionLedger savedPetitions;
        private List<GovernmentRegistry.Appointment> savedAppointments;
        private GalaxyView savedActive;

        private GalaxyView view;
        private List<Person> civilians;
        private PoliticsState pol;
        private int premierId, ministryId;

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
            savedAppointments = new List<GovernmentRegistry.Appointment>(GovernmentRegistry.Appointments);
            StrategySession.Decisions = new DecisionQueue();
            StrategySession.Petitions = new PetitionLedger();
        }

        [TearDown]
        public void TearDown()
        {
            CabinetAppointmentPanel panel = CabinetAppointmentPanel.InstanceForTest;
            if (panel != null) Object.DestroyImmediate(panel.gameObject);
            if (view != null) view.BindPlayerCharacterForQa(null);
            view = null;
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            spawned.Clear();

            GalaxyView.SwapActiveForQa(savedActive);
            StrategySession.Campaign = savedCampaign;
            StrategySession.Map = savedMap;
            StrategySession.Provinces = savedProvinces;
            StrategySession.Clock = savedClock;
            StrategySession.Decisions = savedDecisions;
            StrategySession.Petitions = savedPetitions;
            GovernmentRegistry.Clear();
            for (int i = 0; i < savedAppointments.Count; i++)
            {
                GovernmentRegistry.Appointment a = savedAppointments[i];
                GovernmentRegistry.TryAppoint(a.faction, a.office, a.holder, a.scopeKey);
            }
        }

        /// <summary>本番の経路で内閣まで作る（CabinetLiveContextPlayModeTests と同じ固定条件の同盟）。</summary>
        private void BuildLiveWorld()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(1, "人事試験首都星", Vector2.zero, F));
            map.AddSystem(new StarSystem(2, "人事試験辺境星", new Vector2(1f, 0f), F));
            var provinces = new Dictionary<int, Province>
            {
                { 1, new Province(1, "", 1000f) },
                { 2, new Province(2, "", 600f) },
            };
            civilians = new List<Person>();
            for (int i = 0; i < PoliticianCount; i++)
            {
                int id = FirstPoliticianId + i;
                civilians.Add(new Person(id, "人事試験政治家" + id, F, PersonRole.文民)
                {
                    isPolitician = true, birthYear = 760, charisma = 40 + i, intelligence = 60, operation = 70,
                });
            }
            var campaign = new CampaignState(map);
            var alliance = new FactionState(F) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            alliance.politics.parties.Add(new Party(1, "人事試験民政党", F) { support = 0.6f });
            alliance.politics.parties.Add(new Party(2, "人事試験進歩党", F) { support = 0.4f });
            campaign.states.Add(alliance);
            StrategySession.Map = map;
            StrategySession.Campaign = campaign;
            StrategySession.Provinces = provinces;
            StrategySession.Clock = new GameClock { elapsedSeconds = YearSeconds + 1d };

            var go = new GameObject("GalaxyView_CabinetPanelQa");
            go.SetActive(false); // Start を走らせない
            spawned.Add(go);
            view = go.AddComponent<GalaxyView>();
            view.BindElectionQaWorld(map, provinces, new List<Person>(), civilians);
            view.SeedGovernmentForQa();
            Assert.AreEqual(FirstYear, view.ElectionYearForQa);
            view.RunPoliticsTickForQa();

            pol = alliance.politics;
            Assert.IsNotNull(pol.government, "前提：組閣されていない");
            premierId = pol.government.premierPersonId;
            Assert.GreaterOrEqual(premierId, FirstPoliticianId, "前提：首相が決まらない");
            List<Ministry> mins = CabinetAppointmentRules.CabinetMinistries(view.MinistriesOf(F) as IList<Ministry>, view.TopMinistryIdOf(F));
            Assert.Greater(mins.Count, 0, "前提：大臣を置く省がない");
            ministryId = mins[0].id;
            GalaxyView.SwapActiveForQa(view);
        }

        private CabinetPost Post(CabinetPostKind kind)
        {
            CabinetPost p = CabinetAppointmentRules.FindPost(pol.cabinet, ministryId, kind);
            Assert.IsNotNull(p, "前提：" + kind + " の職がない");
            return p;
        }

        private Person P(int id)
        {
            Person p = view.FindPersonById(id);
            Assert.IsNotNull(p, "人物#" + id + " が実名簿にない");
            return p;
        }

        [Test]
        public void Panel_BuildsScrollableListsAndClickEntries()
        {
            BuildLiveWorld();
            view.BindPlayerCharacterForQa(P(premierId));
            CabinetAppointmentPanel.Show();
            CabinetAppointmentPanel panel = CabinetAppointmentPanel.InstanceForTest;
            Assert.IsNotNull(panel, "内閣人事メニューが生成されない");
            Assert.IsTrue(CabinetAppointmentPanel.IsOpen);
            Assert.IsTrue(panel.ListsHaveScrollbarsForTest, "職一覧・候補一覧にスクロールバーがない");
            Assert.IsTrue(panel.FilterAndReasonFieldsExistForTest, "絞り込み・理由の入力欄がない");
            Assert.AreEqual(4, panel.ActionButtonCountForTest, "任命/解任/委任/委任撤回の操作ボタン");
            Assert.IsFalse(panel.ConfirmInteractableForTest, "操作を選ぶ前に確定できる");
            CabinetAppointmentPanel.Toggle();
            Assert.IsFalse(CabinetAppointmentPanel.IsOpen);
        }

        /// <summary>首相本人が理由つきで解任→同じメニューの確認→確定で任命。在任・履歴は PoliticsState.cabinet に載り、年次の補充で差し替わらない。</summary>
        [Test]
        public void Premier_DismissThenAppointThroughPanel_Succeeds()
        {
            BuildLiveWorld();
            view.BindPlayerCharacterForQa(P(premierId));
            CabinetPost minister = Post(CabinetPostKind.大臣);
            Assert.GreaterOrEqual(minister.holderId, 0, "前提：大臣が在任していない");

            Assert.IsFalse(view.PlayerCabinetDismiss(ministryId, CabinetPostKind.大臣, " ").ok, "理由なしで解任できた");
            AppointmentResult dismissed = view.PlayerCabinetDismiss(ministryId, CabinetPostKind.大臣, "試験の解任");
            Assert.IsTrue(dismissed.ok, dismissed.reason);
            Assert.Less(minister.holderId, 0);

            int candidate = -1;
            for (int i = 0; i < civilians.Count && candidate < 0; i++)
                if (CabinetAppointmentRules.CandidateProblem(pol, F, pol.cabinet, civilians[i].id, premierId, civilians, GalaxyView.CabinetParamsInUse) == null)
                    candidate = civilians[i].id;
            Assert.GreaterOrEqual(candidate, 0, "前提：適格な候補がいない");

            CabinetAppointmentPanel.Show();
            CabinetAppointmentPanel panel = CabinetAppointmentPanel.InstanceForTest;
            panel.SelectForTest(ministryId, CabinetPostKind.大臣, candidate, "試験の任命", CabinetAppointmentPanel.PendingOp.任命);
            Assert.IsTrue(panel.ConfirmInteractableForTest, "首相・適格候補・理由ありで確定できない：" + panel.ConfirmTextForTest);
            panel.ExecuteForTest();
            StringAssert.Contains("実行", panel.MessageTextForTest);
            Assert.AreEqual(candidate, minister.holderId, "任命が内閣の台帳に載らない");
            Assert.AreEqual(premierId, minister.appointedById);
            StringAssert.Contains("試験の任命", minister.appointmentReason);
            AppointmentHistoryEntry last = pol.cabinet.history[pol.cabinet.history.Count - 1];
            Assert.AreEqual(candidate, last.personId);
            Assert.AreEqual(premierId, last.actorId);

            view.RunPoliticsTickForQa();
            Assert.AreEqual(candidate, minister.holderId, "年次の補充が手動の在任者を差し替えた");
        }

        /// <summary>政務官が操作者：任命は首相への上申扱いで失敗し台帳は変わらず、メニューも確定できない。</summary>
        [Test]
        public void Secretary_CannotAppoint_ReasonShownAndNothingChanges()
        {
            BuildLiveWorld();
            CabinetPost secretary = Post(CabinetPostKind.政務官);
            CabinetPost minister = Post(CabinetPostKind.大臣);
            Assert.GreaterOrEqual(secretary.holderId, 0, "前提：政務官が在任していない");
            int holderBefore = minister.holderId;
            int historyBefore = pol.cabinet.history.Count;
            view.BindPlayerCharacterForQa(P(secretary.holderId));

            AppointmentResult check = view.CheckPlayerCabinetAppoint(ministryId, CabinetPostKind.大臣, FirstPoliticianId);
            Assert.IsFalse(check.ok);
            Assert.IsTrue(check.canPetition, check.reason);
            Assert.AreEqual(premierId, check.petitionToId);
            AppointmentResult r = view.PlayerCabinetAppoint(ministryId, CabinetPostKind.大臣, FirstPoliticianId, "権限外の任命");
            Assert.IsFalse(r.ok);
            Assert.AreEqual(check.reason, r.reason, "確認表示と実行の判定が別");
            Assert.IsFalse(view.PlayerCabinetDismiss(ministryId, CabinetPostKind.大臣, "権限外の解任").ok);
            Assert.AreEqual(holderBefore, minister.holderId);
            Assert.AreEqual(historyBefore, pol.cabinet.history.Count);

            CabinetAppointmentPanel.Show();
            CabinetAppointmentPanel panel = CabinetAppointmentPanel.InstanceForTest;
            panel.SelectForTest(ministryId, CabinetPostKind.大臣, FirstPoliticianId, "権限外の任命", CabinetAppointmentPanel.PendingOp.任命);
            Assert.IsFalse(panel.ConfirmInteractableForTest, "無権限で確定できる");
            StringAssert.Contains("権限外", panel.ConfirmTextForTest);
        }

        /// <summary>大臣本人：最長を超える期限は失敗、今年＋1年は成功し期限が記録される。政務官は委任できず、撤回は理由必須。</summary>
        [Test]
        public void Minister_DelegationRespectsLimit_OthersDenied()
        {
            BuildLiveWorld();
            CabinetPost minister = Post(CabinetPostKind.大臣);
            CabinetPost vice = Post(CabinetPostKind.副大臣);
            CabinetPost secretary = Post(CabinetPostKind.政務官);
            Assert.GreaterOrEqual(minister.holderId, 0, "前提：大臣が在任していない");
            Assert.GreaterOrEqual(vice.holderId, 0, "前提：副大臣が在任していない");
            int max = GalaxyView.CabinetParamsInUse.maxDelegationYears;

            view.BindPlayerCharacterForQa(P(secretary.holderId));
            Assert.IsFalse(view.PlayerCabinetDelegate(ministryId, CabinetDelegation.所管決裁, FirstYear + 1).ok, "政務官が委任できた");
            Assert.AreEqual(CabinetDelegation.なし, vice.delegation);

            view.BindPlayerCharacterForQa(P(minister.holderId));
            AppointmentResult tooLong = view.PlayerCabinetDelegate(ministryId, CabinetDelegation.所管決裁, FirstYear + max + 1);
            Assert.IsFalse(tooLong.ok);
            StringAssert.Contains("長すぎる", tooLong.reason);
            Assert.AreEqual(CabinetDelegation.なし, vice.delegation);

            AppointmentResult ok = view.PlayerCabinetDelegate(ministryId, CabinetDelegation.所管決裁, FirstYear + 1);
            Assert.IsTrue(ok.ok, ok.reason);
            Assert.AreEqual(CabinetDelegation.所管決裁, vice.delegation);
            Assert.AreEqual(FirstYear + 1, vice.delegationEndYear);
            Assert.AreEqual(minister.holderId, vice.delegatedById);

            Assert.IsFalse(view.PlayerCabinetRevokeDelegation(ministryId, "").ok, "理由なしで撤回できた");
            Assert.IsTrue(view.PlayerCabinetRevokeDelegation(ministryId, "試験の撤回").ok);
            Assert.AreEqual(CabinetDelegation.なし, vice.delegation);
        }
    }
}
