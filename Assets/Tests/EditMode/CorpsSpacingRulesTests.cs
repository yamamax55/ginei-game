using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>軍団スロット間隔（SPEED-07 残件）：既定は旧式と一致・最小間隔が下限を超えれば広がる・下回れば下限を維持。</summary>
    public class CorpsSpacingRulesTests
    {
        private static readonly float[] Footprints = { 0f, 0.5f, 1f, 2f, 3.5f, 10f };

        [Test]
        public void Defaults_MatchFormerPrivateConstants()
        {
            Assert.AreEqual(7f, CorpsSpacingRules.DefaultMinSpacing);
            Assert.AreEqual(3f, CorpsSpacingRules.FootprintMargin);
        }

        [Test]
        public void Resolve_WithDefault_MatchesFormerFormula()
        {
            foreach (float foot in Footprints)
            {
                // 旧実装：Mathf.Max(7f, 2f * maxFoot + 3f)
                float former = foot * 2f + 3f > 7f ? 2f * foot + 3f : 7f;
                CorpsSpacingResult r = CorpsSpacingRules.Resolve(CorpsSpacingRules.DefaultMinSpacing, foot);
                Assert.AreEqual(former, r.actualSpacing, 1e-6f, "占有半径 " + foot);
                Assert.AreEqual(7f, r.minSpacing);
                Assert.AreEqual(2f * foot + 3f, r.footprintFloor, 1e-6f);
                Assert.AreEqual(foot, r.maxFootprint);
            }
        }

        [Test]
        public void Resolve_MinAboveFloor_WidensActual()
        {
            CorpsSpacingResult r = CorpsSpacingRules.Resolve(14f, 1f);   // 下限 5
            Assert.AreEqual(14f, r.actualSpacing, 1e-6f);
            Assert.IsFalse(r.FootprintBound);
            StringAssert.Contains("最小間隔が効いている", r.Describe());
        }

        [Test]
        public void Resolve_MinBelowFloor_KeepsFloor_AndFurtherLoweringDoesNotChangeActual()
        {
            CorpsSpacingResult a = CorpsSpacingRules.Resolve(7f, 5f);    // 下限 13
            CorpsSpacingResult b = CorpsSpacingRules.Resolve(1f, 5f);
            Assert.AreEqual(13f, a.actualSpacing, 1e-6f);
            Assert.AreEqual(13f, b.actualSpacing, 1e-6f, "下限を割って詰めた");
            Assert.IsTrue(a.FootprintBound);
            Assert.IsTrue(b.FootprintBound);
            Assert.AreEqual(7f, a.minSpacing, "指定値と実間隔を区別して持っていない");
            Assert.AreEqual(1f, b.minSpacing);
            StringAssert.Contains("占有半径の下限が勝つ", b.Describe());
            StringAssert.Contains("指定最小間隔=1", b.Describe());
            StringAssert.Contains("実間隔=13", b.Describe());
        }

        [Test]
        public void Resolve_MinEqualsFloor_IsNotFootprintBound()
        {
            CorpsSpacingResult r = CorpsSpacingRules.Resolve(5f, 1f);
            Assert.AreEqual(5f, r.actualSpacing, 1e-6f);
            Assert.IsFalse(r.FootprintBound);
        }

        [Test]
        public void EffectiveMinSpacing_InvalidFallsBackToDefault()
        {
            Assert.AreEqual(9f, CorpsSpacingRules.EffectiveMinSpacing(9f));
            Assert.AreEqual(0.25f, CorpsSpacingRules.EffectiveMinSpacing(0.25f));
            foreach (float bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.IsFalse(CorpsSpacingRules.IsValidMinSpacing(bad), bad + " を受け付けた");
                Assert.AreEqual(7f, CorpsSpacingRules.EffectiveMinSpacing(bad), bad + " が既定に戻らない");
            }
        }
    }
}
