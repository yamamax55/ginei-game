using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>CorruptionRules（汚職・中抜き・行政効率）の EditMode テスト。</summary>
    public class CorruptionRulesTests
    {
        private const float Tol = 1e-5f;

        [Test]
        public void SkimRate_IsDeterministic()
        {
            float a = CorruptionRules.SkimRate(0.5f, 0.2f);
            float b = CorruptionRules.SkimRate(0.5f, 0.2f);
            Assert.AreEqual(a, b, Tol);
        }

        [Test]
        public void SkimRate_RisesWithCorruption()
        {
            float low = CorruptionRules.SkimRate(0.2f, 0f);
            float high = CorruptionRules.SkimRate(0.8f, 0f);
            Assert.Less(low, high);
        }

        [Test]
        public void SkimRate_OversightReducesSkim()
        {
            float noOversight = CorruptionRules.SkimRate(0.8f, 0f);
            float withOversight = CorruptionRules.SkimRate(0.8f, 0.5f);
            Assert.Less(withOversight, noOversight);
        }

        [Test]
        public void SkimRate_ClampedToMaxSkim()
        {
            var p = CorruptionRules.Params.Default;
            // 腐敗最大・監督ゼロでも maxSkim を超えない
            float skim = CorruptionRules.SkimRate(1f, 0f, p);
            Assert.AreEqual(p.maxSkim, skim, Tol);
            Assert.LessOrEqual(skim, p.maxSkim);
        }

        [Test]
        public void SkimRate_ClampedAtZero()
        {
            // 監督が腐敗を上回っても負にならない
            float skim = CorruptionRules.SkimRate(0.1f, 1f);
            Assert.AreEqual(0f, skim, Tol);
        }

        [Test]
        public void NetRevenue_DecreasesWithCorruption()
        {
            float clean = CorruptionRules.NetRevenue(1000f, 0.1f, 0f);
            float dirty = CorruptionRules.NetRevenue(1000f, 0.9f, 0f);
            Assert.Greater(clean, dirty);
        }

        [Test]
        public void NetRevenue_ZeroCorruption_FullRevenue()
        {
            float net = CorruptionRules.NetRevenue(1000f, 0f, 0f);
            Assert.AreEqual(1000f, net, Tol);
        }

        [Test]
        public void AdminEfficiency_DecreasesWithCorruption()
        {
            float low = CorruptionRules.AdminEfficiency(0.2f);
            float high = CorruptionRules.AdminEfficiency(0.8f);
            Assert.Greater(low, high);
        }

        [Test]
        public void AdminEfficiency_ZeroCorruption_IsOne()
        {
            Assert.AreEqual(1f, CorruptionRules.AdminEfficiency(0f), Tol);
        }

        [Test]
        public void AdminEfficiency_ClampedToZero()
        {
            // ペナルティが強く腐敗最大でも 0 を下回らない
            var p = new CorruptionRules.Params(0.4f, 5f, 0.8f);
            float eff = CorruptionRules.AdminEfficiency(1f, p);
            Assert.AreEqual(0f, eff, Tol);
            Assert.GreaterOrEqual(eff, 0f);
        }
    }
}
