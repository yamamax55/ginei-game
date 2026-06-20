using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>WarWearinessModifiersRules（厭戦の修飾 DIPLO #2119／#192 拡張）の純ロジック検証。</summary>
    public class WarWearinessModifiersRulesTests
    {
        private static WarGoalRules.WarGoalParams P => WarGoalRules.WarGoalParams.Default;

        private static WarState War(int turns, float casualties)
            => new WarState("A", "B") { turnsAtWar = turns, casualties = casualties };

        [Test]
        public void Null_ReturnsZero()
        {
            Assert.AreEqual(0f, WarWearinessModifiersRules.AdjustedWeariness(null, 1f, 1f));
        }

        [Test]
        public void HealthyHome_EqualsBaseWeariness()
        {
            // 民心・兵糧十分・短期戦＝修飾なしで基礎厭戦と一致。
            var w = War(5, 0.2f); // 5*0.02 + 0.2*0.6 = 0.22
            float expected = WarGoalRules.WarWeariness(5, 0.2f, P);
            float got = WarWearinessModifiersRules.AdjustedWeariness(w, 1f, 1f);
            Assert.AreEqual(expected, got, 1e-5f);
            Assert.AreEqual(0.22f, got, 1e-5f);
        }

        [Test]
        public void LowSupport_AddsPenalty()
        {
            var w = War(5, 0.2f);
            float baseline = WarWearinessModifiersRules.AdjustedWeariness(w, 1f, 1f);
            float low = WarWearinessModifiersRules.AdjustedWeariness(w, 0.2f, 1f);
            Assert.Greater(low, baseline);
            Assert.AreEqual(baseline + 0.10f, low, 1e-5f);
        }

        [Test]
        public void LowFood_AddsPenalty()
        {
            var w = War(5, 0.2f);
            float baseline = WarWearinessModifiersRules.AdjustedWeariness(w, 1f, 1f);
            float low = WarWearinessModifiersRules.AdjustedWeariness(w, 1f, 0.1f);
            Assert.AreEqual(baseline + 0.15f, low, 1e-5f);
        }

        [Test]
        public void LongWar_AddsPerTurnPenalty()
        {
            // 15ターン＝基礎 15*0.02+0.12=0.42、長期 (15-10)*0.05=0.25 → 0.67。
            var w = War(15, 0.2f);
            float got = WarWearinessModifiersRules.AdjustedWeariness(w, 1f, 1f);
            Assert.AreEqual(0.67f, got, 1e-5f);
        }

        [Test]
        public void Result_ClampedToOne()
        {
            var w = War(40, 1f); // 大損害・超長期＋低支持・低兵糧
            float got = WarWearinessModifiersRules.AdjustedWeariness(w, 0f, 0f);
            Assert.LessOrEqual(got, 1f);
            Assert.AreEqual(1f, got, 1e-5f);
        }
    }
}
