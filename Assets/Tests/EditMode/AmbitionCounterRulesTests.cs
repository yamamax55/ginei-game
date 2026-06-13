using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>野心相殺設計（FED-2 #1476）の純ロジックテスト。既定Paramsで期待値固定。</summary>
    public class AmbitionCounterRulesTests
    {
        /// <summary>制度的自己利益＝愛着と利害の重み付き合成。両0で0、両満で1、既定（重み均等）は単純平均。</summary>
        [Test]
        public void InstitutionalSelfInterest_重み付き合成()
        {
            Assert.AreEqual(0f, AmbitionCounterRules.InstitutionalSelfInterest(0f, 0f), 1e-4f);
            Assert.AreEqual(1f, AmbitionCounterRules.InstitutionalSelfInterest(1f, 1f), 1e-4f);
            // 既定は愛着0.5/利害0.5＝平均。愛着0.8×利害0.4 → 0.6
            Assert.AreEqual(0.6f, AmbitionCounterRules.InstitutionalSelfInterest(0.8f, 0.4f), 1e-4f);
        }

        /// <summary>相互牽制力＝二部門の自己利益の幾何平均。片方が無関心(0)なら相殺せず0。</summary>
        [Test]
        public void CounterveilingForce_両者が守る動機で最大_片方0で消える()
        {
            Assert.AreEqual(0f, AmbitionCounterRules.CounterveilingForce(0f, 0.9f), 1e-4f);
            Assert.AreEqual(0.5f, AmbitionCounterRules.CounterveilingForce(0.5f, 0.5f), 1e-4f);
            // 0.9×0.4 の平方根 ≒ 0.6
            Assert.AreEqual(Mathf.Sqrt(0.36f), AmbitionCounterRules.CounterveilingForce(0.9f, 0.4f), 1e-4f);
        }

        /// <summary>抑制の発動＝侵害があって初めて自己利益が反撃する。越権0なら発動0。</summary>
        [Test]
        public void CheckActivation_侵害なしは発動せず_侵害で牽制比例()
        {
            Assert.AreEqual(0f, AmbitionCounterRules.CheckActivation(0.8f, 0f), 1e-4f);
            // 牽制力0.8 × 越権0.5 = 0.4
            Assert.AreEqual(0.4f, AmbitionCounterRules.CheckActivation(0.8f, 0.5f), 1e-4f);
        }

        /// <summary>権力集中への抵抗＝相互牽制力に直結。牽制が強いほど独占を阻む。</summary>
        [Test]
        public void PowerConcentrationResistance_牽制力に直結()
        {
            Assert.AreEqual(0f, AmbitionCounterRules.PowerConcentrationResistance(0f), 1e-4f);
            Assert.AreEqual(0.7f, AmbitionCounterRules.PowerConcentrationResistance(0.7f), 1e-4f);
        }

        /// <summary>紙の防壁の弱さ＝裏打ちなしは下限(0.2)止まり、満ちると法的制限の額面まで効く。</summary>
        [Test]
        public void ParchmentBarrierWeakness_自己利益の裏打ちで初めて機能()
        {
            // 法的制限1.0だが裏打ち0 → floor(0.2)止まり
            Assert.AreEqual(0.2f, AmbitionCounterRules.ParchmentBarrierWeakness(1f, 0f), 1e-4f);
            // 裏打ち満ちる → 法的制限の額面そのまま
            Assert.AreEqual(1f, AmbitionCounterRules.ParchmentBarrierWeakness(1f, 1f), 1e-4f);
            // 法的制限0.8・裏打ち0.5 → 0.8×(0.2+0.8×0.5)=0.8×0.6=0.48
            Assert.AreEqual(0.48f, AmbitionCounterRules.ParchmentBarrierWeakness(0.8f, 0.5f), 1e-4f);
        }

        /// <summary>野心の整合＝個人の野心と職務の利害が結びつくほど強い。どちらか0なら整合せず0。</summary>
        [Test]
        public void AmbitionAlignment_野心と職務の結合は積()
        {
            Assert.AreEqual(0f, AmbitionCounterRules.AmbitionAlignment(0f, 0.9f), 1e-4f);
            Assert.AreEqual(0.72f, AmbitionCounterRules.AmbitionAlignment(0.8f, 0.9f), 1e-4f);
        }

        /// <summary>結託リスク＝部門間の共有利害に直結。利害が同じ方を向くほど牽制でなく結託へ。</summary>
        [Test]
        public void CollusionRisk_共有利害に直結()
        {
            Assert.AreEqual(0f, AmbitionCounterRules.CollusionRisk(0f), 1e-4f);
            Assert.AreEqual(0.85f, AmbitionCounterRules.CollusionRisk(0.85f), 1e-4f);
        }

        /// <summary>自己執行的均衡判定＝牽制力が既定しきい値(0.5)以上で自律維持が成立。</summary>
        [Test]
        public void IsSelfEnforcingBalance_しきい値で判定()
        {
            Assert.IsFalse(AmbitionCounterRules.IsSelfEnforcingBalance(0.49f));
            Assert.IsTrue(AmbitionCounterRules.IsSelfEnforcingBalance(0.5f));
            Assert.IsTrue(AmbitionCounterRules.IsSelfEnforcingBalance(0.8f));
        }

        /// <summary>入力は全てクランプされ範囲外でも破綻しない。</summary>
        [Test]
        public void 入力クランプ()
        {
            Assert.AreEqual(1f, AmbitionCounterRules.InstitutionalSelfInterest(5f, 5f), 1e-4f);
            Assert.AreEqual(0f, AmbitionCounterRules.CounterveilingForce(-3f, 0.5f), 1e-4f);
            Assert.AreEqual(0f, AmbitionCounterRules.CheckActivation(-1f, 2f), 1e-4f);
        }
    }
}
