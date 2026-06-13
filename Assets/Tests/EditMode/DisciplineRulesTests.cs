using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 軍紀・査問を固定する：厳格さは軍紀を引き締めるが士気コストを伴う、軍紀崩壊＋不満で抗命リスク
    /// （roll決定論）、人望ある士官の査問は全軍士気を削る（ヤン査問会型）、軍紀→戦闘秩序倍率0.5..1。
    /// 境界・クランプを担保。
    /// </summary>
    public class DisciplineRulesTests
    {
        private static readonly DisciplineParams P = DisciplineParams.Default;
        // 引き締め0.5/士気減0.3/抗命閾値0.4/査問幅0.3

        [Test]
        public void OrderAfterEnforcement_TightensWithHarshness()
        {
            Assert.AreEqual(0.75f, DisciplineRules.OrderAfterEnforcement(0.5f, 0.5f, P), 1e-5f); // +0.5×0.5
            Assert.AreEqual(1f, DisciplineRules.OrderAfterEnforcement(0.8f, 1f, P), 1e-5f);       // 上限1
            Assert.AreEqual(0.5f, DisciplineRules.OrderAfterEnforcement(0.5f, 0f, P), 1e-5f);     // 締めなければ不変
        }

        [Test]
        public void MoraleCostOfHarshness_TheOtherEdgeOfTheSword()
        {
            Assert.AreEqual(0.3f, DisciplineRules.MoraleCostOfHarshness(1f, P), 1e-5f);
            Assert.AreEqual(0.15f, DisciplineRules.MoraleCostOfHarshness(0.5f, P), 1e-5f);
            Assert.AreEqual(0f, DisciplineRules.MoraleCostOfHarshness(0f, P), 1e-5f);
        }

        [Test]
        public void InsubordinationRisk_ZeroAboveThreshold()
        {
            Assert.AreEqual(0f, DisciplineRules.InsubordinationRisk(0.4f, 1f, P), 1e-5f); // 閾値ちょうど＝0
            Assert.AreEqual(0f, DisciplineRules.InsubordinationRisk(0.9f, 1f, P), 1e-5f);
        }

        [Test]
        public void InsubordinationRisk_RisesWithCollapseAndGrievance()
        {
            // 軍紀0・不満0＝不足1×0.5=0.5
            Assert.AreEqual(0.5f, DisciplineRules.InsubordinationRisk(0f, 0f, P), 1e-5f);
            // 軍紀0・不満1＝不足1×1.0=1.0
            Assert.AreEqual(1f, DisciplineRules.InsubordinationRisk(0f, 1f, P), 1e-5f);
            // 軍紀0.2＝不足0.5×0.5=0.25
            Assert.AreEqual(0.25f, DisciplineRules.InsubordinationRisk(0.2f, 0f, P), 1e-5f);
        }

        [Test]
        public void InsubordinationOccurs_DeterministicByRoll()
        {
            Assert.IsTrue(DisciplineRules.InsubordinationOccurs(0f, 0f, 0.49f, P));
            Assert.IsFalse(DisciplineRules.InsubordinationOccurs(0f, 0f, 0.51f, P));
        }

        [Test]
        public void InquiryMoralePenalty_ProportionalToRenown()
        {
            Assert.AreEqual(0.3f, DisciplineRules.InquiryMoralePenalty(1f, P), 1e-5f);  // 英雄を吊るせば全軍が冷める
            Assert.AreEqual(0f, DisciplineRules.InquiryMoralePenalty(0f, P), 1e-5f);    // 無名なら波風立たず
            Assert.AreEqual(0.15f, DisciplineRules.InquiryMoralePenalty(0.5f, P), 1e-5f);
        }

        [Test]
        public void CommandEfficiency_LerpsHalfToFull()
        {
            Assert.AreEqual(1f, DisciplineRules.CommandEfficiency(1f), 1e-5f);
            Assert.AreEqual(0.5f, DisciplineRules.CommandEfficiency(0f), 1e-5f);
            Assert.AreEqual(0.75f, DisciplineRules.CommandEfficiency(0.5f), 1e-5f);
        }
    }
}
