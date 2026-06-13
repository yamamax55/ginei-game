using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>半身（キルヒアイス）：万能・懐柔・端正な陣形・身代わり・良心・精神無効・カリスマ・絆の相関。</summary>
    public class RightHandRulesTests
    {
        [Test]
        public void Versatility_Pacification_Formation()
        {
            // 万能＝役割不一致でも100%（並は不一致で0.5）。
            Assert.AreEqual(1.0f, RightHandRules.RoleAdaptationFactor(true, false), 1e-4f);
            Assert.AreEqual(1.0f, RightHandRules.RoleAdaptationFactor(true, true), 1e-4f);
            Assert.AreEqual(0.5f, RightHandRules.RoleAdaptationFactor(false, false), 1e-4f);
            Assert.AreEqual(1.0f, RightHandRules.RoleAdaptationFactor(false, true), 1e-4f);

            Assert.AreEqual(0.25f, RightHandRules.PacificationAttackDebuff(true), 1e-4f); // 流血なき懐柔
            Assert.AreEqual(0f, RightHandRules.PacificationAttackDebuff(false), 1e-4f);

            Assert.AreEqual(0.85f, RightHandRules.FormationDamageTakenFactor(true), 1e-4f); // 端正な陣形
            Assert.AreEqual(1.0f, RightHandRules.FormationDamageTakenFactor(false), 1e-4f);
        }

        [Test]
        public void Sacrifice_Conscience_Mind_Negotiation()
        {
            Assert.IsTrue(RightHandRules.CanShieldAlly(true));   // 身代わり
            Assert.IsFalse(RightHandRules.CanShieldAlly(false));
            Assert.AreEqual(2.0f, RightHandRules.MartyrAllyBuffFactor(true), 1e-4f); // 命と引き換えの超強化
            Assert.AreEqual(1.0f, RightHandRules.MartyrAllyBuffFactor(false), 1e-4f);

            // 主君の良心＝健在の間は暴走を止める。
            Assert.IsTrue(RightHandRules.PreventsBerserk(true, true));
            Assert.IsFalse(RightHandRules.PreventsBerserk(true, false));  // 戦脱したら止められない→覇王暴走
            Assert.IsFalse(RightHandRules.PreventsBerserk(false, true));

            Assert.IsTrue(RightHandRules.ImmuneToMentalAttack(true));   // アンネローゼの誓い
            Assert.IsFalse(RightHandRules.ImmuneToMentalAttack(false));
            Assert.IsTrue(RightHandRules.GuaranteesNegotiation(true));  // 真のカリスマ
            Assert.IsFalse(RightHandRules.GuaranteesNegotiation(false));
        }

        [Test]
        public void PartnerSynergy_DoublesWhenPaired()
        {
            Assert.AreEqual(2.0f, RightHandRules.PartnerSynergyFactor(true, true), 1e-4f);  // 覇王と組むと倍化
            Assert.AreEqual(1.0f, RightHandRules.PartnerSynergyFactor(true, false), 1e-4f); // 単独
            Assert.AreEqual(1.0f, RightHandRules.PartnerSynergyFactor(false, true), 1e-4f);
        }
    }
}
