using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>RetirementRules（軍人の退役ライフサイクル）の EditMode テスト。決定論・境界・クランプを担保する。</summary>
    public class RetirementRulesTests
    {
        private static RetirementRules.RetireParams P => RetirementRules.RetireParams.Default;

        // --- MandatoryRetirementAge：階級別の停年と丸め ---

        [Test]
        public void MandatoryRetirementAge_PerTier()
        {
            Assert.AreEqual(50, RetirementRules.MandatoryRetirementAge(5, P));
            Assert.AreEqual(54, RetirementRules.MandatoryRetirementAge(6, P));
            Assert.AreEqual(58, RetirementRules.MandatoryRetirementAge(7, P));
            Assert.AreEqual(62, RetirementRules.MandatoryRetirementAge(8, P));
        }

        [Test]
        public void MandatoryRetirementAge_ClampsLowAndHighTier()
        {
            // tier5 未満は tier5 の停年へ、tier8 超（9=上級大将など）は tier8 の停年へ丸める
            Assert.AreEqual(50, RetirementRules.MandatoryRetirementAge(1, P));
            Assert.AreEqual(50, RetirementRules.MandatoryRetirementAge(4, P));
            Assert.AreEqual(62, RetirementRules.MandatoryRetirementAge(9, P));
        }

        // --- ShouldRetireByAge：停年到達と元帥終身の例外 ---

        [Test]
        public void ShouldRetireByAge_BoundaryAndMarshalException()
        {
            Assert.IsFalse(RetirementRules.ShouldRetireByAge(57, 7, P)); // 停年58の1歳前
            Assert.IsTrue(RetirementRules.ShouldRetireByAge(58, 7, P));  // 停年ちょうど＝退役
            Assert.IsTrue(RetirementRules.ShouldRetireByAge(70, 8, P));
            // 元帥(tier10)は終身＝何歳でも停年退役しない
            Assert.IsFalse(RetirementRules.ShouldRetireByAge(90, 10, P));
        }

        // --- ShouldUpOrOut：在級年数超過で予備役、元帥は対象外 ---

        [Test]
        public void ShouldUpOrOut_BoundaryAndMarshalException()
        {
            Assert.IsFalse(RetirementRules.ShouldUpOrOut(5, 7, P)); // 上限ちょうどは留任
            Assert.IsTrue(RetirementRules.ShouldUpOrOut(6, 7, P));  // 上限超＝予備役編入
            Assert.IsFalse(RetirementRules.ShouldUpOrOut(20, 10, P)); // 元帥は終身＝据え置き
        }

        // --- IsMarshalTenure：最上位 tier の終身判定 ---

        [Test]
        public void IsMarshalTenure_AtAndAboveMarshalTier()
        {
            Assert.IsFalse(RetirementRules.IsMarshalTenure(9, P));
            Assert.IsTrue(RetirementRules.IsMarshalTenure(10, P));
            Assert.IsTrue(RetirementRules.IsMarshalTenure(11, P));
        }

        // --- CanRecall：現役は対象外、年齢上限内のみ召集可 ---

        [Test]
        public void CanRecall_StatusAndAgeLimit()
        {
            Assert.IsFalse(RetirementRules.CanRecall(ServiceStatus.現役, 40, P)); // 既に現役
            Assert.IsTrue(RetirementRules.CanRecall(ServiceStatus.予備役, 65, P)); // 上限ちょうど
            Assert.IsFalse(RetirementRules.CanRecall(ServiceStatus.予備役, 66, P)); // 上限超
            Assert.IsTrue(RetirementRules.CanRecall(ServiceStatus.退役, 50, P));
        }

        // --- PensionFactor：比例・満額・クランプ ---

        [Test]
        public void PensionFactor_ProportionFullAndClamp()
        {
            Assert.AreEqual(0f, RetirementRules.PensionFactor(0, P), 1e-4f);
            Assert.AreEqual(0f, RetirementRules.PensionFactor(-5, P), 1e-4f); // 負は0
            Assert.AreEqual(0.5f, RetirementRules.PensionFactor(15, P), 1e-4f); // 30年で満額の半分
            Assert.AreEqual(1f, RetirementRules.PensionFactor(30, P), 1e-4f);  // 満額ちょうど
            Assert.AreEqual(1f, RetirementRules.PensionFactor(40, P), 1e-4f);  // 超過もクランプ
        }

        // --- Params のガード（0以下は1へクランプ）---

        [Test]
        public void RetireParams_GuardsDivisorsToOne()
        {
            var p = new RetirementRules.RetireParams(50, 54, 58, 62, 0, 65, 10, 0);
            Assert.IsTrue(RetirementRules.ShouldUpOrOut(2, 7, p)); // maxYearsInGrade=1 へクランプ＝2で超過
            Assert.AreEqual(1f, RetirementRules.PensionFactor(1, p), 1e-4f); // fullPensionYears=1 へクランプ
        }
    }
}
