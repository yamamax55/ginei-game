using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 稟議システム完成（2026-09-10 作業票）の通し担保。
    /// ①決裁経路の統一と一度だけの執行 ②状態と保存 ③実ゲームへの接続 ④状況起案 ⑤判断材料/結果
    /// ＋GitHub #67（権限）を、Core の純ロジックとして固定する。
    /// </summary>
    public class RingiCompletionTests
    {
        private static PendingDecision Card(string effectKey = "tax.cut", int id = 1)
        {
            var d = new PendingDecision(id, "テスト決裁", DecisionSeverity.通常,
                                        DecisionSource.建白結果, effectKey, defaultChoiceIndex: 1);
            d.choices.Add("裁可する");
            d.choices.Add("見送る（現状維持）");
            return d;
        }

        // ===== ① 決裁経路の統一・一度だけの執行 =====

        [Test]
        public void Resolve_SettledCard_IsRejected()
        {
            PendingDecision d = Card();
            Assert.IsTrue(DecisionResolutionRules.Settle(d, 0, auto: false, out _));
            Assert.AreEqual(DecisionStatus.決裁済, d.status);

            Assert.AreEqual(DecisionResolveRejection.すでに決裁済み,
                DecisionResolutionRules.CanResolve(d, 0), "解決済みを再解決できてはいけない");
            Assert.IsFalse(DecisionResolutionRules.Settle(d, 1, auto: false, out _));
            Assert.AreEqual(0, d.chosenIndex, "拒否したのに選択が書き換わっている");
        }

        [Test]
        public void Resolve_OutOfRangeChoice_IsRejected()
        {
            PendingDecision d = Card();
            Assert.AreEqual(DecisionResolveRejection.選択肢が不正, DecisionResolutionRules.CanResolve(d, 5));
            Assert.AreEqual(DecisionResolveRejection.選択肢が不正, DecisionResolutionRules.CanResolve(d, -1));
            Assert.IsFalse(DecisionResolutionRules.Settle(d, 5, auto: false, out _));
            Assert.AreEqual(DecisionStatus.新着, d.status);
        }

        [Test]
        public void Resolve_NullCard_IsSafe()
        {
            Assert.AreEqual(DecisionResolveRejection.案件が無い, DecisionResolutionRules.CanResolve(null, 0));
            Assert.IsFalse(DecisionResolutionRules.Settle(null, 0, auto: false, out _));
            Assert.IsFalse(DecisionResolutionRules.ClaimForApply(null));
        }

        /// <summary>★どの経路から来ても効果の適用は1回だけ（二重クリック・期限切れ×手動の競合）。</summary>
        [Test]
        public void ClaimForApply_SucceedsExactlyOnce()
        {
            PendingDecision d = Card();
            Assert.IsTrue(DecisionResolutionRules.ClaimForApply(d), "1回目は取れる");
            Assert.IsFalse(DecisionResolutionRules.ClaimForApply(d), "2回目は取れない");
            Assert.IsFalse(DecisionResolutionRules.ClaimForApply(d));
            Assert.IsTrue(d.applied);
        }

        /// <summary>期限切れの自動解決が先に確定した案件は、手で押しても確定し直せない。</summary>
        [Test]
        public void AutoResolvedCard_CannotBeResolvedManually()
        {
            PendingDecision d = Card();
            Assert.IsTrue(DecisionResolutionRules.Settle(d, d.defaultChoiceIndex, auto: true, out _));
            Assert.AreEqual(DecisionStatus.自動解決, d.status);

            Assert.AreEqual(DecisionResolveRejection.すでに決裁済み, DecisionResolutionRules.CanResolve(d, 0));
        }

        /// <summary>勝敗メーターの二重加算も止まる（効果の適用とは別勘定）。</summary>
        [Test]
        public void MeterApplied_IsSeparateAndAlsoOnce()
        {
            PendingDecision d = Card();
            Assert.IsTrue(DecisionMeterEffects.MarkMeterApplied(d));
            Assert.IsFalse(DecisionMeterEffects.MarkMeterApplied(d));
            Assert.IsTrue(d.meterApplied);
            Assert.IsFalse(d.applied, "メーターと効果は別勘定");
            Assert.IsTrue(DecisionResolutionRules.ClaimForApply(d), "メーター済みでも効果は別に取れる");
        }

        // ===== ② 状態と保存 =====

        [Test]
        public void Decisions_RoundTrip_KeepsAppliedAndPetitionLink()
        {
            var queue = new DecisionQueue();
            PendingDecision d = Card("mil.mobilize", 80001);
            d.petitionId = 7;
            d.friction = 0.42f;
            DecisionResolutionRules.Settle(d, 0, auto: false, out _);
            DecisionResolutionRules.ClaimForApply(d);
            DecisionResolutionRules.RecordResult(d,
                new PetitionActionResult(PetitionActionOutcome.実行, "造船所へ 3 隻を発注", 3f, 90f));
            queue.Enqueue(d);

            var save = new CampaignSaveData();
            CampaignSerializer.WriteDecisions(save, queue);
            DecisionQueue restored = CampaignSerializer.ReadDecisions(save);

            Assert.AreEqual(1, restored.items.Count);
            PendingDecision r = restored.items[0];
            Assert.AreEqual(80001, r.id);
            Assert.AreEqual(7, r.petitionId, "稟議との対応が失われている");
            Assert.AreEqual(0.42f, r.friction, 1e-3f);
            Assert.IsTrue(r.applied, "★適用済みが復元されないとロード後に二重執行する");
            Assert.AreEqual(PetitionActionOutcome.実行, r.outcome);
            StringAssert.Contains("発注", r.resultDetail);
            Assert.AreEqual(2, r.choices.Count);
            Assert.AreEqual(DecisionStatus.決裁済, r.status);

            // 復元した案件はもう確定も適用もできない。
            Assert.AreEqual(DecisionResolveRejection.すでに決裁済み, DecisionResolutionRules.CanResolve(r, 0));
            Assert.IsFalse(DecisionResolutionRules.ClaimForApply(r));
        }

        [Test]
        public void Petitions_RoundTrip_KeepsStatusAndEffectKey()
        {
            var ledger = new PetitionLedger();
            var pet = new Petition(0, "減税の建白", Faction.同盟, BoxKind.政治家, PetitionOrigin.建白, "tax.cut");
            ledger.Add(pet);
            pet.status = PetitionStatus.執行済;
            pet.distorted = true;

            var save = new CampaignSaveData();
            CampaignSerializer.WritePetitions(save.petitions, ledger);

            var restored = new PetitionLedger();
            CampaignSerializer.ReadPetitions(save.petitions, restored);

            Assert.AreEqual(1, restored.Count);
            Petition r = restored.Get(pet.id);
            Assert.IsNotNull(r, "案件IDで引けなければ決裁カードと対応できない");
            Assert.AreEqual("tax.cut", r.effectKey);
            Assert.AreEqual(PetitionStatus.執行済, r.status);
            Assert.AreEqual(Faction.同盟, r.faction);
            Assert.IsTrue(r.distorted);
        }

        [Test]
        public void LegacySave_WithoutRingiFields_LoadsEmpty()
        {
            var save = new CampaignSaveData();   // 旧セーブ＝リストが空
            DecisionQueue q = CampaignSerializer.ReadDecisions(save);
            Assert.AreEqual(0, q.items.Count);

            var ledger = new PetitionLedger();
            ledger.Add(new Petition(0, "残っていた案件", Faction.同盟, BoxKind.国王, PetitionOrigin.建白, "tax.cut"));
            CampaignSerializer.ReadPetitions(save.petitions, ledger);
            Assert.AreEqual(0, ledger.Count, "旧セーブの読み込みで前の案件が残ってはいけない");
        }

        // ===== ③ 実ゲームへの接続 =====

        private static PetitionActionContext Board(Faction faction = Faction.同盟, float treasury = 500f)
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem { id = 0, systemName = "本国", owner = faction });
            map.AddSystem(new StarSystem { id = 1, systemName = "敵地", owner = Faction.帝国 });
            map.AddCorridor(new Corridor(0, 1, 5f, CorridorType.通商));

            var campaign = new CampaignState(map);
            // 国家状態は自分で用意する（CampaignState は map を持つだけで states は空）。
            campaign.states.Add(new FactionState(Faction.同盟));
            campaign.states.Add(new FactionState(Faction.帝国));
            FactionState fs = CampaignRules.GetState(campaign, faction);
            Assert.IsNotNull(fs, "テストの前提：勢力の国家状態が要る");
            fs.treasury = treasury;

            return new PetitionActionContext
            {
                campaign = campaign,
                map = map,
                fleets = new StrategicFleetRegistry(map),
                faction = faction,
                shipyards = new List<Shipyard>(),
                useWarLedger = false,
            };
        }

        [Test]
        public void Mobilize_WithoutShipyards_FailsInsteadOfPretending()
        {
            PetitionActionContext ctx = Board();
            PetitionActionResult r = PetitionActionRules.Execute("mil.mobilize", ctx, 1f);
            Assert.AreEqual(PetitionActionOutcome.対象なし, r.outcome);
            Assert.IsFalse(r.ok, "造船所が無いのに成功にしてはいけない");
            Assert.AreEqual(500f, ctx.State().treasury, 1e-3f, "失敗したのに国庫が減っている");
        }

        [Test]
        public void Mobilize_PlacesRealBuildOrders_AndSpendsTreasury()
        {
            PetitionActionContext ctx = Board(treasury: 100000f);
            var yard = new Shipyard { faction = Faction.同盟 };
            ctx.shipyards.Add(yard);

            PetitionActionResult r = PetitionActionRules.Execute("mil.mobilize", ctx, 1f);

            Assert.IsTrue(r.ok, r.detail);
            // ActiveCount は同時建造数（parallelCapacity 上限）なので、積まれた総数はキューで数える。
            Assert.AreEqual(PetitionActionRules.MobilizeOrdersFull, yard.queue.Count,
                            "★造船所に実際の建艦オーダーが積まれること（架空の増艦をしない）");
            Assert.Greater(r.spent, 0f);
            Assert.AreEqual(100000f - r.spent, ctx.State().treasury, 1e-2f);
        }

        [Test]
        public void Mobilize_PoorTreasury_IsResourceFailure()
        {
            PetitionActionContext ctx = Board(treasury: 0f);
            ctx.shipyards.Add(new Shipyard { faction = Faction.同盟 });
            PetitionActionResult r = PetitionActionRules.Execute("mil.mobilize", ctx, 1f);
            Assert.AreEqual(PetitionActionOutcome.資源不足, r.outcome);
            Assert.AreEqual(0f, ctx.State().treasury, 1e-3f);
        }

        [Test]
        public void Offensive_WithoutFleets_FailsWithReason()
        {
            PetitionActionContext ctx = Board();
            PetitionActionResult r = PetitionActionRules.Execute("mil.offensive", ctx, 1f);
            Assert.AreEqual(PetitionActionOutcome.手が空いていない, r.outcome);
            Assert.IsFalse(string.IsNullOrEmpty(r.detail));
        }

        [Test]
        public void Offensive_OrdersRealFleetsToMove()
        {
            PetitionActionContext ctx = Board();
            var f = new StrategicFleet(1, 0, Faction.同盟) { strength = 200 };
            ctx.fleets.Add(f);

            PetitionActionResult r = PetitionActionRules.Execute("mil.offensive", ctx, 1f);

            Assert.IsTrue(r.ok, r.detail);
            Assert.IsTrue(f.IsMoving || f.IsOnCorridor, "★実在の艦隊に実際の進軍命令が出ること");
            StringAssert.Contains("第1艦隊", r.detail);
        }

        [Test]
        public void Defend_RestoresRealOrbitalDefense()
        {
            PetitionActionContext ctx = Board(treasury: 1000f);
            StarSystem home = ctx.map.GetSystem(0);
            home.planet = new Planet(0, Faction.同盟, 100f, 100f) { orbitalDefense = 20f };

            PetitionActionResult r = PetitionActionRules.Execute("mil.defend", ctx, 1f);

            Assert.IsTrue(r.ok, r.detail);
            Assert.Greater(home.planet.orbitalDefense, 20f, "★攻城戦で実際に消費される値が上がること");
            Assert.LessOrEqual(home.planet.orbitalDefense, home.planet.maxOrbitalDefense);
            Assert.Less(ctx.State().treasury, 1000f, "費用が引かれていない");
        }

        [Test]
        public void Defend_NothingToFix_FailsWithoutSpending()
        {
            PetitionActionContext ctx = Board(treasury: 1000f);
            PetitionActionResult r = PetitionActionRules.Execute("mil.defend", ctx, 1f);
            Assert.AreEqual(PetitionActionOutcome.対象なし, r.outcome);
            Assert.AreEqual(1000f, ctx.State().treasury, 1e-3f);
        }

        [Test]
        public void Ceasefire_NotAtWar_FailsWithReason()
        {
            PetitionActionContext ctx = Board();
            ctx.diplomacy = new DiplomacyState();
            PetitionActionResult r = PetitionActionRules.Execute("diplo.ceasefire", ctx, 1f);
            Assert.AreEqual(PetitionActionOutcome.対象なし, r.outcome);
        }

        /// <summary>★敵が受け入れなければ講和は成立しない（無効果の成功にしない）。</summary>
        [Test]
        public void Ceasefire_EnemyRefuses_DoesNotEndTheWar()
        {
            PetitionActionContext ctx = Board();
            ctx.diplomacy = new DiplomacyState();
            DiplomacyRules.DeclareWar(ctx.diplomacy, "同盟", "帝国", DiplomacyRules.DiplomacyParams.Default);

            PetitionActionResult r = PetitionActionRules.Execute("diplo.ceasefire", ctx, 1f);

            Assert.AreEqual(PetitionActionOutcome.相手が拒否, r.outcome);
            Assert.AreEqual(DiplomacyState.DiplomaticStatus.交戦, ctx.diplomacy.Status("同盟", "帝国"),
                            "拒否されたのに戦争が終わっている");
        }

        [Test]
        public void UnknownKey_HasNoBoardAction()
        {
            Assert.IsFalse(PetitionActionRules.IsActionKey("tax.cut"));
            Assert.IsTrue(PetitionActionRules.IsActionKey("mil.mobilize"));
            PetitionActionResult r = PetitionActionRules.Execute("tax.cut", Board(), 1f);
            Assert.AreEqual(PetitionActionOutcome.対象外, r.outcome);
        }

        // ===== ④ 状況起案 =====

        private static PetitionAgendaParams AgendaP => PetitionAgendaParams.Default;

        [Test]
        public void Agenda_NoSituation_RaisesNothing()
        {
            // 潤沢・低税・平和・余剰なし＝上げるべき案件は無い。
            var calm = new PetitionSituation(1000f, 0.1f, 0.9f, 0, false, 0f, 0, 0);
            Assert.AreEqual(0, PetitionAgendaRules.Candidates(calm, AgendaP).Count,
                            "★状況が無いのに定型案を出してはいけない");
            Assert.IsFalse(PetitionAgendaRules.Next(calm, AgendaP, 0, _ => false, _ => 9999f).IsValid);
        }

        [Test]
        public void Agenda_LowTreasury_RaisesTaxHike()
        {
            var poor = new PetitionSituation(10f, 0.1f, 0.9f, 0, false, 0f, 0, 0);
            PetitionAgendaItem item = PetitionAgendaRules.Next(poor, AgendaP, 0, _ => false, _ => 9999f);
            Assert.IsTrue(item.IsValid);
            Assert.AreEqual(PetitionTrigger.財政難, item.trigger);
            Assert.AreEqual("tax.hike", item.effectKey);
            StringAssert.Contains("国庫", item.reason);
        }

        [Test]
        public void Agenda_EnemyApproaching_RaisesMobilize()
        {
            var threat = new PetitionSituation(1000f, 0.1f, 0.9f, 2, false, 0f, 0, 0);
            PetitionAgendaItem item = PetitionAgendaRules.Next(threat, AgendaP, 0, _ => false, _ => 9999f);
            Assert.AreEqual(PetitionTrigger.敵の接近, item.trigger);
            Assert.AreEqual("mil.mobilize", item.effectKey);
        }

        [Test]
        public void Agenda_SkipsPendingAndCooldown()
        {
            var poor = new PetitionSituation(10f, 0.1f, 0.9f, 0, false, 0f, 0, 0);

            // 同じ状況の未解決案件があるなら出さない。
            Assert.IsFalse(PetitionAgendaRules.Next(poor, AgendaP, 0,
                t => t == PetitionTrigger.財政難, _ => 9999f).IsValid);

            // クールダウン中も出さない。
            Assert.IsFalse(PetitionAgendaRules.Next(poor, AgendaP, 0,
                _ => false, _ => 1f).IsValid);
        }

        [Test]
        public void Agenda_RespectsConcurrentLimit()
        {
            var poor = new PetitionSituation(10f, 0.1f, 0.9f, 0, false, 0f, 0, 0);
            Assert.IsFalse(PetitionAgendaRules.Next(poor, AgendaP, AgendaP.maxConcurrent,
                _ => false, _ => 9999f).IsValid, "件数上限を超えて積んではいけない");
        }

        [Test]
        public void Agenda_MostUrgentFirst_AndDeterministic()
        {
            // 財政難（極端）と敵接近（軽い）が同時＝切迫度の高いほうが先。
            var both = new PetitionSituation(0f, 0.1f, 0.9f, 1, false, 0f, 0, 0);
            List<PetitionAgendaItem> a = PetitionAgendaRules.Candidates(both, AgendaP);
            List<PetitionAgendaItem> b = PetitionAgendaRules.Candidates(both, AgendaP);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i].trigger, b[i].trigger, "決定論でない");
            Assert.AreEqual(PetitionTrigger.財政難, a[0].trigger);
        }

        /// <summary>成立条件が消えた案件は再評価で落ちる（国庫が戻れば増税の建白は用済み）。</summary>
        [Test]
        public void Agenda_StaleItem_IsNoLongerRelevant()
        {
            var poor = new PetitionSituation(10f, 0.1f, 0.9f, 0, false, 0f, 0, 0);
            var rich = new PetitionSituation(9999f, 0.1f, 0.9f, 0, false, 0f, 0, 0);
            Assert.IsTrue(PetitionAgendaRules.IsStillRelevant(PetitionTrigger.財政難, poor, AgendaP));
            Assert.IsFalse(PetitionAgendaRules.IsStillRelevant(PetitionTrigger.財政難, rich, AgendaP));
        }

        [Test]
        public void Agenda_TriggerOf_MapsBackFromEffectKey()
        {
            Assert.AreEqual(PetitionTrigger.財政難, PetitionAgendaRules.TriggerOf("tax.hike"));
            Assert.AreEqual(PetitionTrigger.戦争の長期化, PetitionAgendaRules.TriggerOf("diplo.ceasefire"));
            Assert.AreEqual(PetitionTrigger.なし, PetitionAgendaRules.TriggerOf("purge"));
        }

        // ===== ⑤ 判断材料と結果 =====

        [Test]
        public void Briefing_ShowsCostTargetEffectAndTiming()
        {
            string brief = PetitionBriefingRules.Brief("mil.mobilize", Board(treasury: 300f));
            StringAssert.Contains("費用", brief);
            StringAssert.Contains("対象", brief);
            StringAssert.Contains("見込み", brief);   // 不確実な効果は見込みと明示
            StringAssert.Contains("実行", brief);
        }

        [Test]
        public void Briefing_UnknownKey_StillSafe()
        {
            Assert.IsFalse(string.IsNullOrEmpty(PetitionBriefingRules.Brief("nope", null)));
        }

        /// <summary>★承認できたことと、実際に効いたことを区別して出す。</summary>
        [Test]
        public void ResultLine_DistinguishesApprovalFromExecution()
        {
            PendingDecision ok = Card();
            DecisionResolutionRules.ClaimForApply(ok);
            DecisionResolutionRules.RecordResult(ok,
                new PetitionActionResult(PetitionActionOutcome.実行, "制空を +40 回復"));
            StringAssert.StartsWith("執行：", DecisionResolutionRules.ResultLine(ok));

            PendingDecision ng = Card(id: 2);
            DecisionResolutionRules.ClaimForApply(ng);
            DecisionResolutionRules.RecordResult(ng,
                PetitionActionResult.Fail(PetitionActionOutcome.資源不足, "国庫が足りません"));
            StringAssert.StartsWith("執行できず：", DecisionResolutionRules.ResultLine(ng));

            Assert.AreEqual("", DecisionResolutionRules.ResultLine(Card(id: 3)), "未適用なら結果を出さない");
        }

        // ===== GitHub #67：権限 =====

        private sealed class FakeChar : ICharacter
        {
            public int Id { get; set; }
            public string CharacterName { get; set; } = "誰か";
            public Faction Faction { get; set; } = Faction.同盟;
            public int RankTier { get; set; }
            public bool IsMilitary { get; set; }
            public bool IsPolitician { get; set; }
            public int BirthYear { get; set; }
            public bool IsDeceased { get; set; }
            public bool IsAvailable => !IsDeceased;
        }

        private static Office Off(OfficeDomain domain, OfficeScope scope = OfficeScope.国家, string name = "役職")
            => new Office { officeName = name, domain = domain, scope = scope };

        [Test]
        public void Authority_DomainOf_MapsEffectKeys()
        {
            Assert.AreEqual(OfficeDomain.財政, DecisionAuthorityRules.DomainOf("tax.hike"));
            Assert.AreEqual(OfficeDomain.軍事, DecisionAuthorityRules.DomainOf("mil.offensive"));
            Assert.AreEqual(OfficeDomain.軍事, DecisionAuthorityRules.DomainOf("fleet.establish:100"));
            Assert.AreEqual(OfficeDomain.外交, DecisionAuthorityRules.DomainOf("diplo.ceasefire"));
            Assert.AreEqual(OfficeDomain.内政, DecisionAuthorityRules.DomainOf("welfare.up"));
        }

        [Test]
        public void Authority_OfficeInDomain_CanDecide()
        {
            var actor = new FakeChar { Id = 1, IsMilitary = true, RankTier = 8 };
            var offices = new List<Office> { Off(OfficeDomain.軍事, OfficeScope.国家, "宇宙艦隊司令長官") };

            DecisionAuthorityResult r = DecisionAuthorityRules.Evaluate(
                actor, "mil.offensive", OfficeScope.国家, offices, CivilianControlType.文民統制);

            Assert.IsTrue(r.CanDecide);
            StringAssert.Contains("宇宙艦隊司令長官", r.basis, "根拠に役職名が出ること");
        }

        /// <summary>★高い階級だけでは全権にならない（役職が無ければ裁可できない）。</summary>
        [Test]
        public void Authority_HighRankWithoutOffice_CannotDecide()
        {
            var actor = new FakeChar { Id = 1, IsMilitary = true, RankTier = 10 };
            DecisionAuthorityResult r = DecisionAuthorityRules.Evaluate(
                actor, "mil.offensive", OfficeScope.国家, new List<Office>(), CivilianControlType.文民統制);
            Assert.IsFalse(r.CanDecide, "元帥でも役職が無ければ裁可できない");
            Assert.AreEqual(DecisionAuthority.権限外, r.authority);
        }

        [Test]
        public void Authority_NoOfficeButAddresseeExists_BecomesPetition()
        {
            var actor = new FakeChar { Id = 1, IsMilitary = true, RankTier = 7 };
            var boss = new FakeChar { Id = 9, CharacterName = "上官", IsMilitary = true, RankTier = 9 };

            DecisionAuthorityResult r = DecisionAuthorityRules.Evaluate(
                actor, "mil.offensive", OfficeScope.国家, new List<Office>(),
                CivilianControlType.文民統制, _ => boss);

            Assert.AreEqual(DecisionAuthority.上申, r.authority);
            Assert.AreEqual(9, r.addresseeId);
            Assert.AreEqual("上官", r.addresseeName);
        }

        /// <summary>★文民統制は役職より先に効く（軍人は政治案件を裁可できない政体がある）。</summary>
        [Test]
        public void Authority_CivilianControl_BlocksMilitaryOnPoliticalMatters()
        {
            var soldier = new FakeChar { Id = 1, IsMilitary = true, RankTier = 10 };
            var offices = new List<Office> { Off(OfficeDomain.財政, OfficeScope.国家, "大蔵卿") };

            DecisionAuthorityResult r = DecisionAuthorityRules.Evaluate(
                soldier, "tax.hike", OfficeScope.国家, offices, CivilianControlType.文民統制);

            Assert.IsFalse(r.CanDecide, "役職があっても文民統制で覆せてはいけない");
            StringAssert.Contains("文民統制", r.basis);
        }

        [Test]
        public void Authority_Civilian_CanDecidePoliticalMatters()
        {
            var civil = new FakeChar { Id = 2, IsMilitary = false, IsPolitician = true };
            var offices = new List<Office> { Off(OfficeDomain.財政, OfficeScope.国家, "大蔵卿") };

            DecisionAuthorityResult r = DecisionAuthorityRules.Evaluate(
                civil, "tax.hike", OfficeScope.国家, offices, CivilianControlType.文民統制);

            Assert.IsTrue(r.CanDecide);
        }

        [Test]
        public void Authority_HeadOfState_CoversEveryDomain()
        {
            var king = new FakeChar { Id = 3, IsMilitary = false };
            var offices = new List<Office> { Off(OfficeDomain.元首, OfficeScope.国家, "皇帝") };

            Assert.IsTrue(DecisionAuthorityRules.Evaluate(king, "mil.offensive", OfficeScope.国家, offices,
                          CivilianControlType.君主統帥).CanDecide);
            Assert.IsTrue(DecisionAuthorityRules.Evaluate(king, "diplo.ceasefire", OfficeScope.国家, offices,
                          CivilianControlType.君主統帥).CanDecide);
        }

        [Test]
        public void Authority_NarrowScope_CannotDecideNationalMatter()
        {
            var governor = new FakeChar { Id = 4, IsMilitary = false };
            var offices = new List<Office> { Off(OfficeDomain.内政, OfficeScope.星系, "星系総督") };

            DecisionAuthorityResult r = DecisionAuthorityRules.Evaluate(
                governor, "welfare.up", OfficeScope.国家, offices, CivilianControlType.文民統制);
            Assert.IsFalse(r.CanDecide, "星系の役職で国家の案件を決めてはいけない");
        }

        [Test]
        public void Authority_NullActor_IsRefused()
        {
            DecisionAuthorityResult r = DecisionAuthorityRules.Evaluate(
                null, "tax.cut", OfficeScope.国家, new List<Office>(), CivilianControlType.文民統制);
            Assert.AreEqual(DecisionAuthority.権限外, r.authority);
        }

        // ===== #67：会戦の指揮系統 =====

        [Test]
        public void Command_OwnFleetAndOwnCorps_AreDirect()
        {
            // 自分が司令の艦隊
            Assert.IsTrue(DecisionAuthorityRules.CanCommandDirectly(
                actorPersonId: 5, actorCorpsId: -1, actorCommandsAll: false,
                targetCorpsId: 3, targetCommanderId: 5));

            // 自分の軍団の隷下
            Assert.IsTrue(DecisionAuthorityRules.CanCommandDirectly(
                actorPersonId: 5, actorCorpsId: 2, actorCommandsAll: false,
                targetCorpsId: 2, targetCommanderId: 9));
        }

        [Test]
        public void Command_OtherChain_IsNotDirect()
        {
            Assert.IsFalse(DecisionAuthorityRules.CanCommandDirectly(
                actorPersonId: 5, actorCorpsId: 2, actorCommandsAll: false,
                targetCorpsId: 7, targetCommanderId: 9), "★他系統に直接命令を通してはいけない");
        }

        [Test]
        public void Command_TopCommander_CommandsEverything()
        {
            Assert.IsTrue(DecisionAuthorityRules.CanCommandDirectly(
                actorPersonId: 5, actorCorpsId: -1, actorCommandsAll: true,
                targetCorpsId: 7, targetCommanderId: 9));
        }

        [Test]
        public void Command_MixedSelection_ExplainsInWords()
        {
            Assert.AreEqual("", DecisionAuthorityRules.MixedSelectionText(3, 0));
            StringAssert.Contains("支援要請", DecisionAuthorityRules.MixedSelectionText(2, 1));
            StringAssert.Contains("すべて", DecisionAuthorityRules.MixedSelectionText(0, 2));
            StringAssert.Contains("指揮系統外", DecisionAuthorityRules.OutOfChainText("第7艦隊"));
        }
    }
}
