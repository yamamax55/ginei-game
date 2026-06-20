using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    public class SecurityGuaranteeRulesTests
    {
        const float Eps = 1e-5f;

        [Test]
        public void Deterrence_RisesWithStrength()
        {
            float lo = SecurityGuaranteeRules.DeterrenceValue(0.2f, 0.5f);
            float hi = SecurityGuaranteeRules.DeterrenceValue(0.8f, 0.5f);
            Assert.Greater(hi, lo);
        }

        [Test]
        public void Deterrence_RisesWithCredibility()
        {
            float lo = SecurityGuaranteeRules.DeterrenceValue(0.5f, 0.2f);
            float hi = SecurityGuaranteeRules.DeterrenceValue(0.5f, 0.8f);
            Assert.Greater(hi, lo);
        }

        [Test]
        public void Deterrence_BothMaxed_NormalizesToOne()
        {
            // 重み和正規化により両者最大なら 1
            float v = SecurityGuaranteeRules.DeterrenceValue(1f, 1f);
            Assert.AreEqual(1f, v, Eps);
        }

        [Test]
        public void Deterrence_BothMin_IsZero()
        {
            float v = SecurityGuaranteeRules.DeterrenceValue(0f, 0f);
            Assert.AreEqual(0f, v, Eps);
        }

        [Test]
        public void Deterrence_Clamped()
        {
            float over = SecurityGuaranteeRules.DeterrenceValue(5f, 5f);
            Assert.AreEqual(1f, over, Eps);
            float under = SecurityGuaranteeRules.DeterrenceValue(-5f, -5f);
            Assert.AreEqual(0f, under, Eps);
        }

        [Test]
        public void Deterrence_WeightSumNormalization()
        {
            // 信頼性のみ重み付け＝信頼性だけが結果を決める
            var p = new SecurityGuaranteeRules.Params(0f, 2f);
            float v = SecurityGuaranteeRules.DeterrenceValue(0.1f, 0.7f, p);
            Assert.AreEqual(0.7f, v, Eps);
        }

        [Test]
        public void Deterrence_ZeroWeightSum_IsZero()
        {
            var p = new SecurityGuaranteeRules.Params(0f, 0f);
            float v = SecurityGuaranteeRules.DeterrenceValue(1f, 1f, p);
            Assert.AreEqual(0f, v, Eps);
        }

        [Test]
        public void Hesitation_PositiveWhenDeterred()
        {
            float h = SecurityGuaranteeRules.AggressorHesitation(0.8f, 0.3f);
            Assert.AreEqual(0.5f, h, Eps);
        }

        [Test]
        public void Hesitation_ZeroWhenResolveWins()
        {
            float h = SecurityGuaranteeRules.AggressorHesitation(0.3f, 0.9f);
            Assert.AreEqual(0f, h, Eps); // クランプで負にならない
        }

        [Test]
        public void DetersAggression_Threshold()
        {
            Assert.IsTrue(SecurityGuaranteeRules.DetersAggression(0.6f, 0.5f));
            Assert.IsFalse(SecurityGuaranteeRules.DetersAggression(0.4f, 0.5f));
        }

        [Test]
        public void DetersAggression_Equality_DefersToTrue()
        {
            Assert.IsTrue(SecurityGuaranteeRules.DetersAggression(0.5f, 0.5f));
        }

        [Test]
        public void Deterrence_Deterministic()
        {
            float a = SecurityGuaranteeRules.DeterrenceValue(0.4f, 0.6f);
            float b = SecurityGuaranteeRules.DeterrenceValue(0.4f, 0.6f);
            Assert.AreEqual(a, b, Eps);
        }
    }
}
