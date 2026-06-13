using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>CDR-2 個人忠誠・下剋上：厚遇/功名心で目標が動き、低忠誠で離反・低忠誠×高功名心で簒奪。</summary>
    public class AllegianceDriftRulesTests
    {
        [Test]
        public void LoyaltyTarget_TreatmentUpAmbitionDown()
        {
            Assert.AreEqual(1.0f, AllegianceDriftRules.LoyaltyTarget(1f, 50), 1e-4f);
            Assert.AreEqual(0.75f, AllegianceDriftRules.LoyaltyTarget(1f, 100), 1e-4f); // 高功名心で割引
            Assert.AreEqual(0.5f, AllegianceDriftRules.LoyaltyTarget(0.5f, 50), 1e-4f);
            Assert.AreEqual(0f, AllegianceDriftRules.LoyaltyTarget(0f, 50), 1e-4f);     // 不遇
        }

        [Test]
        public void Drift_ConvergesAndClamps()
        {
            Assert.AreEqual(0.5f, AllegianceDriftRules.Drift(1.0f, 0.0f, 0.5f, 1.0f), 1e-4f);
            Assert.AreEqual(1.0f, AllegianceDriftRules.Drift(0.5f, 1.0f, 5f, 1.0f), 1e-4f); // オーバーシュートしない
        }

        [Test]
        public void Defect_And_Usurp()
        {
            Assert.IsTrue(AllegianceDriftRules.WouldDefect(0.2f));
            Assert.IsFalse(AllegianceDriftRules.WouldDefect(0.4f));
            Assert.IsFalse(AllegianceDriftRules.WouldDefect(0.3f)); // 境界＝しきい値未満でない

            Assert.IsTrue(AllegianceDriftRules.WouldUsurp(0.1f, 80));   // 下剋上
            Assert.IsFalse(AllegianceDriftRules.WouldUsurp(0.1f, 70));  // 功名心不足
            Assert.IsFalse(AllegianceDriftRules.WouldUsurp(0.25f, 90)); // 忠誠が簒奪閾値以上
        }
    }
}
