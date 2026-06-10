using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 機雷原を固定する：通過損害・減速は密度比例、掃海と敷設の綱引き、安全化判定、完全掃海所要時間
    /// （努力ゼロは無限大）＝「血で払うか時間で払うか」。境界を担保。
    /// </summary>
    public class MinefieldRulesTests
    {
        private static readonly MinefieldParams P = MinefieldParams.Default;
        // 損害上限0.2/減速上限0.6/掃海0.1/安全閾値0.05

        [Test]
        public void TransitLossRatio_ProportionalToDensity()
        {
            Assert.AreEqual(0.2f, MinefieldRules.TransitLossRatio(1f, P), 1e-5f);
            Assert.AreEqual(0.1f, MinefieldRules.TransitLossRatio(0.5f, P), 1e-5f);
            Assert.AreEqual(0f, MinefieldRules.TransitLossRatio(0f, P), 1e-5f);
        }

        [Test]
        public void SpeedFactor_SlowsByDensity()
        {
            Assert.AreEqual(0.4f, MinefieldRules.SpeedFactor(1f, P), 1e-5f);  // 最大減速60%
            Assert.AreEqual(0.7f, MinefieldRules.SpeedFactor(0.5f, P), 1e-5f);
            Assert.AreEqual(1f, MinefieldRules.SpeedFactor(0f, P), 1e-5f);
        }

        [Test]
        public void DensityTick_SweepVsLaying()
        {
            // 掃海のみ：1 − 0.1×1×1 = 0.9
            Assert.AreEqual(0.9f, MinefieldRules.DensityTick(1f, 1f, 0f, 1f, P), 1e-5f);
            // 敷設が掃海を上回る綱引き：0.5 −0.1 +0.2 = 0.6
            Assert.AreEqual(0.6f, MinefieldRules.DensityTick(0.5f, 1f, 0.2f, 1f, P), 1e-5f);
            // 上限1・下限0
            Assert.AreEqual(1f, MinefieldRules.DensityTick(0.95f, 0f, 0.5f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, MinefieldRules.DensityTick(0.05f, 1f, 0f, 1f, P), 1e-5f);
        }

        [Test]
        public void IsCleared_AtThreshold()
        {
            Assert.IsTrue(MinefieldRules.IsCleared(0.05f, P));
            Assert.IsFalse(MinefieldRules.IsCleared(0.06f, P));
            Assert.IsTrue(MinefieldRules.IsCleared(0f, P));
        }

        [Test]
        public void TimeToClear_DeficitOverRate()
        {
            // 密度1→閾値0.05：必要0.95、速度0.1×1 ＝9.5
            Assert.AreEqual(9.5f, MinefieldRules.TimeToClear(1f, 1f, P), 1e-4f);
            // 既に安全＝0
            Assert.AreEqual(0f, MinefieldRules.TimeToClear(0.05f, 1f, P), 1e-5f);
            // 掃海努力ゼロ＝永遠
            Assert.IsTrue(float.IsPositiveInfinity(MinefieldRules.TimeToClear(1f, 0f, P)));
        }
    }
}
