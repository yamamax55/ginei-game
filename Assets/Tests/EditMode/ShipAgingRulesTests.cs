using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦齢を固定する：設計寿命内は性能満額・超過で漸減（下限あり）、維持費は単調増、更新需要の境界艦齢、
    /// 艦隊の兵力加重平均。null安全・境界を担保。
    /// </summary>
    public class ShipAgingRulesTests
    {
        private static readonly ShipAgingParams P = ShipAgingParams.Default;
        // 設計寿命30/低下0.02/下限0.5/維持増0.02/更新閾値0.7

        [Test]
        public void PerformanceFactor_FullWithinDesignLife()
        {
            Assert.AreEqual(1f, ShipAgingRules.PerformanceFactor(0f, P), 1e-5f);
            Assert.AreEqual(1f, ShipAgingRules.PerformanceFactor(30f, P), 1e-5f); // 寿命ちょうど＝満額
        }

        [Test]
        public void PerformanceFactor_DeclinesAfterDesignLife()
        {
            Assert.AreEqual(0.8f, ShipAgingRules.PerformanceFactor(40f, P), 1e-5f);  // 超過10×0.02=−0.2
            Assert.AreEqual(0.5f, ShipAgingRules.PerformanceFactor(55f, P), 1e-5f);  // 超過25＝−0.5＝下限
            Assert.AreEqual(0.5f, ShipAgingRules.PerformanceFactor(200f, P), 1e-5f); // 下限で止まる
        }

        [Test]
        public void UpkeepFactor_MonotonicGrowth()
        {
            Assert.AreEqual(1f, ShipAgingRules.UpkeepFactor(0f, P), 1e-5f);
            Assert.AreEqual(1.6f, ShipAgingRules.UpkeepFactor(30f, P), 1e-5f);  // 寿命内でも手はかかる
            Assert.AreEqual(2f, ShipAgingRules.UpkeepFactor(50f, P), 1e-5f);
        }

        [Test]
        public void NeedsReplacement_AndBoundaryAge()
        {
            // 性能0.7を割る境界＝30+(1−0.7)/0.02=45
            Assert.AreEqual(45f, ShipAgingRules.ReplacementAge(P), 1e-4f);
            Assert.IsFalse(ShipAgingRules.NeedsReplacement(45f, P)); // 境界ちょうど＝0.7＝まだ可
            Assert.IsTrue(ShipAgingRules.NeedsReplacement(46f, P));
            Assert.IsFalse(ShipAgingRules.NeedsReplacement(30f, P));
        }

        [Test]
        public void ReplacementAge_InfiniteWhenUnreachable()
        {
            // 閾値が下限以下＝性能では更新需要が立たない
            var lowBar = new ShipAgingParams(30f, 0.02f, 0.5f, 0.02f, 0.4f);
            Assert.IsTrue(float.IsPositiveInfinity(ShipAgingRules.ReplacementAge(lowBar)));
            // 劣化しない艦も同様
            var eternal = new ShipAgingParams(30f, 0f, 0.5f, 0.02f, 0.7f);
            Assert.IsTrue(float.IsPositiveInfinity(ShipAgingRules.ReplacementAge(eternal)));
        }

        [Test]
        public void FleetPerformanceFactor_StrengthWeighted()
        {
            // 新鋭100隻(1.0)＋老朽100隻(0.8)＝平均0.9
            float f = ShipAgingRules.FleetPerformanceFactor(new[] { 0f, 40f }, new[] { 100f, 100f }, P);
            Assert.AreEqual(0.9f, f, 1e-5f);
            // 偏った兵力は重みが効く
            float skewed = ShipAgingRules.FleetPerformanceFactor(new[] { 0f, 40f }, new[] { 300f, 100f }, P);
            Assert.AreEqual(0.95f, skewed, 1e-5f);
        }

        [Test]
        public void FleetPerformanceFactor_EdgeCases()
        {
            Assert.AreEqual(1f, ShipAgingRules.FleetPerformanceFactor(null, null, P), 1e-5f);
            Assert.AreEqual(1f, ShipAgingRules.FleetPerformanceFactor(new float[0], new float[0], P), 1e-5f);
            // 総兵力0＝補正なし
            Assert.AreEqual(1f, ShipAgingRules.FleetPerformanceFactor(new[] { 40f }, new[] { 0f }, P), 1e-5f);
        }
    }
}
