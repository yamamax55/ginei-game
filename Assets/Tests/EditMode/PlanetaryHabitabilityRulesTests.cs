using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// PlanetaryHabitabilityRules（惑星居住性）の EditMode テスト。
    /// 既定Params期待値を手計算で固定（誤差1e-4f、Pow/Sqrt箇所のみ1e-3f）。決定論。
    /// </summary>
    public class PlanetaryHabitabilityRulesTests
    {
        private const float Eps = 1e-4f;
        private const float PowEps = 1e-3f;

        [Test]
        public void AtmosphereScore_Oxygen_Toxicity_Pressure_Multiply()
        {
            // 0.8 × (1-0.1) × Proximity(1,1,0.5)=1 → 0.72
            Assert.AreEqual(0.72f, PlanetaryHabitabilityRules.AtmosphereScore(0.8f, 0.1f, 1.0f), Eps);
            // 気圧が許容を超えて外れると press=0 → 全体0
            Assert.AreEqual(0f, PlanetaryHabitabilityRules.AtmosphereScore(1f, 0f, 2f), Eps);
        }

        [Test]
        public void GravityScore_PenalizesDeviationBothWays()
        {
            Assert.AreEqual(1f, PlanetaryHabitabilityRules.GravityScore(1.0f), Eps);   // 標準1G
            Assert.AreEqual(0f, PlanetaryHabitabilityRules.GravityScore(1.5f), Eps);   // 過大で0
            Assert.AreEqual(0f, PlanetaryHabitabilityRules.GravityScore(0.5f), Eps);   // 過小で0
            Assert.AreEqual(0.5f, PlanetaryHabitabilityRules.GravityScore(1.25f), Eps);
        }

        [Test]
        public void ThermalScore_PeaksAtIdealBand()
        {
            Assert.AreEqual(1f, PlanetaryHabitabilityRules.ThermalScore(0.5f), Eps);   // 適温
            Assert.AreEqual(0f, PlanetaryHabitabilityRules.ThermalScore(0.85f), Eps);  // 灼熱
            Assert.AreEqual(0f, PlanetaryHabitabilityRules.ThermalScore(0.15f), Eps);  // 極寒
        }

        [Test]
        public void WaterAvailability_IceDiscounted()
        {
            // 地表水0.4 + 氷床0.6×0.5 = 0.7
            Assert.AreEqual(0.7f, PlanetaryHabitabilityRules.WaterAvailability(0.4f, 0.6f), Eps);
            // 飽和してもClamp01で1
            Assert.AreEqual(1f, PlanetaryHabitabilityRules.WaterAvailability(1f, 1f), Eps);
        }

        [Test]
        public void BaseHabitability_SynergyCollapsesOnMissingElement()
        {
            // 全要素満点 → 1
            Assert.AreEqual(1f, PlanetaryHabitabilityRules.BaseHabitability(1f, 1f, 1f, 1f), PowEps);
            // どれか1要素が0なら総崩れ
            Assert.AreEqual(0f, PlanetaryHabitabilityRules.BaseHabitability(1f, 1f, 1f, 0f), PowEps);
            // 全0.5: product=0.0625, geo=0.5, shaped=0.5^1.5≈0.353553
            Assert.AreEqual(0.353553f, PlanetaryHabitabilityRules.BaseHabitability(0.5f, 0.5f, 0.5f, 0.5f), PowEps);
        }

        [Test]
        public void LifeSupportCost_InverseOfHabitability()
        {
            Assert.AreEqual(0f, PlanetaryHabitabilityRules.LifeSupportCost(1f), Eps);          // 楽園は無コスト
            Assert.AreEqual(1f, PlanetaryHabitabilityRules.LifeSupportCost(0f), Eps);          // 致死環境は最大
            Assert.AreEqual(0.646447f, PlanetaryHabitabilityRules.LifeSupportCost(0.353553f), Eps);
        }

        [Test]
        public void TerraformingPotential_MaxAtMidHabitability_ScaledByMass()
        {
            // peak=0.4 で window=1、質量1 → 1
            Assert.AreEqual(1f, PlanetaryHabitabilityRules.TerraformingPotential(0.4f, 1f), Eps);
            // h>peak: (1-0.7)/(1-0.4)=0.5、質量1 → 0.5
            Assert.AreEqual(0.5f, PlanetaryHabitabilityRules.TerraformingPotential(0.7f, 1f), Eps);
            // h<peak: 0.2/0.4=0.5、質量0.5 → 0.25
            Assert.AreEqual(0.25f, PlanetaryHabitabilityRules.TerraformingPotential(0.2f, 0.5f), Eps);
            // 楽園(1.0)は伸びしろ無し
            Assert.AreEqual(0f, PlanetaryHabitabilityRules.TerraformingPotential(1f, 1f), Eps);
        }

        [Test]
        public void EffectiveCapacity_ArtificialHabitatFillsTheGap()
        {
            // 素0.3 + 残り0.7×(1×0.6)=0.42 → 0.72
            Assert.AreEqual(0.72f, PlanetaryHabitabilityRules.EffectiveCapacity(0.3f, 1f), Eps);
            // 完全な致死環境でも人工環境フルで 0.6 まで住める（過酷環境のプレミアム入植）
            Assert.AreEqual(0.6f, PlanetaryHabitabilityRules.EffectiveCapacity(0f, 1f), Eps);
            // 人工環境ゼロなら素の居住性のまま
            Assert.AreEqual(0.3f, PlanetaryHabitabilityRules.EffectiveCapacity(0.3f, 0f), Eps);
        }

        [Test]
        public void ClampsOutOfRangeInputs()
        {
            // 入力が範囲外でも破綻せず0..1に収まる
            float atmos = PlanetaryHabitabilityRules.AtmosphereScore(5f, -3f, 1f);
            Assert.AreEqual(1f, atmos, Eps); // oxy→1, clean→1, press→1
            Assert.AreEqual(0f, PlanetaryHabitabilityRules.GravityScore(-10f), Eps);
            Assert.AreEqual(1f, PlanetaryHabitabilityRules.EffectiveCapacity(2f, 2f), Eps);
            Assert.AreEqual(0f, PlanetaryHabitabilityRules.LifeSupportCost(9f), Eps);
        }

        [Test]
        public void Narrative_DeadWorldVsGardenWorld()
        {
            var p = PlanetaryHabitabilityParams.Default;

            // 庭園惑星：濃い酸素・無毒・1気圧・1G・適温・豊富な水
            float gAtmos = PlanetaryHabitabilityRules.AtmosphereScore(0.95f, 0f, 1f, p);
            float gGrav = PlanetaryHabitabilityRules.GravityScore(1f, p);
            float gTherm = PlanetaryHabitabilityRules.ThermalScore(0.5f, p);
            float gWater = PlanetaryHabitabilityRules.WaterAvailability(0.7f, 0.3f, p);
            float gardenHab = PlanetaryHabitabilityRules.BaseHabitability(gAtmos, gGrav, gTherm, gWater, p);

            // 死の惑星：希薄な酸素・有毒・低重力・極寒・氷床のみ
            float dAtmos = PlanetaryHabitabilityRules.AtmosphereScore(0.1f, 0.8f, 0.2f, p);
            float dGrav = PlanetaryHabitabilityRules.GravityScore(0.4f, p);
            float dTherm = PlanetaryHabitabilityRules.ThermalScore(0.05f, p);
            float dWater = PlanetaryHabitabilityRules.WaterAvailability(0f, 0.5f, p);
            float deadHab = PlanetaryHabitabilityRules.BaseHabitability(dAtmos, dGrav, dTherm, dWater, p);

            // 庭園は死の惑星より遥かに住みよい
            Assert.Greater(gardenHab, deadHab);
            // 死の惑星ほど生命維持コストが高い
            Assert.Greater(PlanetaryHabitabilityRules.LifeSupportCost(deadHab, p),
                           PlanetaryHabitabilityRules.LifeSupportCost(gardenHab, p));
            // 死の惑星でも人工環境（ドーム都市）に頼れば実効収容力は素の居住性を上回る
            float domed = PlanetaryHabitabilityRules.EffectiveCapacity(deadHab, 0.9f, p);
            Assert.Greater(domed, deadHab);
        }
    }
}
