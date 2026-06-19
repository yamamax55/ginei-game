using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// DeepStrikeRules（縦深打撃）の EditMode テスト。既定 Params で期待値固定。
    /// </summary>
    public class DeepStrikeRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void PenetrationDepth_MobilityVsDefenseDepth_Ratio()
        {
            // mobility 100 ×1.2 = 120, defenseDepth 40 → 120/(120+40)=0.75
            Assert.AreEqual(0.75f, DeepStrikeRules.PenetrationDepth(100f, 40f), Eps);
        }

        [Test]
        public void PenetrationDepth_HeavyDefense_RespectsFloor()
        {
            // mobility 0 → 0、だが depthFloor 0.1 ぶんは必ず侵入できる
            Assert.AreEqual(0.1f, DeepStrikeRules.PenetrationDepth(0f, 100f), Eps);
        }

        [Test]
        public void RearTargetValue_DeeperHitsMoreImportantTargets()
        {
            // depth 0.75 × importance 100 = 75
            Assert.AreEqual(75f, DeepStrikeRules.RearTargetValue(0.75f, 100f), Eps);
        }

        [Test]
        public void SystemicDisruption_ScalesWithValueAndDependency()
        {
            // rearValue 75 × dependency 0.8 × 0.01 = 0.6
            Assert.AreEqual(0.6f, DeepStrikeRules.SystemicDisruption(75f, 0.8f), Eps);
        }

        [Test]
        public void SupplyTether_OverstretchesWithDepth()
        {
            // depth 0.75 × 1.0 / range 0.5 = 1.5（補給範囲を超過＝危険域）
            Assert.AreEqual(1.5f, DeepStrikeRules.SupplyTether(0.75f, 0.5f), Eps);
        }

        [Test]
        public void IsolationRisk_DeepAndManyReserves_HighRisk()
        {
            // depth 0.75 × reserves 0.8 × 1.0 = 0.6
            Assert.AreEqual(0.6f, DeepStrikeRules.IsolationRisk(0.75f, 0.8f), Eps);
        }

        [Test]
        public void ShockEffect_FastSurpriseAmplifies()
        {
            // speed 0.8 × surprise 0.5 × 1.0 = 0.4
            Assert.AreEqual(0.4f, DeepStrikeRules.ShockEffect(0.8f, 0.5f), Eps);
        }

        [Test]
        public void DeepStrikeNetValue_SubtractsIsolationRisk()
        {
            // disruption 0.6 − isolation 0.3 = 0.3
            Assert.AreEqual(0.3f, DeepStrikeRules.DeepStrikeNetValue(0.6f, 0.3f), Eps);
            // リスクが利得を上回れば正味は負（突出の罠）
            Assert.Less(DeepStrikeRules.DeepStrikeNetValue(0.3f, 0.7f), 0f);
        }

        [Test]
        public void IsRearCollapsed_AtThreshold()
        {
            // 既定閾値 0.7
            Assert.IsFalse(DeepStrikeRules.IsRearCollapsed(0.6f));
            Assert.IsTrue(DeepStrikeRules.IsRearCollapsed(0.7f));
            Assert.IsTrue(DeepStrikeRules.IsRearCollapsed(0.95f));
        }

        [Test]
        public void Narrative_MobileForcePunchesDeepAndParalyzesRearButOverstretchesAndRisksIsolation()
        {
            var p = DeepStrikeRules.DeepStrikeParams.Default;

            // 機動部隊が敵縦深を突破して後方深くへ突進する（機動 150、縦深 30）。
            float depth = DeepStrikeRules.PenetrationDepth(150f, 30f, p);
            // 150×1.2=180 → 180/(180+30)=0.857142...
            Assert.AreEqual(180f / 210f, depth, Eps);

            // 深く入るほど中枢（生産中枢）の価値が高い目標を叩ける。
            float rearVal = DeepStrikeRules.RearTargetValue(depth, 100f);
            float shallowVal = DeepStrikeRules.RearTargetValue(0.3f, 100f);
            Assert.Greater(rearVal, shallowVal);

            // 後方依存の高い敵は麻痺し、後方が崩壊する。
            float disruption = DeepStrikeRules.SystemicDisruption(rearVal, 0.95f, p);
            Assert.Greater(disruption, p.collapseThreshold);
            Assert.IsTrue(DeepStrikeRules.IsRearCollapsed(disruption, p));

            // 奇襲的な突進ほど衝撃が増す。
            float fastSurprise = DeepStrikeRules.ShockEffect(0.9f, 0.8f, p);
            float slowKnown = DeepStrikeRules.ShockEffect(0.3f, 0.2f, p);
            Assert.Greater(fastSurprise, slowKnown);

            // しかし突出で補給線が伸びきり（≧1＝補給範囲超過）、
            float tether = DeepStrikeRules.SupplyTether(depth, 0.4f, p);
            Assert.Greater(tether, 1f);

            // 敵予備が厚いと孤立リスクが上がり、正味価値を削る。
            float risk = DeepStrikeRules.IsolationRisk(depth, 0.9f, p);
            float netWithRisk = DeepStrikeRules.DeepStrikeNetValue(disruption, risk);
            float netNoRisk = DeepStrikeRules.DeepStrikeNetValue(disruption, 0f);
            Assert.Less(netWithRisk, netNoRisk);
        }
    }
}
