using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 党人事メニューの接続試験（#2768 #159 #165）。実 GalaxyView（政府シード＋年次の政治 Tick で組閣・党首・党三役）に対し、
    /// 操作者を実在の人物に固定して GalaxyView の PlayerParty* 入口（＝PartyExecutiveRules の共通入口）を通す。
    /// Core の判定を写す試験ではなく、メニューの生成（スクロールバー・入力欄・操作ボタン）と、党首の任免成功・権限のない操作者の失敗だけを見る。
    /// Start・実セーブは走らせない。TearDown で static・Registry・Clock・Active を戻す。
    /// </summary>
    public class PartyExecutivePanelPlayModeTests
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
        private Party ruling, opposition;

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
            PartyExecutivePanel panel = PartyExecutivePanel.InstanceForTest;
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

        /// <summary>本番の経路で組閣・党首・党三役まで作る（CabinetAppointmentPanelPlayModeTests と同じ固定条件の同盟）。</summary>
        private void BuildLiveWorld()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(1, "党人事試験首都星", Vector2.zero, F));
            map.AddSystem(new StarSystem(2, "党人事試験辺境星", new Vector2(1f, 0f), F));
            var provinces = new Dictionary<int, Province>
            {
                { 1, new Province(1, "", 1000f) },
                { 2, new Province(2, "", 600f) },
            };
            civilians = new List<Person>();
            for (int i = 0; i < PoliticianCount; i++)
            {
                int id = FirstPoliticianId + i;
                civilians.Add(new Person(id, "党人事試験政治家" + id, F, PersonRole.文民)
                {
                    isPolitician = true, birthYear = 760, charisma = 40 + i, intelligence = 60, operation = 70,
                });
            }
            var campaign = new CampaignState(map);
            var alliance = new FactionState(F) { governmentForm = GovernmentForm.共和制, politics = new PoliticsState() };
            alliance.politics.parties.Add(new Party(1, "党人事試験民政党", F) { support = 0.6f });
            alliance.politics.parties.Add(new Party(2, "党人事試験進歩党", F) { support = 0.4f });
            campaign.states.Add(alliance);
            StrategySession.Map = map;
            StrategySession.Campaign = campaign;
            StrategySession.Provinces = provinces;
            StrategySession.Clock = new GameClock { elapsedSeconds = YearSeconds + 1d };

            var go = new GameObject("GalaxyView_PartyPanelQa");
            go.SetActive(false); // Start を走らせない
            spawned.Add(go);
            view = go.AddComponent<GalaxyView>();
            view.BindElectionQaWorld(map, provinces, new List<Person>(), civilians);
            view.SeedGovernmentForQa();
            Assert.AreEqual(FirstYear, view.ElectionYearForQa);
            view.RunPoliticsTickForQa();

            pol = alliance.politics;
            Assert.IsNotNull(pol.government, "前提：組閣されていない");
            ruling = ElectionCycleRules.FindParty(pol.parties, pol.government.partyId);
            Assert.IsNotNull(ruling, "前提：与党がない");
            Assert.GreaterOrEqual(ruling.leaderId, FirstPoliticianId, "前提：与党の党首が決まらない");
            opposition = null;
            for (int i = 0; i < pol.parties.Count; i++)
                if (pol.parties[i] != null && pol.parties[i].id != ruling.id) opposition = pol.parties[i];
            Assert.IsNotNull(opposition, "前提：他党がない");
            GalaxyView.SwapActiveForQa(view);
        }

        private Person P(int id)
        {
            Person p = view.FindPersonById(id);
            Assert.IsNotNull(p, "人物#" + id + " が実名簿にない");
            return p;
        }

        private int FirstEligible(Party party)
        {
            for (int i = 0; i < civilians.Count; i++)
                if (PartyExecutiveRules.CandidateProblem(pol, F, party, civilians[i].id, civilians, GalaxyView.CabinetParamsInUse) == null)
                    return civilians[i].id;
            return -1;
        }

        [Test]
        public void Panel_BuildsScrollableListsAndClickEntries()
        {
            BuildLiveWorld();
            view.BindPlayerCharacterForQa(P(ruling.leaderId));
            PartyExecutivePanel.Show();
            PartyExecutivePanel panel = PartyExecutivePanel.InstanceForTest;
            Assert.IsNotNull(panel, "党人事メニューが生成されない");
            Assert.IsTrue(PartyExecutivePanel.IsOpen);
            Assert.IsTrue(panel.ListsHaveScrollbarsForTest, "党・職一覧と候補一覧にスクロールバーがない");
            Assert.IsTrue(panel.FilterAndReasonFieldsExistForTest, "絞り込み・理由の入力欄がない");
            Assert.AreEqual(8, panel.ActionButtonCountForTest, "任命/解任と総裁選・派閥の操作ボタンがそろわない");
            Assert.IsFalse(panel.ConfirmInteractableForTest, "操作を選ぶ前に確定できる");

            panel.SelectForTest(ruling.id, PartyPost.幹事長, -1, "", PartyExecutivePanel.PendingOp.なし);
            Assert.GreaterOrEqual(panel.PostRowCountForTest, pol.parties.Count + PartyExecutiveRules.ExecutivePosts.Length,
                "党を開いたとき党の行と三役の行が並ばない");
            Assert.Greater(panel.CandidateRowCountForTest, 0, "職を選んでも候補欄が空");

            PartyExecutivePanel.Toggle();
            Assert.IsFalse(PartyExecutivePanel.IsOpen);
        }

        [Test]
        public void LeadershipElection_AnnounceCandidacyCloseAndCount_ThroughVisiblePanel()
        {
            BuildLiveWorld();
            int leader = ruling.leaderId;
            view.BindPlayerCharacterForQa(P(leader));
            ruling.leadership.lastElectionYear = 0;
            ruling.leadership.process = new LeadershipElectionProcess();

            PartyExecutivePanel.Show();
            PartyExecutivePanel panel = PartyExecutivePanel.InstanceForTest;
            panel.SelectPoliticsForTest(ruling.id, leader, -1);
            panel.AnnounceElectionForTest();
            Assert.AreEqual(LeadershipElectionPhase.立候補受付, ruling.leadership.process.phase, panel.MessageTextForTest);
            panel.ToggleCandidacyForTest();
            Assert.AreEqual(1, ruling.leadership.process.candidacies.Count, panel.MessageTextForTest);
            panel.AdvanceElectionForTest();
            Assert.AreEqual(LeadershipElectionPhase.投開票待ち, ruling.leadership.process.phase, panel.MessageTextForTest);
            panel.AdvanceElectionForTest();
            Assert.AreEqual(LeadershipElectionPhase.完了, ruling.leadership.process.phase, panel.MessageTextForTest);
            Assert.IsNotNull(ruling.leadership.Latest, "画面の投開票で結果記録が作られない");
            Assert.AreEqual(leader, ruling.leadership.Latest.winnerId, "単独立候補が党首にならない");
        }

        /// <summary>党首本人：理由なしは失敗→理由つき解任→同じメニューの確認→確定で任命。台帳は Party.posts/postHistory に載り、年次の補充で差し替わらない。</summary>
        [Test]
        public void Leader_DismissThenAppointThroughPanel_Succeeds()
        {
            BuildLiveWorld();
            int leader = ruling.leaderId;
            view.BindPlayerCharacterForQa(P(leader));

            PartyPost post = PartyPost.幹事長;
            int holder = -1;
            for (int k = 0; k < PartyExecutiveRules.ExecutivePosts.Length && holder < 0; k++)
            {
                PartyAppointment a0 = PartyExecutiveRules.AppointmentOf(ruling, PartyExecutiveRules.ExecutivePosts[k]);
                if (a0 != null) { post = a0.post; holder = a0.holderId; }
            }

            if (holder >= 0)
            {
                Assert.IsFalse(view.PlayerPartyDismiss(ruling.id, post, " ").ok, "理由なしで解任できた");
                Assert.AreEqual(holder, PartyOrganizationRules.HolderOf(ruling, post));

                PartyExecutivePanel.Show();
                PartyExecutivePanel dp = PartyExecutivePanel.InstanceForTest;
                dp.SelectForTest(ruling.id, post, -1, "試験の解任", PartyExecutivePanel.PendingOp.解任);
                Assert.IsTrue(dp.ConfirmInteractableForTest, "党首・在任者あり・理由ありで解任を確定できない：" + dp.ConfirmTextForTest);
                dp.ExecuteForTest();
                StringAssert.Contains("実行", dp.MessageTextForTest);
                Assert.AreEqual(-1, PartyOrganizationRules.HolderOf(ruling, post), "解任が党の台帳に載らない");
                AppointmentHistoryEntry dismissed = ruling.postHistory[ruling.postHistory.Count - 1];
                Assert.AreEqual("解任", dismissed.action);
                Assert.AreEqual(leader, dismissed.actorId);
                StringAssert.Contains("試験の解任", dismissed.reason);
            }

            int candidate = FirstEligible(ruling);
            if (candidate < 0 && pol.government.premierPersonId == leader && pol.cabinet != null)
            {
                // 前提づくり：党員が閣僚で埋まっていれば、首相（＝党首）が与党の政務官を理由つきで解任して党員を空ける（内閣の共通入口）
                for (int i = 0; i < pol.cabinet.posts.Count && candidate < 0; i++)
                {
                    CabinetPost cp = pol.cabinet.posts[i];
                    if (cp == null || cp.kind != CabinetPostKind.政務官 || cp.holderId < 0 || !PartyOrganizationRules.IsMember(ruling, cp.holderId)) continue;
                    Assert.IsTrue(view.PlayerCabinetDismiss(cp.ministryId, cp.kind, "試験の前提：党員を空ける").ok);
                    candidate = FirstEligible(ruling);
                }
            }
            Assert.GreaterOrEqual(candidate, 0, "前提：適格な候補がいない");
            Assert.IsNull(PartyExecutiveRules.AppointmentOf(ruling, post), "前提：対象の職が空席でない");

            PartyExecutivePanel.Show();
            PartyExecutivePanel panel = PartyExecutivePanel.InstanceForTest;
            panel.SelectForTest(ruling.id, post, candidate, "", PartyExecutivePanel.PendingOp.任命);
            Assert.IsFalse(panel.ConfirmInteractableForTest, "理由なしで任命を確定できる");
            Assert.IsFalse(view.PlayerPartyAppoint(ruling.id, post, candidate, "").ok, "理由なしで任命できた");

            panel.SelectForTest(ruling.id, post, candidate, "試験の任命", PartyExecutivePanel.PendingOp.任命);
            Assert.IsTrue(panel.ConfirmInteractableForTest, "党首・適格候補・理由ありで確定できない：" + panel.ConfirmTextForTest);
            panel.ExecuteForTest();
            StringAssert.Contains("実行", panel.MessageTextForTest);
            PartyAppointment a = PartyExecutiveRules.AppointmentOf(ruling, post);
            Assert.IsNotNull(a, "任命が党の台帳に載らない");
            Assert.AreEqual(candidate, a.holderId);
            Assert.AreEqual(leader, a.appointedById);
            StringAssert.Contains("試験の任命", a.reason);
            AppointmentHistoryEntry last = ruling.postHistory[ruling.postHistory.Count - 1];
            Assert.AreEqual(candidate, last.personId);
            Assert.AreEqual(leader, last.actorId);
            Assert.IsFalse(PartyExecutiveRules.Authority(ruling, F, candidate, PartyExecutiveAction.政府決裁, civilians, FirstYear).ok,
                "党三役に政府の決裁権が付いた");

            view.RunPoliticsTickForQa();
            Assert.AreEqual(candidate, PartyOrganizationRules.HolderOf(ruling, post), "年次の補充が手動の在任者を差し替えた");
        }

        /// <summary>党首でない操作者（与党の非党首＝閣僚・党三役を含む／他党の人物）：確認も実行も失敗し台帳は変わらず、メニューも確定できない。</summary>
        [Test]
        public void NonLeaders_CannotAppointOrDismiss_ReasonShownAndNothingChanges()
        {
            BuildLiveWorld();
            var actors = new List<int>();
            for (int i = 0; i < civilians.Count; i++)
            {
                int id = civilians[i].id;
                if (id == ruling.leaderId) continue;
                if (PartyOrganizationRules.IsMember(ruling, id)) { actors.Add(id); break; }
            }
            for (int k = 0; k < PartyExecutiveRules.ExecutivePosts.Length; k++)
            {
                int h = PartyOrganizationRules.HolderOf(ruling, PartyExecutiveRules.ExecutivePosts[k]);
                if (h >= 0 && !actors.Contains(h)) { actors.Add(h); break; }
            }
            int other = opposition.leaderId >= 0 ? opposition.leaderId : (opposition.memberIds.Count > 0 ? opposition.memberIds[0] : -1);
            if (other >= 0 && !actors.Contains(other)) actors.Add(other);
            Assert.GreaterOrEqual(actors.Count, 2, "前提：与党の非党首と他党の人物がそろわない");

            int target = -1;
            for (int i = 0; i < civilians.Count && target < 0; i++)
                if (PartyOrganizationRules.IsMember(ruling, civilians[i].id) && civilians[i].id != ruling.leaderId) target = civilians[i].id;

            for (int n = 0; n < actors.Count; n++)
            {
                int actor = actors[n];
                view.BindPlayerCharacterForQa(P(actor));
                var holders = new int[PartyExecutiveRules.ExecutivePosts.Length];
                for (int k = 0; k < holders.Length; k++) holders[k] = PartyOrganizationRules.HolderOf(ruling, PartyExecutiveRules.ExecutivePosts[k]);
                int historyBefore = ruling.postHistory.Count;

                for (int k = 0; k < PartyExecutiveRules.ExecutivePosts.Length; k++)
                {
                    PartyPost post = PartyExecutiveRules.ExecutivePosts[k];
                    AppointmentResult check = view.CheckPlayerPartyAppoint(ruling.id, post, target);
                    Assert.IsFalse(check.ok, "人物#" + actor + " が " + post + " を任命できると表示された");
                    AppointmentResult r = view.PlayerPartyAppoint(ruling.id, post, target, "権限外の任命");
                    Assert.IsFalse(r.ok, "人物#" + actor + " が " + post + " を任命できた");
                    Assert.AreEqual(check.reason, r.reason, "確認表示と実行の判定が別");
                    StringAssert.Contains("権限外", r.reason);
                    Assert.IsFalse(view.CheckPlayerPartyDismiss(ruling.id, post).ok);
                    Assert.IsFalse(view.PlayerPartyDismiss(ruling.id, post, "権限外の解任").ok, "人物#" + actor + " が " + post + " を解任できた");
                }
                for (int k = 0; k < holders.Length; k++)
                    Assert.AreEqual(holders[k], PartyOrganizationRules.HolderOf(ruling, PartyExecutiveRules.ExecutivePosts[k]), "権限外の操作で在任が変わった");
                Assert.AreEqual(historyBefore, ruling.postHistory.Count, "権限外の操作で履歴が増えた");

                PartyExecutivePanel.Show();
                PartyExecutivePanel panel = PartyExecutivePanel.InstanceForTest;
                panel.SelectForTest(ruling.id, PartyPost.幹事長, target, "権限外の任命", PartyExecutivePanel.PendingOp.任命);
                Assert.IsFalse(panel.ConfirmInteractableForTest, "人物#" + actor + " が無権限で確定できる");
                StringAssert.Contains("権限外", panel.ConfirmTextForTest);
                panel.ExecuteForTest();
                StringAssert.Contains("失敗", panel.MessageTextForTest);
                Assert.AreEqual(historyBefore, ruling.postHistory.Count);
            }
        }
    }
}
