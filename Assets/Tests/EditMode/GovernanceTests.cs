using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 内政（#109 P-1/P-2 最小ループ）の純ロジックを固定する：
    /// 安定度は目標値へ収束／思想一致で安定・不一致で不安定／占領直後は不安定→統合で回復／
    /// 産出は安定度比例（支配≠即産出）／低安定で反乱リスク。
    /// </summary>
    public class GovernanceTests
    {
        private static FactionData Faction(string ideology)
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ideology = ideology;
            return f;
        }

        private Province NativeProvince(string ideology = "民主") => new Province(1, ideology, 100f);

        // --- EquilibriumStability（目標値の純関数） ---

        [Test]
        public void Equilibrium_IdeologyMatch_RaisesAboveBase()
        {
            // 統合済み・思想一致・補給OK・非戦時 → 基準50＋一致25 = 75
            float t = GovernanceRules.EquilibriumStability(1f, GovernanceRules.IdeologyMatchBonus, true, false);
            Assert.AreEqual(75f, t, 1e-4f);
        }

        [Test]
        public void Equilibrium_Mismatch_War_Supply_StackPenalties()
        {
            // 統合済み・不一致(-20)・補給不足(-20)・戦時(-15) → 50-20-20-15 = -5 → clamp 0
            float t = GovernanceRules.EquilibriumStability(1f, -GovernanceRules.IdeologyMismatchPenalty, false, true);
            Assert.AreEqual(0f, t, 1e-4f);
        }

        [Test]
        public void Equilibrium_FreshlyOccupied_OccupationUnrestDominates()
        {
            // 未統合(integration=0)・不一致 → 50-20-(1-0)*40 = -10 → clamp 0
            float t0 = GovernanceRules.EquilibriumStability(0f, -GovernanceRules.IdeologyMismatchPenalty, true, false);
            Assert.AreEqual(0f, t0, 1e-4f);

            // 半分統合 → 50-20-(1-0.5)*40 = 10
            float t05 = GovernanceRules.EquilibriumStability(0.5f, -GovernanceRules.IdeologyMismatchPenalty, true, false);
            Assert.AreEqual(10f, t05, 1e-4f);

            // 完全統合 → 50-20 = 30（占領不満は消えるが思想不一致は残る）
            float t1 = GovernanceRules.EquilibriumStability(1f, -GovernanceRules.IdeologyMismatchPenalty, true, false);
            Assert.AreEqual(30f, t1, 1e-4f);
        }

        // --- IdeologyModifier（3状態） ---

        [Test]
        public void IdeologyModifier_Match_Mismatch_Unknown()
        {
            Assert.AreEqual(GovernanceRules.IdeologyMatchBonus,
                GovernanceRules.IdeologyModifier(Faction("民主"), "民主"), 1e-4f);
            Assert.AreEqual(-GovernanceRules.IdeologyMismatchPenalty,
                GovernanceRules.IdeologyModifier(Faction("専制"), "民主"), 1e-4f);
            // どちらか空＝中立(0)
            Assert.AreEqual(0f, GovernanceRules.IdeologyModifier(Faction(""), "民主"), 1e-4f);
            Assert.AreEqual(0f, GovernanceRules.IdeologyModifier(Faction("専制"), ""), 1e-4f);
            Assert.AreEqual(0f, GovernanceRules.IdeologyModifier(null, "民主"), 1e-4f);
        }

        // --- Tick（収束・統合の進行） ---

        [Test]
        public void Tick_RaisesIntegrationOverTime()
        {
            var p = NativeProvince();
            p.integration = 0f;
            GovernanceRules.Tick(p, Faction("民主"), supplyOk: true, atWar: false, deltaTime: 1f);
            Assert.AreEqual(GovernanceRules.IntegrationRate, p.integration, 1e-4f);
        }

        [Test]
        public void Tick_MovesStabilityTowardTarget()
        {
            // 思想一致・統合済み → 目標75。基準50から上昇する。
            var p = NativeProvince("民主");
            float before = p.stability; // = BaseStability(50)
            GovernanceRules.Tick(p, Faction("民主"), true, false, 1f);
            Assert.Greater(p.stability, before);
            Assert.LessOrEqual(p.stability, 75f);
            // 1tickで StabilitySpeed ぶんだけ寄る（50→56）
            Assert.AreEqual(before + GovernanceRules.StabilitySpeed, p.stability, 1e-4f);
        }

        [Test]
        public void Tick_ZeroOrNegativeDelta_NoChange()
        {
            var p = NativeProvince();
            float s = p.stability; float ig = p.integration;
            GovernanceRules.Tick(p, Faction("民主"), true, false, 0f);
            Assert.AreEqual(s, p.stability, 1e-4f);
            Assert.AreEqual(ig, p.integration, 1e-4f);
        }

        // --- OnOccupied（占領で不安定化） ---

        [Test]
        public void OnOccupied_ResetsIntegrationAndLowersStability()
        {
            var p = NativeProvince();
            p.stability = 80f; p.integration = 1f;
            GovernanceRules.OnOccupied(p);
            Assert.AreEqual(0f, p.integration, 1e-4f);
            Assert.AreEqual(GovernanceRules.OccupiedInitialStability, p.stability, 1e-4f);
            Assert.IsTrue(GovernanceRules.IsUnrest(p)); // 占領直後は反乱リスク域
        }

        // --- OutputFactor（支配≠即産出） ---

        [Test]
        public void OutputFactor_ScalesWithStability_Monotonic()
        {
            var low = NativeProvince(); low.stability = 0f;
            var mid = NativeProvince(); mid.stability = 50f;
            var high = NativeProvince(); high.stability = 100f;

            Assert.AreEqual(GovernanceRules.MinOutputFactor, GovernanceRules.OutputFactor(low), 1e-4f);
            Assert.AreEqual(1f, GovernanceRules.OutputFactor(high), 1e-4f);
            Assert.Less(GovernanceRules.OutputFactor(low), GovernanceRules.OutputFactor(mid));
            Assert.Less(GovernanceRules.OutputFactor(mid), GovernanceRules.OutputFactor(high));
        }

        // --- 反乱圧 ---

        [Test]
        public void RebelPressure_ZeroAboveThreshold_RisesBelow()
        {
            var p = NativeProvince();
            p.stability = GovernanceRules.RebelThreshold; // 境界＝0
            Assert.AreEqual(0f, GovernanceRules.RebelPressure(p), 1e-4f);
            Assert.IsFalse(GovernanceRules.IsUnrest(p));

            p.stability = 0f; // 最大圧＝1
            Assert.AreEqual(1f, GovernanceRules.RebelPressure(p), 1e-4f);
            Assert.IsTrue(GovernanceRules.IsUnrest(p));
        }

        // --- 統合シナリオ：占領→放置で統合し安定が回復（思想一致なら高安定） ---

        [Test]
        public void Scenario_OccupiedFriendlyIdeology_IntegratesToHighStability()
        {
            var p = NativeProvince("民主");
            GovernanceRules.OnOccupied(p);              // 占領直後：integration0・stability15
            var owner = Faction("民主");                // 解放＝思想一致
            // 十分長く統治（統合が 1 に達するまで回す）
            for (int i = 0; i < 200; i++)
                GovernanceRules.Tick(p, owner, supplyOk: true, atWar: false, deltaTime: 1f);

            Assert.AreEqual(1f, p.integration, 1e-3f);
            Assert.AreEqual(75f, p.stability, 0.5f);    // 50+25（一致）へ収束
            Assert.IsFalse(GovernanceRules.IsUnrest(p));
        }

        [Test]
        public void Scenario_OccupiedHostileIdeology_StaysRestless()
        {
            var p = NativeProvince("民主");
            GovernanceRules.OnOccupied(p);
            var owner = Faction("専制");                // 占領＝思想不一致
            for (int i = 0; i < 200; i++)
                GovernanceRules.Tick(p, owner, supplyOk: true, atWar: false, deltaTime: 1f);

            Assert.AreEqual(1f, p.integration, 1e-3f);
            Assert.AreEqual(30f, p.stability, 0.5f);    // 50-20 止まり＝統合しても燻る
        }
    }
}
