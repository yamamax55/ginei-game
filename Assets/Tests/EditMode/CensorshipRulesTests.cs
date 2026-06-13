using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 検閲水準と情報自由度の純ロジックのテスト（MILL-1 #1474・ミル『自由論』）。
    /// 短期安定・長期腐敗・非対称トレードオフ・封殺された真理の喪失・死んだ教条・隠れた誤りの蓄積・
    /// 情報自由の便益・検閲の罠判定を既定 Params の具体値で固定する。
    /// </summary>
    public class CensorshipRulesTests
    {
        const float E = 1e-4f;

        /// <summary>短期安定＝検閲水準×抑圧効率×異論。検閲は即座に異論を抑え体制を安定させる。</summary>
        [Test]
        public void ShortTermStability_検閲が異論を抑えて即座に安定させる()
        {
            // 0.6 * 0.8(効率) * 0.5 = 0.24
            Assert.AreEqual(0.24f, CensorshipRules.ShortTermStability(0.6f, 0.5f), E);
            // 異論ゼロなら安定への寄与もゼロ
            Assert.AreEqual(0f, CensorshipRules.ShortTermStability(1f, 0f), E);
        }

        /// <summary>長期腐敗＝検閲水準×腐敗率×dt。批判なき権力はじわじわ腐る。</summary>
        [Test]
        public void LongTermCorruption_検閲が長期的に腐敗を蓄積する()
        {
            // 0.8 * 0.1 * 2 = 0.16
            Assert.AreEqual(0.16f, CensorshipRules.LongTermCorruption(0.8f, 2f), E);
            // 検閲ゼロなら腐敗の増分もゼロ
            Assert.AreEqual(0f, CensorshipRules.LongTermCorruption(0f, 5f), E);
        }

        /// <summary>非対称トレードオフ＝短期安定−長期腐敗×重み(2.0)。長期腐敗が重く効き割に合わない。</summary>
        [Test]
        public void AsymmetricTradeoff_短期安定と長期腐敗の非対称()
        {
            // 0.5 - 0.3*2.0 = -0.1（長期腐敗が重く効いて負＝割に合わない）
            Assert.AreEqual(-0.1f, CensorshipRules.AsymmetricTradeoff(0.5f, 0.3f), E);
            // 腐敗ゼロなら短期安定がそのまま正味
            Assert.AreEqual(0.4f, CensorshipRules.AsymmetricTradeoff(0.4f, 0f), E);
        }

        /// <summary>封殺された真理の喪失＝検閲水準×封じた意見の真理割合。封じた意見は正しかったかもしれない。</summary>
        [Test]
        public void SuppressedTruthLoss_封殺された意見に含まれた真理が失われる()
        {
            // 0.7 * 0.5 = 0.35
            Assert.AreEqual(0.35f, CensorshipRules.SuppressedTruthLoss(0.7f, 0.5f), E);
            // 検閲ゼロなら喪失もゼロ
            Assert.AreEqual(0f, CensorshipRules.SuppressedTruthLoss(0f, 1f), E);
        }

        /// <summary>死んだ教条＝検閲水準×教条化率(0.08)×dt。反論なき支配的意見は形骸化する。</summary>
        [Test]
        public void DeadDogma_反論のない支配的意見が死んだ教条になる()
        {
            // 0.5 * 0.08 * 2 = 0.08
            Assert.AreEqual(0.08f, CensorshipRules.DeadDogma(0.5f, 2f), E);
            // 検閲ゼロ（反論が生きている）なら教条化しない
            Assert.AreEqual(0f, CensorshipRules.DeadDogma(0f, 3f), E);
        }

        /// <summary>隠れた誤りの蓄積＝検閲水準ぶんは表に出ず溜まる。自由なら露見して溜まらない。</summary>
        [Test]
        public void HiddenErrorAccumulation_検閲下では誤りが隠れて積もる()
        {
            // 0.2 + 0.5(newErrors)*0.8(検閲)*0.1(率)*3(dt) = 0.2 + 0.12 = 0.32
            Assert.AreEqual(0.32f, CensorshipRules.HiddenErrorAccumulation(0.2f, 0.8f, 0.5f, 3f), E);
            // 検閲ゼロなら新規の誤りは溜まらず既存ストックのまま
            Assert.AreEqual(0.2f, CensorshipRules.HiddenErrorAccumulation(0.2f, 0f, 1f, 5f), E);
        }

        /// <summary>情報自由の便益＝情報自由度×便益率(0.05)。自由は誤りの早期発見と真理の精錬をもたらす。</summary>
        [Test]
        public void InformationFreedomBenefit_情報自由が長期の健全性をもたらす()
        {
            // 0.8 * 0.05 = 0.04
            Assert.AreEqual(0.04f, CensorshipRules.InformationFreedomBenefit(0.8f), E);
            // 自由ゼロなら便益もゼロ
            Assert.AreEqual(0f, CensorshipRules.InformationFreedomBenefit(0f), E);
        }

        /// <summary>検閲の罠＝長期腐敗が閾値(0.5)超かつ短期安定が閾値以上。目先の安定が腐敗を覆い隠す。</summary>
        [Test]
        public void IsCensorshipTrap_短期安定に釣られ長期腐敗に陥った罠を判定する()
        {
            // 腐敗0.6>0.5 かつ 安定0.7>=0.5 → 罠
            Assert.IsTrue(CensorshipRules.IsCensorshipTrap(0.6f, 0.7f));
            // 腐敗が閾値以下なら罠でない
            Assert.IsFalse(CensorshipRules.IsCensorshipTrap(0.4f, 0.9f));
            // 短期安定が閾値未満なら（腐敗を覆い隠せていない）罠でない
            Assert.IsFalse(CensorshipRules.IsCensorshipTrap(0.6f, 0.3f));
        }
    }
}
