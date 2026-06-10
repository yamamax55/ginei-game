using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 私有財産の保護（#1036 保護強度→投資意欲）を固定する：法の支配×契約履行×(1−没収リスク)で保護強度を出し、
    /// 保護が強いほど投資・蓄財が進み、弱保護では資本が逃げ取引が闇に潜り、没収一発で信頼が削れる。境界・クランプ・決定論。
    /// </summary>
    public class PropertyRightsRulesTests
    {
        private static PropertyRightsParams P => PropertyRightsParams.Default;

        // --- ProtectionStrength ---

        [Test]
        public void ProtectionStrength_AllStrong_HighProtection()
        {
            // 法0.8×契約0.75×(1−没収0.0)=0.6
            Assert.AreEqual(0.6f, PropertyRightsRules.ProtectionStrength(0.8f, 0.75f, 0f), 1e-4f);
        }

        [Test]
        public void ProtectionStrength_HighRisk_CollapsesProtection()
        {
            // 法・契約が完璧でも没収リスク1ですべて無に＝積構造（恣意的没収が横行すれば財産権は崩れる）
            Assert.AreEqual(0f, PropertyRightsRules.ProtectionStrength(1f, 1f, 1f), 1e-4f);
        }

        // --- InvestmentIncentive ---

        [Test]
        public void InvestmentIncentive_StrongerProtection_MoreInvestment()
        {
            // 財産権は成長の土台＝保護が強いほど投資意欲が上がる。床0.1→1.0へ線形
            Assert.AreEqual(0.1f, PropertyRightsRules.InvestmentIncentive(0f, P), 1e-4f);
            Assert.AreEqual(0.55f, PropertyRightsRules.InvestmentIncentive(0.5f, P), 1e-4f);
            Assert.AreEqual(1f, PropertyRightsRules.InvestmentIncentive(1f, P), 1e-4f);
        }

        // --- CapitalFlight ---

        [Test]
        public void CapitalFlight_WeakProtectionMobileCapital_Flees()
        {
            // 弱保護＝資本が安全な場所へ逃げる。(1−0.2)×機動1.0=0.8
            Assert.AreEqual(0.8f, PropertyRightsRules.CapitalFlight(0.2f, 1f), 1e-4f);
            // 機動が無ければ弱保護でも逃げられない
            Assert.AreEqual(0f, PropertyRightsRules.CapitalFlight(0.2f, 0f), 1e-4f);
            // 保護が完璧なら逃げる理由がない
            Assert.AreEqual(0f, PropertyRightsRules.CapitalFlight(1f, 1f), 1e-4f);
        }

        // --- WealthAccumulationRate ---

        [Test]
        public void WealthAccumulationRate_ProtectionScalesGrowth()
        {
            // 守られない財産は築かれない＝弱保護では基準成長の一部しか実らない（床0.2）
            Assert.AreEqual(2f, PropertyRightsRules.WealthAccumulationRate(0f, 10f, P), 1e-4f);   // 10*0.2
            Assert.AreEqual(10f, PropertyRightsRules.WealthAccumulationRate(1f, 10f, P), 1e-4f);  // 10*1.0
        }

        // --- InformalEconomyShare ---

        [Test]
        public void InformalEconomyShare_WeakProtection_GoesUnderground()
        {
            // 財産権が弱いと取引が闇に潜る（上限0.6）。保護1で底へ
            Assert.AreEqual(0.6f, PropertyRightsRules.InformalEconomyShare(0f, P), 1e-4f);
            Assert.AreEqual(0.3f, PropertyRightsRules.InformalEconomyShare(0.5f, P), 1e-4f);
            Assert.AreEqual(0f, PropertyRightsRules.InformalEconomyShare(1f, P), 1e-4f);
        }

        // --- ExpropriationShock ---

        [Test]
        public void ExpropriationShock_EventErodesTrust_NoEventNoChange()
        {
            // 一度の恣意的没収が保護の4割を削る＝信頼を長く損なう（ConfiscationRules の長期帰結）
            Assert.AreEqual(0.48f, PropertyRightsRules.ExpropriationShock(0.8f, true, P), 1e-4f); // 0.8*(1-0.4)
            // 没収が無ければ非破壊で素通し（決定論）
            Assert.AreEqual(0.8f, PropertyRightsRules.ExpropriationShock(0.8f, false, P), 1e-4f);
        }
    }
}
