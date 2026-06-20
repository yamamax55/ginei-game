using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>InfrastructureTickRules（社会基盤の進行 内政・経済の素地）の純ロジック検証。</summary>
    public class InfrastructureTickRulesTests
    {
        private static InfrastructureTickRules.Params P => InfrastructureTickRules.Params.Default;

        [Test]
        public void Tick_NoInvestment_Decays()
        {
            var s = new InfrastructureState(0.5f);
            float after = InfrastructureTickRules.Tick(s, 0f, P);
            Assert.Less(after, 0.5f);
            Assert.AreEqual(0.5f - P.decayPerTick, after, 1e-5f);
        }

        [Test]
        public void Tick_Investment_RaisesNetOfDecay()
        {
            var s = new InfrastructureState(0.5f);
            // 投資100 → +100*0.001=0.1、劣化-0.005 → 0.595
            float after = InfrastructureTickRules.Tick(s, 100f, P);
            Assert.AreEqual(0.5f + 100f * P.investEfficiency - P.decayPerTick, after, 1e-5f);
            Assert.Greater(after, 0.5f);
        }

        [Test]
        public void Tick_ClampedToUnitInterval()
        {
            var low = new InfrastructureState(0.001f);
            Assert.AreEqual(0f, InfrastructureTickRules.Tick(low, 0f, P), 1e-6f);
            var high = new InfrastructureState(0.99f);
            float after = InfrastructureTickRules.Tick(high, 100000f, P);
            Assert.LessOrEqual(after, 1f);
            Assert.AreEqual(1f, after, 1e-6f);
        }

        [Test]
        public void Tick_Null_Zero()
        {
            Assert.AreEqual(0f, InfrastructureTickRules.Tick(null, 100f, P));
        }

        [Test]
        public void OutputFactor_MonotonicInLevel()
        {
            float lo = InfrastructureTickRules.OutputFactor(0f, P);
            float mid = InfrastructureTickRules.OutputFactor(0.5f, P);
            float hi = InfrastructureTickRules.OutputFactor(1f, P);
            Assert.AreEqual(P.minOutputFactor, lo, 1e-5f);
            Assert.AreEqual(P.maxOutputFactor, hi, 1e-5f);
            Assert.Greater(mid, lo);
            Assert.Less(mid, hi);
        }
    }
}
