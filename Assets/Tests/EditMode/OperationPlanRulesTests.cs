using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 作戦立案を固定する：質＝運営能力×準備充足（満了で頭打ち）、初期条件倍率は質の差、補給ボーナス、
    /// 接敵後の陳腐化（静的戦況では長持ち）＝「計画は敵との接触に耐えない」。境界を担保。
    /// </summary>
    public class OperationPlanRulesTests
    {
        private static readonly OperationPlanParams P = OperationPlanParams.Default;
        // 準備満了10/初期優位0.2/補給0.3/陳腐化0.1

        [Test]
        public void PlanQuality_AbilityTimesPrep()
        {
            // 運営100×準備満了＝1.0
            Assert.AreEqual(1f, OperationPlanRules.PlanQuality(100f, 10f, P), 1e-5f);
            // 運営50×準備半分＝0.25
            Assert.AreEqual(0.25f, OperationPlanRules.PlanQuality(50f, 5f, P), 1e-5f);
            // 準備時間を倍かけても満了で頭打ち
            Assert.AreEqual(1f, OperationPlanRules.PlanQuality(100f, 100f, P), 1e-5f);
            // 無準備＝ゼロ（天才でも即興は計画でない）
            Assert.AreEqual(0f, OperationPlanRules.PlanQuality(100f, 0f, P), 1e-5f);
        }

        [Test]
        public void InitialAdvantageFactor_ByQualityDifference()
        {
            // 完璧 vs 杜撰＝1+1×0.2=1.2
            Assert.AreEqual(1.2f, OperationPlanRules.InitialAdvantageFactor(1f, 0f, P), 1e-5f);
            // 双方完璧＝互角
            Assert.AreEqual(1f, OperationPlanRules.InitialAdvantageFactor(1f, 1f, P), 1e-5f);
            // 自分が杜撰＝不利
            Assert.AreEqual(0.8f, OperationPlanRules.InitialAdvantageFactor(0f, 1f, P), 1e-5f);
        }

        [Test]
        public void SupplyEfficiencyFactor_ScalesWithQuality()
        {
            Assert.AreEqual(1.3f, OperationPlanRules.SupplyEfficiencyFactor(1f, P), 1e-5f);
            Assert.AreEqual(1f, OperationPlanRules.SupplyEfficiencyFactor(0f, P), 1e-5f);
        }

        [Test]
        public void DecayedQuality_PlanDiesOnContact()
        {
            // 高テンポ：質1.0 が接敵5 で 1−0.1×1×5=0.5
            Assert.AreEqual(0.5f, OperationPlanRules.DecayedQuality(1f, 5f, 1f, P), 1e-5f);
            // 接敵10 で消滅（下限0）
            Assert.AreEqual(0f, OperationPlanRules.DecayedQuality(1f, 10f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, OperationPlanRules.DecayedQuality(1f, 100f, 1f, P), 1e-5f);
            // 静的な包囲戦（tempo=0）＝計画は長持ち
            Assert.AreEqual(1f, OperationPlanRules.DecayedQuality(1f, 100f, 0f, P), 1e-5f);
        }
    }
}
