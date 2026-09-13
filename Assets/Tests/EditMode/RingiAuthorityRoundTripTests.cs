using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 稟議の残件（2026-09-10 第2次）：
    /// ①会戦の指揮系統 ②上申の往復 ③提案者/決裁者/対象の表示 ④提案対象の固定 を固定する。
    /// </summary>
    public class RingiAuthorityRoundTripTests
    {
        private static PendingDecision Card(string effectKey = "mil.offensive", int id = 1)
        {
            var d = new PendingDecision(id, "テスト決裁", DecisionSeverity.通常,
                                        DecisionSource.建白結果, effectKey, defaultChoiceIndex: 1);
            d.choices.Add("裁可する");
            d.choices.Add("見送る（現状維持）");
            return d;
        }

        private static PetitionActionContext Board(float treasury = 500f)
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem { id = 0, systemName = "本国", owner = Faction.同盟 });
            map.AddSystem(new StarSystem { id = 1, systemName = "敵地", owner = Faction.帝国 });
            map.AddSystem(new StarSystem { id = 2, systemName = "別の敵地", owner = Faction.帝国 });
            map.AddCorridor(new Corridor(0, 1, 5f, CorridorType.通商));
            map.AddCorridor(new Corridor(1, 2, 5f, CorridorType.通商));

            var campaign = new CampaignState(map);
            campaign.states.Add(new FactionState(Faction.同盟));
            campaign.states.Add(new FactionState(Faction.帝国));
            CampaignRules.GetState(campaign, Faction.同盟).treasury = treasury;

            return new PetitionActionContext
            {
                campaign = campaign, map = map, fleets = new StrategicFleetRegistry(map),
                faction = Faction.同盟, shipyards = new List<Shipyard>(), useWarLedger = false,
            };
        }

        // ===== ④ 提案対象の固定（承認後に別対象へ振り替えない） =====

        [Test]
        public void PlanTarget_Offensive_NamesAConcreteSystem()
        {
            PetitionActionContext ctx = Board();
            ctx.fleets.Add(new StrategicFleet(1, 0, Faction.同盟) { strength = 200 });

            PetitionTarget t = PetitionActionRules.PlanTarget("mil.offensive", ctx);
            Assert.IsTrue(t.HasTarget, "提案の時点で目標が決まっていない");
            Assert.AreEqual(PetitionTargetKind.星系, t.kind);
            Assert.AreEqual(1, t.id, "経路のある最も近い敵星系");
            Assert.AreEqual("敵地", t.name);
        }

        /// <summary>★提案した目標へ進撃する（別の敵星系へ勝手に振り替えない）。</summary>
        [Test]
        public void Offensive_UsesTheProposedTarget()
        {
            PetitionActionContext ctx = Board();
            var f = new StrategicFleet(1, 0, Faction.同盟) { strength = 200 };
            ctx.fleets.Add(f);

            var far = new PetitionTarget(PetitionTargetKind.星系, 2, "別の敵地");
            PetitionActionResult r = PetitionActionRules.Execute("mil.offensive", ctx, 1f, far);

            Assert.IsTrue(r.ok, r.detail);
            StringAssert.Contains("別の敵地", r.detail, "提案した目標と違う星系へ進撃している");
            Assert.IsTrue(f.IsMoving, "命令が出ていない");
            // ★実際の到着地が目標そのものとは限らない：既存の「飛び石禁止」
            //（<see cref="FleetOrderRules.FirstUnownedIndex"/>）で、経路上の最初の非自勢力星系
            // （ここでは 1「敵地」）でいったん止まる。これは仕様であって振り替えではない。
            Assert.AreEqual(1, f.FinalDestinationId, "飛び石禁止で最初の非自勢力星系に止まる");
        }

        [Test]
        public void Offensive_ProposedTargetNoLongerHostile_FailsInsteadOfRetargeting()
        {
            PetitionActionContext ctx = Board();
            ctx.fleets.Add(new StrategicFleet(1, 0, Faction.同盟) { strength = 200 });
            ctx.map.GetSystem(1).owner = Faction.同盟;   // 提案後に自勢力になった

            var t = new PetitionTarget(PetitionTargetKind.星系, 1, "敵地");
            PetitionActionResult r = PetitionActionRules.Execute("mil.offensive", ctx, 1f, t);

            Assert.AreEqual(PetitionActionOutcome.対象なし, r.outcome);
            StringAssert.Contains("敵地", r.detail);
        }

        [Test]
        public void Defend_ProposedPlanetIsUsed_NotAnother()
        {
            PetitionActionContext ctx = Board(treasury: 1000f);
            StarSystem home = ctx.map.GetSystem(0);
            home.planet = new Planet(0, Faction.同盟, 100f, 100f) { orbitalDefense = 90f };  // 軽傷
            StarSystem other = ctx.map.GetSystem(2);
            other.owner = Faction.同盟;
            other.planet = new Planet(2, Faction.同盟, 100f, 100f) { orbitalDefense = 10f };  // 重傷

            // 提案では「本国」を名指ししたので、より削られている別の惑星へ振り替えない。
            var t = new PetitionTarget(PetitionTargetKind.惑星, 0, "本国");
            PetitionActionResult r = PetitionActionRules.Execute("mil.defend", ctx, 1f, t);

            Assert.IsTrue(r.ok, r.detail);
            Assert.Greater(home.planet.orbitalDefense, 90f, "提案した惑星が回復していない");
            Assert.AreEqual(10f, other.planet.orbitalDefense, 1e-3f, "提案していない惑星を触っている");
        }

        [Test]
        public void Ceasefire_ProposedOpponentOnly()
        {
            PetitionActionContext ctx = Board();
            ctx.diplomacy = new DiplomacyState();
            DiplomacyRules.DeclareWar(ctx.diplomacy, "同盟", "帝国", DiplomacyRules.DiplomacyParams.Default);

            // 交戦していない相手を名指しした＝失敗（交戦中の別勢力へ振り替えない）。
            var wrong = new PetitionTarget(PetitionTargetKind.勢力, 0, "同盟");
            PetitionActionResult r = PetitionActionRules.Execute("diplo.ceasefire", ctx, 1f, wrong);
            Assert.AreNotEqual(PetitionActionOutcome.実行, r.outcome);
            Assert.AreEqual(DiplomacyState.DiplomaticStatus.交戦, ctx.diplomacy.Status("同盟", "帝国"));
        }

        [Test]
        public void Target_EncodeDecode_RoundTrips()
        {
            var t = new PetitionTarget(PetitionTargetKind.星系, 42, "アムリッツァ");
            PetitionTarget back = PetitionTarget.Decode(t.Encode());
            Assert.AreEqual(t.kind, back.kind);
            Assert.AreEqual(t.id, back.id);
            Assert.AreEqual(t.name, back.name);

            Assert.IsFalse(PetitionTarget.Decode("").HasTarget);
            Assert.IsFalse(PetitionTarget.Decode("こわれた").HasTarget);
            Assert.AreEqual("勢力全体", PetitionTarget.None.Label);
        }

        [Test]
        public void Card_TargetSurvivesSaveRoundTrip()
        {
            var queue = new DecisionQueue();
            PendingDecision d = Card();
            d.SetTarget(new PetitionTarget(PetitionTargetKind.星系, 7, "イゼルローン"));
            d.proposerId = 3; d.proposerName = "ヤン";
            d.deciderId = 9; d.deciderName = "ビュコック";
            d.authorityBasis = "宇宙艦隊司令長官（軍事・国家）";
            d.escalated = true;
            queue.Enqueue(d);

            var save = new CampaignSaveData();
            CampaignSerializer.WriteDecisions(save, queue);
            PendingDecision r = CampaignSerializer.ReadDecisions(save).items[0];

            Assert.AreEqual(7, r.Target.id);
            Assert.AreEqual("イゼルローン", r.Target.name);
            Assert.AreEqual("ヤン", r.proposerName);
            Assert.AreEqual("ビュコック", r.deciderName);
            StringAssert.Contains("宇宙艦隊司令長官", r.authorityBasis);
            Assert.IsTrue(r.escalated, "上申中かどうかが保存されていない");
        }

        // ===== ③ 提案者/決裁者/対象の表示 =====

        [Test]
        public void Attribution_ShowsWhoAndTargetAndAuthority()
        {
            PendingDecision d = Card();
            d.proposerName = "ヤン";
            d.deciderName = "ビュコック";
            d.authorityBasis = "宇宙艦隊司令長官（軍事・国家）";
            d.SetTarget(new PetitionTarget(PetitionTargetKind.星系, 1, "敵地"));

            string text = DecisionAttributionRules.DetailText(d);
            StringAssert.Contains("提案：ヤン", text);
            StringAssert.Contains("決裁：ビュコック", text);
            StringAssert.Contains("敵地", text);
            StringAssert.Contains("宇宙艦隊司令長官", text);
            StringAssert.Contains("状態：", text);
        }

        /// <summary>★名前が分からないときに架空の名前を作らない。</summary>
        [Test]
        public void Attribution_UnknownNames_ShowDash()
        {
            PendingDecision d = Card();
            string text = DecisionAttributionRules.DetailText(d);
            StringAssert.Contains($"提案：{DecisionAttributionRules.Unknown}", text);
            StringAssert.Contains($"決裁：{DecisionAttributionRules.Unknown}", text);
            StringAssert.Contains("勢力全体", text, "対象を名指ししていないことが分かること");
        }

        [Test]
        public void Attribution_EscalatedCard_SaysSo()
        {
            PendingDecision d = Card();
            d.escalated = true;
            d.deciderName = "ビュコック";
            StringAssert.Contains("上申中", DecisionAttributionRules.StatusLine(d));
            StringAssert.Contains("ビュコック", DecisionAttributionRules.HeadlineNote(d));
        }

        // ===== ② 上申の往復 =====

        private static PetitionEscalationParams EscP => PetitionEscalationParams.Default;

        [Test]
        public void Escalation_NotYetReviewed_StaysPending()
        {
            Assert.AreEqual(EscalationOutcome.審査中,
                PetitionEscalationRules.Review(true, 0f, 1f, EscP));
            Assert.IsFalse(PetitionEscalationRules.IsReviewDone(EscP.reviewSeconds - 1f, EscP));
        }

        [Test]
        public void Escalation_CapableDecider_Approves()
        {
            float favor = PetitionEscalationRules.Favor(90, 90, 0.8f);
            Assert.AreEqual(EscalationOutcome.承認,
                PetitionEscalationRules.Review(true, EscP.reviewSeconds, favor, EscP));
        }

        [Test]
        public void Escalation_PoorCaseIsRejected()
        {
            float favor = PetitionEscalationRules.Favor(10, 10, 0.0f);
            Assert.AreEqual(EscalationOutcome.却下,
                PetitionEscalationRules.Review(true, EscP.reviewSeconds, favor, EscP));
        }

        /// <summary>★決裁権者が居なくなったら差し戻し（勝手に代理で承認しない）。</summary>
        [Test]
        public void Escalation_DeciderGone_IsReturned()
        {
            Assert.AreEqual(EscalationOutcome.決裁者不在,
                PetitionEscalationRules.Review(false, EscP.reviewSeconds, 1f, EscP));
        }

        [Test]
        public void Escalation_Favor_IsDeterministicAndBounded()
        {
            Assert.AreEqual(PetitionEscalationRules.Favor(50, 50, 0.5f),
                            PetitionEscalationRules.Favor(50, 50, 0.5f), 1e-6f);
            Assert.GreaterOrEqual(PetitionEscalationRules.Favor(0, 0, 0f), 0f);
            Assert.LessOrEqual(PetitionEscalationRules.Favor(100, 100, 1f), 1f);
        }

        [Test]
        public void Escalation_Texts_NameTheDecider()
        {
            StringAssert.Contains("ビュコック",
                PetitionEscalationRules.ResultText(EscalationOutcome.承認, "ビュコック", "減税の建白"));
            StringAssert.Contains("差し戻し",
                PetitionEscalationRules.ResultText(EscalationOutcome.決裁者不在, "", "減税の建白"));
            StringAssert.Contains("上申中", PetitionEscalationRules.PendingText("ビュコック", 0f, EscP));
        }

        // ===== ① 会戦の指揮系統 =====

        [Test]
        public void Battle_OwnCorps_IsDirect()
        {
            var actor = new BattleChain("A軍団", commandsAll: false);
            Assert.AreEqual(BattleCommandRight.直接命令,
                BattleCommandAuthorityRules.RightFor(actor, true, false, "A軍団"));
        }

        [Test]
        public void Battle_OtherCorps_BecomesRequest()
        {
            var actor = new BattleChain("A軍団", commandsAll: false);
            Assert.AreEqual(BattleCommandRight.要請,
                BattleCommandAuthorityRules.RightFor(actor, true, false, "B軍団"));
        }

        [Test]
        public void Battle_OwnShip_IsAlwaysDirect()
        {
            var actor = new BattleChain("", commandsAll: false);
            Assert.AreEqual(BattleCommandRight.直接命令,
                BattleCommandAuthorityRules.RightFor(actor, true, true, "B軍団"));
        }

        [Test]
        public void Battle_Enemy_IsBlocked()
        {
            var actor = new BattleChain("A軍団", commandsAll: false);
            Assert.AreEqual(BattleCommandRight.不可,
                BattleCommandAuthorityRules.RightFor(actor, false, false, "A軍団"));
        }

        /// <summary>主人公が居ない会戦は従来どおり全部を直接操作できる（後方互換）。</summary>
        [Test]
        public void Battle_EverythingChain_KeepsLegacyFeel()
        {
            Assert.AreEqual(BattleCommandRight.直接命令,
                BattleCommandAuthorityRules.RightFor(BattleChain.Everything, true, false, "どこかの軍団"));
        }

        [Test]
        public void Battle_MixedSelection_IsExplainedInWords()
        {
            Assert.AreEqual("", BattleCommandAuthorityRules.SelectionNote(3, 0, 0));
            string note = BattleCommandAuthorityRules.SelectionNote(2, 1, 1);
            StringAssert.Contains("直接命令 2 隊", note);
            StringAssert.Contains("支援要請", note);
            StringAssert.Contains("命令できない", note);
        }

        [Test]
        public void Battle_ReasonAndRequestTexts()
        {
            Assert.AreEqual("", BattleCommandAuthorityRules.ReasonText(BattleCommandRight.直接命令, "第1艦隊"));
            StringAssert.Contains("指揮系統外",
                BattleCommandAuthorityRules.ReasonText(BattleCommandRight.要請, "第7艦隊"));
            StringAssert.Contains("要請しました",
                BattleCommandAuthorityRules.RequestSentText("第7艦隊", "移動"));
        }
    }
}
