using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ADM-1 運営・情報の会戦活用：継戦/再編（運営）・索敵/不意打ち耐性（情報）。</summary>
    public class CommandAcumenRulesTests
    {
        [Test]
        public void OperationFactors_50IsNeutral()
        {
            Assert.AreEqual(1.0f, CommandAcumenRules.SustainmentFactor(50f), 1e-4f);
            Assert.AreEqual(1.25f, CommandAcumenRules.SustainmentFactor(100f), 1e-4f);
            Assert.AreEqual(0.75f, CommandAcumenRules.SustainmentFactor(0f), 1e-4f);
            Assert.AreEqual(1.25f, CommandAcumenRules.ReformSpeedFactor(100f), 1e-4f);
        }

        [Test]
        public void IntelligenceFactors_DetectionAndAmbush()
        {
            Assert.AreEqual(1.0f, CommandAcumenRules.DetectionRangeFactor(50f), 1e-4f);
            Assert.AreEqual(1.5f, CommandAcumenRules.DetectionRangeFactor(100f), 1e-4f);
            Assert.AreEqual(0.5f, CommandAcumenRules.DetectionRangeFactor(0f), 1e-4f);

            Assert.AreEqual(0f, CommandAcumenRules.AmbushResistance(50f), 1e-4f);
            Assert.AreEqual(1f, CommandAcumenRules.AmbushResistance(100f), 1e-4f);
            Assert.AreEqual(0.5f, CommandAcumenRules.AmbushResistance(75f), 1e-4f);
            Assert.AreEqual(0f, CommandAcumenRules.AmbushResistance(0f), 1e-4f); // 50未満は0

            // 明察な提督は不意打ち倍率を 1.0 へ近づける。
            Assert.AreEqual(1.0f, CommandAcumenRules.ResolveAmbushFactor(1.3f, 100f), 1e-4f);
            Assert.AreEqual(1.3f, CommandAcumenRules.ResolveAmbushFactor(1.3f, 50f), 1e-4f);
            Assert.AreEqual(1.15f, CommandAcumenRules.ResolveAmbushFactor(1.3f, 75f), 1e-4f);
        }
    }
}
