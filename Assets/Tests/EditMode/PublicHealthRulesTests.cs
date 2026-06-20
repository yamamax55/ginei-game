using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>公衆衛生（PublicHealthRules）の純ロジック検証。</summary>
    public class PublicHealthRulesTests
    {
        const float Tol = 1e-5f;

        [Test]
        public void HealthLevel_ZeroSpend_IsZero()
        {
            Assert.AreEqual(0f, PublicHealthRules.HealthLevel(0f), Tol);
        }

        [Test]
        public void HealthLevel_RisesWithSpend()
        {
            float low = PublicHealthRules.HealthLevel(0.5f);
            float high = PublicHealthRules.HealthLevel(2f);
            Assert.Greater(high, low);
        }

        [Test]
        public void HealthLevel_DiminishingReturns()
        {
            // 等量の追加支出ほど伸び幅が小さくなる（逓減）。
            float d1 = PublicHealthRules.HealthLevel(1f) - PublicHealthRules.HealthLevel(0f);
            float d2 = PublicHealthRules.HealthLevel(2f) - PublicHealthRules.HealthLevel(1f);
            Assert.Greater(d1, d2);
        }

        [Test]
        public void HealthLevel_Clamped01()
        {
            Assert.GreaterOrEqual(PublicHealthRules.HealthLevel(1000f), 0f);
            Assert.LessOrEqual(PublicHealthRules.HealthLevel(1000f), 1f);
            // 負の支出も 0..1 内（0）。
            Assert.AreEqual(0f, PublicHealthRules.HealthLevel(-5f), Tol);
        }

        [Test]
        public void HealthLevel_Deterministic()
        {
            Assert.AreEqual(
                PublicHealthRules.HealthLevel(1.3f),
                PublicHealthRules.HealthLevel(1.3f), Tol);
        }

        [Test]
        public void Productivity_ZeroHealth_IsOne()
        {
            Assert.AreEqual(1f, PublicHealthRules.ProductivityFactor(0f), Tol);
        }

        [Test]
        public void Productivity_AtLeastOne_AndRisesWithHealth()
        {
            float low = PublicHealthRules.ProductivityFactor(0.2f);
            float high = PublicHealthRules.ProductivityFactor(0.9f);
            Assert.GreaterOrEqual(low, 1f);
            Assert.Greater(high, low);
        }

        [Test]
        public void Productivity_HealthClamped()
        {
            // health01 > 1 でも上限健康として扱う。
            Assert.AreEqual(
                PublicHealthRules.ProductivityFactor(1f),
                PublicHealthRules.ProductivityFactor(5f), Tol);
        }

        [Test]
        public void Mortality_ZeroHealth_IsOne()
        {
            Assert.AreEqual(1f, PublicHealthRules.MortalityFactor(0f), Tol);
        }

        [Test]
        public void Mortality_AtMostOne_AndFallsWithHealth()
        {
            float low = PublicHealthRules.MortalityFactor(0.2f);
            float high = PublicHealthRules.MortalityFactor(0.9f);
            Assert.LessOrEqual(low, 1f);
            Assert.Less(high, low);
        }

        [Test]
        public void Mortality_Clamped01()
        {
            float m = PublicHealthRules.MortalityFactor(5f); // health clamp + 結果 clamp
            Assert.GreaterOrEqual(m, 0f);
            Assert.LessOrEqual(m, 1f);
        }

        [Test]
        public void DefaultOverloads_MatchExplicitParams()
        {
            var p = PublicHealthRules.Params.Default;
            Assert.AreEqual(
                PublicHealthRules.HealthLevel(1.1f, p),
                PublicHealthRules.HealthLevel(1.1f), Tol);
            Assert.AreEqual(
                PublicHealthRules.ProductivityFactor(0.7f, p),
                PublicHealthRules.ProductivityFactor(0.7f), Tol);
            Assert.AreEqual(
                PublicHealthRules.MortalityFactor(0.7f, p),
                PublicHealthRules.MortalityFactor(0.7f), Tol);
        }
    }
}
