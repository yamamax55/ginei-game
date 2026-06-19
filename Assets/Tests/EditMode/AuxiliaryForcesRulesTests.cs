using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 補助部隊を固定する：訓練×士気で質、指揮互換×(1−言語障壁)で連携、地形×繋がりで現地知識、文化×不満で忠誠の不確かさ、
    /// 比率で依存リスク、不確かさ×兵力で反乱潜在、質/コストで費用対効果、質×連携−反乱で正味価値。境界・クランプを担保。
    /// </summary>
    public class AuxiliaryForcesRulesTests
    {
        private static readonly AuxiliaryForcesParams P = AuxiliaryForcesParams.Default;
        // 既定＝質1.0/現地0.9/文化0.6/不満0.4/依存指数0.7/反乱0.8/反乱罰1.0/効率上限3

        [Test]
        public void AuxiliaryQuality_GeometricMean()
        {
            // sqrt(0.64×1.0)×1.0 = 0.8
            Assert.AreEqual(0.8f, AuxiliaryForcesRules.AuxiliaryQuality(0.64f, 1f, P), 1e-3f);
            // どちらか0なら0
            Assert.AreEqual(0f, AuxiliaryForcesRules.AuxiliaryQuality(0f, 1f, P), 1e-4f);
        }

        [Test]
        public void Integration_CompatibilityTimesLanguage()
        {
            // 0.8×(1−0.25) = 0.6
            Assert.AreEqual(0.6f, AuxiliaryForcesRules.Integration(0.8f, 0.25f, P), 1e-4f);
            // 言語障壁が最大なら連携消失
            Assert.AreEqual(0f, AuxiliaryForcesRules.Integration(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void LocalKnowledgeValue_GeometricMeanScaled()
        {
            // sqrt(1×1)×0.9 = 0.9
            Assert.AreEqual(0.9f, AuxiliaryForcesRules.LocalKnowledgeValue(1f, 1f, P), 1e-3f);
            // sqrt(0.25×1)×0.9 = 0.5×0.9 = 0.45
            Assert.AreEqual(0.45f, AuxiliaryForcesRules.LocalKnowledgeValue(0.25f, 1f, P), 1e-3f);
        }

        [Test]
        public void LoyaltyUncertainty_WeightedSum()
        {
            // 0.5×0.6 + 0.5×0.4 = 0.3+0.2 = 0.5
            Assert.AreEqual(0.5f, AuxiliaryForcesRules.LoyaltyUncertainty(0.5f, 0.5f, P), 1e-4f);
            // 最大で 0.6+0.4 = 1.0
            Assert.AreEqual(1f, AuxiliaryForcesRules.LoyaltyUncertainty(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void DependencyRisk_RisesWithRatio()
        {
            Assert.AreEqual(0f, AuxiliaryForcesRules.DependencyRisk(0f, P), 1e-4f);
            Assert.AreEqual(1f, AuxiliaryForcesRules.DependencyRisk(1f, P), 1e-4f);
            // 0.5^0.7 ≒ 0.6156
            Assert.AreEqual(0.6156f, AuxiliaryForcesRules.DependencyRisk(0.5f, P), 1e-3f);
        }

        [Test]
        public void RebellionPotential_UncertaintyTimesStrength()
        {
            // 0.5×0.5×0.8 = 0.2
            Assert.AreEqual(0.2f, AuxiliaryForcesRules.RebellionPotential(0.5f, 0.5f, P), 1e-4f);
            // 兵力0なら反乱しようがない
            Assert.AreEqual(0f, AuxiliaryForcesRules.RebellionPotential(1f, 0f, P), 1e-4f);
        }

        [Test]
        public void CostEfficiency_QualityOverCost_Capped()
        {
            // 0.8/0.4 = 2.0（補助兵は安く効率が高い）
            Assert.AreEqual(2f, AuxiliaryForcesRules.CostEfficiency(0.8f, 0.4f, P), 1e-4f);
            // 極小コストでも上限3で頭打ち
            Assert.AreEqual(3f, AuxiliaryForcesRules.CostEfficiency(1f, 0.01f, P), 1e-4f);
        }

        [Test]
        public void AuxiliaryValue_QualityIntegrationMinusRebellion()
        {
            // 0.8×0.6 − 0.2×1.0 = 0.48−0.2 = 0.28
            Assert.AreEqual(0.28f, AuxiliaryForcesRules.AuxiliaryValue(0.8f, 0.6f, 0.2f, P), 1e-4f);
        }

        [Test]
        public void ParamsClampAndDefaultWiringMatch()
        {
            // ctor の Clamp（範囲外を丸める）と Default 委譲版が Params 明示版と一致
            var clamped = new AuxiliaryForcesParams(9f, -1f, 2f, 2f, 99f, 9f, -1f, 0f);
            Assert.AreEqual(2f, clamped.qualityScale, 1e-4f);
            Assert.AreEqual(0f, clamped.localKnowledgeScale, 1e-4f);
            Assert.AreEqual(1f, clamped.culturalWeight, 1e-4f);
            Assert.AreEqual(4f, clamped.dependencyExponent, 1e-4f);
            Assert.AreEqual(0f, clamped.rebellionPenalty, 1e-4f);
            Assert.AreEqual(1f, clamped.maxEfficiency, 1e-4f); // 下限1

            Assert.AreEqual(AuxiliaryForcesRules.Integration(0.8f, 0.25f, P),
                            AuxiliaryForcesRules.Integration(0.8f, 0.25f), 1e-4f);
        }

        /// <summary>
        /// 物語：従属国の現地兵を大量に雇った勢力。安く頭数は揃うが、文化が遠く不満が募って忠誠は不確か。
        /// 依存しすぎて本国軍が手薄になり、ついに補助兵が反乱の刃へ転じて正味価値が負に沈む。
        /// </summary>
        [Test]
        public void Narrative_OverDependentAuxiliariesTurnNegative()
        {
            // 安く揃えた並の補助兵（質も連携もほどほど）
            float quality = AuxiliaryForcesRules.AuxiliaryQuality(0.6f, 0.6f, P);   // sqrt(0.36)=0.6
            float integ = AuxiliaryForcesRules.Integration(0.7f, 0.3f, P);          // 0.49
            float eff = AuxiliaryForcesRules.CostEfficiency(quality, 0.3f, P);       // 0.6/0.3=2.0（安い）
            Assert.AreEqual(0.6f, quality, 1e-3f);
            Assert.Greater(eff, 1f); // 正規軍より費用対効果は高い

            // だが補助兵に依存しすぎ＝本国軍が手薄
            float dep = AuxiliaryForcesRules.DependencyRisk(0.9f, P);
            Assert.Greater(dep, AuxiliaryForcesRules.DependencyRisk(0.3f, P));

            // 文化的距離と不満で忠誠は不確か、大兵力ゆえ反乱潜在は高い
            float uncertainty = AuxiliaryForcesRules.LoyaltyUncertainty(0.9f, 0.9f, P);
            float rebellion = AuxiliaryForcesRules.RebellionPotential(uncertainty, 0.9f, P);
            Assert.Greater(rebellion, 0.5f);

            // 正味価値は反乱潜在に蝕まれて負（味方に刃を向ける）
            float value = AuxiliaryForcesRules.AuxiliaryValue(quality, integ, rebellion, P);
            Assert.Less(value, 0f);

            // 忠実な少数精鋭の補助兵なら正の価値
            float loyalRebellion = AuxiliaryForcesRules.RebellionPotential(0.1f, 0.2f, P);
            float loyalValue = AuxiliaryForcesRules.AuxiliaryValue(0.8f, 0.8f, loyalRebellion, P);
            Assert.Greater(loyalValue, value);
            Assert.Greater(loyalValue, 0f);
        }
    }
}
