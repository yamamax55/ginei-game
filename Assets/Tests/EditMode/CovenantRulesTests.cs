using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// CovenantRules（保護と服従の契約・LEVI-3 #1463）の純ロジックテスト。
    /// 既定 CovenantParams（安全の重み0.6／服従消滅閾値0.4／脅威の重み0.6）で期待値を固定。
    /// </summary>
    public class CovenantRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>提供される保護＝戦力と安全の加重和（既定 0.4×戦力＋0.6×安全）。</summary>
        [Test]
        public void ProtectionProvided_WeightsSecurityAndStrength()
        {
            // 0.4*0.5 + 0.6*1.0 = 0.8
            Assert.AreEqual(0.8f, CovenantRules.ProtectionProvided(0.5f, 1.0f), Eps);
            // 戦力があっても安全が届かなければ保護は低い：0.4*1.0 + 0.6*0.0 = 0.4
            Assert.AreEqual(0.4f, CovenantRules.ProtectionProvided(1.0f, 0.0f), Eps);
        }

        /// <summary>服従義務は保護に比例（保護されるから従う＝相互的）。保護ゼロなら服従ゼロ。</summary>
        [Test]
        public void ObedienceObligation_TracksProtection()
        {
            Assert.AreEqual(0.7f, CovenantRules.ObedienceObligation(0.7f), Eps);
            Assert.AreEqual(0f, CovenantRules.ObedienceObligation(0f), Eps);
        }

        /// <summary>契約の健全性＝保護と服従が釣り合うほど高く、乖離で壊れる。</summary>
        [Test]
        public void CovenantIntegrity_PeaksWhenBalanced()
        {
            // 釣り合い：min(0.8,0.8)*(1-0)=0.8
            Assert.AreEqual(0.8f, CovenantRules.CovenantIntegrity(0.8f, 0.8f), Eps);
            // 乖離：保護はあるが服従しない min(0.8,0.2)*(1-0.6)=0.08
            Assert.AreEqual(0.08f, CovenantRules.CovenantIntegrity(0.8f, 0.2f), Eps);
        }

        /// <summary>保護の失敗＝脅威の実現と応答の欠如の加重和（既定 0.6×脅威＋0.4×(1-応答)）。</summary>
        [Test]
        public void ProtectionFailure_RisesWithThreatAndWeakResponse()
        {
            // 脅威1・応答0：0.6*1 + 0.4*1 = 1.0
            Assert.AreEqual(1.0f, CovenantRules.ProtectionFailure(1.0f, 0.0f), Eps);
            // 脅威1・応答完全：0.6*1 + 0.4*0 = 0.6
            Assert.AreEqual(0.6f, CovenantRules.ProtectionFailure(1.0f, 1.0f), Eps);
        }

        /// <summary>服従義務の消滅＝保護の失敗が閾値0.4を割ると（超えると）消滅が進む。閾値手前は0。</summary>
        [Test]
        public void ObedienceDissolution_ZeroBelowThreshold_RampsAbove()
        {
            // 失敗0.4以下は服従消えず
            Assert.AreEqual(0f, CovenantRules.ObedienceDissolution(0.4f), Eps);
            Assert.AreEqual(0f, CovenantRules.ObedienceDissolution(0.3f), Eps);
            // 失敗0.7：(0.7-0.4)/(1-0.4)=0.5
            Assert.AreEqual(0.5f, CovenantRules.ObedienceDissolution(0.7f), Eps);
            // 完全失敗は完全消滅
            Assert.AreEqual(1.0f, CovenantRules.ObedienceDissolution(1.0f), Eps);
        }

        /// <summary>服従義務の消滅が合意撤回（ConsentRules.Withdraw）へ転送される＝消滅量に等しい。</summary>
        [Test]
        public void WithdrawalTrigger_ForwardsDissolution()
        {
            float dissolution = CovenantRules.ObedienceDissolution(0.7f);
            Assert.AreEqual(0.5f, CovenantRules.WithdrawalTrigger(dissolution), Eps);

            // 実際に Polity の協力撤回へ流せること（守れない主権者から手を引く）
            var polity = new Polity { cooperation = 0.9f };
            ConsentRules.Withdraw(polity, CovenantRules.WithdrawalTrigger(dissolution));
            Assert.AreEqual(0.4f, polity.cooperation, Eps);
        }

        /// <summary>征服者でも保護を提供すれば服従が移る／自己保存の権利は死の脅威に比例（譲渡不能）。</summary>
        [Test]
        public void Acquisition_And_SelfPreservation()
        {
            // 保護できる者が主権者＝勝者への服従も正当
            Assert.AreEqual(0.85f, CovenantRules.SovereignByAcquisition(0.85f), Eps);
            // 死の脅威が大きいほど自己保存権が服従に優先（服従の限界）
            Assert.AreEqual(0.9f, CovenantRules.SelfPreservationRight(0.9f), Eps);
            Assert.AreEqual(0f, CovenantRules.SelfPreservationRight(0f), Eps);
        }

        /// <summary>契約破綻判定＝保護の失敗が閾値0.4を超えれば破れる（守れない＝契約終了）。</summary>
        [Test]
        public void IsCovenantBroken_AtThreshold()
        {
            Assert.IsFalse(CovenantRules.IsCovenantBroken(0.4f));   // 境界＝まだ破れない
            Assert.IsFalse(CovenantRules.IsCovenantBroken(0.3f));
            Assert.IsTrue(CovenantRules.IsCovenantBroken(0.5f));    // 守れない＝破綻
            Assert.IsTrue(CovenantRules.IsCovenantBroken(1.0f));
        }

        /// <summary>全入力はクランプされる（範囲外でも安全）。</summary>
        [Test]
        public void Inputs_AreClamped()
        {
            Assert.AreEqual(1.0f, CovenantRules.ObedienceObligation(2.0f), Eps);
            Assert.AreEqual(0f, CovenantRules.SelfPreservationRight(-1.0f), Eps);
            Assert.AreEqual(1.0f, CovenantRules.SovereignByAcquisition(5.0f), Eps);
        }
    }
}
