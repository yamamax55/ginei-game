using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>特殊指揮：効果仕様とクールダウンの統率短縮。</summary>
    public class ActiveCommandRulesTests
    {
        [Test]
        public void Spec_PerCommand()
        {
            var alpha = ActiveCommandRules.Spec(ActiveCommand.一斉砲撃);
            Assert.AreEqual(1.5f, alpha.attackFactor, 1e-4f);
            Assert.IsFalse(alpha.moraleLock);

            var charge = ActiveCommandRules.Spec(ActiveCommand.突撃);
            Assert.Greater(charge.speedFactor, 1f);        // 速い
            Assert.Greater(charge.damageTakenFactor, 1f);  // 前のめり＝脆い

            var hold = ActiveCommandRules.Spec(ActiveCommand.不退転);
            Assert.IsTrue(hold.moraleLock);                // 敗走しない
            Assert.Less(hold.damageTakenFactor, 1f);       // 堅い
        }

        [Test]
        public void EffectiveCooldown_ShorterWithLeadership()
        {
            float baseCd = ActiveCommandRules.Spec(ActiveCommand.一斉砲撃).cooldown; // 20
            Assert.AreEqual(baseCd, ActiveCommandRules.EffectiveCooldown(ActiveCommand.一斉砲撃, 50f), 1e-3f);    // 中庸＝基準
            Assert.AreEqual(baseCd * 0.7f, ActiveCommandRules.EffectiveCooldown(ActiveCommand.一斉砲撃, 100f), 1e-3f); // 名将＝-30%
            Assert.AreEqual(baseCd * 1.3f, ActiveCommandRules.EffectiveCooldown(ActiveCommand.一斉砲撃, 0f), 1e-3f);   // 無能＝+30%
        }
    }
}
