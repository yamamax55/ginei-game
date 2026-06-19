using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class RiotControlRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void CrowdIntensity_MaxInputs_Saturates()
        {
            // size=1^0.7=1, agit=0.4+0.6*1=1 → 1.0
            Assert.AreEqual(1f, RiotControlRules.CrowdIntensity(1f, 1f), Eps);
        }

        [Test]
        public void CrowdIntensity_Mid_UsesPowAndWeight()
        {
            // size=0.5^0.7≈0.615572, agit=0.4+0.6*0.5=0.7 → ≈0.430900
            Assert.AreEqual(0.430900f, RiotControlRules.CrowdIntensity(0.5f, 0.5f), PowEps);
        }

        [Test]
        public void ControlForce_QualityIsWeightedMean()
        {
            // quality=(0.8*0.5+0.6*0.5)/1=0.7, *unit 1 → 0.7
            Assert.AreEqual(0.7f, RiotControlRules.ControlForce(1f, 0.8f, 0.6f), Eps);
        }

        [Test]
        public void DispersalEffect_RatioOfForce()
        {
            // 0.7/(0.7+0.3)=0.7, min(0.7,0.9)=0.7
            Assert.AreEqual(0.7f, RiotControlRules.DispersalEffect(0.7f, 0.3f), Eps);
        }

        [Test]
        public void DispersalEffect_BothZero_IsZero()
        {
            Assert.AreEqual(0f, RiotControlRules.DispersalEffect(0f, 0f), Eps);
        }

        [Test]
        public void ForceEscalation_Overforce_StokesAndClampsToOne()
        {
            // ratio=0.8/0.2=4, excess=4-1.5=2.5, *0.5=1.25 → clamp 1.0
            Assert.AreEqual(1f, RiotControlRules.ForceEscalation(0.2f, 0.8f), Eps);
        }

        [Test]
        public void ForceEscalation_Proportionate_NoStoking()
        {
            // ratio=1, excess=max(0,1-1.5)=0 → 0
            Assert.AreEqual(0f, RiotControlRules.ForceEscalation(0.5f, 0.5f), Eps);
        }

        [Test]
        public void Casualties_ScaledByRestraint()
        {
            // 0.8*0.5*(1-0.2)*0.5 = 0.16
            Assert.AreEqual(0.16f, RiotControlRules.Casualties(0.8f, 0.5f, 0.2f), Eps);
        }

        [Test]
        public void PublicBacklash_AmplifiedByMedia()
        {
            // 0.16*(1+1*1.5)=0.4
            Assert.AreEqual(0.4f, RiotControlRules.PublicBacklash(0.16f, 1f), Eps);
        }

        [Test]
        public void MartialLawEffect_WeightedMean()
        {
            Assert.AreEqual(1f, RiotControlRules.MartialLawEffect(1f, 1f), Eps);
            // (0.5*0.6+0*0.4)/1 = 0.3
            Assert.AreEqual(0.3f, RiotControlRules.MartialLawEffect(0.5f, 0f), Eps);
        }

        [Test]
        public void IsRiotContained_DefaultThreshold()
        {
            Assert.IsTrue(RiotControlRules.IsRiotContained(0.7f, 0.2f));   // 0.5>=0.1
            Assert.IsFalse(RiotControlRules.IsRiotContained(0.3f, 0.25f)); // 0.05<0.1
        }

        [Test]
        public void Clamps_AreNullAndRangeSafe()
        {
            // 過剰入力でも 0..1 に収まる
            Assert.AreEqual(1f, RiotControlRules.CrowdIntensity(5f, 5f), Eps);
            Assert.AreEqual(0f, RiotControlRules.CrowdIntensity(-1f, -1f), Eps);
            Assert.AreEqual(0f, RiotControlRules.ControlForce(-3f, 2f, 2f), Eps);
            Assert.AreEqual(0f, RiotControlRules.Casualties(-1f, -1f, 2f), Eps);
        }

        [Test]
        public void Narrative_HeavyHandedCrackdownBacklashesAndFailsToContain()
        {
            // 小規模だがくすぶる騒乱（規模0.3・激昂0.5）
            var p = RiotControlParams.Default;
            float intensity = RiotControlRules.CrowdIntensity(0.3f, 0.5f, p);
            Assert.Greater(intensity, 0f);

            // 重装の治安部隊を圧倒的に投入（部隊1・装備1・訓練1）
            float force = RiotControlRules.ControlForce(1f, 1f, 1f, p);
            Assert.AreEqual(1f, force, Eps);

            // 解散はある程度進むが…
            float dispersal = RiotControlRules.DispersalEffect(force, intensity, p);

            // 過剰使用が激昂を煽る（鎮圧力が激しさを大きく上回る＝force/intensity が過剰閾超え）
            float escalation = RiotControlRules.ForceEscalation(intensity, force, p);
            Assert.Greater(escalation, 0f);

            // 自制を欠いた弾圧は犠牲を生み（自制0.1）…
            float casualties = RiotControlRules.Casualties(1f, 0.9f, 0.1f, p);
            Assert.Greater(casualties, 0f);

            // 報道で世論が反発する
            float backlash = RiotControlRules.PublicBacklash(casualties, 1f, p);
            Assert.Greater(backlash, casualties);

            // 煽りが解散を相殺し、暴動は鎮圧しきれない
            Assert.IsFalse(RiotControlRules.IsRiotContained(dispersal, escalation, 0.1f));

            // 対照＝節度ある対応（激しさに見合った鎮圧力）なら煽らず鎮圧できる
            float calmIntensity = RiotControlRules.CrowdIntensity(0.4f, 0.4f, p); // ≈0.3362
            float calmForce = 0.45f; // ratio≈1.34<1.5 ＝過剰閾未満で煽らない
            float calmEsc = RiotControlRules.ForceEscalation(calmIntensity, calmForce, p);
            float calmDisp = RiotControlRules.DispersalEffect(calmForce, calmIntensity, p);
            Assert.AreEqual(0f, calmEsc, Eps);
            Assert.IsTrue(RiotControlRules.IsRiotContained(calmDisp, calmEsc, 0.1f));
        }
    }
}
