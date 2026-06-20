using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ProvincialAutonomyRules の EditMode テスト。</summary>
    public class ProvincialAutonomyRulesTests
    {
        [Test]
        public void Determinism_SameInputSameOutput()
        {
            float a = ProvincialAutonomyRules.LocalUnrest(0.7f, 0.5f);
            float b = ProvincialAutonomyRules.LocalUnrest(0.7f, 0.5f);
            Assert.AreEqual(a, b, 1e-5f);
        }

        [Test]
        public void LocalUnrest_RisesWithControl_WhenCultureNonZero()
        {
            float low = ProvincialAutonomyRules.LocalUnrest(0.2f, 0.8f);
            float high = ProvincialAutonomyRules.LocalUnrest(0.9f, 0.8f);
            Assert.Greater(high, low);
        }

        [Test]
        public void LocalUnrest_CulturalDistanceAmplifies()
        {
            float near = ProvincialAutonomyRules.LocalUnrest(0.8f, 0.1f);
            float far = ProvincialAutonomyRules.LocalUnrest(0.8f, 0.9f);
            Assert.Greater(far, near);
        }

        [Test]
        public void LocalUnrest_NoCultureMeansBaseOnly()
        {
            // 文化的距離0なら統制に依らず素の不満のみ
            float v = ProvincialAutonomyRules.LocalUnrest(1.0f, 0.0f);
            Assert.AreEqual(ProvincialAutonomyRules.Params.Default.baseUnrest, v, 1e-5f);
        }

        [Test]
        public void LocalUnrest_Clamped01()
        {
            float v = ProvincialAutonomyRules.LocalUnrest(5f, 5f);
            Assert.LessOrEqual(v, 1f);
            Assert.GreaterOrEqual(v, 0f);
        }

        [Test]
        public void AdminReach_RisesWithControl()
        {
            float low = ProvincialAutonomyRules.AdminReach(0.2f, 0.3f);
            float high = ProvincialAutonomyRules.AdminReach(0.9f, 0.3f);
            Assert.Greater(high, low);
        }

        [Test]
        public void AdminReach_FallsWithDistance()
        {
            float near = ProvincialAutonomyRules.AdminReach(0.8f, 0.1f);
            float far = ProvincialAutonomyRules.AdminReach(0.8f, 0.9f);
            Assert.Greater(near, far);
        }

        [Test]
        public void AdminReach_Clamped01()
        {
            float v = ProvincialAutonomyRules.AdminReach(10f, -5f);
            Assert.LessOrEqual(v, 1f);
            Assert.GreaterOrEqual(v, 0f);
            // 距離1で到達ゼロ
            Assert.AreEqual(0f, ProvincialAutonomyRules.AdminReach(1f, 1f), 1e-5f);
        }

        [Test]
        public void Autonomy_ComplementaryToControl()
        {
            Assert.AreEqual(0.3f, ProvincialAutonomyRules.Autonomy(0.7f), 1e-5f);
            Assert.AreEqual(1f, ProvincialAutonomyRules.Autonomy(0f), 1e-5f);
            Assert.AreEqual(0f, ProvincialAutonomyRules.Autonomy(1f), 1e-5f);
        }

        [Test]
        public void Autonomy_ClampsControlInput()
        {
            Assert.AreEqual(0f, ProvincialAutonomyRules.Autonomy(2f), 1e-5f);
            Assert.AreEqual(1f, ProvincialAutonomyRules.Autonomy(-2f), 1e-5f);
        }
    }
}
