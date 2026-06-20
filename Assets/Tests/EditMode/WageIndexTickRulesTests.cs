using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>WageIndexTickRules（賃金指数の進行 内政・労働）の純ロジック検証。</summary>
    public class WageIndexTickRulesTests
    {
        private static WageIndexTickRules.Params P => WageIndexTickRules.Params.Default;

        [Test]
        public void TargetIndex_BalancedDemand_EqualsProductivity()
        {
            // 需給均衡（demand==supply）＝逼迫0 → 目標は生産性そのもの。
            Assert.AreEqual(1f, WageIndexTickRules.TargetIndex(1f, 0.5f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void TargetIndex_TightLabor_RaisesTarget()
        {
            float tight = WageIndexTickRules.TargetIndex(1f, 1f, 0f, P);   // 需要過多
            float slack = WageIndexTickRules.TargetIndex(1f, 0f, 1f, P);   // 供給過多
            Assert.Greater(tight, 1f);
            Assert.Less(slack, 1f);
        }

        [Test]
        public void TargetIndex_HigherProductivity_RaisesTarget()
        {
            float lo = WageIndexTickRules.TargetIndex(1f, 0.5f, 0.5f, P);
            float hi = WageIndexTickRules.TargetIndex(2f, 0.5f, 0.5f, P);
            Assert.Greater(hi, lo);
        }

        [Test]
        public void Tick_MovesTowardTarget()
        {
            float next = WageIndexTickRules.Tick(1.0f, 2.0f, 1f, P);
            Assert.Greater(next, 1.0f);
            Assert.Less(next, 2.0f); // 1Tickでは到達しない（収束）
        }

        [Test]
        public void Tick_DownwardStickier_ThanUpward()
        {
            // 同じ乖離幅でも下落の方が動きが小さい（下方硬直）。
            float up = WageIndexTickRules.Tick(1.0f, 1.5f, 1f, P);   // +0.5 目標
            float down = WageIndexTickRules.Tick(1.5f, 1.0f, 1f, P); // -0.5 目標
            float upMove = up - 1.0f;
            float downMove = 1.5f - down;
            Assert.Greater(upMove, downMove);
        }

        [Test]
        public void Tick_ClampedToBounds()
        {
            float high = WageIndexTickRules.Tick(2.99f, 100f, 1f, P);
            Assert.LessOrEqual(high, P.maxIndex + 1e-6f);
            float low = WageIndexTickRules.Tick(0.21f, -100f, 1f, P);
            Assert.GreaterOrEqual(low, P.minIndex - 1e-6f);
        }
    }
}
