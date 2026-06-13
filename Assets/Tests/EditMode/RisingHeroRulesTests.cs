using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>立身出世（豊臣秀吉）：門地無視の出世・有能ほど最速昇進・人たらし・戦略機動（中国大返し）。</summary>
    public class RisingHeroRulesTests
    {
        [Test]
        public void IgnoresPedigreeCeiling()
        {
            Assert.IsTrue(RisingHeroRules.IgnoresPedigreeCeiling(true));
            Assert.IsFalse(RisingHeroRules.IgnoresPedigreeCeiling(false));
        }

        [Test]
        public void PromotionSpeed_FasterWhenCapable()
        {
            Assert.AreEqual(2.0f, RisingHeroRules.PromotionSpeedFactor(true, 100), 1e-4f); // 有能＝倍速出世
            Assert.AreEqual(1.5f, RisingHeroRules.PromotionSpeedFactor(true, 50), 1e-4f);
            Assert.AreEqual(1.0f, RisingHeroRules.PromotionSpeedFactor(true, 0), 1e-4f);
            Assert.AreEqual(1.0f, RisingHeroRules.PromotionSpeedFactor(false, 100), 1e-4f); // 並は加速なし
        }

        [Test]
        public void Charm_And_ForcedMarch()
        {
            Assert.AreEqual(1.5f, RisingHeroRules.CharmFactor(true, 100), 1e-4f); // 人たらし
            Assert.AreEqual(1.25f, RisingHeroRules.CharmFactor(true, 50), 1e-4f);
            Assert.AreEqual(1.0f, RisingHeroRules.CharmFactor(false, 100), 1e-4f);

            Assert.AreEqual(1.5f, RisingHeroRules.ForcedMarchFactor(true), 1e-4f);  // 中国大返し
            Assert.AreEqual(1.0f, RisingHeroRules.ForcedMarchFactor(false), 1e-4f);
        }
    }
}
