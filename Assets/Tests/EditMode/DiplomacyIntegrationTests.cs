using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 外交システム基盤（#2119）：セッション(DIPLO-1)/AI(2)/戦争追跡(3)/条約(4)/年次オーケストレータ(5)。
    /// </summary>
    public class DiplomacyIntegrationTests
    {
        [TearDown]
        public void Cleanup()
        {
            DiplomacySession.Clear();
            WarLedger.Clear();
            TreatyLedger.Clear();
        }

        // --- DIPLO-1 セッション ---
        [Test]
        public void Session_EnsureWiresActiveDiplomacy()
        {
            Assert.IsFalse(DiplomacySession.HasState);
            var state = DiplomacySession.Ensure(new List<string> { "帝国", "同盟" });
            Assert.IsNotNull(state);
            Assert.AreSame(state, FactionRelations.ActiveDiplomacy); // 敵対判定へ配線
            Assert.IsNotNull(state.GetEntry("帝国", "同盟"));
            DiplomacySession.Clear();
            Assert.IsNull(FactionRelations.ActiveDiplomacy); // 従来動作へ戻る
        }

        // --- DIPLO-2 AI判断 ---
        [Test]
        public void Ai_WarPeaceAlliance()
        {
            var p = DiplomacyAiRules.DiploAiParams.Default;
            Assert.IsTrue(DiplomacyAiRules.ShouldDeclareWar(-70f, 120f, 100f, p)); // 険悪×優位
            Assert.IsFalse(DiplomacyAiRules.ShouldDeclareWar(-50f, 120f, 100f, p)); // 関係足りる
            Assert.IsFalse(DiplomacyAiRules.ShouldDeclareWar(-70f, 100f, 100f, p)); // 優位なし
            Assert.IsTrue(DiplomacyAiRules.ShouldProposeAlliance(60f, p));
            Assert.IsFalse(DiplomacyAiRules.ShouldProposeAlliance(40f, p));
            Assert.IsTrue(DiplomacyAiRules.ShouldMakePeace(0.7f, p));
            Assert.IsFalse(DiplomacyAiRules.ShouldMakePeace(0.5f, p));
            Assert.AreEqual(CasusBelli.征服, DiplomacyAiRules.ChooseCasusBelli(-90f));
            Assert.AreEqual(CasusBelli.懲罰, DiplomacyAiRules.ChooseCasusBelli(-60f));
            Assert.AreEqual(CasusBelli.従属, DiplomacyAiRules.ChooseCasusBelli(-10f));
        }

        // --- DIPLO-3 戦争追跡 ---
        [Test]
        public void War_LedgerAndWeariness()
        {
            var w = WarLedger.GetOrCreate("A", "B");
            Assert.AreSame(w, WarLedger.Get("B", "A")); // 無向
            w.turnsAtWar = 10; w.casualties = 0.5f; w.warScore = -0.5f;
            var wp = WarGoalRules.WarGoalParams.Default;
            Assert.AreEqual(0.5f, WarStateRules.Weariness(w, wp), 1e-4f);          // 10×0.02+0.5×0.6
            Assert.AreEqual(0.625f, WarStateRules.PeaceAcceptanceFor(w, true, wp), 1e-4f);  // A劣勢で講和へ
            Assert.AreEqual(0.375f, WarStateRules.PeaceAcceptanceFor(w, false, wp), 1e-4f); // B優勢で講和渋る
            WarStateRules.Tick(w, 1);
            Assert.AreEqual(11, w.turnsAtWar);
            Assert.IsTrue(WarLedger.Remove("A", "B"));
        }

        // --- DIPLO-4 条約 ---
        [Test]
        public void Treaty_SignAndExpire()
        {
            var state = new DiplomacyState();
            Assert.IsTrue(TreatyManagementRules.Sign(state, TreatyType.不可侵, "A", "B", 800, 5));
            Assert.AreEqual(DiplomacyState.DiplomaticStatus.不可侵, state.Status("A", "B"));
            Assert.AreEqual(1, TreatyLedger.All.Count);
            float expectedOpinion = TreatyRules.OpinionEffect(TreatyType.不可侵, TreatyRules.TreatyParams.Default);
            Assert.AreEqual(expectedOpinion, state.Opinion("A", "B"), 1e-3f);

            Assert.AreEqual(0, TreatyManagementRules.ExpireDue(state, 804)); // まだ有効（失効805）
            Assert.AreEqual(1, TreatyManagementRules.ExpireDue(state, 805)); // 失効
            Assert.AreEqual(DiplomacyState.DiplomaticStatus.平時, state.Status("A", "B")); // 平時へ戻る
            Assert.AreEqual(0, TreatyLedger.All.Count);
        }

        // --- DIPLO-5 オーケストレータ ---
        [Test]
        public void Tick_DeclareWarPeaceAlliance()
        {
            var dp = DiplomacyRules.DiplomacyParams.Default;
            var ai = DiplomacyAiRules.DiploAiParams.Default;
            var wp = WarGoalRules.WarGoalParams.Default;

            // 宣戦：険悪（強い負の要因）×国力優位
            var s1 = new DiplomacyState();
            DiplomacyRules.AdjustOpinion(s1, "A", "B", -95f);
            var hostile = new DiplomacyRules.OpinionFactors(-1f, 0f, true, 1f, false); // 目標も強く負
            var ev1 = DiplomacyTickRules.TickPair(s1, "A", "B", hostile, 300f, 100f, 800, dp, ai, wp);
            Assert.AreEqual(DiplomacyEvent.宣戦布告, ev1);
            Assert.AreEqual(DiplomacyState.DiplomaticStatus.交戦, s1.Status("A", "B"));
            Assert.IsNotNull(WarLedger.Get("A", "B"));

            // 講和：交戦中で厭戦MAX
            var s2 = new DiplomacyState();
            DiplomacyRules.DeclareWar(s2, "C", "D", dp);
            var w = WarLedger.GetOrCreate("C", "D");
            w.turnsAtWar = 100; w.warScore = -1f; // 長期戦＋劣勢
            var any = new DiplomacyRules.OpinionFactors(0f, 0f, false, 0f, false);
            var ev2 = DiplomacyTickRules.TickPair(s2, "C", "D", any, 100f, 100f, 800, dp, ai, wp);
            Assert.AreEqual(DiplomacyEvent.講和, ev2);
            Assert.AreEqual(DiplomacyState.DiplomaticStatus.平時, s2.Status("C", "D"));
            Assert.IsNull(WarLedger.Get("C", "D"));

            // 同盟：良好（強い正の要因）
            var s3 = new DiplomacyState();
            DiplomacyRules.AdjustOpinion(s3, "E", "F", 60f);
            var friendly = new DiplomacyRules.OpinionFactors(1f, 1f, false, 0f, true);
            var ev3 = DiplomacyTickRules.TickPair(s3, "E", "F", friendly, 100f, 100f, 800, dp, ai, wp);
            Assert.AreEqual(DiplomacyEvent.同盟締結, ev3);
            Assert.AreEqual(DiplomacyState.DiplomaticStatus.同盟, s3.Status("E", "F"));
        }
    }
}
