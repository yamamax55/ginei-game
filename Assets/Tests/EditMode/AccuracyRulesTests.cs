using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>命中・回避：命中率（拮抗0.75・クランプ）と命中倍率（命中1.0/外れかすり）。</summary>
    public class AccuracyRulesTests
    {
        [Test]
        public void HitChance_ParityAndClamp()
        {
            Assert.AreEqual(AccuracyRules.BaseHit, AccuracyRules.HitChance(50f, 50f), 1e-4f); // 拮抗
            Assert.AreEqual(AccuracyRules.MaxHit, AccuracyRules.HitChance(100f, 0f), 1e-4f);  // 精度圧倒→上限
            Assert.AreEqual(AccuracyRules.MinHit, AccuracyRules.HitChance(0f, 100f), 1e-4f);  // 回避圧倒→下限
            Assert.AreEqual(0.85f, AccuracyRules.HitChance(70f, 50f), 1e-4f);                  // 0.75+20/200
        }

        [Test]
        public void HitFactor_HitOrGraze()
        {
            Assert.AreEqual(1f, AccuracyRules.HitFactor(0.8f, 0.5f), 1e-4f);                  // roll≤命中＝命中
            Assert.AreEqual(AccuracyRules.GrazeFactor, AccuracyRules.HitFactor(0.8f, 0.9f), 1e-4f); // 外れ＝かすり
            Assert.AreEqual(1f, AccuracyRules.HitFactor(0.8f, 0.8f), 1e-4f);                  // 境界＝命中
        }
    }
}
