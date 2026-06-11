using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 恥の文化係数（KIKU-1 #1832・菊と刀）の純ロジックテスト。既定 ShameParams の具体値で期待値を固定する。
    /// 恥の圧力／行動抑制／面目の喪失／隠蔽の誘因／面目の回復／恥罪の軸／集団同調／規範の緩み／恥文化判定を担保。
    /// </summary>
    public class ShameRulesTests
    {
        /// <summary>恥の圧力＝可視性×規範違反。人目がなければ違反しても恥にならない（恥の文化の核）。</summary>
        [Test]
        public void ShamePressure_HumanEyesNeeded()
        {
            Assert.AreEqual(0.48f, ShameRules.ShamePressure(0.8f, 0.6f), 1e-4f); // 0.8*0.6
            Assert.AreEqual(0f, ShameRules.ShamePressure(0f, 1.0f), 1e-4f);      // 人目なし＝恥なし
            Assert.AreEqual(1.0f, ShameRules.ShamePressure(2f, 2f), 1e-4f);      // クランプ
        }

        /// <summary>行動抑制＝恥の圧力×面目への敏感さ。面目に鈍感なら抑制は効かない。</summary>
        [Test]
        public void BehaviorControl_FaceSensitivityScalesInhibition()
        {
            Assert.AreEqual(0.4f, ShameRules.BehaviorControl(0.5f, 0.8f), 1e-4f);
            Assert.AreEqual(0f, ShameRules.BehaviorControl(0.5f, 0f), 1e-4f); // 面目に鈍感
        }

        /// <summary>面目の喪失＝失敗×観衆の効き（既定 audienceWeight=0.5）。観衆が多いほど面目を失う。</summary>
        [Test]
        public void FaceLoss_BiggerAudienceCostsMoreFace()
        {
            // factor = 0.5 + 0.5*audience
            Assert.AreEqual(0.8f, ShameRules.FaceLoss(0.8f, 1.0f), 1e-4f); // factor=1.0
            Assert.AreEqual(0.4f, ShameRules.FaceLoss(0.8f, 0.0f), 1e-4f); // factor=0.5
            Assert.AreEqual(0.6f, ShameRules.FaceLoss(0.8f, 0.5f), 1e-4f); // factor=0.75
        }

        /// <summary>隠蔽の誘因＝違反×(1-可視性)。人目がなければ隠して恥を回避できる。</summary>
        [Test]
        public void ConcealmentIncentive_HideWhenUnseen()
        {
            Assert.AreEqual(0.8f, ShameRules.ConcealmentIncentive(0.8f, 0f), 1e-4f);   // 人目なし＝隠す
            Assert.AreEqual(0f, ShameRules.ConcealmentIncentive(0.8f, 1.0f), 1e-4f);   // 衆人環視＝隠せない
            Assert.AreEqual(0.4f, ShameRules.ConcealmentIncentive(0.8f, 0.5f), 1e-4f); // 0.8*0.5
        }

        /// <summary>面目の回復＝公的な償いで回復（既定 restorationScale=0.8）。内面でなく公的行為で。</summary>
        [Test]
        public void HonorRestoration_PublicAtonementRecoversFace()
        {
            // loss=0.8 - atone=0.5*0.8=0.4 → 残り0.4
            Assert.AreEqual(0.4f, ShameRules.HonorRestoration(0.8f, 0.5f), 1e-4f);
            // 大きな償いで完全回復（クランプで0）
            Assert.AreEqual(0f, ShameRules.HonorRestoration(0.6f, 1.0f), 1e-4f);
            // 償いゼロなら喪失そのまま
            Assert.AreEqual(0.7f, ShameRules.HonorRestoration(0.7f, 0f), 1e-4f);
        }

        /// <summary>恥／罪の軸＝可視性依存度を -1..1 に写す。高ければ恥の文化(+)・低ければ罪の文化(-)。</summary>
        [Test]
        public void ShameVsGuilt_VisibilityDependenceMapsToAxis()
        {
            Assert.AreEqual(1.0f, ShameRules.ShameVsGuilt(1.0f), 1e-4f);  // 完全な恥の文化
            Assert.AreEqual(-1.0f, ShameRules.ShameVsGuilt(0f), 1e-4f);   // 完全な罪の文化
            Assert.AreEqual(0f, ShameRules.ShameVsGuilt(0.5f), 1e-4f);    // 中間
        }

        /// <summary>集団同調＝恥の圧力×結束×conformityScale(0.9)。恥が同調を強める。</summary>
        [Test]
        public void SocialConformity_ShameDrivesConformity()
        {
            Assert.AreEqual(0.36f, ShameRules.SocialConformity(0.8f, 0.5f, ShameParams.Default), 1e-4f); // 0.8*0.5*0.9
            Assert.AreEqual(0f, ShameRules.SocialConformity(0f, 1.0f), 1e-4f);
        }

        /// <summary>規範の緩み＝(1-可視性)(1-内面化)×privateDecayScale(0.5)。人目も良心もなければ最も緩む。</summary>
        [Test]
        public void PrivateNormDecay_NormSlipsWhenUnseenAndUninternalized()
        {
            // (1-0.2)*(1-0.4)*0.5 = 0.8*0.6*0.5 = 0.24
            Assert.AreEqual(0.24f, ShameRules.PrivateNormDecay(0.2f, 0.4f), 1e-4f);
            // 内面化が高ければ人目がなくても緩まない（罪の文化的）
            Assert.AreEqual(0f, ShameRules.PrivateNormDecay(0f, 1.0f), 1e-4f);
        }

        /// <summary>恥の文化駆動の判定＝可視性依存が閾値(既定0.5)以上で成立。</summary>
        [Test]
        public void IsShameDriven_HighVisibilityDependence()
        {
            Assert.IsTrue(ShameRules.IsShameDriven(0.6f));   // 既定閾値0.5以上
            Assert.IsFalse(ShameRules.IsShameDriven(0.4f));
            Assert.IsTrue(ShameRules.IsShameDriven(0.3f, 0.3f)); // 明示閾値
        }

        /// <summary>
        /// 物語テスト：人目があれば恥の圧力が行動を抑制するが、見られていなければ規範が緩み隠蔽に走る。
        /// そして公的な償いで面目が回復する＝恥の文化の可視性依存と公的回復を一括検証する。
        /// </summary>
        [Test]
        public void Narrative_VisibilityControlsThenPublicAtonementRestores()
        {
            // 内面化の低い人物（良心が薄い）。同じ規範違反でも人目の有無で挙動が変わる。
            float internalizedNorm = 0.2f;
            float faceSensitivity = 0.9f;
            float normViolation = 0.8f;

            // (1) 人目がある＝恥の圧力が立ち、行動が抑制される
            float seenPressure = ShameRules.ShamePressure(1.0f, normViolation);      // 0.8
            float seenControl = ShameRules.BehaviorControl(seenPressure, faceSensitivity); // 0.72
            // (2) 人目がない＝恥の圧力は消え、行動抑制も消える
            float unseenPressure = ShameRules.ShamePressure(0f, normViolation);      // 0
            float unseenControl = ShameRules.BehaviorControl(unseenPressure, faceSensitivity); // 0
            Assert.Greater(seenControl, unseenControl, "人目があるほうが行動が抑制される");
            Assert.AreEqual(0f, unseenControl, 1e-4f, "見られていなければ抑制は効かない");

            // (3) 人目がなく良心も薄いと規範が緩み、隠蔽の誘因が立つ
            float decay = ShameRules.PrivateNormDecay(0f, internalizedNorm);          // (1)(0.8)(0.5)=0.4
            float conceal = ShameRules.ConcealmentIncentive(normViolation, 0f);       // 0.8
            Assert.Greater(decay, 0f, "人目も良心もなければ規範が緩む");
            Assert.Greater(conceal, 0.5f, "見られていなければ隠して恥を回避しようとする");

            // (4) いざ公衆の面前で失敗し面目を失う → 公的な償いで回復する（内面でなく公的行為で）
            float faceLoss = ShameRules.FaceLoss(0.8f, 1.0f);                         // 0.8
            float beforeAtone = ShameRules.HonorRestoration(faceLoss, 0f);            // 0.8（償いなし）
            float afterAtone = ShameRules.HonorRestoration(faceLoss, 1.0f);           // 0.8-0.8=0
            Assert.AreEqual(0.8f, beforeAtone, 1e-4f);
            Assert.Less(afterAtone, beforeAtone, "公的な謝罪・償いで面目が回復する");
            Assert.AreEqual(0f, afterAtone, 1e-4f, "十分な公的償いで面目は完全に回復しうる");
        }
    }
}
