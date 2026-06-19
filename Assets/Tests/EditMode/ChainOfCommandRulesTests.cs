using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 指揮系統の純ロジック <see cref="ChainOfCommandRules"/> の EditMode テスト。
    /// 既定 Params の期待値を手計算で固定（Pow/Sqrt 箇所のみ 1e-3 許容）。
    /// </summary>
    public class ChainOfCommandRulesTests
    {
        private const float Tol = 1e-4f;
        private const float PowTol = 1e-3f;

        // ---- SpanOfControl ----

        [Test]
        public void SpanOfControl_WithinCapacity_NoLoad()
        {
            // sub<=cap → 過負荷なし
            Assert.AreEqual(0f, ChainOfCommandRules.SpanOfControl(5f, 10f), Tol);
        }

        [Test]
        public void SpanOfControl_Overloaded_RaisesLoad()
        {
            // (8/5 - 1)*1 = 0.6
            Assert.AreEqual(0.6f, ChainOfCommandRules.SpanOfControl(8f, 5f), Tol);
            // (10/5 - 1)*1 = 1 → clamp01 = 1
            Assert.AreEqual(1f, ChainOfCommandRules.SpanOfControl(10f, 5f), Tol);
        }

        [Test]
        public void SpanOfControl_ZeroCapacity_MaxLoadIfAnySubordinate()
        {
            Assert.AreEqual(1f, ChainOfCommandRules.SpanOfControl(3f, 0f), Tol);
            Assert.AreEqual(0f, ChainOfCommandRules.SpanOfControl(0f, 0f), Tol);
        }

        // ---- ControlDilution ----

        [Test]
        public void ControlDilution_DepthZero_NoDilution()
        {
            Assert.AreEqual(1f, ChainOfCommandRules.ControlDilution(0), Tol);
        }

        [Test]
        public void ControlDilution_DeepHierarchy_DilutesTowardFloor()
        {
            // pow(0.85, 3) = 0.614125（下限0.2より上）
            Assert.AreEqual(0.614125f, ChainOfCommandRules.ControlDilution(3), PowTol);
            // pow(0.85, 20) ≈ 0.0388 → 下限0.2でクランプ
            Assert.AreEqual(0.2f, ChainOfCommandRules.ControlDilution(20), PowTol);
        }

        // ---- OrderPenetration ----

        [Test]
        public void OrderPenetration_HighLoadAndDilution_Reduces()
        {
            // 0.8 / (1+0.6) * 0.614125 = 0.5 * 0.614125 = 0.3070625
            float pen = ChainOfCommandRules.OrderPenetration(0.8f, 0.6f, 0.614125f);
            Assert.AreEqual(0.3070625f, pen, PowTol);
            // 負荷ゼロ・統制1.0 なら明瞭さがそのまま通る
            Assert.AreEqual(0.8f, ChainOfCommandRules.OrderPenetration(0.8f, 0f, 1f), Tol);
        }

        // ---- SuccessionReadiness ----

        [Test]
        public void SuccessionReadiness_DeputyAndPlan()
        {
            // 0.8 * lerp(1,0.6,0.5) = 0.8 * 0.8 = 0.64
            Assert.AreEqual(0.64f, ChainOfCommandRules.SuccessionReadiness(0.8f, 0.6f), Tol);
            // 副官が居なければ計画があっても備えゼロ
            Assert.AreEqual(0f, ChainOfCommandRules.SuccessionReadiness(0f, 1f), Tol);
        }

        // ---- CommandVacuum / VacuumRecovery ----

        [Test]
        public void CommandVacuum_LessWhenReady()
        {
            // 1 * (1-0.64) = 0.36
            Assert.AreEqual(0.36f, ChainOfCommandRules.CommandVacuum(1f, 0.64f), Tol);
            // 喪失なしなら空白なし
            Assert.AreEqual(0f, ChainOfCommandRules.CommandVacuum(0f, 0f), Tol);
        }

        [Test]
        public void VacuumRecovery_ReadinessAndCohesion()
        {
            // 0.64 * lerp(1,0.8,0.5) = 0.64 * 0.9 = 0.576
            Assert.AreEqual(0.576f, ChainOfCommandRules.VacuumRecovery(0.64f, 0.8f), Tol);
            // 備えが無ければ結束だけでは回復しない
            Assert.AreEqual(0f, ChainOfCommandRules.VacuumRecovery(0f, 1f), Tol);
        }

        // ---- EffectiveControl / IsChainIntact ----

        [Test]
        public void EffectiveControl_PenetrationMinusVacuum_Signed()
        {
            // 0.3070625 - 0.36 = -0.0529375
            Assert.AreEqual(-0.0529375f, ChainOfCommandRules.EffectiveControl(0.3070625f, 0.36f), PowTol);
            // 完全空白・無貫徹 → -1 へクランプ
            Assert.AreEqual(-1f, ChainOfCommandRules.EffectiveControl(0f, 1f), Tol);
        }

        [Test]
        public void IsChainIntact_DefaultThresholdZero()
        {
            Assert.IsTrue(ChainOfCommandRules.IsChainIntact(0.5f));
            Assert.IsFalse(ChainOfCommandRules.IsChainIntact(-0.0529375f));
            Assert.IsFalse(ChainOfCommandRules.IsChainIntact(0f)); // 厳密に > 閾値
        }

        // ---- 物語テスト：深い指揮系統＋過大なスパン＋旗艦喪失 → 指揮系統崩壊 ----

        [Test]
        public void Narrative_DeepOverloadedChainLosesLeader_ChainBreaks()
        {
            var p = ChainOfCommandParams.Default;

            // 深い梯団（5段）＋過大なスパン（部下12 vs 能力5）
            float dilution = ChainOfCommandRules.ControlDilution(5, p);
            float span = ChainOfCommandRules.SpanOfControl(12f, 5f, p);
            float penetration = ChainOfCommandRules.OrderPenetration(0.9f, span, dilution, p);

            // 継承の備えが薄く（副官凡庸・無計画）、旗艦を喪失
            float readiness = ChainOfCommandRules.SuccessionReadiness(0.3f, 0.2f, p);
            float vacuum = ChainOfCommandRules.CommandVacuum(1f, readiness, p);

            float control = ChainOfCommandRules.EffectiveControl(penetration, vacuum, p);
            // 空白が貫徹度を上回り、指揮系統は機能しない
            Assert.Less(control, 0f);
            Assert.IsFalse(ChainOfCommandRules.IsChainIntact(control, p.intactThreshold));

            // 対照：浅い系統・余裕あるスパン・厚い継承なら機能する
            float dilution2 = ChainOfCommandRules.ControlDilution(1, p);
            float span2 = ChainOfCommandRules.SpanOfControl(4f, 8f, p);
            float penetration2 = ChainOfCommandRules.OrderPenetration(0.9f, span2, dilution2, p);
            float readiness2 = ChainOfCommandRules.SuccessionReadiness(0.9f, 0.9f, p);
            float vacuum2 = ChainOfCommandRules.CommandVacuum(1f, readiness2, p);
            float control2 = ChainOfCommandRules.EffectiveControl(penetration2, vacuum2, p);
            Assert.Greater(control2, control);
            Assert.IsTrue(ChainOfCommandRules.IsChainIntact(control2, p.intactThreshold));

            // 回復力も備え・結束で立て直せる
            float recovery = ChainOfCommandRules.VacuumRecovery(readiness2, 0.8f, p);
            Assert.Greater(recovery, 0.5f);
        }
    }
}
