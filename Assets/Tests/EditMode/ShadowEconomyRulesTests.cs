using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ShadowEconomyRules の EditMode テスト。</summary>
    public class ShadowEconomyRulesTests
    {
        [Test]
        public void Determinism_SameInputSameOutput()
        {
            float a = ShadowEconomyRules.BlackMarketShare(0.4f, 0.3f, 0.2f);
            float b = ShadowEconomyRules.BlackMarketShare(0.4f, 0.3f, 0.2f);
            Assert.AreEqual(a, b, 1e-5f);
        }

        [Test]
        public void BlackMarketShare_RisesWithTax()
        {
            float low = ShadowEconomyRules.BlackMarketShare(0.1f, 0.2f, 0.1f);
            float high = ShadowEconomyRules.BlackMarketShare(0.9f, 0.2f, 0.1f);
            Assert.Greater(high, low);
        }

        [Test]
        public void BlackMarketShare_RisesWithScarcity()
        {
            float low = ShadowEconomyRules.BlackMarketShare(0.3f, 0.1f, 0.1f);
            float high = ShadowEconomyRules.BlackMarketShare(0.3f, 0.9f, 0.1f);
            Assert.Greater(high, low);
        }

        [Test]
        public void BlackMarketShare_FallsWithEnforcement()
        {
            float low = ShadowEconomyRules.BlackMarketShare(0.6f, 0.5f, 0.1f);
            float high = ShadowEconomyRules.BlackMarketShare(0.6f, 0.5f, 0.9f);
            Assert.Greater(low, high);
        }

        [Test]
        public void BlackMarketShare_ClampedToMaxShare()
        {
            float v = ShadowEconomyRules.BlackMarketShare(1f, 1f, 0f);
            Assert.LessOrEqual(v, ShadowEconomyRules.Params.Default.maxShare + 1e-5f);
        }

        [Test]
        public void BlackMarketShare_ClampedToZero()
        {
            // 強力な取締りで負にならずゼロ止まり
            float v = ShadowEconomyRules.BlackMarketShare(0f, 0f, 1f);
            Assert.AreEqual(0f, v, 1e-5f);
        }

        [Test]
        public void TaxableBase_ShrinksWithShare()
        {
            float full = ShadowEconomyRules.TaxableBase(1000f, 0f);
            float partial = ShadowEconomyRules.TaxableBase(1000f, 0.3f);
            Assert.AreEqual(1000f, full, 1e-5f);
            Assert.AreEqual(700f, partial, 1e-5f);
            Assert.Greater(full, partial);
        }

        [Test]
        public void TaxableBase_ClampsShare()
        {
            // share>1 でも負のベースにならない
            Assert.AreEqual(0f, ShadowEconomyRules.TaxableBase(500f, 2f), 1e-5f);
            Assert.AreEqual(500f, ShadowEconomyRules.TaxableBase(500f, -1f), 1e-5f);
        }

        [Test]
        public void EnforcementCost_Proportional()
        {
            Assert.AreEqual(20f, ShadowEconomyRules.EnforcementCost(0.5f, 40f), 1e-5f);
            float c1 = ShadowEconomyRules.EnforcementCost(0.2f, 100f);
            float c2 = ShadowEconomyRules.EnforcementCost(0.8f, 100f);
            Assert.Greater(c2, c1);
        }

        [Test]
        public void EnforcementCost_NonNegative()
        {
            Assert.AreEqual(0f, ShadowEconomyRules.EnforcementCost(-1f, 50f), 1e-5f);
            Assert.AreEqual(0f, ShadowEconomyRules.EnforcementCost(0.5f, -50f), 1e-5f);
        }
    }
}
