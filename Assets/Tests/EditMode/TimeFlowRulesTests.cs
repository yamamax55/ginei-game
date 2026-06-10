using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>暦時間の自動スロー（TimeFlowRules・TIME-7 #959）の EditMode テスト。</summary>
    public class TimeFlowRulesTests
    {
        private static TimeFlowRules.TimeFlowParams P => TimeFlowRules.TimeFlowParams.Default; // fast30/slow1/ease8

        [Test]
        public void TargetCompression_SalientSlow_IdleFast()
        {
            Assert.AreEqual(1f, TimeFlowRules.TargetCompression(true, P), 1e-5f);   // 観戦＝実時間
            Assert.AreEqual(30f, TimeFlowRules.TargetCompression(false, P), 1e-5f); // 平時＝圧縮
        }

        [Test]
        public void Ease_MovesTowardTarget_ByEaseRateTimesDt()
        {
            // 1→30 へ ease8/s・dt0.5 → +4 = 5
            float v = TimeFlowRules.Ease(1f, 30f, P, 0.5f);
            Assert.AreEqual(5f, v, 1e-4f);
        }

        [Test]
        public void Ease_DoesNotOvershoot()
        {
            // 残り差1.0 を ease8×dt1=8 で詰めても 30 を超えない
            float v = TimeFlowRules.Ease(29f, 30f, P, 1f);
            Assert.AreEqual(30f, v, 1e-5f);
        }

        [Test]
        public void Ease_NonPositiveDt_KeepsCurrent()
        {
            Assert.AreEqual(12f, TimeFlowRules.Ease(12f, 30f, P, 0f), 1e-5f);
            Assert.AreEqual(12f, TimeFlowRules.Ease(12f, 30f, P, -1f), 1e-5f);
        }

        [Test]
        public void Params_ClampsInvalidValues()
        {
            // fast<1→1, slow<0→0, ease<0→0
            var p = new TimeFlowRules.TimeFlowParams(0.2f, -5f, -3f);
            Assert.AreEqual(1f, p.fastCompression, 1e-5f);
            Assert.AreEqual(0f, p.slowCompression, 1e-5f);
            Assert.AreEqual(0f, p.easeRate, 1e-5f);
            // ease=0 なら現状維持（遷移しない）
            Assert.AreEqual(10f, TimeFlowRules.Ease(10f, 30f, p, 1f), 1e-5f);
        }

        [Test]
        public void Ease_SlowDown_FromFastToSlow()
        {
            // 30→1（減速）も対称に動く。ease8×dt0.25=2 → 28
            float v = TimeFlowRules.Ease(30f, 1f, P, 0.25f);
            Assert.AreEqual(28f, v, 1e-4f);
        }
    }
}
