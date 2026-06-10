using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 傭兵団を固定する：給与で忠誠が回復し未払いで蝕まれる、閾値未満で離反確率が立ち給与遅配・敵の好条件で増す、
    /// 契約満了/低忠誠で解散、低忠誠は戦闘信頼性を下げる。境界・クランプ・決定論を担保。
    /// </summary>
    public class MercenaryRulesTests
    {
        private static readonly MercenaryParams P = MercenaryParams.Default; // 未払い0.5/支払い0.3/維持0.1/離反0.3/信頼下限0.4

        [Test]
        public void Upkeep_PerStrength()
        {
            Assert.AreEqual(10f, MercenaryRules.Upkeep(100f, P), 1e-5f);
        }

        [Test]
        public void LoyaltyAfterPay_FullPayRecovers_ArrearsErode()
        {
            // 満額＝+0.3、上限1
            Assert.AreEqual(0.8f, MercenaryRules.LoyaltyAfterPay(0.5f, 1f, P), 1e-5f);
            // 完全未払い＝−0.5
            Assert.AreEqual(0.3f, MercenaryRules.LoyaltyAfterPay(0.8f, 0f, P), 1e-5f);
            // 半額＝+0.5×0.3 −0.5×0.5 = 0.15−0.25 = −0.1
            Assert.AreEqual(0.4f, MercenaryRules.LoyaltyAfterPay(0.5f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void DefectChance_ZeroAboveThreshold()
        {
            Assert.AreEqual(0f, MercenaryRules.DefectChance(0.3f, 1f, 1f, P), 1e-5f); // 閾値ちょうどは0
            Assert.AreEqual(0f, MercenaryRules.DefectChance(0.5f, 1f, 1f, P), 1e-5f);
        }

        [Test]
        public void DefectChance_RisesAsLoyaltyFalls()
        {
            // loyalty=0, arrears=0, offer=0: shortfall=1 → 1×(0.5)=0.5
            Assert.AreEqual(0.5f, MercenaryRules.DefectChance(0f, 0f, 0f, P), 1e-5f);
            // 給与遅配・好条件で上がる: 1×(0.5+0.25+0.25)=1.0
            Assert.AreEqual(1f, MercenaryRules.DefectChance(0f, 1f, 1f, P), 1e-5f);
            // 低忠誠ほど高い
            Assert.Greater(MercenaryRules.DefectChance(0.1f, 0f, 0f, P),
                           MercenaryRules.DefectChance(0.25f, 0f, 0f, P));
        }

        [Test]
        public void WillDefect_DeterministicByRoll()
        {
            float chance = MercenaryRules.DefectChance(0f, 0f, 0f, P); // 0.5
            Assert.IsTrue(MercenaryRules.WillDefect(0f, 0f, 0f, 0.4f, P));
            Assert.IsFalse(MercenaryRules.WillDefect(0f, 0f, 0f, 0.6f, P));
        }

        [Test]
        public void WillDisband_OnContractEndOrLowLoyalty()
        {
            Assert.IsTrue(MercenaryRules.WillDisband(0, 1f, P));    // 契約満了
            Assert.IsTrue(MercenaryRules.WillDisband(5, 0.2f, P));  // 低忠誠で去る
            Assert.IsFalse(MercenaryRules.WillDisband(5, 0.8f, P)); // 契約中・忠誠十分
        }

        [Test]
        public void CombatReliability_LerpsFromFloor()
        {
            Assert.AreEqual(1f, MercenaryRules.CombatReliability(1f, P), 1e-5f);
            Assert.AreEqual(0.4f, MercenaryRules.CombatReliability(0f, P), 1e-5f); // 下限
            Assert.AreEqual(0.7f, MercenaryRules.CombatReliability(0.5f, P), 1e-5f);
        }

        [Test]
        public void MercenaryBand_DefaultsAndClamp()
        {
            var m = new MercenaryBand();
            Assert.AreEqual(1f, m.loyalty, 1e-5f);
            var c = new MercenaryBand(-5f, 2f, 2f, -3);
            Assert.AreEqual(0f, c.strength, 1e-5f);
            Assert.AreEqual(1f, c.loyalty, 1e-5f);
            Assert.AreEqual(1f, c.payArrears, 1e-5f);
            Assert.AreEqual(0, c.contractTurnsLeft);
        }
    }
}
