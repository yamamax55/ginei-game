using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 軍の質（技術×練度×訓練）→戦力倍率（ForceQualityFactorRules.QualityFactor）を固定する。
    /// 各入力増で質↑・全0で下限・全高で上限・各入力に単調非減少。委譲先（VeterancyRules/SkillEffectRules）整合。
    /// </summary>
    public class ForceQualityFactorRulesTests
    {
        [Test]
        public void QualityFactor_AllZero_AtFloor()
        {
            var p = ForceQualityParams.Default;
            float q = ForceQualityFactorRules.QualityFactor(0f, 0f, 0f, p);
            Assert.AreEqual(p.floor, q, 1e-3f);
        }

        [Test]
        public void QualityFactor_AllMax_AtCeiling()
        {
            var p = ForceQualityParams.Default;
            // techLevel を基準段以上にすれば技術スコアは満点。練度/訓練は 1。
            float q = ForceQualityFactorRules.QualityFactor(p.techReference, 1f, 1f, p);
            Assert.AreEqual(p.ceiling, q, 1e-3f);
        }

        [Test]
        public void QualityFactor_StaysWithinBounds()
        {
            var p = ForceQualityParams.Default;
            // 入力が範囲外（超過/負）でも floor..ceiling に収まる。
            float over = ForceQualityFactorRules.QualityFactor(9999f, 5f, 5f, p);
            float under = ForceQualityFactorRules.QualityFactor(-10f, -3f, -3f, p);
            Assert.LessOrEqual(over, p.ceiling + 1e-4f);
            Assert.GreaterOrEqual(over, p.floor - 1e-4f);
            Assert.AreEqual(p.floor, under, 1e-3f);
        }

        [Test]
        public void QualityFactor_TechIncrease_RaisesQuality()
        {
            var p = ForceQualityParams.Default;
            float low = ForceQualityFactorRules.QualityFactor(0f, 0.5f, 0.5f, p);
            float high = ForceQualityFactorRules.QualityFactor(p.techReference, 0.5f, 0.5f, p);
            Assert.Greater(high, low);
        }

        [Test]
        public void QualityFactor_VeterancyIncrease_RaisesQuality()
        {
            var p = ForceQualityParams.Default;
            float low = ForceQualityFactorRules.QualityFactor(5f, 0f, 0.5f, p);
            float high = ForceQualityFactorRules.QualityFactor(5f, 1f, 0.5f, p);
            Assert.Greater(high, low);
        }

        [Test]
        public void QualityFactor_TrainingIncrease_RaisesQuality_AndMonotonic()
        {
            var p = ForceQualityParams.Default;
            float a = ForceQualityFactorRules.QualityFactor(5f, 0.5f, 0.0f, p);
            float b = ForceQualityFactorRules.QualityFactor(5f, 0.5f, 0.5f, p);
            float c = ForceQualityFactorRules.QualityFactor(5f, 0.5f, 1.0f, p);
            Assert.Greater(b, a);
            Assert.Greater(c, b); // 単調非減少（厳密増加で確認）
        }
    }
}
