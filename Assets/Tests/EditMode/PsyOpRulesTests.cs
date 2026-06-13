using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 積極的情報戦・世論戦 <see cref="PsyOpRules"/> の純ロジック検証（ULW-3 #1386）。
    /// メッセージの浸透・合意の侵食・偽情報の効果・戦意の破壊・信頼の腐食・防諜の耐性・露見の逆効果・合意崩壊判定。
    /// 既定 PsyOpParams（最大侵食0.15・最大腐食0.12・防諜耐性0.7・露見逆効果1.5・崩壊閾値0.6）で期待値固定。
    /// </summary>
    public class PsyOpRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>浸透＝到達×受容性。既存の不満が大きいほど刺さる。</summary>
        [Test]
        public void MessagePenetration_到達と受容性の積()
        {
            // 0.8 × 0.5 = 0.4
            Assert.AreEqual(0.4f, PsyOpRules.MessagePenetration(0.8f, 0.5f), Eps);
            // 届かなければ浸透しない
            Assert.AreEqual(0f, PsyOpRules.MessagePenetration(0f, 1f), Eps);
            // 受容性が高い（不満が大きい）ほど刺さる
            Assert.Greater(PsyOpRules.MessagePenetration(0.6f, 0.9f), PsyOpRules.MessagePenetration(0.6f, 0.3f));
        }

        /// <summary>合意侵食＝浸透×(1＋既存分断)×最大侵食×dt。分断があるほど深く効く。</summary>
        [Test]
        public void ConsensusErosion_既存分断が侵食を増幅()
        {
            // 浸透0.5・分断0・dt1 → 0.5 × 1.0 × 0.15 × 1 = 0.075
            Assert.AreEqual(0.075f, PsyOpRules.ConsensusErosion(0.5f, 0f, 1f), Eps);
            // 分断0.4 → 0.5 × 1.4 × 0.15 = 0.105
            Assert.AreEqual(0.105f, PsyOpRules.ConsensusErosion(0.5f, 0.4f, 1f), Eps);
            // 既存の分断があるほど侵食が大きい
            Assert.Greater(PsyOpRules.ConsensusErosion(0.5f, 0.8f, 1f), PsyOpRules.ConsensusErosion(0.5f, 0f, 1f));
        }

        /// <summary>偽情報の効果＝もっともらしく事実検証が弱いほど効く。</summary>
        [Test]
        public void DisinformationEffect_もっともらしさと検証弱さ()
        {
            // 偽物語0.8 × もっともらしさ0.75 × (1−検証0.0) = 0.6
            Assert.AreEqual(0.6f, PsyOpRules.DisinformationEffect(0.8f, 0.75f, 0f), Eps);
            // 事実検証が完璧なら効かない
            Assert.AreEqual(0f, PsyOpRules.DisinformationEffect(1f, 1f, 1f), Eps);
            // 検証が弱いほど効く
            Assert.Greater(PsyOpRules.DisinformationEffect(0.8f, 0.8f, 0.2f), PsyOpRules.DisinformationEffect(0.8f, 0.8f, 0.7f));
        }

        /// <summary>戦意の破壊＝合意侵食×敵の戦争支持。厭戦を煽って内部から崩す。</summary>
        [Test]
        public void MoraleSubversion_合意侵食と戦意の積()
        {
            // 侵食0.5 × 戦争支持0.6 = 0.3
            Assert.AreEqual(0.3f, PsyOpRules.MoraleSubversion(0.5f, 0.6f), Eps);
            // 合意が崩れていなければ戦意は削げない
            Assert.AreEqual(0f, PsyOpRules.MoraleSubversion(0f, 0.9f), Eps);
        }

        /// <summary>信頼の腐食＝浸透×(1−正統性)×最大腐食×dt。正統性が低い政府ほど崩しやすい。</summary>
        [Test]
        public void TrustCorrosion_正統性が低いほど崩れる()
        {
            // 浸透0.5・正統性0.4・dt1 → 0.5 × 0.6 × 0.12 × 1 = 0.036
            Assert.AreEqual(0.036f, PsyOpRules.TrustCorrosion(0.5f, 0.4f, 1f), Eps);
            // 正統な政府ほど楔が入らない（腐食が小さい）
            Assert.Less(PsyOpRules.TrustCorrosion(0.5f, 0.9f, 1f), PsyOpRules.TrustCorrosion(0.5f, 0.2f, 1f));
            // 完全に正統な政府は信頼を崩せない
            Assert.AreEqual(0f, PsyOpRules.TrustCorrosion(0.8f, 1f, 1f), Eps);
        }

        /// <summary>防諜＝事実検証とメディアリテラシーが心理作戦を防ぐ耐性。</summary>
        [Test]
        public void Counterintelligence_検証とリテラシーが耐性()
        {
            // defense = 1−(1−0.5)(1−0.5) = 0.75、×0.7 = 0.525
            Assert.AreEqual(0.525f, PsyOpRules.Counterintelligence(0.5f, 0.5f), Eps);
            // どちらも0なら耐性なし
            Assert.AreEqual(0f, PsyOpRules.Counterintelligence(0f, 0f), Eps);
            // リテラシーが高いほど耐性が高い
            Assert.Greater(PsyOpRules.Counterintelligence(0.3f, 0.9f), PsyOpRules.Counterintelligence(0.3f, 0.1f));
        }

        /// <summary>露見の逆効果＝偽情報がバレて発信元が特定されると信用失墜。</summary>
        [Test]
        public void BlowbackRisk_露見と特定で逆効果()
        {
            // 露見0.8 × 特定0.5 × 1.5 = 0.6
            Assert.AreEqual(0.6f, PsyOpRules.BlowbackRisk(0.8f, 0.5f), Eps);
            // 発信元が特定されなければ逆効果は生じない（匿名性）
            Assert.AreEqual(0f, PsyOpRules.BlowbackRisk(1f, 0f), Eps);
            // バレていなければ逆効果なし
            Assert.AreEqual(0f, PsyOpRules.BlowbackRisk(0f, 1f), Eps);
        }

        /// <summary>合意崩壊判定＝累積侵食が閾値以上。</summary>
        [Test]
        public void IsConsensusCollapsing_閾値超えで崩壊()
        {
            // 既定閾値0.6
            Assert.IsTrue(PsyOpRules.IsConsensusCollapsing(0.6f));
            Assert.IsTrue(PsyOpRules.IsConsensusCollapsing(0.85f));
            Assert.IsFalse(PsyOpRules.IsConsensusCollapsing(0.59f));
            // 明示閾値
            Assert.IsTrue(PsyOpRules.IsConsensusCollapsing(0.5f, 0.5f));
            Assert.IsFalse(PsyOpRules.IsConsensusCollapsing(0.49f, 0.5f));
        }
    }
}
