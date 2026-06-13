using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>三日天下（明智光秀）：中央にあかるい・謀反の成就（本能寺の隙）・主殺しの正統性喪失（三日天下）。</summary>
    public class ThreeDayReignRulesTests
    {
        [Test]
        public void CentralAffairsFactor_FromOperationAndIntelligence()
        {
            Assert.AreEqual(1.5f, ThreeDayReignRules.CentralAffairsFactor(100, 100), 1e-4f);
            Assert.AreEqual(1.25f, ThreeDayReignRules.CentralAffairsFactor(50, 50), 1e-4f);
            Assert.AreEqual(1.25f, ThreeDayReignRules.CentralAffairsFactor(100, 0), 1e-4f); // 平均50
            Assert.AreEqual(1.0f, ThreeDayReignRules.CentralAffairsFactor(0, 0), 1e-4f);
        }

        [Test]
        public void CoupSuccessBonus_OnlyWhenLordExposed()
        {
            Assert.AreEqual(0.3f, ThreeDayReignRules.CoupSuccessBonus(true, true), 1e-4f);   // 本能寺の隙
            Assert.AreEqual(0f, ThreeDayReignRules.CoupSuccessBonus(true, false), 1e-4f);    // 主君に隙なし
            Assert.AreEqual(0f, ThreeDayReignRules.CoupSuccessBonus(false, true), 1e-4f);    // 光秀型でない
        }

        [Test]
        public void PostCoup_RegicideCollapsesLegitimacy()
        {
            Assert.AreEqual(0.1f, ThreeDayReignRules.PostCoupLegitimacy(true, 0.5f), 1e-4f); // 0.5*0.2=0.1
            Assert.AreEqual(0.2f, ThreeDayReignRules.PostCoupLegitimacy(true, 1.0f), 1e-4f); // 上限クランプ
            Assert.AreEqual(0.5f, ThreeDayReignRules.PostCoupLegitimacy(false, 0.5f), 1e-4f); // 並は不変

            Assert.IsTrue(ThreeDayReignRules.IsThreeDayReign(true, 0.1f));   // 短命確定
            Assert.IsTrue(ThreeDayReignRules.IsThreeDayReign(true, 0.2f));
            Assert.IsFalse(ThreeDayReignRules.IsThreeDayReign(true, 0.3f));
            Assert.IsFalse(ThreeDayReignRules.IsThreeDayReign(false, 0.1f)); // 並は対象外
        }
    }
}
