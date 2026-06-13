using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>予備兵力の投入：温存価値・適時性・補強/拡張効果・早撃ち無駄/遅延損失・枯渇リスク・投入判定。</summary>
    public class ReserveDeploymentRulesTests
    {
        [Test]
        public void ReserveValue_FreshnessDiscountsWornReserve()
        {
            // 新鋭（freshness=1）＝兵力満額／消耗（freshness=0）＝freshnessWeight=0.5 ぶん割引
            Assert.AreEqual(0.8f, ReserveDeploymentRules.ReserveValue(0.8f, 1f), 1e-4f); // 0.8×(0.5+0.5×1)
            Assert.AreEqual(0.4f, ReserveDeploymentRules.ReserveValue(0.8f, 0f), 1e-4f); // 0.8×0.5
        }

        [Test]
        public void CommitTiming_PeaksWithCrisisAndFreshReserve()
        {
            Assert.AreEqual(0.8f, ReserveDeploymentRules.CommitTiming(1f, 0.8f), 1e-4f);   // 切迫×価値
            Assert.AreEqual(0.24f, ReserveDeploymentRules.CommitTiming(0.3f, 0.8f), 1e-4f); // 平時は低い
        }

        [Test]
        public void ReinforceEffect_CoversDeficit()
        {
            Assert.AreEqual(1f, ReserveDeploymentRules.ReinforceEffect(0.6f, 0.4f), 1e-4f);   // 不足を上回る→立て直す
            Assert.AreEqual(0.5f, ReserveDeploymentRules.ReinforceEffect(0.3f, 0.6f), 1e-4f); // 半分しか埋まらない
            Assert.AreEqual(1f, ReserveDeploymentRules.ReinforceEffect(0.2f, 0f), 1e-4f);     // 不足なし→完全
        }

        [Test]
        public void ExploitEffect_NeedsBothReserveAndOpening()
        {
            Assert.AreEqual(0.4f, ReserveDeploymentRules.ExploitEffect(0.8f, 0.5f), 1e-4f); // 0.8×0.5
            Assert.AreEqual(0f, ReserveDeploymentRules.ExploitEffect(0.9f, 0f), 1e-4f);     // 突破口なし＝薄い
        }

        [Test]
        public void PrematureCommitWaste_LowTimingWastes()
        {
            Assert.AreEqual(0.6f, ReserveDeploymentRules.PrematureCommitWaste(0f), 1e-4f); // (1-0)×0.6
            Assert.AreEqual(0f, ReserveDeploymentRules.PrematureCommitWaste(1f), 1e-4f);   // 適時＝無駄なし
        }

        [Test]
        public void TooLateCommitLoss_RisesWithCriticality()
        {
            Assert.AreEqual(0.8f, ReserveDeploymentRules.TooLateCommitLoss(1f), 1e-4f);   // 1×0.8
            Assert.AreEqual(0.4f, ReserveDeploymentRules.TooLateCommitLoss(0.5f), 1e-4f); // 0.5×0.8
        }

        [Test]
        public void ReserveExhaustionRisk_SpikesNearFullCommit()
        {
            // c²×0.7：Pow 箇所のみ許容を緩める
            Assert.AreEqual(0.7f, ReserveDeploymentRules.ReserveExhaustionRisk(1f), 1e-3f);    // 使い切り＝最大
            Assert.AreEqual(0.175f, ReserveDeploymentRules.ReserveExhaustionRisk(0.5f), 1e-3f); // 0.25×0.7
        }

        [Test]
        public void Story_DecisiveCommitWinsButEarlyIsWasteAndExhaustionLeavesNothing()
        {
            // 新鋭の予備（兵力0.9・無傷）を温存
            float rv = ReserveDeploymentRules.ReserveValue(0.9f, 1f);
            Assert.AreEqual(0.9f, rv, 1e-4f);

            // 早撃ち（戦況がまだ切迫していない 0.2）＝投入すべきでなく、無駄が大きい
            Assert.IsFalse(ReserveDeploymentRules.ShouldCommitReserve(0.2f, rv)); // timing 0.18 < 0.5
            float earlyWaste = ReserveDeploymentRules.PrematureCommitWaste(ReserveDeploymentRules.CommitTiming(0.2f, rv));
            Assert.Greater(earlyWaste, 0.4f);

            // 決定的瞬間（切迫 0.8）＝切り札を切るべき＝勝敗を決める
            Assert.IsTrue(ReserveDeploymentRules.ShouldCommitReserve(0.8f, rv)); // timing 0.72 ≥ 0.5

            // ただし全予備を使い切れば後がない（次の好機/危機に対応不能）＝枯渇リスク最大
            Assert.Greater(ReserveDeploymentRules.ReserveExhaustionRisk(1f),
                           ReserveDeploymentRules.ReserveExhaustionRisk(0.5f));
        }
    }
}
