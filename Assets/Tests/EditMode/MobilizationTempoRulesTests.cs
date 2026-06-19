using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// MobilizationTempoRules（動員の速度と先手）の EditMode テスト。既定 Params で期待値固定。
    /// </summary>
    public class MobilizationTempoRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void Progress_AdvancesByRateTimesDt()
        {
            // 0.2 + 0.1*3 = 0.5
            float prog = MobilizationTempoRules.MobilizationProgress(0.2f, 0.1f, 3f);
            Assert.AreEqual(0.5f, prog, Eps);
        }

        [Test]
        public void Progress_ClampsToOne()
        {
            // 0.9 + 0.1*3 = 1.2 -> 1.0
            float prog = MobilizationTempoRules.MobilizationProgress(0.9f, 0.1f, 3f);
            Assert.AreEqual(1f, prog, Eps);
        }

        [Test]
        public void MobilizedStrength_ScalesWithProgress()
        {
            float s = MobilizationTempoRules.MobilizedStrength(10000f, 0.5f);
            Assert.AreEqual(5000f, s, Eps);
        }

        [Test]
        public void MobilizationTime_TargetOverCapacity()
        {
            float t = MobilizationTempoRules.MobilizationTime(0.8f, 0.1f);
            Assert.AreEqual(8f, t, Eps);
            // 能力0以下は到達不能
            Assert.IsTrue(float.IsInfinity(MobilizationTempoRules.MobilizationTime(0.8f, 0f)));
        }

        [Test]
        public void FirstMover_IsSignedDifference()
        {
            Assert.AreEqual(0.4f, MobilizationTempoRules.FirstMoverAdvantage(0.7f, 0.3f), Eps);
            Assert.AreEqual(-0.4f, MobilizationTempoRules.FirstMoverAdvantage(0.3f, 0.7f), Eps);
            // 範囲は -1..1
            float adv = MobilizationTempoRules.FirstMoverAdvantage(1f, 0f);
            Assert.LessOrEqual(adv, 1f);
        }

        [Test]
        public void PrematureWarPenalty_ZeroWhenFull()
        {
            // (1-0.5)*0.6 = 0.3
            Assert.AreEqual(0.3f, MobilizationTempoRules.PrematureWarPenalty(0.5f), Eps);
            // 完全動員ならペナルティなし
            Assert.AreEqual(0f, MobilizationTempoRules.PrematureWarPenalty(1f), Eps);
        }

        [Test]
        public void Strain_RisesWhenEconomyThin()
        {
            // 0.5 / 1.0 * 1 = 0.5
            Assert.AreEqual(0.5f, MobilizationTempoRules.MobilizationStrain(0.5f, 1f), Eps);
            // 余力が薄いほど重い
            float thin = MobilizationTempoRules.MobilizationStrain(0.5f, 0.5f);
            Assert.Greater(thin, 0.5f);
        }

        [Test]
        public void Demobilization_CostsAndThresholdHold()
        {
            // 0.8 * 0.5 = 0.4（動員したら後戻りしにくい）
            Assert.AreEqual(0.4f, MobilizationTempoRules.DemobilizationCost(0.8f), Eps);
            Assert.IsTrue(MobilizationTempoRules.IsFullyMobilized(0.95f));
            Assert.IsFalse(MobilizationTempoRules.IsFullyMobilized(0.9f));
        }

        [Test]
        public void Narrative_FirstMoverWinsButPrematureWarHurts()
        {
            // 帝国は急速動員で先手を取り、同盟はまだ平時に近い。
            var p = MobilizationTempoParams.Default;
            float empire = MobilizationTempoRules.MobilizationProgress(0.2f, 0.2f, 3f, p); // 0.2+0.6=0.8
            float alliance = MobilizationTempoRules.MobilizationProgress(0.1f, 0.05f, 3f, p); // 0.1+0.15=0.25
            Assert.AreEqual(0.8f, empire, Eps);
            Assert.AreEqual(0.25f, alliance, Eps);

            // 帝国が先手の優位を持つ
            float adv = MobilizationTempoRules.FirstMoverAdvantage(empire, alliance);
            Assert.Greater(adv, 0f);
            Assert.AreEqual(0.55f, adv, Eps);

            // それでも不完全動員（0.8）で開戦すれば戦力は揃わずペナルティが残る
            float penalty = MobilizationTempoRules.PrematureWarPenalty(empire, p); // (1-0.8)*0.6=0.12
            Assert.AreEqual(0.12f, penalty, Eps);
            Assert.IsFalse(MobilizationTempoRules.IsFullyMobilized(empire, p.fullThreshold));

            // 動員できている戦力は総戦力の8割
            float strength = MobilizationTempoRules.MobilizedStrength(10000f, empire);
            Assert.AreEqual(8000f, strength, Eps);
        }
    }
}
