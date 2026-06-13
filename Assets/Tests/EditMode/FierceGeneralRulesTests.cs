using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>猛将（張飛）：猪突猛進・長坂の一喝・一騎打ち・正義感／部下に厳しい・酒癖・暗殺リスク。</summary>
    public class FierceGeneralRulesTests
    {
        [Test]
        public void Charge_Reckless_Duel()
        {
            Assert.AreEqual(1.2f, FierceGeneralRules.ChargeAttackFactor(true), 1e-4f);
            Assert.AreEqual(1.0f, FierceGeneralRules.ChargeAttackFactor(false), 1e-4f);
            Assert.AreEqual(1.25f, FierceGeneralRules.RecklessDamageTakenFactor(true), 1e-4f); // 猪突の隙
            Assert.AreEqual(1.0f, FierceGeneralRules.RecklessDamageTakenFactor(false), 1e-4f);
            Assert.AreEqual(1.3f, FierceGeneralRules.DuelStrengthFactor(true), 1e-4f);
            Assert.AreEqual(1.0f, FierceGeneralRules.DuelStrengthFactor(false), 1e-4f);
        }

        [Test]
        public void Roar_And_Righteousness()
        {
            Assert.AreEqual(0.5f, FierceGeneralRules.IntimidationFactor(true, true), 1e-4f);  // 当陽橋で単騎の殿
            Assert.AreEqual(0.3f, FierceGeneralRules.IntimidationFactor(true, false), 1e-4f);
            Assert.AreEqual(0f, FierceGeneralRules.IntimidationFactor(false, true), 1e-4f);

            Assert.AreEqual(0.15f, FierceGeneralRules.RighteousMoraleBonus(true, true), 1e-4f); // 大義の戦
            Assert.AreEqual(0f, FierceGeneralRules.RighteousMoraleBonus(true, false), 1e-4f);
            Assert.AreEqual(0f, FierceGeneralRules.RighteousMoraleBonus(false, true), 1e-4f);
        }

        [Test]
        public void Subordinates_Drink_Assassination()
        {
            Assert.AreEqual(0.85f, FierceGeneralRules.SubordinateMoraleFactor(true), 1e-4f); // 部下に厳しい
            Assert.AreEqual(1.0f, FierceGeneralRules.SubordinateMoraleFactor(false), 1e-4f);

            Assert.AreEqual(0.7f, FierceGeneralRules.DrunkAbilityFactor(true), 1e-4f);  // 泥酔
            Assert.AreEqual(1.0f, FierceGeneralRules.DrunkAbilityFactor(false), 1e-4f);

            Assert.AreEqual(0.1f, FierceGeneralRules.AssassinationRisk(true, false), 1e-4f);  // 素面でも残る
            Assert.AreEqual(0.4f, FierceGeneralRules.AssassinationRisk(true, true), 1e-4f);   // 泥酔で跳ね上がる
            Assert.AreEqual(0f, FierceGeneralRules.AssassinationRisk(false, true), 1e-4f);    // 並は対象外

            Assert.IsTrue(FierceGeneralRules.IsAssassinated(true, true, 0.3f));   // roll<0.4（泥酔）
            Assert.IsFalse(FierceGeneralRules.IsAssassinated(true, true, 0.5f));
            Assert.IsFalse(FierceGeneralRules.IsAssassinated(true, false, 0.2f)); // 素面 0.2<0.1=false
            Assert.IsTrue(FierceGeneralRules.IsAssassinated(true, false, 0.05f)); // 素面 0.05<0.1=true
        }
    }
}
