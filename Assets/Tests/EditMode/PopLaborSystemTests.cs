using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>POP労働システム基盤（#2026）：供給(POPLAB-1)/配分(POPLAB-2)/マッチング(POPLAB-3)/賃金(POPLAB-4)/生産性(POPLAB-5)/動員(POPLAB-6)。</summary>
    public class PopLaborSystemTests
    {
        // --- POPLAB-1 労働力供給 ---
        [Test]
        public void Supply_LaborForceAndParticipation()
        {
            Assert.AreEqual(600f, LaborSupplyRules.LaborForce(1000f, 0.6f), 1e-2f);
            Assert.AreEqual(0.63f, LaborSupplyRules.GenderAdjustedRate(0.7f, 0.5f, 0.8f), 1e-4f); // 女性参加低で供給細る
            Assert.AreEqual(0.66f, LaborSupplyRules.PhaseAdjustedRate(0.6f, 1.1f), 1e-4f);          // 人口ボーナス
            Assert.AreEqual(0.63f, LaborSupplyRules.NurseryAdjustedRate(0.6f, 1.05f), 1e-4f);       // 保育で働く親増
            Assert.AreEqual(0.63f, LaborSupplyRules.EffectiveParticipation(0.7f, 0.5f, 0.8f, 1.0f, 1.0f), 1e-4f);
            Assert.AreEqual(630f, LaborSupplyRules.EffectiveLaborForce(1000f, 0.7f, 0.5f, 0.8f, 1.0f, 1.0f), 1e-2f);
        }

        // --- POPLAB-2 職業配分（転職フロー・総量保存） ---
        [Test]
        public void Allocation_TargetAndConverge()
        {
            var target = OccupationAllocationRules.TargetShareFromDemand(new List<float> { 10, 50, 10, 15, 10, 5 });
            Assert.AreEqual(0.5f, target.Share(Occupation.工員), 1e-4f); // 需要50/100
            Assert.AreEqual(1f, target.Total, 1e-4f);

            var cur = new Workforce(new[] { 0.5f, 0.5f, 0f, 0f, 0f, 0f });
            var tgt = new Workforce(new[] { 0.1f, 0.9f, 0f, 0f, 0f, 0f });
            var next = OccupationAllocationRules.Converge(cur, tgt, 0.5f);
            Assert.AreEqual(0.3f, next.Share(Occupation.農民), 1e-4f);
            Assert.AreEqual(0.7f, next.Share(Occupation.工員), 1e-4f);
            Assert.AreEqual(1f, next.Total, 1e-4f); // 合計保存
        }

        // --- POPLAB-3 マッチング・失業 ---
        [Test]
        public void Matching_EmploymentAndUnemployment()
        {
            Assert.AreEqual(80f, LaborMatchingRules.Employed(100f, 80f), 1e-2f);
            Assert.AreEqual(20f, LaborMatchingRules.Unemployed(100f, 80f), 1e-2f);
            Assert.AreEqual(20f, LaborMatchingRules.Shortage(80f, 100f), 1e-2f);
            Assert.AreEqual(0.1f, LaborMatchingRules.UnemploymentRate(20f, 200f), 1e-4f);
        }

        [Test]
        public void Matching_UnemploymentDecomposition()
        {
            Assert.AreEqual(30f, LaborMatchingRules.FrictionalUnemployment(1000f, 0.03f), 1e-2f);
            Assert.AreEqual(50f, LaborMatchingRules.CyclicalUnemployment(100f, 20f, 30f), 1e-2f);
            // 3分解の合計＝総失業
            float fr = LaborMatchingRules.Decompose(100f, 20f, 30f, UnemploymentType.摩擦的);
            float st = LaborMatchingRules.Decompose(100f, 20f, 30f, UnemploymentType.構造的);
            float cy = LaborMatchingRules.Decompose(100f, 20f, 30f, UnemploymentType.循環的);
            Assert.AreEqual(20f, fr, 1e-2f);
            Assert.AreEqual(30f, st, 1e-2f);
            Assert.AreEqual(50f, cy, 1e-2f);
            Assert.AreEqual(100f, fr + st + cy, 1e-2f);
        }

        // --- POPLAB-4 賃金需給連動 ---
        [Test]
        public void Wage_DemandLinked()
        {
            Assert.AreEqual(1.1f, LaborWageRules.WageDemandFactor(120f, 100f, 0.5f), 1e-4f); // 人手不足で↑
            Assert.AreEqual(0.9f, LaborWageRules.WageDemandFactor(80f, 100f, 0.5f), 1e-4f);  // 過剰で↓
            Assert.AreEqual(1.5f, LaborWageRules.WageDemandFactor(100f, 0f, 0.5f), 1e-4f);    // 求職ゼロ＝極端
            Assert.AreEqual(1100f, LaborWageRules.OccupationWage(1000f, 1.1f), 1e-2f);
            Assert.AreEqual(1000f, LaborWageRules.RealWage(1100f, 1.1f), 1e-2f);
            Assert.AreEqual(0.1f, LaborWageRules.WageSupportDelta(1200f, 1000f, 0.5f), 1e-4f);
        }

        // --- POPLAB-5 生産性・適所度 ---
        [Test]
        public void Productivity_AlignmentSkillEmployment()
        {
            Assert.AreEqual(1.2f, LaborProductivityRules.AlignmentBonus(1f), 1e-4f);
            Assert.AreEqual(1.3f, LaborProductivityRules.SkillBonus(1f), 1e-4f);
            Assert.AreEqual(1.56f, LaborProductivityRules.ProductivityFactor(1f, 1f, 1f), 1e-4f); // 適所×熟練×全就業
            Assert.AreEqual(1.0f, LaborProductivityRules.ProductivityFactor(0.5f, 0.5f, 1f), 1e-4f); // 標準
            Assert.AreEqual(1560f, LaborProductivityRules.EffectiveOutput(1000f, 1.56f), 1e-1f);
        }

        // --- POPLAB-6 徴募↔労働競合（総力戦） ---
        [Test]
        public void Mobilization_Tradeoff()
        {
            Assert.AreEqual(200f, MobilizationRules.MobilizedPool(1000f, 0.2f), 1e-2f);
            Assert.AreEqual(600f, MobilizationRules.ProductionLaborAfterMobilization(800f, 200f), 1e-2f);
            Assert.AreEqual(0.8f, MobilizationRules.OutputFactor(0.2f, 1.0f), 1e-4f); // 動員で産出↓
            Assert.AreEqual(0.4f, MobilizationRules.SupportPenalty(0.5f, 0.3f, 2.0f), 1e-4f); // 過大動員で支持↓
            Assert.AreEqual(0f, MobilizationRules.SupportPenalty(0.2f, 0.3f, 2.0f), 1e-4f);    // 閾値以下は無し
            Assert.AreEqual(100f, MobilizationRules.MaleDrawnMobilization(200f, 0.5f, 1.0f), 1e-2f);
        }
    }
}
