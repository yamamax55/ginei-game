using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>索敵と不意打ち：情報→索敵範囲、発見判定、不意打ち倍率。</summary>
    public class DetectionRulesTests
    {
        [Test]
        public void DetectionRange_ScalesWithIntelligence()
        {
            Assert.AreEqual(DetectionRules.BaseDetectionRange, DetectionRules.DetectionRange(50f), 1e-3f);       // 中庸＝基準
            Assert.AreEqual(DetectionRules.BaseDetectionRange * 1.5f, DetectionRules.DetectionRange(100f), 1e-3f); // 高情報＝広い
            Assert.AreEqual(DetectionRules.BaseDetectionRange * 0.5f, DetectionRules.DetectionRange(0f), 1e-3f);   // 低情報＝狭い
        }

        [Test]
        public void IsDetected_WithinRange()
        {
            Assert.IsTrue(DetectionRules.IsDetected(10f, 18f));
            Assert.IsFalse(DetectionRules.IsDetected(20f, 18f));
        }

        [Test]
        public void AttackFactor_AmbushWhenConcealed()
        {
            Assert.AreEqual(DetectionRules.AmbushDamageFactor, DetectionRules.AttackFactor(true), 1e-4f);  // 未発見＝不意打ち
            Assert.AreEqual(1f, DetectionRules.AttackFactor(false), 1e-4f);                                 // 発見済み＝等倍
        }
    }
}
