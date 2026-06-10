using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戒厳令を固定する：布告中のみ騒乱が即効で鎮まり、継続コスト（正統性・希望）は限界時間超過で倍速、
    /// 自傷フェーズ判定、解除推奨（鎮静 or 限界超過）。境界を担保。
    /// </summary>
    public class MartialLawRulesTests
    {
        private static readonly MartialLawParams P = MartialLawParams.Default;
        // 鎮静0.2/正統性減0.01/希望減0.015/限界30

        [Test]
        public void UnrestTick_OnlyUnderMartialLaw()
        {
            Assert.AreEqual(0.6f, MartialLawRules.UnrestTick(0.8f, true, 1f, P), 1e-5f);  // −0.2
            Assert.AreEqual(0.8f, MartialLawRules.UnrestTick(0.8f, false, 1f, P), 1e-5f); // 非戒厳＝据え置き
            Assert.AreEqual(0f, MartialLawRules.UnrestTick(0.1f, true, 1f, P), 1e-5f);    // 下限0
        }

        [Test]
        public void LegitimacyCost_LinearWithinLimit()
        {
            Assert.AreEqual(0.1f, MartialLawRules.LegitimacyCost(10f, P), 1e-5f);  // 0.01×10
            Assert.AreEqual(0.3f, MartialLawRules.LegitimacyCost(30f, P), 1e-5f);  // 限界ちょうど
        }

        [Test]
        public void LegitimacyCost_DoublesBeyondLimit()
        {
            // 40＝限界30＋超過10×2倍 → 0.01×(30+20)=0.5
            Assert.AreEqual(0.5f, MartialLawRules.LegitimacyCost(40f, P), 1e-5f);
        }

        [Test]
        public void HopeCost_SameShapeDifferentScale()
        {
            Assert.AreEqual(0.45f, MartialLawRules.HopeCost(30f, P), 1e-5f);  // 0.015×30
            Assert.AreEqual(0.75f, MartialLawRules.HopeCost(40f, P), 1e-5f);  // 0.015×(30+20)
        }

        [Test]
        public void IsOverstaying_BeyondDiminishingTime()
        {
            Assert.IsFalse(MartialLawRules.IsOverstaying(30f, P)); // ちょうど＝まだ
            Assert.IsTrue(MartialLawRules.IsOverstaying(31f, P));
        }

        [Test]
        public void ShouldLift_WhenCalmOrOverstaying()
        {
            Assert.IsTrue(MartialLawRules.ShouldLift(0.1f, 5f, P));   // 鎮静した＝用済み
            Assert.IsTrue(MartialLawRules.ShouldLift(0.8f, 31f, P));  // 続けるだけ損
            Assert.IsFalse(MartialLawRules.ShouldLift(0.5f, 10f, P)); // まだ仕事中
        }
    }
}
