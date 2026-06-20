using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>AgriculturalOutputRules の EditMode テスト。</summary>
    public class AgriculturalOutputRulesTests
    {
        private const float Tol = 1e-5f;

        [Test]
        public void FoodOutput_Default_BaselineFormula()
        {
            // 既定: landYield1, techWeight0.5, weatherSwing0.4
            // 耕地10, 技術0, 天候1(良好) → 10*1*(1)*(1) = 10
            Assert.AreEqual(10f, AgriculturalOutputRules.FoodOutput(10f, 0f, 1f), Tol);
        }

        [Test]
        public void FoodOutput_RisesWithLand()
        {
            float a = AgriculturalOutputRules.FoodOutput(10f, 0.5f, 1f);
            float b = AgriculturalOutputRules.FoodOutput(20f, 0.5f, 1f);
            Assert.Greater(b, a);
        }

        [Test]
        public void FoodOutput_RisesWithTech()
        {
            float low = AgriculturalOutputRules.FoodOutput(10f, 0.2f, 1f);
            float high = AgriculturalOutputRules.FoodOutput(10f, 0.9f, 1f);
            Assert.Greater(high, low);
        }

        [Test]
        public void FoodOutput_BadWeatherReducesOutput()
        {
            float good = AgriculturalOutputRules.FoodOutput(10f, 0.3f, 1f);
            float bad = AgriculturalOutputRules.FoodOutput(10f, 0.3f, 0f);
            Assert.Less(bad, good);
            // weather0 → 1-0.4*(1-0) = 0.6 倍
            float techFactor = 1f + 0.3f * 0.5f;
            Assert.AreEqual(10f * techFactor * 0.6f, bad, Tol);
        }

        [Test]
        public void FoodOutput_NeverNegative()
        {
            // 負の耕地でも0以上
            Assert.GreaterOrEqual(AgriculturalOutputRules.FoodOutput(-5f, 0.5f, 1f), 0f);
            // weather超過もclampで安全
            Assert.GreaterOrEqual(AgriculturalOutputRules.FoodOutput(10f, 0f, -3f), 0f);
        }

        [Test]
        public void FoodOutput_Deterministic()
        {
            float a = AgriculturalOutputRules.FoodOutput(7f, 0.4f, 0.6f);
            float b = AgriculturalOutputRules.FoodOutput(7f, 0.4f, 0.6f);
            Assert.AreEqual(a, b, Tol);
        }

        [Test]
        public void FamineSeverity_ZeroWhenSurplus()
        {
            Assert.AreEqual(0f, AgriculturalOutputRules.FamineSeverity(100f, 80f), Tol);
            Assert.AreEqual(0f, AgriculturalOutputRules.FamineSeverity(80f, 80f), Tol);
        }

        [Test]
        public void FamineSeverity_PositiveWhenDeficit()
        {
            // 需要100, 生産60 → (100-60)/100 = 0.4
            Assert.AreEqual(0.4f, AgriculturalOutputRules.FamineSeverity(60f, 100f), Tol);
        }

        [Test]
        public void FamineSeverity_Clamped()
        {
            // 生産0 → 1.0 上限
            Assert.AreEqual(1f, AgriculturalOutputRules.FamineSeverity(0f, 100f), Tol);
            // 生産が負でも上限1
            Assert.AreEqual(1f, AgriculturalOutputRules.FamineSeverity(-50f, 100f), Tol);
        }

        [Test]
        public void FamineSeverity_ZeroDemandGuarded()
        {
            // 需要0は飢饉なし（生産>=需要）
            Assert.AreEqual(0f, AgriculturalOutputRules.FamineSeverity(0f, 0f), Tol);
            Assert.AreEqual(0f, AgriculturalOutputRules.FamineSeverity(10f, 0f), Tol);
        }

        [Test]
        public void IsFamine_ThresholdBehavior()
        {
            // severity 0.4
            Assert.IsTrue(AgriculturalOutputRules.IsFamine(60f, 100f, 0.3f));
            Assert.IsFalse(AgriculturalOutputRules.IsFamine(60f, 100f, 0.4f)); // 厳密超過なので等しいとfalse
            Assert.IsFalse(AgriculturalOutputRules.IsFamine(60f, 100f, 0.5f));
        }
    }
}
