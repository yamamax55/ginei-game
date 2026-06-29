using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 通商保護の純ロジック（#通商保護）の EditMode テスト。既定パラメータの具体値で期待値を固定。
    /// </summary>
    public class CommerceProtectionRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        // EscortAllocation: perRoute=20, 20/(20+10)=0.6667
        [Test]
        public void EscortAllocation_NormalizesPerRoute()
        {
            float v = CommerceProtectionRules.EscortAllocation(100f, 5f);
            Assert.AreEqual(0.666667f, v, Eps);
        }

        [Test]
        public void EscortAllocation_ZeroRoutesOrForce_ReturnsZero()
        {
            Assert.AreEqual(0f, CommerceProtectionRules.EscortAllocation(100f, 0f), Eps);
            Assert.AreEqual(0f, CommerceProtectionRules.EscortAllocation(0f, 5f), Eps);
            // 負入力もクランプ＝0。
            Assert.AreEqual(0f, CommerceProtectionRules.EscortAllocation(-50f, 5f), Eps);
        }

        // RaiderSuppression: 0.6*(1+0.5*0.4)=0.72
        [Test]
        public void RaiderSuppression_PatrolBoostsEscort()
        {
            float v = CommerceProtectionRules.RaiderSuppression(0.6f, 0.4f);
            Assert.AreEqual(0.72f, v, Eps);
            // 護衛0なら哨戒だけでは制圧0。
            Assert.AreEqual(0f, CommerceProtectionRules.RaiderSuppression(0f, 1f), Eps);
        }

        // ConvoyEffectiveness: 0.6*(1+0.5*0.8)=0.84
        [Test]
        public void ConvoyEffectiveness_DisciplineWithEscort()
        {
            float v = CommerceProtectionRules.ConvoyEffectiveness(0.8f, 0.6f);
            Assert.AreEqual(0.84f, v, Eps);
            // 護衛0なら規律だけでは無力。
            Assert.AreEqual(0f, CommerceProtectionRules.ConvoyEffectiveness(1f, 0f), Eps);
        }

        // RouteSafety: 1-(1-0.72)(1-0.84)=1-0.28*0.16=0.9552
        [Test]
        public void RouteSafety_CombinesSuppressionAndConvoy()
        {
            float v = CommerceProtectionRules.RouteSafety(0.72f, 0.84f);
            Assert.AreEqual(0.9552f, v, Eps);
            // 両方0なら安全0。
            Assert.AreEqual(0f, CommerceProtectionRules.RouteSafety(0f, 0f), Eps);
        }

        // ProtectionCost: 50*(1+1*0.4)=70
        [Test]
        public void ProtectionCost_PatrolAddsCost()
        {
            float v = CommerceProtectionRules.ProtectionCost(50f, 0.4f);
            Assert.AreEqual(70f, v, Eps);
        }

        // TradeDisruptionDamage: (1-0.9552)*0.8=0.03584
        [Test]
        public void TradeDisruptionDamage_SafetyAndDependency()
        {
            float v = CommerceProtectionRules.TradeDisruptionDamage(0.9552f, 0.8f);
            Assert.AreEqual(0.03584f, v, PowEps);
            // 完全安全なら打撃0。
            Assert.AreEqual(0f, CommerceProtectionRules.TradeDisruptionDamage(1f, 1f), PowEps);
        }

        // AttackDefenseBalance: 0.9*(1-0.2)-0.2=0.72-0.2=0.52
        [Test]
        public void AttackDefenseBalance_AttackerAdvantageWhenUnsafe()
        {
            float v = CommerceProtectionRules.AttackDefenseBalance(0.9f, 0.2f);
            Assert.AreEqual(0.52f, v, Eps);
            // 完全安全なら守者有利（-1）。
            Assert.AreEqual(-1f, CommerceProtectionRules.AttackDefenseBalance(0f, 1f), Eps);
        }

        // CommerceProtectionValue: 0.9552 - 70/80 - 0.03584 = 0.04436
        [Test]
        public void CommerceProtectionValue_SafetyMinusCostMinusDamage()
        {
            float v = CommerceProtectionRules.CommerceProtectionValue(0.9552f, 70f, 0.03584f);
            Assert.AreEqual(0.04436f, v, Eps);
        }

        [Test]
        public void CommerceProtectionValue_ClampsToSignedRange()
        {
            // 安全0・巨大コスト・最大打撃＝強い負へ（-1にクランプ）。
            float v = CommerceProtectionRules.CommerceProtectionValue(0f, 10000f, 1f);
            Assert.AreEqual(-1f, v, Eps);
        }

        /// <summary>
        /// 物語テスト：通商国家（経済依存0.9）が護衛を厚く配り哨戒網と船団規律を整えると、
        /// 航路は安全になり途絶打撃が小さくなって通商保護が割に合う。手薄な防衛では攻者有利・打撃甚大で割に合わない。
        /// </summary>
        [Test]
        public void Narrative_StrongProtectionPaysOff_WeakProtectionDoesNot()
        {
            float dependency = 0.9f;

            // 手厚い通商保護：潤沢な護衛（200）を少数の主要航路（4）へ＋広い哨戒（0.8）・高い船団規律（0.9）。
            float allocStrong = CommerceProtectionRules.EscortAllocation(200f, 4f);
            float suppStrong = CommerceProtectionRules.RaiderSuppression(allocStrong, 0.8f);
            float convStrong = CommerceProtectionRules.ConvoyEffectiveness(0.9f, allocStrong);
            float safeStrong = CommerceProtectionRules.RouteSafety(suppStrong, convStrong);
            float costStrong = CommerceProtectionRules.ProtectionCost(200f, 0.8f);
            float dmgStrong = CommerceProtectionRules.TradeDisruptionDamage(safeStrong, dependency);
            float balStrong = CommerceProtectionRules.AttackDefenseBalance(0.5f, safeStrong);
            float valStrong = CommerceProtectionRules.CommerceProtectionValue(safeStrong, costStrong, dmgStrong);

            // 手薄な防衛：わずかな護衛（20）を多くの航路（20）へ＋哨戒乏しく（0.1）・船団規律低い（0.2）。
            float allocWeak = CommerceProtectionRules.EscortAllocation(20f, 20f);
            float suppWeak = CommerceProtectionRules.RaiderSuppression(allocWeak, 0.1f);
            float convWeak = CommerceProtectionRules.ConvoyEffectiveness(0.2f, allocWeak);
            float safeWeak = CommerceProtectionRules.RouteSafety(suppWeak, convWeak);
            float dmgWeak = CommerceProtectionRules.TradeDisruptionDamage(safeWeak, dependency);
            float balWeak = CommerceProtectionRules.AttackDefenseBalance(0.5f, safeWeak);

            // 手厚い方が航路は安全で途絶打撃が小さく、攻防は守者寄り。
            Assert.Greater(safeStrong, safeWeak);
            Assert.Less(dmgStrong, dmgWeak);
            Assert.Less(balStrong, balWeak);
            // 手厚い保護は正味プラス、手薄は途絶打撃で割に合わない（攻者有利）。
            Assert.Greater(valStrong, 0f);
            Assert.Greater(balWeak, balStrong);
        }
    }
}
