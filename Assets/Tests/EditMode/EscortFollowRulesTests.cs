using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    public class EscortFollowRulesTests
    {
        [Test]
        public void EffectiveCatchUpMultiplier_SlowBattleshipStillConvergesOnFlagship()
        {
            Assert.AreEqual(1.05f, EscortFollowRules.EffectiveCatchUpMultiplier(1.3f, 0.7f), 1e-5f);
        }

        [Test]
        public void EffectiveCatchUpMultiplier_PreservesFasterClassDifference()
        {
            Assert.AreEqual(1.3f, EscortFollowRules.EffectiveCatchUpMultiplier(1.3f, 1f), 1e-5f);
            Assert.AreEqual(1.82f, EscortFollowRules.EffectiveCatchUpMultiplier(1.3f, 1.4f), 1e-5f);
        }

        [Test]
        public void EffectiveCatchUpMultiplier_NeverFallsBelowFlagshipSpeed()
        {
            Assert.AreEqual(1f, EscortFollowRules.EffectiveCatchUpMultiplier(0f, 0f, 0f), 1e-5f);
        }
    }
}
