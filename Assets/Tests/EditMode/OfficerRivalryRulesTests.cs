using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>将官の確執（派閥・反目・連携）の純ロジック検証。</summary>
    public class OfficerRivalryRulesTests
    {
        const float Eps = 1e-4f;
        const float EpsSqrt = 1e-3f; // Sqrt を含む箇所

        /// <summary>確執＝双方の功名心の平均×地位の近さ。近い地位の野心家同士で激しい。</summary>
        [Test]
        public void RivalryIntensity_近い地位の野心家同士で激しい()
        {
            // avg=(0.8+0.9)/2=0.85, prox=1-0.1=0.9 → 0.765
            Assert.AreEqual(0.765f, OfficerRivalryRules.RivalryIntensity(80f, 90f, 0.1f), Eps);
            // 地位差が最大なら反目しない
            Assert.AreEqual(0f, OfficerRivalryRules.RivalryIntensity(80f, 90f, 1f), Eps);
            // どちらも無欲なら確執なし
            Assert.AreEqual(0f, OfficerRivalryRules.RivalryIntensity(0f, 0f, 0f), Eps);
        }

        /// <summary>協調摩擦＝確執×共通目標の不在。共通目標があれば反目も抑えられる。</summary>
        [Test]
        public void CooperationFriction_共通目標が摩擦を抑える()
        {
            // 0.8*(1-0.25)=0.6
            Assert.AreEqual(0.6f, OfficerRivalryRules.CooperationFriction(0.8f, 0.25f), Eps);
            // 共通目標が完全なら摩擦ゼロ
            Assert.AreEqual(0f, OfficerRivalryRules.CooperationFriction(0.8f, 1f), Eps);
            // 共通目標が薄いほど摩擦は増える
            Assert.Greater(OfficerRivalryRules.CooperationFriction(0.8f, 0.1f),
                           OfficerRivalryRules.CooperationFriction(0.8f, 0.7f));
        }

        /// <summary>派閥分極＝派閥忠誠×思想距離。忠誠が強く思想が遠いほど深まる。</summary>
        [Test]
        public void FactionPolarization_忠誠と思想距離で分極()
        {
            // 0.9*0.8=0.72
            Assert.AreEqual(0.72f, OfficerRivalryRules.FactionPolarization(0.9f, 0.8f), Eps);
            // 思想が同じなら分極しない
            Assert.AreEqual(0f, OfficerRivalryRules.FactionPolarization(1f, 0f), Eps);
            // 派閥に無関心なら分極しない
            Assert.AreEqual(0f, OfficerRivalryRules.FactionPolarization(0f, 1f), Eps);
        }

        /// <summary>友誼＝苦楽の共有×相互敬意の幾何平均。片方だけでは厚くならない。</summary>
        [Test]
        public void Camaraderie_苦楽と敬意の幾何平均()
        {
            // sqrt(0.64*0.25)=sqrt(0.16)=0.4
            Assert.AreEqual(0.4f, OfficerRivalryRules.Camaraderie(0.64f, 0.25f), EpsSqrt);
            // 敬意が無ければ友誼も無い
            Assert.AreEqual(0f, OfficerRivalryRules.Camaraderie(1f, 0f), EpsSqrt);
            // 両方最大で1
            Assert.AreEqual(1f, OfficerRivalryRules.Camaraderie(1f, 1f), EpsSqrt);
        }

        /// <summary>共同作戦効率＝友誼−摩擦。友誼が勝てば正、確執が勝てば負（-1..1）。</summary>
        [Test]
        public void JointOperationEffect_友誼と摩擦の綱引き()
        {
            // 0.7-0.2=0.5（友誼が勝る）
            Assert.AreEqual(0.5f, OfficerRivalryRules.JointOperationEffect(0.7f, 0.2f), Eps);
            // 0.2-0.9=-0.7（確執が足を引っ張る）
            Assert.AreEqual(-0.7f, OfficerRivalryRules.JointOperationEffect(0.2f, 0.9f), Eps);
            // 出力は-1..1にクランプ
            float v = OfficerRivalryRules.JointOperationEffect(0f, 1f);
            Assert.GreaterOrEqual(v, -1f);
            Assert.LessOrEqual(v, 1f);
        }

        /// <summary>不服従＝確執/(確執+正統性)。正統性が勝てば命令は通る。</summary>
        [Test]
        public void Insubordination_正統性が確執を抑える()
        {
            // 0.6/(0.6+0.6)=0.5
            Assert.AreEqual(0.5f, OfficerRivalryRules.Insubordination(0.6f, 0.6f), Eps);
            // 正統性が確執に勝てば不服従は小さい
            Assert.Less(OfficerRivalryRules.Insubordination(0.3f, 0.9f),
                        OfficerRivalryRules.Insubordination(0.9f, 0.3f));
            // 確執も正統性も無ければゼロ安全（ゼロ除算なし）
            Assert.AreEqual(0f, OfficerRivalryRules.Insubordination(0f, 0f), Eps);
        }

        /// <summary>手柄横取り＝確執×功名心。野心家ほど功を奪い合う。</summary>
        [Test]
        public void CreditRivalry_野心が手柄争いを煽る()
        {
            // 0.5*(80/100)=0.4
            Assert.AreEqual(0.4f, OfficerRivalryRules.CreditRivalry(0.5f, 80f), Eps);
            // 無欲なら横取りしない
            Assert.AreEqual(0f, OfficerRivalryRules.CreditRivalry(0.8f, 0f), Eps);
            // 確執が無ければ争いも無い
            Assert.AreEqual(0f, OfficerRivalryRules.CreditRivalry(0f, 100f), Eps);
        }

        /// <summary>協力判定＝共同作戦効率が閾値以上。既定閾値0。</summary>
        [Test]
        public void WillCooperate_閾値で協力可否()
        {
            Assert.IsTrue(OfficerRivalryRules.WillCooperate(0.5f));      // 友誼が摩擦に勝る
            Assert.IsFalse(OfficerRivalryRules.WillCooperate(-0.3f));    // 確執が勝る
            Assert.IsTrue(OfficerRivalryRules.WillCooperate(0f));        // 境界＝既定閾値0で協力
            // 明示閾値版
            Assert.IsFalse(OfficerRivalryRules.WillCooperate(0.2f, 0.5f));
            Assert.IsTrue(OfficerRivalryRules.WillCooperate(0.6f, 0.5f));
        }

        /// <summary>
        /// 物語：門閥貴族の二将（共に野心家・地位伯仲）。共通目標を欠き派閥対立を抱えると確執が燃え、
        /// 上官の正統性が弱いと不服従が芽生え、手柄を奪い合い、友誼が摩擦に飲まれて共同作戦が崩れる。
        /// だが共通目標を据え友誼を積めば、同じ二将でも連携が成り立つ。
        /// </summary>
        [Test]
        public void Story_確執が作戦を蝕み友誼が連携を救う()
        {
            // 共に野心家(85,85)・地位伯仲(差0.05) → 強い確執
            float rivalry = OfficerRivalryRules.RivalryIntensity(85f, 85f, 0.05f);
            Assert.Greater(rivalry, 0.7f);

            // 共通目標を欠く（0.1）→ 摩擦が高い
            float frictionDivided = OfficerRivalryRules.CooperationFriction(rivalry, 0.1f);
            // 共通目標を据える（0.9）→ 摩擦が低い
            float frictionUnited = OfficerRivalryRules.CooperationFriction(rivalry, 0.9f);
            Assert.Greater(frictionDivided, frictionUnited);

            // 弱い正統性の上官 → 不服従が芽生える
            float insub = OfficerRivalryRules.Insubordination(rivalry, 0.2f);
            Assert.Greater(insub, 0.5f);

            // 確執と野心で手柄を奪い合う
            float credit = OfficerRivalryRules.CreditRivalry(rivalry, 85f);
            Assert.Greater(credit, 0.5f);

            // 友誼が薄い × 高摩擦 → 共同作戦は崩れる（負）
            float coldEffect = OfficerRivalryRules.JointOperationEffect(0.1f, frictionDivided);
            Assert.Less(coldEffect, 0f);
            Assert.IsFalse(OfficerRivalryRules.WillCooperate(coldEffect));

            // 苦楽を共にし敬意を積んだ友誼 × 共通目標の低摩擦 → 連携が成り立つ（正）
            float warmCamaraderie = OfficerRivalryRules.Camaraderie(0.9f, 0.9f);
            float warmEffect = OfficerRivalryRules.JointOperationEffect(warmCamaraderie, frictionUnited);
            Assert.Greater(warmEffect, coldEffect);
            Assert.IsTrue(OfficerRivalryRules.WillCooperate(warmEffect));
        }
    }
}
