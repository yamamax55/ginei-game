using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// ProbeAttackRules（威力偵察）の EditMode テスト。既定 Params で期待値固定。
    /// </summary>
    public class ProbeAttackRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void ProbeExposure_StrengthTimesResponse()
        {
            // 0.6 × 0.7 × 1.0 = 0.42
            Assert.AreEqual(0.42f, ProbeAttackRules.ProbeExposure(0.6f, 0.7f), Eps);
        }

        [Test]
        public void IntelGathered_ScalesWithObservation()
        {
            // 0.42 × 0.8 × 1.0 = 0.336
            Assert.AreEqual(0.336f, ProbeAttackRules.IntelGathered(0.42f, 0.8f), Eps);
        }

        [Test]
        public void WeakPointRevealed_UnevenDefenseShowsGaps()
        {
            // 0.336 × (1 - 0.25)=0.75 → 0.252
            Assert.AreEqual(0.252f, ProbeAttackRules.WeakPointRevealed(0.336f, 0.25f), Eps);
            // 均一な防御は隙が見えない（uniformity=1 → 0）
            Assert.AreEqual(0f, ProbeAttackRules.WeakPointRevealed(0.336f, 1f), Eps);
        }

        [Test]
        public void ProbeLosses_StrongEnemyHurtsProbe()
        {
            // 0.6 × 0.5 × 0.5 = 0.15
            Assert.AreEqual(0.15f, ProbeAttackRules.ProbeLosses(0.6f, 0.5f), Eps);
        }

        [Test]
        public void IntentionTelegraphed_FocusedProbeIsReadable()
        {
            // 0.6 × 0.9 × 1.0 = 0.54
            Assert.AreEqual(0.54f, ProbeAttackRules.IntentionTelegraphed(0.6f, 0.9f), Eps);
        }

        [Test]
        public void ReserveFlushing_TracksExposure()
        {
            // 0.42 × 1.0 = 0.42
            Assert.AreEqual(0.42f, ProbeAttackRules.ReserveFlushing(0.42f), Eps);
        }

        [Test]
        public void ProbeNetValue_SubtractsLossAndTelegraph()
        {
            // 0.336 - (0.15 + 0.54) × 0.5 = 0.336 - 0.345 = -0.009
            Assert.AreEqual(-0.009f, ProbeAttackRules.ProbeNetValue(0.336f, 0.15f, 0.54f), Eps);
        }

        [Test]
        public void IsWeakPointFound_ThresholdGate()
        {
            Assert.IsTrue(ProbeAttackRules.IsWeakPointFound(0.252f, 0.2f));
            Assert.IsFalse(ProbeAttackRules.IsWeakPointFound(0.252f, 0.5f));
        }

        [Test]
        public void Story_ProbeFlushesReservesAndGapsButBleedsAndGetsRead()
        {
            // 威力偵察：兵力0.7で積極的に応じる敵(0.8)へ仕掛ける。
            float exposure = ProbeAttackRules.ProbeExposure(0.7f, 0.8f);
            Assert.AreEqual(0.56f, exposure, Eps);

            // 敵の予備が引っ張り出され位置が暴かれる。
            float flush = ProbeAttackRules.ReserveFlushing(exposure);
            Assert.AreEqual(0.56f, flush, Eps);

            // 観測の質0.8で情報を得て、ムラのある防御(均一0.3)から弱点が浮かぶ。
            float intel = ProbeAttackRules.IntelGathered(exposure, 0.8f);
            Assert.AreEqual(0.448f, intel, Eps);
            float weak = ProbeAttackRules.WeakPointRevealed(intel, 0.3f);
            Assert.AreEqual(0.3136f, weak, Eps);
            Assert.IsTrue(ProbeAttackRules.IsWeakPointFound(weak, 0.25f));

            // だが偵察部隊は血を流し（敵火力0.6）、本命方向(0.8)へ向けて意図も読まれる。
            float losses = ProbeAttackRules.ProbeLosses(0.7f, 0.6f);
            Assert.AreEqual(0.21f, losses, Eps);
            float telegraph = ProbeAttackRules.IntentionTelegraphed(0.7f, 0.8f);
            Assert.AreEqual(0.56f, telegraph, Eps);

            // 正味は情報の利得が代償をわずかに上回る（割に合う偵察）。
            float net = ProbeAttackRules.ProbeNetValue(intel, losses, telegraph);
            Assert.AreEqual(0.063f, net, Eps);
            Assert.Greater(net, 0f);
        }
    }
}
