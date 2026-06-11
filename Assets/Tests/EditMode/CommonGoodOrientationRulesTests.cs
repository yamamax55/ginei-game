using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>公益-私益の政体品質スコアの純ロジック検証（#1499・アリストテレス）。既定Paramsで期待値を固定。</summary>
    public class CommonGoodOrientationRulesTests
    {
        const float EPS = 1e-4f;

        /// <summary>公益志向度＝公益貢献−私益横領（−1私益〜+1公益）。公益寄りで正・私益寄りで負。</summary>
        [Test]
        public void CommonGoodScore_IsBenefitMinusCapture()
        {
            // 公益0.8・私益0.2 → +0.6（公益志向）
            Assert.AreEqual(0.6f, CommonGoodOrientationRules.CommonGoodScore(0.8f, 0.2f), EPS);
            // 公益0.2・私益0.9 → −0.7（私益志向）
            Assert.AreEqual(-0.7f, CommonGoodOrientationRules.CommonGoodScore(0.2f, 0.9f), EPS);
            // 完全私益 → −1にクランプ
            Assert.AreEqual(-1f, CommonGoodOrientationRules.CommonGoodScore(0f, 1f), EPS);
        }

        /// <summary>政体の品質＝公益志向×法の支配。正しい政体（公益＋法の支配）ほど高い。</summary>
        [Test]
        public void PolityQuality_OrientationTimesRuleOfLaw()
        {
            // 公益志向+1.0（→1.0に写る）×法の支配0.8 = 0.8
            Assert.AreEqual(0.8f, CommonGoodOrientationRules.PolityQuality(1f, 0.8f), EPS);
            // 公益志向0（→0.5）×法の支配0.8 = 0.4
            Assert.AreEqual(0.4f, CommonGoodOrientationRules.PolityQuality(0f, 0.8f), EPS);
            // 私益志向−1（→0）→ 法の支配があっても0
            Assert.AreEqual(0f, CommonGoodOrientationRules.PolityQuality(-1f, 1f), EPS);
        }

        /// <summary>腐敗加速＝私益志向ほど1.0を超えて加速し公益志向ほど減速する。スコア0で等倍。</summary>
        [Test]
        public void CorruptionAcceleration_PrivateInterestAccelerates()
        {
            // 完全私益−1.0 → 1.0+1.0×1.0 = 2.0倍（腐敗が加速）
            Assert.AreEqual(2f, CommonGoodOrientationRules.CorruptionAcceleration(-1f), EPS);
            // スコア0 → 等倍1.0
            Assert.AreEqual(1f, CommonGoodOrientationRules.CorruptionAcceleration(0f), EPS);
            // 完全公益+1.0 → 1.0−1.0×0.5 = 0.5倍（腐敗を抑制）
            Assert.AreEqual(0.5f, CommonGoodOrientationRules.CorruptionAcceleration(1f), EPS);
            // 私益−0.5 → 1.5倍 ＞ 公益+0.5 → 0.75倍（私益ほど速い）
            Assert.Greater(CommonGoodOrientationRules.CorruptionAcceleration(-0.5f),
                           CommonGoodOrientationRules.CorruptionAcceleration(0.5f));
        }

        /// <summary>公益志向の制度化＝累進度とエリート制約の加重和（既定は同等0.5）。</summary>
        [Test]
        public void Progressivity_WeightedSumOfProgAndConstraint()
        {
            // 累進0.6・制約0.4 → 0.6×0.5+0.4×0.5 = 0.5
            Assert.AreEqual(0.5f, CommonGoodOrientationRules.Progressivity(0.6f, 0.4f), EPS);
            // 両者最大 → 1.0
            Assert.AreEqual(1f, CommonGoodOrientationRules.Progressivity(1f, 1f), EPS);
        }

        /// <summary>堕落形態への転化＝正しい政体ほど・私益追求ほど進む（両者の積）。</summary>
        [Test]
        public void DegenerateForm_LegitimateTimesSelfInterest()
        {
            // 正統0.8×私益0.5 = 0.4
            Assert.AreEqual(0.4f, CommonGoodOrientationRules.DegenerateForm(0.8f, 0.5f), EPS);
            // 私益0 → 転化なし
            Assert.AreEqual(0f, CommonGoodOrientationRules.DegenerateForm(1f, 0f), EPS);
        }

        /// <summary>公的信頼＝政体の品質をそのまま反映（公益志向の政体ほど信頼を得る）。</summary>
        [Test]
        public void PublicTrust_ReflectsPolityQuality()
        {
            float quality = CommonGoodOrientationRules.PolityQuality(1f, 0.8f); // 0.8
            Assert.AreEqual(0.8f, CommonGoodOrientationRules.PublicTrust(quality), EPS);
        }

        /// <summary>制度的制約＝説明責任×透明性。どちらか欠ければ縛れない。</summary>
        [Test]
        public void InstitutionalConstraint_AccountabilityTimesTransparency()
        {
            // 説明責任0.8×透明性0.5 = 0.4
            Assert.AreEqual(0.4f, CommonGoodOrientationRules.InstitutionalConstraint(0.8f, 0.5f), EPS);
            // 透明性0 → 制約0（どちらか欠ければ無力）
            Assert.AreEqual(0f, CommonGoodOrientationRules.InstitutionalConstraint(1f, 0f), EPS);
        }

        /// <summary>堕落政体の判定＝公益志向度が既定閾値0未満（私益のための僭主/寡頭/衆愚）で true。</summary>
        [Test]
        public void IsDegeneratePolity_TrueWhenPrivateOriented()
        {
            // 私益志向（−0.3）→ 堕落政体
            Assert.IsTrue(CommonGoodOrientationRules.IsDegeneratePolity(-0.3f));
            // 公益志向（+0.3）→ 正しい政体
            Assert.IsFalse(CommonGoodOrientationRules.IsDegeneratePolity(0.3f));
            // 境界0 → false（閾値未満でないため）
            Assert.IsFalse(CommonGoodOrientationRules.IsDegeneratePolity(0f));
        }
    }
}
