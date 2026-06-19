using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 保護国・従属国（プロテクトレート）を固定する：主権制限は外交・軍事の統制で深まり、庇護の恩恵は宗主の力と
    /// 外的脅威で増し、内政自治は制限の裏返し、宗主の負担は属国の不安定で膨らみ、独立志向は自治・民族意識・自立能力の
    /// 積で募り、正統性は恩恵×（1−制限）×合意で支えられ、移行圧は宗主弱体化で加速し、関係は正統性が独立志向を
    /// 上回る限り安定する。クランプを担保。既定＝外交0.4/軍事0.6/脅威恩恵0.5/不安定負担0.5/民族0.5/自立0.5/弱体化1。
    /// </summary>
    public class ProtectorateRulesTests
    {
        private static readonly ProtectorateParams P = ProtectorateParams.Default;

        [Test]
        public void SovereigntyRestriction_DeepensWithBothControls()
        {
            // 外交0.5×0.4 + 軍事0.5×0.6 = 0.2+0.3 = 0.5
            Assert.AreEqual(0.5f, ProtectorateRules.SovereigntyRestriction(0.5f, 0.5f, P), 1e-4f);
            // 双方を完全に握る＝制限1.0（属国化）
            Assert.AreEqual(1f, ProtectorateRules.SovereigntyRestriction(1f, 1f, P), 1e-4f);
            // 両統制ゼロ＝制限なし＝対等な独立国
            Assert.AreEqual(0f, ProtectorateRules.SovereigntyRestriction(0f, 0f, P), 1e-4f);
            // 軍事の方が重い＝同じ統制値でも軍事を握られる方が深い
            Assert.Greater(ProtectorateRules.SovereigntyRestriction(0f, 1f, P),
                           ProtectorateRules.SovereigntyRestriction(1f, 0f, P));
            // 範囲外入力はクランプ
            Assert.AreEqual(0f, ProtectorateRules.SovereigntyRestriction(-1f, -1f, P), 1e-4f);
        }

        [Test]
        public void ProtectionBenefit_RisesWithStrengthAndThreat()
        {
            // 宗主の力0.6×(1+脅威0.5×0.5)=0.6×1.25=0.75
            Assert.AreEqual(0.75f, ProtectorateRules.ProtectionBenefit(0.6f, 0.5f, P), 1e-4f);
            // 平時（脅威0）＝宗主の力そのまま
            Assert.AreEqual(0.8f, ProtectorateRules.ProtectionBenefit(0.8f, 0f, P), 1e-4f);
            // 敵が多いほど後ろ盾の価値が増す（脅威で恩恵増）
            Assert.Greater(ProtectorateRules.ProtectionBenefit(0.6f, 1f, P),
                           ProtectorateRules.ProtectionBenefit(0.6f, 0f, P));
            // 上限1.0でクランプ
            Assert.AreEqual(1f, ProtectorateRules.ProtectionBenefit(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void InternalAutonomy_IsInverseOfRestriction()
        {
            // 制限0.5＝自治0.5（残された内政）
            Assert.AreEqual(0.5f, ProtectorateRules.InternalAutonomy(0.5f, P), 1e-4f);
            // 完全併合（制限1）＝自治ゼロ
            Assert.AreEqual(0f, ProtectorateRules.InternalAutonomy(1f, P), 1e-4f);
            // 無制限（制限0）＝自治1.0
            Assert.AreEqual(1f, ProtectorateRules.InternalAutonomy(0f, P), 1e-4f);
            // 範囲外はクランプ
            Assert.AreEqual(1f, ProtectorateRules.InternalAutonomy(-1f, P), 1e-4f);
        }

        [Test]
        public void OverlordBurden_GrowsWithCommitmentAndInstability()
        {
            // 庇護義務0.6×(1+不安定0.5×0.5)=0.6×1.25=0.75
            Assert.AreEqual(0.75f, ProtectorateRules.OverlordBurden(0.6f, 0.5f, P), 1e-4f);
            // 庇護していない（義務0）＝負担なし
            Assert.AreEqual(0f, ProtectorateRules.OverlordBurden(0f, 1f, P), 1e-4f);
            // 揺れる属国ほど手がかかる（不安定で負担増）
            Assert.Greater(ProtectorateRules.OverlordBurden(0.6f, 1f, P),
                           ProtectorateRules.OverlordBurden(0.6f, 0f, P));
            // 上限1.0でクランプ
            Assert.AreEqual(1f, ProtectorateRules.OverlordBurden(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void IndependenceAspiration_NeedsAutonomyIdentityCapability()
        {
            // 自治0.8×(民族0.6×0.5＋自立0.4×0.5)=0.8×(0.3+0.2)=0.8×0.5=0.4
            Assert.AreEqual(0.4f, ProtectorateRules.IndependenceAspiration(0.8f, 0.6f, 0.4f, P), 1e-4f);
            // 自治ゼロ（完全併合）＝独立を志す余地なし
            Assert.AreEqual(0f, ProtectorateRules.IndependenceAspiration(0f, 1f, 1f, P), 1e-4f);
            // 民族意識・自立能力ともゼロ＝志向なし（守られて満足）
            Assert.AreEqual(0f, ProtectorateRules.IndependenceAspiration(1f, 0f, 0f, P), 1e-4f);
            // 民族意識・能力ともに高いほど志向は強い
            Assert.Greater(ProtectorateRules.IndependenceAspiration(1f, 1f, 1f, P),
                           ProtectorateRules.IndependenceAspiration(1f, 0.3f, 0.3f, P));
        }

        [Test]
        public void RelationshipLegitimacy_FromBenefitFreedomConsent()
        {
            // 恩恵0.8×(1−制限0.5)×合意1 = 0.8×0.5×1 = 0.4
            Assert.AreEqual(0.4f, ProtectorateRules.RelationshipLegitimacy(0.8f, 0.5f, 1f, P), 1e-4f);
            // 合意なき押しつけ＝正統性ゼロ（不平等条約）
            Assert.AreEqual(0f, ProtectorateRules.RelationshipLegitimacy(1f, 0f, 0f, P), 1e-4f);
            // 主権を奪い尽くす（制限1）＝正統性ゼロ
            Assert.AreEqual(0f, ProtectorateRules.RelationshipLegitimacy(1f, 1f, 1f, P), 1e-4f);
            // 恩恵が薄いほど正統性は下がる
            Assert.Less(ProtectorateRules.RelationshipLegitimacy(0.3f, 0.5f, 1f, P),
                        ProtectorateRules.RelationshipLegitimacy(0.9f, 0.5f, 1f, P));
        }

        [Test]
        public void TransitionToIndependence_AcceleratesWhenOverlordWeakens()
        {
            // 独立志向0.4×(1+宗主弱体化0.5×1)=0.4×1.5=0.6
            Assert.AreEqual(0.6f, ProtectorateRules.TransitionToIndependence(0.4f, 0.5f, P), 1e-4f);
            // 弱った宗主ほど離反が加速
            Assert.Greater(ProtectorateRules.TransitionToIndependence(0.4f, 1f, P),
                           ProtectorateRules.TransitionToIndependence(0.4f, 0f, P));
            // 独立を望まない属国は宗主が弱っても動かない
            Assert.AreEqual(0f, ProtectorateRules.TransitionToIndependence(0f, 1f, P), 1e-4f);
            // 上限1.0でクランプ
            Assert.AreEqual(1f, ProtectorateRules.TransitionToIndependence(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void IsProtectorateStable_LegitimacyMustOutweighAspiration()
        {
            // 正統性0.6 ＞ 独立志向0.3（既定閾値0）＝安定
            Assert.IsTrue(ProtectorateRules.IsProtectorateStable(0.6f, 0.3f));
            // 志向が正統性を上回る＝不安定（独立へ）
            Assert.IsFalse(ProtectorateRules.IsProtectorateStable(0.3f, 0.6f));
            // 閾値0.3＝0.5−0.3=0.2 < 0.3＝マージン不足で不安定
            Assert.IsFalse(ProtectorateRules.IsProtectorateStable(0.5f, 0.3f, 0.3f));
            // 同じ差でも閾値が緩ければ安定
            Assert.IsTrue(ProtectorateRules.IsProtectorateStable(0.5f, 0.3f, 0.1f));
        }

        [Test]
        public void Params_CtorClamps()
        {
            var p = new ProtectorateParams(-1f, 2f, -1f, -1f, 2f, -1f, -1f);
            Assert.AreEqual(0f, p.diplomaticWeight, 1e-5f);        // 0..1
            Assert.AreEqual(1f, p.militaryWeight, 1e-5f);          // 0..1
            Assert.AreEqual(0f, p.threatBenefitWeight, 1e-5f);     // 非負
            Assert.AreEqual(0f, p.instabilityBurdenWeight, 1e-5f); // 非負
            Assert.AreEqual(1f, p.identityWeight, 1e-5f);          // 0..1
            Assert.AreEqual(0f, p.capabilityWeight, 1e-5f);        // 0..1
            Assert.AreEqual(0f, p.weaknessWeight, 1e-5f);          // 非負
        }

        [Test]
        public void LongRunStory_WeakeningOverlordLosesAmbitiousVassal()
        {
            // 二つの従属国を比べる：守られて納得した属国 vs 自立を志す属国を、衰えゆく宗主が抱える。
            // 宗主は会戦で力を失い続ける（弱体化が進む）。
            float overlordStrength = 0.9f; // 宗主の力
            bool contentEverDestabilized = false;
            bool ambitiousDestabilized = false;
            for (int year = 0; year < 15; year++)
            {
                overlordStrength = Mathf.Max(0.45f, overlordStrength - 0.05f); // 宗主が衰える（後ろ盾は残る）
                float weakness = 1f - overlordStrength;
                float externalThreat = 0.5f;
                float benefit = ProtectorateRules.ProtectionBenefit(overlordStrength, externalThreat);

                // 満足な属国＝民族意識も自立能力も低い・主権制限は浅く合意あり（正統性高い）
                float contentRestriction = ProtectorateRules.SovereigntyRestriction(0.3f, 0.3f);
                float contentAutonomy = ProtectorateRules.InternalAutonomy(contentRestriction);
                float contentAspiration = ProtectorateRules.IndependenceAspiration(contentAutonomy, 0.1f, 0.1f);
                float contentLegit = ProtectorateRules.RelationshipLegitimacy(benefit, contentRestriction, 0.9f);
                float contentTrans = ProtectorateRules.TransitionToIndependence(contentAspiration, weakness);
                if (!ProtectorateRules.IsProtectorateStable(contentLegit, contentTrans))
                    contentEverDestabilized = true;

                // 野心的な属国＝高い民族意識・自立能力・主権を奪われ合意も薄い（正統性低い）
                float ambRestriction = ProtectorateRules.SovereigntyRestriction(0.9f, 0.9f);
                float ambAutonomy = ProtectorateRules.InternalAutonomy(ambRestriction);
                float ambAspiration = ProtectorateRules.IndependenceAspiration(ambAutonomy, 0.9f, 0.9f);
                float ambLegit = ProtectorateRules.RelationshipLegitimacy(benefit, ambRestriction, 0.3f);
                float ambTrans = ProtectorateRules.TransitionToIndependence(ambAspiration, weakness);
                if (!ProtectorateRules.IsProtectorateStable(ambLegit, ambTrans))
                    ambitiousDestabilized = true;
            }
            // 野心的な属国は衰える宗主のもとで独立へ動き、満足な属国は留まる
            Assert.IsTrue(ambitiousDestabilized);    // 弱った宗主から自立志向の属国が離反
            Assert.IsFalse(contentEverDestabilized); // 守られて納得した属国は安定したまま
        }
    }
}
