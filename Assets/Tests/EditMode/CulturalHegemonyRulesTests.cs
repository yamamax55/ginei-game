using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// CulturalHegemonyRules（文化的覇権＝グラムシ的ヘゲモニー）の EditMode テスト。
    /// 既定 Params での期待値を手計算で固定する。
    /// </summary>
    public class CulturalHegemonyRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void ValuePenetration_WeightedBlend()
        {
            // (0.8*0.5 + 0.6*0.5) / 1.0 = 0.7
            Assert.AreEqual(0.7f, CulturalHegemonyRules.ValuePenetration(0.8f, 0.6f), Eps);
        }

        [Test]
        public void CommonSenseFormation_RepetitionRaises()
        {
            // 0.7 * (1 + 0.8*0.5) = 0.7 * 1.4 = 0.98
            Assert.AreEqual(0.98f, CulturalHegemonyRules.CommonSenseFormation(0.7f, 0.8f), Eps);
        }

        [Test]
        public void ManufacturedConsent_NaturalnessDeepens()
        {
            // 0.5 * (1 + 0.4*0.5) = 0.5 * 1.2 = 0.6
            Assert.AreEqual(0.6f, CulturalHegemonyRules.ManufacturedConsent(0.5f, 0.4f), Eps);
        }

        [Test]
        public void ManufacturedConsent_ClampsAtOne()
        {
            // 0.98 * (1 + 1.0*0.5) = 1.47 -> clamp 1.0
            Assert.AreEqual(1f, CulturalHegemonyRules.ManufacturedConsent(0.98f, 1f), Eps);
        }

        [Test]
        public void CounterHegemony_ProductOfNarrativeAndDissent()
        {
            // 0.5 * 0.6 * 1.0 = 0.3
            Assert.AreEqual(0.3f, CulturalHegemonyRules.CounterHegemony(0.5f, 0.6f), Eps);
        }

        [Test]
        public void HegemonicStability_SignedAdvantage()
        {
            // 0.8 - 0.3*1.0 = 0.5
            Assert.AreEqual(0.5f, CulturalHegemonyRules.HegemonicStability(0.8f, 0.3f), Eps);
            // 0.2 - 0.9*1.0 = -0.7 (覇権が揺らぐ)
            Assert.AreEqual(-0.7f, CulturalHegemonyRules.HegemonicStability(0.2f, 0.9f), Eps);
        }

        [Test]
        public void CulturalLegitimacy_OrganicIntellectualsTemper()
        {
            // 0.7 * Lerp(1, 0.6, 1.0) = 0.7 * 0.6 = 0.42
            Assert.AreEqual(0.42f, CulturalHegemonyRules.CulturalLegitimacy(0.7f, 0.6f), Eps);
        }

        [Test]
        public void SoftPower_LegitimacyTimesAppeal()
        {
            // 0.42 * 0.5 * (1 + 1.0) = 0.21 * 2 = 0.42
            Assert.AreEqual(0.42f, CulturalHegemonyRules.SoftPower(0.42f, 0.5f), Eps);
        }

        [Test]
        public void IsHegemonyMaintained_ThresholdBehavior()
        {
            Assert.IsTrue(CulturalHegemonyRules.IsHegemonyMaintained(0.5f));   // > 0.2 既定閾値
            Assert.IsFalse(CulturalHegemonyRules.IsHegemonyMaintained(-0.7f));
            Assert.IsTrue(CulturalHegemonyRules.IsHegemonyMaintained(0.5f, 0.4f));
            Assert.IsFalse(CulturalHegemonyRules.IsHegemonyMaintained(0.3f, 0.4f));
        }

        [Test]
        public void InputsAreClamped()
        {
            // 範囲外入力でも 0..1 / -1..1 に収まる（NaN/暴走しない）。
            Assert.AreEqual(1f, CulturalHegemonyRules.ValuePenetration(5f, 5f), Eps);
            Assert.AreEqual(0f, CulturalHegemonyRules.ValuePenetration(-5f, -5f), Eps);
            float stab = CulturalHegemonyRules.HegemonicStability(-9f, 9f);
            Assert.GreaterOrEqual(stab, -1f);
            Assert.LessOrEqual(stab, 1f);
        }

        [Test]
        public void Narrative_DominantCultureConsolidatesWhileWeakOneFractures()
        {
            // 物語：強い文化制度＋反復＋自然さの支配勢力は同意を調達し覇権を固める。
            float penA = CulturalHegemonyRules.ValuePenetration(0.9f, 0.85f);
            float csA = CulturalHegemonyRules.CommonSenseFormation(penA, 0.9f);
            float consentA = CulturalHegemonyRules.ManufacturedConsent(csA, 0.9f);
            float counterA = CulturalHegemonyRules.CounterHegemony(0.2f, 0.2f); // 対抗弱い
            float stabA = CulturalHegemonyRules.HegemonicStability(consentA, counterA);
            Assert.IsTrue(CulturalHegemonyRules.IsHegemonyMaintained(stabA), "支配勢力は覇権を維持する");

            // 制度が弱く対抗叙事と知識人の異論が強い勢力は覇権が揺らぐ。
            float penB = CulturalHegemonyRules.ValuePenetration(0.3f, 0.25f);
            float csB = CulturalHegemonyRules.CommonSenseFormation(penB, 0.3f);
            float consentB = CulturalHegemonyRules.ManufacturedConsent(csB, 0.3f);
            float counterB = CulturalHegemonyRules.CounterHegemony(0.85f, 0.85f); // 対抗強い
            float stabB = CulturalHegemonyRules.HegemonicStability(consentB, counterB);
            Assert.IsFalse(CulturalHegemonyRules.IsHegemonyMaintained(stabB), "弱い勢力の覇権は崩れる");

            // 支配勢力の方が覇権が安定している。
            Assert.Greater(stabA, stabB);

            // ソフトパワーは正統性が高く国際的魅力がある支配勢力の方が大きい。
            float legA = CulturalHegemonyRules.CulturalLegitimacy(penA, 0.8f);
            float legB = CulturalHegemonyRules.CulturalLegitimacy(penB, 0.3f);
            float softA = CulturalHegemonyRules.SoftPower(legA, 0.7f);
            float softB = CulturalHegemonyRules.SoftPower(legB, 0.4f);
            Assert.Greater(softA, softB);
        }
    }
}
