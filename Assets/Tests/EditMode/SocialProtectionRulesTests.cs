using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>社会保護制度の内生的成長（POLA-5 #1602・二重運動の保護側ラチェット）の純ロジックテスト。</summary>
    public class SocialProtectionRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>保護需要＝市場圧力×不安定化×感度。どちらかが0なら需要0（市場が生活を脅かして初めて社会が動く）。</summary>
        [Test]
        public void ProtectionDemand_市場圧力と不安定化の積()
        {
            // 0.8 × 0.5 × 1.0 = 0.4
            Assert.AreEqual(0.4f, SocialProtectionRules.ProtectionDemand(0.8f, 0.5f), Eps);
            // 圧力ゼロ→需要ゼロ
            Assert.AreEqual(0f, SocialProtectionRules.ProtectionDemand(0f, 1f), Eps);
            // 不安定化ゼロ→需要ゼロ
            Assert.AreEqual(0f, SocialProtectionRules.ProtectionDemand(1f, 0f), Eps);
        }

        /// <summary>保護の積み上がり＝需要へ向け危機加速つきで build。需要が高いほど速く積み上がる。</summary>
        [Test]
        public void ProtectionBuildup_危機ほど速く積み上がる()
        {
            // current=0, demand=0.8, dt=1: speed=0.5×(1+1.0×0.8)=0.9, step=0.9, lerp(0,0.8,0.9)=0.72
            float built = SocialProtectionRules.ProtectionBuildup(0f, 0.8f, 1f);
            Assert.AreEqual(0.72f, built, Eps);

            // 低需要は遅い: current=0, demand=0.4, dt=1: speed=0.5×(1+0.4)=0.7, step=0.7, lerp(0,0.4,0.7)=0.28
            float slow = SocialProtectionRules.ProtectionBuildup(0f, 0.4f, 1f);
            Assert.AreEqual(0.28f, slow, Eps);
            Assert.Less(slow, built); // 危機（高需要）の方が速い
        }

        /// <summary>需要が現在保護以下なら積み増さない（満たされた保護はそれ以上 build されない）。</summary>
        [Test]
        public void ProtectionBuildup_需要が満たされていれば積み増さない()
        {
            Assert.AreEqual(0.6f, SocialProtectionRules.ProtectionBuildup(0.6f, 0.3f, 1f), Eps);
            Assert.AreEqual(0.6f, SocialProtectionRules.ProtectionBuildup(0.6f, 0.6f, 1f), Eps);
        }

        /// <summary>ラチェット抵抗＝保護水準×(基礎+受益者の盾×受益者比)。受益者が多いほど撤廃に抵抗。</summary>
        [Test]
        public void RatchetResistance_受益者が盾になる()
        {
            // prot=0.8, vested=0.5: 0.8×(0.5+0.4×0.5)=0.8×0.7=0.56
            Assert.AreEqual(0.56f, SocialProtectionRules.RatchetResistance(0.8f, 0.5f), Eps);
            // 受益者ゼロでも基礎抵抗: 0.8×0.5=0.4
            Assert.AreEqual(0.4f, SocialProtectionRules.RatchetResistance(0.8f, 0f), Eps);
            // 受益者多いほど抵抗が強い
            Assert.Greater(SocialProtectionRules.RatchetResistance(0.8f, 1f),
                           SocialProtectionRules.RatchetResistance(0.8f, 0f));
        }

        /// <summary>保護の縮小＝抵抗を上回る分だけ。抵抗が強いほど減りにくく、抵抗1で不動（ラチェット）。</summary>
        [Test]
        public void ProtectionDecay_ラチェットで減りにくい()
        {
            // current=0.6, resist=0.0, dt=1: drop=0.2×1×1=0.2 → 0.4
            float weak = SocialProtectionRules.ProtectionDecay(0.6f, 0f, 1f);
            Assert.AreEqual(0.4f, weak, Eps);

            // current=0.6, resist=0.5, dt=1: drop=0.2×0.5×1=0.1 → 0.5
            float strong = SocialProtectionRules.ProtectionDecay(0.6f, 0.5f, 1f);
            Assert.AreEqual(0.5f, strong, Eps);
            Assert.Greater(strong, weak); // 抵抗が強いほど縮まない（残る）

            // 抵抗1.0で完全に不動
            Assert.AreEqual(0.6f, SocialProtectionRules.ProtectionDecay(0.6f, 1f, 1f), Eps);
        }

        /// <summary>市場効率トレードオフ＝保護が高いほど効率が下がる（安定と効率の交換）。</summary>
        [Test]
        public void MarketEfficiencyTradeoff_保護が効率を下げる()
        {
            // protection=0 → 1.0（無損失）
            Assert.AreEqual(1f, SocialProtectionRules.MarketEfficiencyTradeoff(0f), Eps);
            // protection=0.5 → 1-0.6×0.5=0.7
            Assert.AreEqual(0.7f, SocialProtectionRules.MarketEfficiencyTradeoff(0.5f), Eps);
            // protection=1 → 1-0.6=0.4
            Assert.AreEqual(0.4f, SocialProtectionRules.MarketEfficiencyTradeoff(1f), Eps);
        }

        /// <summary>安定寄与＝保護×不安定化×係数（守るべき混乱があってこそ保護は安定をもたらす）。</summary>
        [Test]
        public void StabilityGain_保護が不安定化を和らげる()
        {
            // prot=0.8, dislocation=0.5: 0.5×0.8×0.5=0.2
            Assert.AreEqual(0.2f, SocialProtectionRules.StabilityGain(0.8f, 0.5f), Eps);
            // 不安定化ゼロなら寄与ゼロ（守るべき混乱がない）
            Assert.AreEqual(0f, SocialProtectionRules.StabilityGain(1f, 0f), Eps);
        }

        /// <summary>二重運動の緊張＝自由化と保護の積。両方高いほど緊張が高まり、片方弱ければ小さい。</summary>
        [Test]
        public void DoubleMovementTension_自由化と保護のせめぎ合い()
        {
            // lib=0.8, prot=0.7: 0.56
            Assert.AreEqual(0.56f, SocialProtectionRules.DoubleMovementTension(0.8f, 0.7f), Eps);
            // 片方ゼロなら緊張ゼロ
            Assert.AreEqual(0f, SocialProtectionRules.DoubleMovementTension(0f, 1f), Eps);
            Assert.AreEqual(0f, SocialProtectionRules.DoubleMovementTension(1f, 0f), Eps);
        }

        /// <summary>過保護判定＝保護水準が既定しきい値0.8以上で硬直化（守りすぎは活力を失う）。</summary>
        [Test]
        public void IsOverprotected_しきい値で硬直化()
        {
            Assert.IsFalse(SocialProtectionRules.IsOverprotected(0.7f));
            Assert.IsTrue(SocialProtectionRules.IsOverprotected(0.8f));
            Assert.IsTrue(SocialProtectionRules.IsOverprotected(0.95f));
        }
    }
}
