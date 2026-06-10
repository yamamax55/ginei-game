using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 摂政・幼君を固定する：幼君の正統性割引（年齢で回復）、摂政の実権の時間成長（成人で停止）、
    /// 簒奪誘惑（実権×野心×成人接近）、円満返上の確率（roll決定論）。境界を担保。
    /// </summary>
    public class RegencyRulesTests
    {
        private static readonly RegencyParams P = RegencyParams.Default;
        // 成人18/割引0.5/成長0.02/簒奪閾値0.6

        [Test]
        public void NeedsRegent_UnderAdultAge()
        {
            Assert.IsTrue(RegencyRules.NeedsRegent(5, P));
            Assert.IsTrue(RegencyRules.NeedsRegent(17, P));
            Assert.IsFalse(RegencyRules.NeedsRegent(18, P));
        }

        [Test]
        public void EffectiveLegitimacy_DiscountFadesWithAge()
        {
            // 0歳＝最大割引：1×(1−0.5)=0.5
            Assert.AreEqual(0.5f, RegencyRules.EffectiveLegitimacy(1f, 0, P), 1e-5f);
            // 9歳＝半分回復：1×(1−0.25)=0.75
            Assert.AreEqual(0.75f, RegencyRules.EffectiveLegitimacy(1f, 9, P), 1e-5f);
            // 成人＝割引なし
            Assert.AreEqual(1f, RegencyRules.EffectiveLegitimacy(1f, 18, P), 1e-5f);
        }

        [Test]
        public void RegentPowerTick_GrowsOnlyDuringMinority()
        {
            Assert.AreEqual(0.52f, RegencyRules.RegentPowerTick(0.5f, 10, 1f, P), 1e-5f);
            // 成人後は育たない（返上フェーズ）
            Assert.AreEqual(0.5f, RegencyRules.RegentPowerTick(0.5f, 18, 1f, P), 1e-5f);
            Assert.AreEqual(1f, RegencyRules.RegentPowerTick(0.999f, 10, 100f, P), 1e-5f); // 上限1
        }

        [Test]
        public void UsurpationTemptation_NowOrNever()
        {
            // 同じ実権×野心でも、成人が近いほど誘惑が強い
            float early = RegencyRules.UsurpationTemptation(0.8f, 1f, 2, P);
            float late = RegencyRules.UsurpationTemptation(0.8f, 1f, 17, P);
            Assert.Greater(late, early);
            // 野心なき摂政に誘惑なし
            Assert.AreEqual(0f, RegencyRules.UsurpationTemptation(1f, 0f, 17, P), 1e-5f);
        }

        [Test]
        public void UsurpationLooms_AtThreshold()
        {
            // 実権0.8×野心1×17/18≈0.756 ≥0.6＝危険
            Assert.IsTrue(RegencyRules.UsurpationLooms(0.8f, 1f, 17, P));
            Assert.IsFalse(RegencyRules.UsurpationLooms(0.3f, 0.5f, 17, P));
        }

        [Test]
        public void Handover_DeterministicByRoll()
        {
            // 実権0.5×野心0.5＝返上確率0.75
            Assert.AreEqual(0.75f, RegencyRules.HandoverChance(0.5f, 0.5f), 1e-5f);
            Assert.IsTrue(RegencyRules.HandsOverPeacefully(0.5f, 0.5f, 0.74f));
            Assert.IsFalse(RegencyRules.HandsOverPeacefully(0.5f, 0.5f, 0.76f));
            // 無欲の摂政は必ず返す
            Assert.AreEqual(1f, RegencyRules.HandoverChance(1f, 0f), 1e-5f);
        }
    }
}
