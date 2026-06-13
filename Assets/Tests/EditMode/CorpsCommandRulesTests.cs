using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>軍団長のバフ/デバフ：統率で軍団全体の能力・士気を底上げ/低下（中央50で等倍・クランプ）。</summary>
    public class CorpsCommandRulesTests
    {
        [Test]
        public void AbilityFactor_BuffsHighDebuffsLow()
        {
            Assert.AreEqual(1.0f, CorpsCommandRules.AbilityFactor(50f), 1e-4f);   // 中庸＝等倍
            Assert.AreEqual(1.2f, CorpsCommandRules.AbilityFactor(100f), 1e-4f);  // 名将＝+20%
            Assert.AreEqual(0.8f, CorpsCommandRules.AbilityFactor(0f), 1e-4f);    // 無能＝-20%
            Assert.AreEqual(1.1f, CorpsCommandRules.AbilityFactor(75f), 1e-4f);
        }

        [Test]
        public void MoraleFactor_SameShapeAsAbility()
        {
            Assert.AreEqual(1.0f, CorpsCommandRules.MoraleFactor(50f), 1e-4f);
            Assert.AreEqual(1.2f, CorpsCommandRules.MoraleFactor(100f), 1e-4f);
            Assert.AreEqual(0.8f, CorpsCommandRules.MoraleFactor(0f), 1e-4f);
        }

        [Test]
        public void Factor_ClampedAndInfluenceParam()
        {
            // 影響幅を変えてもクランプは効く。
            Assert.AreEqual(1.1f, CorpsCommandRules.AbilityFactor(100f, 0.1f), 1e-4f);
            Assert.AreEqual(0.9f, CorpsCommandRules.AbilityFactor(0f, 0.1f), 1e-4f);
            Assert.AreEqual(CorpsCommandRules.LeaderlessFactor, 0.9f, 1e-4f);
        }
    }
}
