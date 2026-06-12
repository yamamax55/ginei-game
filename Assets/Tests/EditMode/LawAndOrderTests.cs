using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 法の支配と法と秩序（#2126）：法の支配指数(LAW-1)/効果(2)/犯罪(3)/取締り(4)/秩序効果・抑圧(5)/Tick(6)。
    /// </summary>
    public class LawAndOrderTests
    {
        // --- LAW-1 法の支配指数・法治との区別 ---
        [Test]
        public void RuleOfLaw_IndexAndRuleByLaw()
        {
            var legal = new LegalSystem(0.8f, 0.7f, 0.6f, 0.7f);
            Assert.AreEqual(0.7f, RuleOfLawRules.RuleOfLawIndex(legal), 1e-4f); // (0.8+0.7+0.6+0.7)/4
            Assert.IsFalse(RuleOfLawRules.IsRuleByLawOnly(legal));              // 権力制約0.6≥0.4

            var ruleByLaw = new LegalSystem(0.7f, 0.4f, 0.25f, 0.6f); // 権力制約が低い＝法治どまり
            Assert.IsTrue(RuleOfLawRules.IsRuleByLawOnly(ruleByLaw));
        }

        // --- LAW-2 法の支配の効果 ---
        [Test]
        public void RuleOfLaw_Effects()
        {
            Assert.AreEqual(8f, RuleOfLawEffectRules.LegitimacyDelta(0.7f, 40f), 1e-3f);     // (0.7-0.5)×40
            Assert.AreEqual(0.7f, RuleOfLawEffectRules.CorruptionResistance(0.7f), 1e-4f);
            Assert.AreEqual(0.85f, RuleOfLawEffectRules.EconomicConfidence(0.7f, 0.5f), 1e-4f); // Lerp(0.5,1,0.7)
            Assert.AreEqual(0.3f, RuleOfLawEffectRules.ArbitraryPowerFactor(0.7f), 1e-4f);   // 1-0.7
        }

        // --- LAW-3 犯罪圧力・実効犯罪 ---
        [Test]
        public void Crime_PressureAndEffective()
        {
            var cp = CrimeRules.CrimeParams.Default;
            Assert.AreEqual(0.5f, CrimeRules.CrimePressure(0.5f, 0.5f, 0.5f, cp), 1e-4f); // 0.2+0.2+0.1
            Assert.AreEqual(0.35f, CrimeRules.EffectiveCrime(0.5f, 0.6f, 0.5f), 1e-4f);  // 0.5×(1-0.3)
        }

        // --- LAW-4 取締り・公共秩序 ---
        [Test]
        public void Enforcement_CapacityAndOrder()
        {
            Assert.AreEqual(1f, LawEnforcementRules.EnforcementCapacity(30f, 100f, 0.2f), 1e-4f); // 30/20 clamp
            Assert.AreEqual(0.5f, LawEnforcementRules.EnforcementCapacity(10f, 100f, 0.2f), 1e-4f);
            Assert.AreEqual(0.65f, LawEnforcementRules.OrderLevel(0.5f, 0.6f, 0.5f), 1e-4f); // 1-0.35
        }

        // --- LAW-5 秩序効果・抑圧トレードオフ ---
        [Test]
        public void Order_EffectsAndRepression()
        {
            Assert.AreEqual(3f, LawOrderEffectRules.StabilityDelta(0.65f, 20f), 1e-3f); // (0.65-0.5)×20
            Assert.AreEqual(3f, LawOrderEffectRules.SupportDelta(0.65f, 20f), 1e-3f);
            // 法の支配が低いほど取締りが抑圧化
            Assert.AreEqual(0.56f, LawOrderEffectRules.RepressionLevel(0.8f, 0.3f), 1e-4f); // 0.8×(1-0.3)
            Assert.AreEqual(0.08f, LawOrderEffectRules.RepressionLevel(0.8f, 0.9f), 1e-4f); // 高い法の支配＝合法
            Assert.AreEqual(16.8f, LawOrderEffectRules.RepressionSupportPenalty(0.56f, 30f), 1e-3f);
        }

        // --- LAW-6 Tick ---
        [Test]
        public void Tick_CrimeOrderStabilityRepression()
        {
            var cp = CrimeRules.CrimeParams.Default;
            var r = LawTickRules.TickProvince(0.3f, 0.5f, 0.5f, 0.5f, 0.6f, cp);
            Assert.AreEqual(0.5f, r.crimePressure, 1e-4f);
            Assert.AreEqual(0.65f, r.orderLevel, 1e-4f);
            Assert.AreEqual(3f, r.stabilityDelta, 1e-3f);
            Assert.AreEqual(0.42f, r.repression, 1e-4f); // 0.6×(1-0.3)＝法治体制での抑圧
        }
    }
}
