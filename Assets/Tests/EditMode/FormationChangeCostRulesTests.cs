using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>陣形変更コスト経済（#陣形コスト・FormationChangeCostRules）の純ロジック検証。</summary>
    public class FormationChangeCostRulesTests
    {
        private static FormationChangeCostRules.Params P => FormationChangeCostRules.Params.Default;

        [Test]
        public void Cost_IsHeavierInCombat()
        {
            float peace = FormationChangeCostRules.Cost(false, P);
            float combat = FormationChangeCostRules.Cost(true, P);
            Assert.Greater(combat, peace, "戦闘中のコストは平時より重いこと");
            Assert.AreEqual(peace * P.combatCostMultiplier, combat, 1e-3f);
        }

        [Test]
        public void MaxPoints_ScalesWithLeadership()
        {
            float low = FormationChangeCostRules.MaxPoints(0f, P);
            float high = FormationChangeCostRules.MaxPoints(100f, P);
            Assert.AreEqual(P.maxPointsMin, low, 1e-3f);
            Assert.AreEqual(P.maxPointsMax, high, 1e-3f);
            Assert.Greater(high, low, "統率が高いほどプール上限が大きいこと");
        }

        [Test]
        public void RegenPerSecond_ScalesWithLeadership()
        {
            float low = FormationChangeCostRules.RegenPerSecond(0f, P);
            float high = FormationChangeCostRules.RegenPerSecond(100f, P);
            Assert.Greater(high, low, "統率が高いほど回復が速いこと");
        }

        [Test]
        public void CanAfford_RespectsThreshold()
        {
            float peaceCost = FormationChangeCostRules.Cost(false, P);
            Assert.IsTrue(FormationChangeCostRules.CanAfford(peaceCost, false, P));
            Assert.IsFalse(FormationChangeCostRules.CanAfford(peaceCost - 0.01f, false, P));
            // 平時に賄えても戦闘中は賄えないことがある（重コスト）。
            Assert.IsFalse(FormationChangeCostRules.CanAfford(peaceCost, true, P));
        }

        [Test]
        public void Regen_AccumulatesAndClampsToMax()
        {
            float max = 100f;
            float pts = FormationChangeCostRules.Regen(50f, max, 10f, 1f);
            Assert.AreEqual(60f, pts, 1e-3f, "10/秒×1秒で+10");
            // 上限クランプ
            float clamped = FormationChangeCostRules.Regen(95f, max, 10f, 1f);
            Assert.AreEqual(max, clamped, 1e-3f);
        }

        [Test]
        public void Regen_NegativeDtIsNoOp()
        {
            Assert.AreEqual(50f, FormationChangeCostRules.Regen(50f, 100f, 10f, -1f), 1e-3f);
        }
    }
}
