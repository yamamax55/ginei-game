using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class AvailabilityBiasRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void RecencyWeight_DecaysFromOne()
        {
            // t=0 で 1、decayRate=0.5 で t=2 のとき 1/(1+1)=0.5
            Assert.AreEqual(1f, AvailabilityBiasRules.RecencyWeight(0f), Eps);
            Assert.AreEqual(0.5f, AvailabilityBiasRules.RecencyWeight(2f), Eps);
            // 負の経過時間は 0 にクランプ＝1
            Assert.AreEqual(1f, AvailabilityBiasRules.RecencyWeight(-5f), Eps);
        }

        [Test]
        public void VividnessBoost_MapsSeverityToOneToTwo()
        {
            Assert.AreEqual(1f, AvailabilityBiasRules.VividnessBoost(0f), Eps);
            Assert.AreEqual(1.5f, AvailabilityBiasRules.VividnessBoost(0.5f), Eps);
            Assert.AreEqual(2f, AvailabilityBiasRules.VividnessBoost(1f), Eps);
            // 上限クランプ
            Assert.AreEqual(2f, AvailabilityBiasRules.VividnessBoost(3f), Eps);
        }

        [Test]
        public void AvailabilityScore_IsProductOfFactors()
        {
            // 0.5 × 1.5 × 0.8 = 0.6
            Assert.AreEqual(0.6f, AvailabilityBiasRules.AvailabilityScore(0.5f, 1.5f, 0.8f), Eps);
            // どれか 0 なら 0
            Assert.AreEqual(0f, AvailabilityBiasRules.AvailabilityScore(0.5f, 1.5f, 0f), Eps);
        }

        [Test]
        public void PerceivedProbability_LiftsTowardOne()
        {
            // score=0 なら真の確率のまま
            Assert.AreEqual(0.2f, AvailabilityBiasRules.PerceivedProbability(0.2f, 0f), Eps);
            // score=1: pull=0.5, lift=0.6*0.5=0.3, Lerp(0.2,1,0.3)=0.44
            Assert.AreEqual(0.44f, AvailabilityBiasRules.PerceivedProbability(0.2f, 1f), Eps);
            // 主観確率は真の確率以上（過大評価）
            Assert.GreaterOrEqual(AvailabilityBiasRules.PerceivedProbability(0.2f, 5f), 0.2f);
        }

        [Test]
        public void ProbabilityDistortion_IsSignedGap()
        {
            Assert.AreEqual(0.24f, AvailabilityBiasRules.ProbabilityDistortion(0.44f, 0.2f), Eps);
            Assert.AreEqual(-0.1f, AvailabilityBiasRules.ProbabilityDistortion(0.2f, 0.3f), Eps);
        }

        [Test]
        public void ThreatOverestimateAfterShock_ScalesWithSeverity()
        {
            Assert.AreEqual(0f, AvailabilityBiasRules.ThreatOverestimateAfterShock(0f), Eps);
            // 0.8 × 0.5 = 0.4
            Assert.AreEqual(0.4f, AvailabilityBiasRules.ThreatOverestimateAfterShock(0.8f), Eps);
            Assert.AreEqual(0.5f, AvailabilityBiasRules.ThreatOverestimateAfterShock(1f), Eps);
        }

        [Test]
        public void ComplacencyAfterCalm_SaturatesTowardOne()
        {
            Assert.AreEqual(0f, AvailabilityBiasRules.ComplacencyAfterCalm(0f), Eps);
            // x=0.05*10=0.5, 0.5/1.5=0.33333
            Assert.AreEqual(0.33333f, AvailabilityBiasRules.ComplacencyAfterCalm(10f), 1e-3f);
            // 平穏が長いほど油断が増す（単調）
            Assert.Greater(
                AvailabilityBiasRules.ComplacencyAfterCalm(100f),
                AvailabilityBiasRules.ComplacencyAfterCalm(10f));
        }

        [Test]
        public void BiasDecayWithData_ShrinksWithExposure()
        {
            // exposure=0 ならそのまま
            Assert.AreEqual(1f, AvailabilityBiasRules.BiasDecayWithData(1f, 0f), Eps);
            // exposure=1: 1.0*(1-0.7)=0.3
            Assert.AreEqual(0.3f, AvailabilityBiasRules.BiasDecayWithData(1f, 1f), Eps);
        }

        [Test]
        public void IsAvailabilityDriven_TriggersAboveThreshold()
        {
            Assert.IsTrue(AvailabilityBiasRules.IsAvailabilityDriven(0.3f, 0.2f));
            Assert.IsFalse(AvailabilityBiasRules.IsAvailabilityDriven(0.1f, 0.2f));
            // 過小評価（負の歪み）も絶対値で判定
            Assert.IsTrue(AvailabilityBiasRules.IsAvailabilityDriven(-0.3f, 0.2f));
        }

        [Test]
        public void Narrative_ShockOverestimates_CalmBreedsComplacency_DataDampens()
        {
            var p = AvailabilityBiasParams.Default;
            float trueThreatProb = 0.3f;

            // 直近の大敗（severity=1）は鮮烈かつ最近＝想起容易性が高く脅威を過大評価する
            float freshRecency = AvailabilityBiasRules.RecencyWeight(0f, p);          // 1.0
            float vivid = AvailabilityBiasRules.VividnessBoost(1f);                   // 2.0
            float shockScore = AvailabilityBiasRules.AvailabilityScore(freshRecency, vivid, 1f); // 2.0
            float perceivedAfterShock = AvailabilityBiasRules.PerceivedProbability(trueThreatProb, shockScore, p);
            float distAfterShock = AvailabilityBiasRules.ProbabilityDistortion(perceivedAfterShock, trueThreatProb);
            Assert.Greater(distAfterShock, 0f, "直近の大敗後は脅威を過大評価する");
            Assert.IsTrue(AvailabilityBiasRules.IsAvailabilityDriven(distAfterShock, 0.05f),
                "歪みが大きく想起バイアス駆動と判定される");

            // 年月が経ち平穏が続くと、同じ大敗も鮮度が落ち想起されにくくなる＝油断が募る
            float staleRecency = AvailabilityBiasRules.RecencyWeight(20f, p);
            Assert.Less(staleRecency, freshRecency, "時間が経つほど想起の鮮度は落ちる");
            float complacency = AvailabilityBiasRules.ComplacencyAfterCalm(20f, p);
            Assert.Greater(complacency, 0f, "平穏が続くと脅威を過小評価する油断が生じる");

            // 基準率データ（統計）に触れると、同じ想起容易性スコアでも歪みが薄まる
            float dampened = AvailabilityBiasRules.BiasDecayWithData(shockScore, 1f, p);
            Assert.Less(dampened, shockScore, "基準率に触れると想起バイアスが薄まる");
            float perceivedDampened = AvailabilityBiasRules.PerceivedProbability(trueThreatProb, dampened, p);
            float distDampened = AvailabilityBiasRules.ProbabilityDistortion(perceivedDampened, trueThreatProb);
            Assert.Less(distDampened, distAfterShock, "脱バイアス後は主観確率が真の確率へ近づく");
        }
    }
}
