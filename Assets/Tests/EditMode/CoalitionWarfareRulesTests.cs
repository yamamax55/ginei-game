using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class CoalitionWarfareRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void CombinedStrength_SumsWithDefaultScale()
        {
            float[] allies = { 6000f, 4000f, 2000f };
            Assert.AreEqual(12000f, CoalitionWarfareRules.CombinedStrength(allies), Eps);
        }

        [Test]
        public void CombinedStrength_NullAndEmptyAreZero()
        {
            Assert.AreEqual(0f, CoalitionWarfareRules.CombinedStrength(null), Eps);
            Assert.AreEqual(0f, CoalitionWarfareRules.CombinedStrength(new float[0]), Eps);
        }

        [Test]
        public void CombinedStrength_NegativeEntriesDropped()
        {
            float[] allies = { 5000f, -1000f, 3000f };
            Assert.AreEqual(8000f, CoalitionWarfareRules.CombinedStrength(allies), Eps);
        }

        [Test]
        public void CommandUnityFactor_CaveatsReduceUnity()
        {
            // 0.8 * (1 - 0.5*0.6) = 0.8 * 0.7 = 0.56
            Assert.AreEqual(0.56f, CoalitionWarfareRules.CommandUnityFactor(0.8f, 0.5f), Eps);
        }

        [Test]
        public void CoordinationEfficiency_MoreAlliesLowerThenFloor()
        {
            // 0.56 * (1 - 2*0.1) = 0.56 * 0.8 = 0.448
            Assert.AreEqual(0.448f, CoalitionWarfareRules.CoordinationEfficiency(0.56f, 3), Eps);
            // single ally => no multiplicity penalty
            Assert.AreEqual(0.56f, CoalitionWarfareRules.CoordinationEfficiency(0.56f, 1), Eps);
            // floored at minCoordination 0.3: 0.2*(1-4*0.1)=0.12 -> 0.3
            Assert.AreEqual(0.3f, CoalitionWarfareRules.CoordinationEfficiency(0.2f, 5), Eps);
        }

        [Test]
        public void DivergentObjectives_IsComplementOfAlignment()
        {
            Assert.AreEqual(0.3f, CoalitionWarfareRules.DivergentObjectives(0.7f), Eps);
        }

        [Test]
        public void BurdenSharingFriction_ScalesImbalance()
        {
            Assert.AreEqual(0.4f, CoalitionWarfareRules.BurdenSharingFriction(0.4f), Eps);
        }

        [Test]
        public void CoalitionCohesion_SharedThreatRaisesCohesion()
        {
            // cuf=0.56, divergent=0.4 => 0.56*(1-0.4*0.7)=0.56*0.72=0.4032; +0.5*0.3=0.15 => 0.5532
            float withThreat = CoalitionWarfareRules.CoalitionCohesion(0.56f, 0.4f, 0.5f);
            float noThreat = CoalitionWarfareRules.CoalitionCohesion(0.56f, 0.4f, 0f);
            Assert.AreEqual(0.5532f, withThreat, Eps);
            Assert.AreEqual(0.4032f, noThreat, Eps);
            Assert.Greater(withThreat, noThreat);
        }

        [Test]
        public void IsCoalitionEffective_ThresholdGate()
        {
            Assert.IsTrue(CoalitionWarfareRules.IsCoalitionEffective(0.448f, 0.4f));
            Assert.IsFalse(CoalitionWarfareRules.IsCoalitionEffective(0.448f, 0.5f));
        }

        [Test]
        public void Story_UnifiedCommandBeatsCaveatRiddenCoalition()
        {
            // 三国同盟（合算12000）が共同作戦。統一指揮が利けば実効戦力は高く、
            // 各国の留保（national caveats）が強ければ連携が崩れて額面どおり戦えない。
            float[] allies = { 6000f, 4000f, 2000f };
            float combined = CoalitionWarfareRules.CombinedStrength(allies);
            Assert.AreEqual(12000f, combined, Eps);

            // 統一指揮（留保 0.5）
            float unifiedCuf = CoalitionWarfareRules.CommandUnityFactor(0.8f, 0.5f);   // 0.56
            float unifiedEff = CoalitionWarfareRules.CoordinationEfficiency(unifiedCuf, 3); // 0.448
            float unifiedPower = CoalitionWarfareRules.EffectiveCombatPower(combined, unifiedEff);
            Assert.AreEqual(5376f, unifiedPower, 1e-2f);

            // 足並みの乱れ（留保 0.9）
            float dividedCuf = CoalitionWarfareRules.CommandUnityFactor(0.8f, 0.9f);   // 0.368
            float dividedEff = CoalitionWarfareRules.CoordinationEfficiency(dividedCuf, 3);
            float dividedPower = CoalitionWarfareRules.EffectiveCombatPower(combined, dividedEff);

            // 同じ合算戦力でも統一指揮の方が強い＝指揮統一の価値
            Assert.Greater(unifiedPower, dividedPower);
            Assert.Greater(unifiedEff, dividedEff);
        }
    }
}
