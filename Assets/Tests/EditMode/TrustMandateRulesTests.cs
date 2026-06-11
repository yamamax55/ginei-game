using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 信託解消連鎖（LOCK-2 #1450・ロック『統治二論』）の EditMode テスト。
    /// 信託の健全性・侵犯の蓄積・信託の侵食・解消閾値・反乱の正当化・再構成の権利・天への訴え・信託破綻判定を担保。
    /// </summary>
    public class TrustMandateRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>権利保護も合意も満点なら信託は満点・どちらか欠けると不全（既定：合意の重み0.5）。</summary>
        [Test]
        public void MandateTrust_保護と合意で信託が健全()
        {
            // 両方満点＝1.0
            Assert.AreEqual(1f, TrustMandateRules.MandateTrust(1f, 1f), Eps);
            // 保護0.8・合意0.4 → 0.5*0.8 + 0.5*0.4 = 0.6
            Assert.AreEqual(0.6f, TrustMandateRules.MandateTrust(0.8f, 0.4f), Eps);
            // 同意ゼロは半減（合意の重み0.5）
            Assert.AreEqual(0.5f, TrustMandateRules.MandateTrust(1f, 0f), Eps);
        }

        /// <summary>侵犯は一度でなく時間で積み重なる（既存＋新侵犯×dt）。</summary>
        [Test]
        public void ViolationAccumulation_侵犯が蓄積する()
        {
            // 蓄積0.2 に 侵犯0.3×dt1.0 を加算 = 0.5
            Assert.AreEqual(0.5f, TrustMandateRules.ViolationAccumulation(0.2f, 0.3f, 1f), Eps);
            // dt 0 では増えない
            Assert.AreEqual(0.2f, TrustMandateRules.ViolationAccumulation(0.2f, 0.9f, 0f), Eps);
            // 上限1.0でクランプ
            Assert.AreEqual(1f, TrustMandateRules.ViolationAccumulation(0.9f, 0.5f, 1f), Eps);
        }

        /// <summary>侵犯の蓄積が信託を蝕む＝信託×蓄積×侵食重み（既定0.8）。</summary>
        [Test]
        public void TrustErosion_蓄積が信託を侵食する()
        {
            // 信託1.0・蓄積0.5 → 1.0*0.5*0.8 = 0.4
            Assert.AreEqual(0.4f, TrustMandateRules.TrustErosion(1f, 0.5f), Eps);
            // 蓄積ゼロなら侵食なし
            Assert.AreEqual(0f, TrustMandateRules.TrustErosion(1f, 0f), Eps);
        }

        /// <summary>侵犯が閾値を超えると信託が解消される（既定閾値0.6・閾値手前は0）。</summary>
        [Test]
        public void DissolutionThreshold_閾値超で信託解消()
        {
            // 閾値0.6以下は0
            Assert.AreEqual(0f, TrustMandateRules.DissolutionThreshold(0.6f), Eps);
            // 蓄積0.8 → (0.8-0.6)/(1-0.6) = 0.5
            Assert.AreEqual(0.5f, TrustMandateRules.DissolutionThreshold(0.8f), Eps);
            // 蓄積満点は完全解消
            Assert.AreEqual(1f, TrustMandateRules.DissolutionThreshold(1f), Eps);
        }

        /// <summary>信託違反が著しく平和的救済が尽きると反乱が正当化される（ロックの抵抗権）。</summary>
        [Test]
        public void RebellionJustification_平和的救済の枯渇で正当化()
        {
            // 侵食0.6・救済枯渇1.0 → 0.6
            Assert.AreEqual(0.6f, TrustMandateRules.RebellionJustification(0.6f, 1f), Eps);
            // 救済が残るうちは割引（枯渇0.5 → 0.3）
            Assert.AreEqual(0.3f, TrustMandateRules.RebellionJustification(0.6f, 0.5f), Eps);
            // 救済が全く尽きていなければ正当化されない
            Assert.AreEqual(0f, TrustMandateRules.RebellionJustification(0.9f, 0f), Eps);
        }

        /// <summary>解消が進むほど人民が政府を作り直す権利は強い（恒等写像）。</summary>
        [Test]
        public void RightToReconstitute_解消で再構成の権利()
        {
            Assert.AreEqual(0.7f, TrustMandateRules.RightToReconstitute(0.7f), Eps);
            Assert.AreEqual(1f, TrustMandateRules.RightToReconstitute(1.5f), Eps); // クランプ
        }

        /// <summary>地上の裁定者がいない究極の場合の実力抵抗＝侵害×裁定者不在（天への訴え）。</summary>
        [Test]
        public void AppealToHeaven_裁定者不在で実力抵抗()
        {
            // 侵害0.8・裁定者不在1.0 → 0.8
            Assert.AreEqual(0.8f, TrustMandateRules.AppealToHeaven(0.8f, 1f), Eps);
            // 上位の裁定者がいれば訴えるべき相手がいる＝天への訴えは不要
            Assert.AreEqual(0f, TrustMandateRules.AppealToHeaven(0.9f, 0f), Eps);
        }

        /// <summary>信託の侵食が閾値を超えると政府は信託を失う（既定閾値0.6）。</summary>
        [Test]
        public void IsTrustBroken_閾値超で信託破綻()
        {
            Assert.IsTrue(TrustMandateRules.IsTrustBroken(0.7f));   // 0.6超
            Assert.IsFalse(TrustMandateRules.IsTrustBroken(0.6f));  // 境界は未破綻
            Assert.IsFalse(TrustMandateRules.IsTrustBroken(0.3f));
        }
    }
}
