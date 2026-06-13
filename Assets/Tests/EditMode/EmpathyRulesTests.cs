using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>共感評判エンジン（道徳感情論 TMS-1 #1578）の純ロジックの担保。</summary>
    public class EmpathyRulesTests
    {
        const float Tol = 1e-4f;

        /// <summary>共感反応＝慈悲は正・残虐は負、近い観察者ほど絶対値が大きい。</summary>
        [Test]
        public void SympatheticResponse_NearObserverFeelsStronger()
        {
            // 既定 proximityWeight=0.5：感受性 = 0.5 + 0.5×prox
            // 慈悲(valence=+1)・近い(prox=1)：1×(0.5+0.5)=1.0
            Assert.AreEqual(1f, EmpathyRules.SympatheticResponse(1f, 1f), Tol);
            // 慈悲・遠い(prox=0)：1×0.5=0.5（遠くても0にはならない）
            Assert.AreEqual(0.5f, EmpathyRules.SympatheticResponse(1f, 0f), Tol);
            // 残虐(valence=-1)・近い：-1.0（否認方向）
            Assert.AreEqual(-1f, EmpathyRules.SympatheticResponse(-1f, 1f), Tol);
            // 近い方が遠いより強く感じる
            Assert.Greater(EmpathyRules.SympatheticResponse(1f, 1f),
                           EmpathyRules.SympatheticResponse(1f, 0f));
        }

        /// <summary>道徳的是認＝正の共感は是認(&gt;0.5)・負は否認(&lt;0.5)・0は中立0.5。</summary>
        [Test]
        public void MoralApproval_PraiseAndBlame()
        {
            // 既定 approvalSensitivity=1.0
            Assert.AreEqual(1f, EmpathyRules.MoralApproval(1f), Tol);   // 全是認
            Assert.AreEqual(0f, EmpathyRules.MoralApproval(-1f), Tol);  // 全否認
            Assert.AreEqual(0.5f, EmpathyRules.MoralApproval(0f), Tol); // 中立
            Assert.AreEqual(0.75f, EmpathyRules.MoralApproval(0.5f), Tol);
        }

        /// <summary>適宜性 propriety＝中庸に近いほど高く、過剰/過小で下がる。</summary>
        [Test]
        public void Propriety_PeaksAtNorm()
        {
            // 行動が規範に一致＝1.0
            Assert.AreEqual(1f, EmpathyRules.Propriety(0.5f, 0.5f), Tol);
            // 過剰：|1.0-0.5|=0.5 ずれ → 0.5
            Assert.AreEqual(0.5f, EmpathyRules.Propriety(1f, 0.5f), Tol);
            // 過小：|0.0-0.5|=0.5 ずれ → 0.5
            Assert.AreEqual(0.5f, EmpathyRules.Propriety(0f, 0.5f), Tol);
            // 完全な隔たり → 0
            Assert.AreEqual(0f, EmpathyRules.Propriety(1f, 0f), Tol);
        }

        /// <summary>支持修正子＝是認0.5を中立として ±supportSwing へ写す。</summary>
        [Test]
        public void SupportModifier_SwingsAroundNeutral()
        {
            // 既定 supportSwing=0.3
            Assert.AreEqual(0.3f, EmpathyRules.SupportModifier(1f), Tol);   // 全是認 +0.3
            Assert.AreEqual(-0.3f, EmpathyRules.SupportModifier(0f), Tol);  // 全否認 -0.3
            Assert.AreEqual(0f, EmpathyRules.SupportModifier(0.5f), Tol);   // 中立 0
        }

        /// <summary>忠誠修正子＝是認×個人的紐帯。紐帯0なら忠誠は動かない。</summary>
        [Test]
        public void LoyaltyModifier_ScaledByBond()
        {
            // 既定 loyaltySwing=0.4
            // 全是認・紐帯1.0：(1-0.5)×2×0.4×1=0.4
            Assert.AreEqual(0.4f, EmpathyRules.LoyaltyModifier(1f, 1f), Tol);
            // 全是認・紐帯0.5：0.2
            Assert.AreEqual(0.2f, EmpathyRules.LoyaltyModifier(1f, 0.5f), Tol);
            // 紐帯0なら是認しても動かない
            Assert.AreEqual(0f, EmpathyRules.LoyaltyModifier(1f, 0f), Tol);
        }

        /// <summary>opinion 修正子＝是認×伝播範囲。広く伝わるほど大きく動く。</summary>
        [Test]
        public void OpinionShift_ScaledByReach()
        {
            // 既定 opinionSwing=0.5
            // 全是認・到達1.0：(1-0.5)×2×0.5×1=0.5
            Assert.AreEqual(0.5f, EmpathyRules.OpinionShift(1f, 1f), Tol);
            // 全否認・到達1.0：-0.5
            Assert.AreEqual(-0.5f, EmpathyRules.OpinionShift(0f, 1f), Tol);
            // 到達0なら広まらず動かない
            Assert.AreEqual(0f, EmpathyRules.OpinionShift(1f, 0f), Tol);
        }

        /// <summary>憤り＝危害の深刻さ×観察者の近さ。身近な不正ほど強い憤りを生む。</summary>
        [Test]
        public void ResentmentFromInjustice_StrongerWhenClose()
        {
            // 既定 proximityWeight=0.5, resentmentScale=1.0
            // 深刻1.0・近い1.0：1×(0.5+0.5)×1=1.0
            Assert.AreEqual(1f, EmpathyRules.ResentmentFromInjustice(1f, 1f), Tol);
            // 深刻1.0・遠い0.0：1×0.5×1=0.5
            Assert.AreEqual(0.5f, EmpathyRules.ResentmentFromInjustice(1f, 0f), Tol);
            // 危害なしなら憤りなし
            Assert.AreEqual(0f, EmpathyRules.ResentmentFromInjustice(0f, 1f), Tol);
            Assert.Greater(EmpathyRules.ResentmentFromInjustice(1f, 1f),
                           EmpathyRules.ResentmentFromInjustice(1f, 0f));
        }

        /// <summary>道徳的運＝意図と結果の中庸。善意でも悪い結果なら評価が割引かれる。</summary>
        [Test]
        public void MoralLuck_OutcomeDiscountsIntent()
        {
            // 善意(+1)で良い結果(+1)：そのまま +1
            Assert.AreEqual(1f, EmpathyRules.MoralLuck(1f, 1f), Tol);
            // 善意(+1)で悪い結果(-1)：割引かれて 0（意図より低い）
            Assert.AreEqual(0f, EmpathyRules.MoralLuck(1f, -1f), Tol);
            Assert.Less(EmpathyRules.MoralLuck(1f, -1f), 1f);
            // 悪意(-1)で良い結果(+1)：割増されて 0（意図より高い）
            Assert.AreEqual(0f, EmpathyRules.MoralLuck(-1f, 1f), Tol);
            Assert.Greater(EmpathyRules.MoralLuck(-1f, 1f), -1f);
        }
    }
}
