using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 採用仕様 #67 の第3次分：会戦の操作モードと指揮系統、支援要請の往復、未実装効果の実行不可を固定する。
    /// 「不在や不明を全権限に読み替えない」ことを特に厚く担保する。
    /// </summary>
    public class BattleAuthorityAndRequestTests
    {
        // ===== 操作モード（主人公不在フォールバックの撤廃） =====

        [Test]
        public void Mode_ScenarioIsFreePlay_CampaignIsChain()
        {
            Assert.AreEqual(BattleCommandMode.自由操作, BattleCommandModeRules.ModeOf(fromCampaign: false));
            Assert.AreEqual(BattleCommandMode.戦役, BattleCommandModeRules.ModeOf(fromCampaign: true));
        }

        [Test]
        public void FreePlay_CommandsEverything()
        {
            BattleChain chain = BattleCommandModeRules.ChainFor(BattleCommandMode.自由操作, false, "");
            Assert.IsTrue(chain.commandsAll, "戦役外の演習は明示的に全部隊を操作できる");
            Assert.AreEqual(BattleCommandRight.直接命令,
                BattleCommandAuthorityRules.RightFor(chain, true, false, "他人の軍団"));
        }

        /// <summary>★戦役で人物・役職・軍団のどれも分からなくても<b>全権限にしない</b>。</summary>
        [Test]
        public void Campaign_UnknownActor_IsNotFullAuthority()
        {
            BattleChain chain = BattleCommandModeRules.ChainFor(BattleCommandMode.戦役, false, "");
            Assert.IsFalse(chain.commandsAll, "不明を全権限へ読み替えている");
            Assert.AreEqual(BattleCommandRight.要請,
                BattleCommandAuthorityRules.RightFor(chain, true, false, "どこかの軍団"),
                "系統が分からないのに他人の部隊を直接動かせてはいけない");
            // 自分の乗艦だけは動かせる。
            Assert.AreEqual(BattleCommandRight.直接命令,
                BattleCommandAuthorityRules.RightFor(chain, true, true, "どこかの軍団"));
        }

        [Test]
        public void Campaign_TopOfficeCommandsWholeFleet()
        {
            BattleChain chain = BattleCommandModeRules.ChainFor(BattleCommandMode.戦役, true, "");
            Assert.AreEqual(BattleCommandRight.直接命令,
                BattleCommandAuthorityRules.RightFor(chain, true, false, "他人の軍団"));
        }

        [Test]
        public void Campaign_CorpsCommander_OnlyOwnCorps()
        {
            BattleChain chain = BattleCommandModeRules.ChainFor(BattleCommandMode.戦役, false, "A軍団");
            Assert.AreEqual(BattleCommandRight.直接命令,
                BattleCommandAuthorityRules.RightFor(chain, true, false, "A軍団"));
            Assert.AreEqual(BattleCommandRight.要請,
                BattleCommandAuthorityRules.RightFor(chain, true, false, "B軍団"));
        }

        [Test]
        public void ModeText_ExplainsWhatCanBeCommanded()
        {
            StringAssert.Contains("自由操作",
                BattleCommandModeRules.ModeText(BattleCommandMode.自由操作, BattleChain.Everything));
            StringAssert.Contains("総司令官",
                BattleCommandModeRules.ModeText(BattleCommandMode.戦役, new BattleChain("", true)));
            StringAssert.Contains("A軍団",
                BattleCommandModeRules.ModeText(BattleCommandMode.戦役, new BattleChain("A軍団", false)));
            StringAssert.Contains("自艦隊のみ",
                BattleCommandModeRules.ModeText(BattleCommandMode.戦役, new BattleChain("", false)));
        }

        // ===== 権限失効（途中で系統が変わる） =====

        /// <summary>★配属が変われば直後の命令から効く（権限を握ったまま持ち歩かない）。</summary>
        [Test]
        public void Authority_ChangesImmediatelyWhenChainChanges()
        {
            var before = new BattleChain("A軍団", false);
            Assert.AreEqual(BattleCommandRight.直接命令,
                BattleCommandAuthorityRules.RightFor(before, true, false, "A軍団"));

            // 指揮を解かれた（軍団を持たなくなった）＝同じ部隊がもう直接命令できない。
            var after = new BattleChain("", false);
            Assert.AreEqual(BattleCommandRight.要請,
                BattleCommandAuthorityRules.RightFor(after, true, false, "A軍団"));
        }

        [Test]
        public void Authority_EnemyIsAlwaysBlocked_EvenForTopCommander()
        {
            var top = new BattleChain("", true);
            Assert.AreEqual(BattleCommandRight.不可,
                BattleCommandAuthorityRules.RightFor(top, false, false, "敵の軍団"));
        }

        // ===== 混在選択 =====

        [Test]
        public void MixedSelection_CountsAreExplained()
        {
            var chain = new BattleChain("A軍団", false);
            string[] corps = { "A軍団", "B軍団", "A軍団" };
            int direct = 0, request = 0;
            for (int i = 0; i < corps.Length; i++)
            {
                switch (BattleCommandAuthorityRules.RightFor(chain, true, false, corps[i]))
                {
                    case BattleCommandRight.直接命令: direct++; break;
                    case BattleCommandRight.要請: request++; break;
                }
            }
            Assert.AreEqual(2, direct);
            Assert.AreEqual(1, request);

            string note = BattleCommandAuthorityRules.SelectionNote(direct, request, 0);
            StringAssert.Contains("直接命令 2 隊", note);
            StringAssert.Contains("支援要請", note);
        }

        // ===== 支援要請の往復 =====

        private static SupportRequestParams P => SupportRequestParams.Default;

        [Test]
        public void Request_BeforeReply_IsPending()
        {
            Assert.AreEqual(SupportRequestOutcome.検討中,
                SupportRequestRules.Judge(true, 0f, 1f, P));
        }

        [Test]
        public void Request_WillingCommander_Accepts()
        {
            float w = SupportRequestRules.Willingness(90, 1f, busyFighting: false);
            Assert.AreEqual(SupportRequestOutcome.承諾,
                SupportRequestRules.Judge(true, P.replySeconds, w, P));
        }

        [Test]
        public void Request_UnwillingCommander_Refuses()
        {
            float w = SupportRequestRules.Willingness(10, 0.2f, busyFighting: true);
            Assert.AreEqual(SupportRequestOutcome.拒否,
                SupportRequestRules.Judge(true, P.replySeconds, w, P));
        }

        /// <summary>★返事が来ないまま期限を過ぎたら流れる（いつまでも待たない）。</summary>
        [Test]
        public void Request_Expires()
        {
            Assert.AreEqual(SupportRequestOutcome.失効,
                SupportRequestRules.Judge(true, P.expireSeconds, 1f, P));
        }

        /// <summary>★相手が戦闘不能になったら失効（勝手に代理で動かさない）。</summary>
        [Test]
        public void Request_TargetGone_Expires()
        {
            Assert.AreEqual(SupportRequestOutcome.失効,
                SupportRequestRules.Judge(false, P.replySeconds, 1f, P));
        }

        [Test]
        public void Request_Willingness_IsDeterministicAndBounded()
        {
            Assert.AreEqual(SupportRequestRules.Willingness(50, 0.5f, false),
                            SupportRequestRules.Willingness(50, 0.5f, false), 1e-6f);
            Assert.GreaterOrEqual(SupportRequestRules.Willingness(0, 0f, true), 0f);
            Assert.LessOrEqual(SupportRequestRules.Willingness(100, 1f, false), 1f);
            Assert.Less(SupportRequestRules.Willingness(60, 0.8f, busyFighting: true),
                        SupportRequestRules.Willingness(60, 0.8f, busyFighting: false),
                        "交戦中は応じにくい");
        }

        [Test]
        public void Request_Texts_AreDistinct()
        {
            StringAssert.Contains("要請しました",
                SupportRequestRules.SentText("第7艦隊", SupportOrderKind.移動));
            StringAssert.Contains("応じました",
                SupportRequestRules.OutcomeText(SupportRequestOutcome.承諾, "第7艦隊", SupportOrderKind.移動));
            StringAssert.Contains("断りました",
                SupportRequestRules.OutcomeText(SupportRequestOutcome.拒否, "第7艦隊", SupportOrderKind.攻撃));
            StringAssert.Contains("流れました",
                SupportRequestRules.OutcomeText(SupportRequestOutcome.失効, "第7艦隊", SupportOrderKind.陣形変更));
            StringAssert.Contains("すでに出しています",
                SupportRequestRules.DuplicateText("第7艦隊", SupportOrderKind.移動));
        }

        [Test]
        public void Request_Params_ClampToSaneOrder()
        {
            var p = new SupportRequestParams(10f, 2f, 5f);
            Assert.GreaterOrEqual(p.expireSeconds, p.replySeconds, "失効が返事より早くならない");
            Assert.LessOrEqual(p.acceptThreshold, 1f);
        }

        // ===== 未実装の効果は実行不可（無効果の成功を作らない） =====

        [Test]
        public void UnimplementedEffect_IsDetected()
        {
            Assert.IsFalse(DecisionEffectRegistryRules.IsImplemented("treaty.sign"),
                           "未実装のキーを実装済みと判定している");
            Assert.IsFalse(DecisionEffectRegistryRules.IsImplemented(""));
            Assert.IsFalse(string.IsNullOrEmpty(DecisionEffectRegistryRules.NotImplementedText("treaty.sign")));
            StringAssert.Contains("treaty.sign", DecisionEffectRegistryRules.NotImplementedText("treaty.sign"));
        }

        [Test]
        public void ImplementedEffects_AreRecognized()
        {
            Assert.IsTrue(DecisionEffectRegistryRules.IsImplemented("tax.cut"), "国家値の効果");
            Assert.IsTrue(DecisionEffectRegistryRules.IsImplemented("mil.offensive"), "盤面まで届く執行");
            Assert.IsTrue(DecisionEffectRegistryRules.IsImplemented("fleet.establish:12000"), "編制（引数付き）");
            Assert.AreEqual("", DecisionEffectRegistryRules.NotImplementedText("tax.cut"));
        }

        // ===== 対象消失（提案した対象が消えたら失敗して振り替えない） =====

        [Test]
        public void ProposedTargetGone_FailsInsteadOfRetargeting()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem { id = 0, systemName = "本国", owner = Faction.同盟 });
            map.AddSystem(new StarSystem { id = 1, systemName = "敵地", owner = Faction.帝国 });
            map.AddCorridor(new Corridor(0, 1, 5f, CorridorType.通商));

            var campaign = new CampaignState(map);
            campaign.states.Add(new FactionState(Faction.同盟));
            campaign.states.Add(new FactionState(Faction.帝国));

            var ctx = new PetitionActionContext
            {
                campaign = campaign, map = map, fleets = new StrategicFleetRegistry(map),
                faction = Faction.同盟, shipyards = new List<Shipyard>(), useWarLedger = false,
            };
            ctx.fleets.Add(new StrategicFleet(1, 0, Faction.同盟) { strength = 200 });

            // 盤面に存在しない星系を提案対象にしていた場合。
            var ghost = new PetitionTarget(PetitionTargetKind.星系, 999, "消えた星系");
            PetitionActionResult r = PetitionActionRules.Execute("mil.offensive", ctx, 1f, ghost);

            Assert.AreEqual(PetitionActionOutcome.対象なし, r.outcome);
            StringAssert.Contains("消えた星系", r.detail, "どの対象が失われたのか分かること");
        }
    }
}
