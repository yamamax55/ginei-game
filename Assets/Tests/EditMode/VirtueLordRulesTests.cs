using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>徳望の主（劉備玄徳）：徳の限界突破・漢室末裔の大義・審美眼・固い絆・夷陵の暴走。</summary>
    public class VirtueLordRulesTests
    {
        [Test]
        public void EffectiveVirtue_TranscendsHundredForLord()
        {
            Assert.AreEqual(130, VirtueLordRules.EffectiveVirtue(100, true)); // 限界突破（上限）
            Assert.AreEqual(104, VirtueLordRules.EffectiveVirtue(80, true));  // 80→104（100超）
            Assert.AreEqual(100, VirtueLordRules.EffectiveVirtue(100, false)); // 並は100止まり
            Assert.AreEqual(80, VirtueLordRules.EffectiveVirtue(80, false));
        }

        [Test]
        public void VirtuePull_HanLegitimacy_Judgment()
        {
            Assert.AreEqual(1.3f, VirtueLordRules.VirtueLoyaltyPull(130), 1e-4f);
            Assert.AreEqual(1.0f, VirtueLordRules.VirtueLoyaltyPull(100), 1e-4f);

            Assert.AreEqual(0.3f, VirtueLordRules.HanLegitimacyBonus(true), 1e-4f);  // 漢室末裔の大義名分
            Assert.AreEqual(0f, VirtueLordRules.HanLegitimacyBonus(false), 1e-4f);

            Assert.AreEqual(1.3f, VirtueLordRules.TalentJudgmentFactor(true, 130), 1e-4f); // 審美眼限界突破（三顧の礼）
            Assert.AreEqual(1.04f, VirtueLordRules.TalentJudgmentFactor(true, 104), 1e-4f);
            Assert.AreEqual(1.0f, VirtueLordRules.TalentJudgmentFactor(false, 130), 1e-4f); // 並は標準
        }

        [Test]
        public void Bonds_And_YilingFlaw()
        {
            Assert.AreEqual(0.9f, VirtueLordRules.LoyaltyFloorForBonded(true), 1e-4f); // 桃園/水魚＝離反しない
            Assert.AreEqual(0f, VirtueLordRules.LoyaltyFloorForBonded(false), 1e-4f);

            // 夷陵：義兄弟を失うと諫言を無視して出陣し大敗。
            Assert.IsTrue(VirtueLordRules.IgnoresCounsel(true, true));
            Assert.IsFalse(VirtueLordRules.IgnoresCounsel(true, false));   // 喪失なし
            Assert.IsFalse(VirtueLordRules.IgnoresCounsel(false, true));   // 並は諫言に従う

            Assert.AreEqual(0.6f, VirtueLordRules.RecklessCampaignFactor(true), 1e-4f);  // 陸遜の火計で大敗
            Assert.AreEqual(1.0f, VirtueLordRules.RecklessCampaignFactor(false), 1e-4f);
        }
    }
}
