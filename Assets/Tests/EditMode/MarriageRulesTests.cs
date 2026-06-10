using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>政略結婚・血縁外交（PDX-2 #647）を固定する：婚姻結束・請求権の世代減衰・関係ボーナス。</summary>
    public class MarriageRulesTests
    {
        // ===== AllianceBond =====

        [Test]
        public void AllianceBond_BaseAndClaim()
        {
            // 基準0.5 + 請求1.0×重み0.3 = 0.8
            var m = new MarriageAlliance("帝国家", "同盟家", 1f);
            Assert.AreEqual(0.8f, MarriageRules.AllianceBond(m), 1e-4f);
        }

        [Test]
        public void AllianceBond_NoClaim_JustBase()
        {
            var m = new MarriageAlliance("A", "B", 0f);
            Assert.AreEqual(0.5f, MarriageRules.AllianceBond(m), 1e-4f); // 既定基準のみ
        }

        [Test]
        public void AllianceBond_ClampedToOne()
        {
            // 基準1.0 + 請求×重みでも 1.0 を超えない
            var p = new MarriageRules.MarriageParams(1f, 0.5f, 0.5f, 20f);
            var m = new MarriageAlliance("A", "B", 1f);
            Assert.AreEqual(1f, MarriageRules.AllianceBond(m, p), 1e-4f);
        }

        [Test]
        public void AllianceBond_SelfOrEmpty_NoAlliance()
        {
            Assert.AreEqual(0f, MarriageRules.AllianceBond(new MarriageAlliance("A", "A", 1f)), 1e-4f); // 自己婚姻
            Assert.AreEqual(0f, MarriageRules.AllianceBond(new MarriageAlliance("A", "", 1f)), 1e-4f);  // 片側欠落
            Assert.AreEqual(0f, MarriageRules.AllianceBond(null), 1e-4f);                                // null安全
        }

        // ===== ClaimInheritance =====

        [Test]
        public void ClaimInheritance_CurrentGeneration_NoDecay()
        {
            Assert.AreEqual(0.8f, MarriageRules.ClaimInheritance(0.8f, 0), 1e-4f); // 当代＝減衰なし
        }

        [Test]
        public void ClaimInheritance_DecaysPerGeneration()
        {
            // 既定減衰0.5：1世代で半分、2世代で1/4
            Assert.AreEqual(0.5f, MarriageRules.ClaimInheritance(1f, 1), 1e-4f);
            Assert.AreEqual(0.25f, MarriageRules.ClaimInheritance(1f, 2), 1e-4f);
        }

        [Test]
        public void ClaimInheritance_NegativeGenerations_ClampedToCurrent()
        {
            Assert.AreEqual(0.6f, MarriageRules.ClaimInheritance(0.6f, -3), 1e-4f); // 負世代→0世代扱い
        }

        [Test]
        public void ClaimInheritance_InputClaimClamped()
        {
            Assert.AreEqual(1f, MarriageRules.ClaimInheritance(2f, 0), 1e-4f);  // 上限クランプ
            Assert.AreEqual(0f, MarriageRules.ClaimInheritance(-1f, 0), 1e-4f); // 下限クランプ
        }

        // ===== MarriageOpinionBonus =====

        [Test]
        public void MarriageOpinionBonus_Default()
        {
            Assert.AreEqual(20f, MarriageRules.MarriageOpinionBonus(), 1e-4f);
        }

        [Test]
        public void MarriageOpinionBonus_NegativeParam_ClampedToZero()
        {
            var p = new MarriageRules.MarriageParams(0.5f, 0.3f, 0.5f, -5f);
            Assert.AreEqual(0f, MarriageRules.MarriageOpinionBonus(p), 1e-4f); // 負は0へ
        }
    }
}
