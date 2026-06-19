using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// SabotageOpsRules（破壊工作）の EditMode テスト。既定 Params で期待値固定。
    /// </summary>
    public class SabotageOpsRulesTests
    {
        const float Eps = 1e-4f;
        // 平方根（SabotageDamage の charges）を含む箇所は緩める。
        const float PowEps = 1e-3f;

        [Test]
        public void InfiltrationSuccess_SkillVsSecurity_Ratio()
        {
            // skill 0.8 ×1.5 = 1.2, security 0.5 → 1.2/(1.2+0.5)=1.2/1.7=0.705882
            Assert.AreEqual(0.705882f, SabotageOpsRules.InfiltrationSuccess(0.8f, 0.5f), Eps);
        }

        [Test]
        public void InfiltrationSuccess_NoSkill_NoEntry()
        {
            Assert.AreEqual(0f, SabotageOpsRules.InfiltrationSuccess(0f, 0.5f), Eps);
        }

        [Test]
        public void SabotageDamage_ScalesWithChargesSqrt()
        {
            // infil 0.5 × vuln 0.5 × sqrt(2)=1.41421 → 0.353553
            Assert.AreEqual(0.353553f, SabotageOpsRules.SabotageDamage(0.5f, 0.5f, 2f), PowEps);
        }

        [Test]
        public void OperationalDisruption_AmplifiedByImportance()
        {
            // dmg 0.5 × importance 0.6 × 1.2 = 0.36
            Assert.AreEqual(0.36f, SabotageOpsRules.OperationalDisruption(0.5f, 0.6f), Eps);
        }

        [Test]
        public void DetectionDuringOp_HighSkillKeepsFloor()
        {
            // base 0.5 × alert 0.5 × (1-0.8)=0.2 → 0.05; floor 0.1×0.5=0.05 → max=0.05
            Assert.AreEqual(0.05f, SabotageOpsRules.DetectionDuringOp(0.8f, 0.5f), Eps);
        }

        [Test]
        public void AgentLoss_MitigatedByExtractionPlan()
        {
            // det 0.05 × (1 - 0.75 × 0.8)=1-0.6=0.4 → 0.02
            Assert.AreEqual(0.02f, SabotageOpsRules.AgentLoss(0.05f, 0.75f), Eps);
        }

        [Test]
        public void RepairDelay_ShortenedByEnemyRepairCapacity()
        {
            // dmg 0.5 × 12 / (1+2) = 6/3 = 2.0
            Assert.AreEqual(2.0f, SabotageOpsRules.RepairDelay(0.5f, 2f), Eps);
        }

        [Test]
        public void EscalationRetaliation_ProportionalToDamage()
        {
            // dmg 0.5 × 0.6 = 0.3
            Assert.AreEqual(0.3f, SabotageOpsRules.EscalationRetaliation(0.5f), Eps);
        }

        [Test]
        public void IsSabotageSuccessful_ThresholdGate()
        {
            // 既定閾値 0.4：0.5 は成功、0.3 は失敗。
            Assert.IsTrue(SabotageOpsRules.IsSabotageSuccessful(0.5f));
            Assert.IsFalse(SabotageOpsRules.IsSabotageSuccessful(0.3f));
        }

        [Test]
        public void Narrative_RaidOnEnemyShipyard()
        {
            // 物語：技量0.8の工作員が警備0.5の敵造船所へ潜入し、脆弱性0.5の箇所に爆薬2個を仕掛ける。
            var p = SabotageOpsRules.SabotageOpsParams.Default;

            float infil = SabotageOpsRules.InfiltrationSuccess(0.8f, 0.5f, p);
            // 0.705882
            float dmg = SabotageOpsRules.SabotageDamage(infil, 0.5f, 2f);
            // 0.705882 × 0.5 × 1.41421 = 0.499134
            Assert.AreEqual(0.499134f, dmg, PowEps);

            // 閾値0.4を超え破壊工作は成功。
            Assert.IsTrue(SabotageOpsRules.IsSabotageSuccessful(dmg, p.successThreshold));

            // 重要造船所（重要度0.7）の運用は大きく麻痺する。
            float disruption = SabotageOpsRules.OperationalDisruption(dmg, 0.7f, p);
            // 0.499134 × 0.7 × 1.2 = 0.419272
            Assert.AreEqual(0.419272f, disruption, PowEps);

            // 復旧は遅延し、報復・警備強化を招く。
            Assert.Greater(SabotageOpsRules.RepairDelay(dmg, 1f, p), 0f);
            Assert.Greater(SabotageOpsRules.EscalationRetaliation(dmg, p), 0f);

            // 練られた脱出計画（0.9）で発見されても工作員は逃げ延びやすい。
            float det = SabotageOpsRules.DetectionDuringOp(0.8f, 0.5f, p);
            float lossPlanned = SabotageOpsRules.AgentLoss(det, 0.9f, p);
            float lossNoPlan = SabotageOpsRules.AgentLoss(det, 0f, p);
            Assert.Less(lossPlanned, lossNoPlan);
        }
    }
}
