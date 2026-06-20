using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>IntelOperationRules の EditMode テスト。</summary>
    public class IntelOperationRulesTests
    {
        private IntelOperationRules.Params P => IntelOperationRules.Params.Default;

        [Test]
        public void Skill_RaisesSuccess()
        {
            float low = IntelOperationRules.SuccessChance(0.1f, 0f, P);
            float high = IntelOperationRules.SuccessChance(0.9f, 0f, P);
            Assert.Greater(high, low);
        }

        [Test]
        public void CounterIntel_LowersSuccess()
        {
            float low = IntelOperationRules.SuccessChance(0.5f, 0.1f, P);
            float high = IntelOperationRules.SuccessChance(0.5f, 0.9f, P);
            Assert.Greater(low, high);
        }

        [Test]
        public void SuccessChance_Clamped()
        {
            // 防諜過大でも 0 未満にならない。
            float lo = IntelOperationRules.SuccessChance(0f, 1f, P);
            Assert.GreaterOrEqual(lo, 0f);
            // 技量過大でも 1 を超えない。
            float hi = IntelOperationRules.SuccessChance(10f, 0f, P);
            Assert.LessOrEqual(hi, 1f);
        }

        [Test]
        public void IntelGain_ProportionalToSuccess()
        {
            float low = IntelOperationRules.IntelGain(0.2f, 100f, P);
            float high = IntelOperationRules.IntelGain(0.8f, 100f, P);
            Assert.Greater(high, low);
            Assert.AreEqual(20f, low, 1e-5f);
            Assert.AreEqual(80f, high, 1e-5f);
        }

        [Test]
        public void IntelGain_ProportionalToValue()
        {
            float small = IntelOperationRules.IntelGain(0.5f, 10f, P);
            float big = IntelOperationRules.IntelGain(0.5f, 100f, P);
            Assert.Greater(big, small);
        }

        [Test]
        public void IntelGain_ZeroOnNegativeValue()
        {
            Assert.AreEqual(0f, IntelOperationRules.IntelGain(1f, -50f, P), 1e-5f);
        }

        [Test]
        public void DecayIntel_Reduces()
        {
            float next = IntelOperationRules.DecayIntel(1f, 1f, P); // -0.1
            Assert.AreEqual(0.9f, next, 1e-5f);
            Assert.Less(next, 1f);
        }

        [Test]
        public void DecayIntel_FloorsAtZero()
        {
            float next = IntelOperationRules.DecayIntel(0.05f, 100f, P);
            Assert.AreEqual(0f, next, 1e-5f);
        }

        [Test]
        public void Deterministic()
        {
            float a = IntelOperationRules.SuccessChance(0.7f, 0.3f, P);
            float b = IntelOperationRules.SuccessChance(0.7f, 0.3f, P);
            Assert.AreEqual(a, b, 1e-5f);
        }

        [Test]
        public void DefaultOverloads_MatchExplicit()
        {
            Assert.AreEqual(
                IntelOperationRules.SuccessChance(0.5f, 0.5f, IntelOperationRules.Params.Default),
                IntelOperationRules.SuccessChance(0.5f, 0.5f), 1e-5f);
            Assert.AreEqual(
                IntelOperationRules.IntelGain(0.5f, 50f, IntelOperationRules.Params.Default),
                IntelOperationRules.IntelGain(0.5f, 50f), 1e-5f);
            Assert.AreEqual(
                IntelOperationRules.DecayIntel(1f, 1f, IntelOperationRules.Params.Default),
                IntelOperationRules.DecayIntel(1f, 1f), 1e-5f);
        }
    }
}
