using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 儀礼・式典を固定する：効果は盛大さ×情勢、敗勢下の盛大な式典は空疎＝逆効果（豪華なほど痛い）、
    /// コストと費用対効果（空疎は負）。境界を担保。
    /// </summary>
    public class CeremonyRulesTests
    {
        private static readonly CeremonyParams P = CeremonyParams.Default;
        // 正統性0.15/士気0.2/費用100/空疎閾値0.3

        [Test]
        public void Cost_ProportionalToGrandeur()
        {
            Assert.AreEqual(100f, CeremonyRules.Cost(1f, P), 1e-4f);
            Assert.AreEqual(50f, CeremonyRules.Cost(0.5f, P), 1e-4f);
            Assert.AreEqual(0f, CeremonyRules.Cost(0f, P), 1e-5f);
        }

        [Test]
        public void IsHollow_GrandeurExceedsGrimReality()
        {
            Assert.IsTrue(CeremonyRules.IsHollow(0.8f, 0.1f, P));   // 敗勢に大式典＝空疎
            Assert.IsFalse(CeremonyRules.IsHollow(0.05f, 0.1f, P)); // 慎ましい式典なら可
            Assert.IsFalse(CeremonyRules.IsHollow(0.8f, 0.5f, P));  // 情勢が良ければ豪華も実
            Assert.IsFalse(CeremonyRules.IsHollow(0.8f, 0.3f, P));  // 閾値ちょうど＝空疎でない
        }

        [Test]
        public void LegitimacyEffect_PositiveWhenBacked()
        {
            // 大勝利の凱旋：1×1×0.15=+0.15
            Assert.AreEqual(0.15f, CeremonyRules.LegitimacyEffect(1f, 1f, P), 1e-5f);
            // 情勢半ば＝半分
            Assert.AreEqual(0.075f, CeremonyRules.LegitimacyEffect(1f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void LegitimacyEffect_NegativeWhenHollow()
        {
            // 敗勢の大式典＝−1×0.15（豪華なほど落差が痛い）
            Assert.AreEqual(-0.15f, CeremonyRules.LegitimacyEffect(1f, 0.1f, P), 1e-5f);
            Assert.AreEqual(-0.075f, CeremonyRules.LegitimacyEffect(0.5f, 0.1f, P), 1e-5f);
        }

        [Test]
        public void MoraleEffect_SameShape()
        {
            Assert.AreEqual(0.2f, CeremonyRules.MoraleEffect(1f, 1f, P), 1e-5f);
            Assert.AreEqual(-0.2f, CeremonyRules.MoraleEffect(1f, 0.1f, P), 1e-5f);
        }

        [Test]
        public void Efficiency_NegativeForHollow_ZeroForNone()
        {
            Assert.Greater(CeremonyRules.Efficiency(1f, 1f, P), 0f);
            Assert.Less(CeremonyRules.Efficiency(1f, 0.1f, P), 0f);  // やらないほうがまし
            Assert.AreEqual(0f, CeremonyRules.Efficiency(0f, 1f, P), 1e-5f);
        }
    }
}
