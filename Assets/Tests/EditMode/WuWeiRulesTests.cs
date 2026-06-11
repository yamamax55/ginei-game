using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 無為（むい）ガバナンス（LAOZ-1 #1546・老子＝無為の治）の純ロジック検証。既定 WuWeiParams の具体値で
    /// 期待値を固定し、自然な安定・介入の逆U字・過統治ペナルティ・控えめのボーナス・自己秩序化・最適介入点・
    /// いじりすぎのダメージ・統治しすぎ判定を担保する。
    /// </summary>
    public class WuWeiRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>民の自己組織化に任せ介入を控えるほど自然に治まる（為さずして治まる）。0.8×(1−0.2)×0.6=0.384。</summary>
        [Test]
        public void NaturalStability_自己組織化と低介入で自然な安定()
        {
            Assert.AreEqual(0.384f, WuWeiRules.NaturalStability(0.8f, 0.2f), Eps);
            // 介入最大なら自己組織化の効は消える（手を出しすぎて自発性を奪う）。
            Assert.AreEqual(0f, WuWeiRules.NaturalStability(0.8f, 1f), Eps);
        }

        /// <summary>介入の効果は逆U字＝最適点で最大(+1)、最適点から離れると下がり、超えると負に転じる。</summary>
        [Test]
        public void InterventionEffect_逆U字で最適点が頂点()
        {
            // 最適点ぴったりは頂点 +1。
            Assert.AreEqual(1f, WuWeiRules.InterventionEffect(0.5f, 0.5f), Eps);
            // 幅(0.5)ぶん離れると効果0（最適点0.5から距離0.5＝逆U字の裾）。
            Assert.AreEqual(0f, WuWeiRules.InterventionEffect(1f, 0.5f), Eps);
            // 反対側に同じ幅ぶん離れても対称に効果0（iv=0 も opt=0.5 から距離0.5＝小魚をいじり崩す手前）。
            Assert.AreEqual(0f, WuWeiRules.InterventionEffect(0f, 0.5f), Eps);
            // 山形＝最適点から左右対称に下がる。
            float left = WuWeiRules.InterventionEffect(0.3f, 0.5f);
            float right = WuWeiRules.InterventionEffect(0.7f, 0.5f);
            Assert.AreEqual(left, right, Eps);
            Assert.Less(left, 1f);
        }

        /// <summary>過度な統治のペナルティ：閾値以下は0（無為）、超えると自発性を殺す損失。(0.5−0.25)×0.8=0.2。</summary>
        [Test]
        public void OverGovernancePenalty_閾値超過で自発性を殺す()
        {
            Assert.AreEqual(0f, WuWeiRules.OverGovernancePenalty(0.2f, 0.25f), Eps); // 閾値以下＝痛みなし
            Assert.AreEqual(0.2f, WuWeiRules.OverGovernancePenalty(0.5f, 0.25f), Eps);
        }

        /// <summary>控えめな統治（為さざる）が民の自発性に与えるボーナス＝抑制×上限0.3。抑制0でゼロ。</summary>
        [Test]
        public void MinimalInterventionBonus_控えめなほどボーナス()
        {
            Assert.AreEqual(0.3f, WuWeiRules.MinimalInterventionBonus(1f), Eps);
            Assert.AreEqual(0.15f, WuWeiRules.MinimalInterventionBonus(0.5f), Eps);
            Assert.AreEqual(0f, WuWeiRules.MinimalInterventionBonus(0f), Eps);
        }

        /// <summary>政府が手を引くほど民が自ら秩序を育てる（無為自然）。0.5＋1.0×0.1×1.0=0.6。抑制0なら不変。</summary>
        [Test]
        public void SelfOrderingTick_政府が手を引くと秩序が育つ()
        {
            Assert.AreEqual(0.6f, WuWeiRules.SelfOrderingTick(0.5f, 1f, 1f), Eps);
            Assert.AreEqual(0.5f, WuWeiRules.SelfOrderingTick(0.5f, 0f, 1f), Eps); // 過介入＝育たない
        }

        /// <summary>状況依存の最適介入度：平時は控えめ・危機時は増やす（最適点が危機で右へ動く）。</summary>
        [Test]
        public void OptimalInterventionPoint_危機で介入を増やす()
        {
            Assert.AreEqual(0f, WuWeiRules.OptimalInterventionPoint(0f), Eps);   // 平時＝無為
            Assert.AreEqual(1f, WuWeiRules.OptimalInterventionPoint(1f), Eps);   // 危機＝手を打つ
            Assert.Less(WuWeiRules.OptimalInterventionPoint(0.3f), WuWeiRules.OptimalInterventionPoint(0.8f));
        }

        /// <summary>脆いシステムほど過介入のダメージが大きい（小魚＝煮崩れやすい）。0.8×0.5=0.4。頑健なら小さい。</summary>
        [Test]
        public void MeddlingDamage_脆い系ほどいじりすぎが効く()
        {
            Assert.AreEqual(0.4f, WuWeiRules.MeddlingDamage(0.8f, 0.5f), Eps);
            // 同じ介入でも頑健な系（脆さ小）はダメージが小さい。
            Assert.Less(WuWeiRules.MeddlingDamage(0.8f, 0.1f), WuWeiRules.MeddlingDamage(0.8f, 0.9f));
        }

        /// <summary>統治しすぎ（無為を忘れた）判定：最適点を許容幅0.25超えて介入すると真。</summary>
        [Test]
        public void IsOverGoverned_最適点を超えた介入を検知()
        {
            Assert.IsTrue(WuWeiRules.IsOverGoverned(0.8f, 0.5f));   // 差0.3>0.25＝過統治
            Assert.IsFalse(WuWeiRules.IsOverGoverned(0.6f, 0.5f));  // 差0.1≤0.25＝無為の範囲
            Assert.IsFalse(WuWeiRules.IsOverGoverned(0.4f, 0.5f));  // 最適点以下＝控えめ
        }
    }
}
