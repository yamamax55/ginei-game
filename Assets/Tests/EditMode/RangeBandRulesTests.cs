using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// #2254 射程帯／キーティング：RangeBandRules の純ロジックテスト。
    /// IdealRange（全3帯）と ApproachOrWithdraw（全3分岐）を担保する。
    /// </summary>
    public class RangeBandRulesTests
    {
        // ── IdealRange ──────────────────────────────────────────

        [Test]
        public void IdealRange_Far_Returns90Percent()
        {
            // 遠距離帯: weaponRange * 0.9
            float result = RangeBandRules.IdealRange(RangeBand.遠, 100f);
            Assert.AreEqual(90f, result, 1e-4f);
        }

        [Test]
        public void IdealRange_Medium_Returns60Percent()
        {
            // 中距離帯: weaponRange * 0.6
            float result = RangeBandRules.IdealRange(RangeBand.中, 100f);
            Assert.AreEqual(60f, result, 1e-4f);
        }

        [Test]
        public void IdealRange_Close_Returns35Percent()
        {
            // 近距離帯: weaponRange * 0.35
            float result = RangeBandRules.IdealRange(RangeBand.近, 100f);
            Assert.AreEqual(35f, result, 1e-4f);
        }

        // ── ApproachOrWithdraw ───────────────────────────────────

        [Test]
        public void ApproachOrWithdraw_TooFar_ReturnsPositive()
        {
            // currentDist > idealRange + deadzone → +1（接近）
            int result = RangeBandRules.ApproachOrWithdraw(75f, 60f, 5f);
            Assert.AreEqual(1, result);
        }

        [Test]
        public void ApproachOrWithdraw_TooClose_ReturnsNegative()
        {
            // currentDist < idealRange - deadzone → -1（後退）
            int result = RangeBandRules.ApproachOrWithdraw(50f, 60f, 5f);
            Assert.AreEqual(-1, result);
        }

        [Test]
        public void ApproachOrWithdraw_InDeadzone_ReturnsZero()
        {
            // idealRange - deadzone <= currentDist <= idealRange + deadzone → 0（保持）
            int result = RangeBandRules.ApproachOrWithdraw(60f, 60f, 5f);
            Assert.AreEqual(0, result);
        }

        [Test]
        public void ApproachOrWithdraw_AtUpperEdge_ReturnsZero()
        {
            // currentDist == idealRange + deadzone（上限ちょうど）→ 0（保持）
            int result = RangeBandRules.ApproachOrWithdraw(65f, 60f, 5f);
            Assert.AreEqual(0, result);
        }

        [Test]
        public void ApproachOrWithdraw_AtLowerEdge_ReturnsZero()
        {
            // currentDist == idealRange - deadzone（下限ちょうど）→ 0（保持）
            int result = RangeBandRules.ApproachOrWithdraw(55f, 60f, 5f);
            Assert.AreEqual(0, result);
        }
    }
}
