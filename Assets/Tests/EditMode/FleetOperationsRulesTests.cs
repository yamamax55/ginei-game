using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>艦隊運用予算：予算算出・行動費用・合計（重複1回）・予算内判定・残額。</summary>
    public class FleetOperationsRulesTests
    {
        private static readonly FleetOpsParams P = FleetOpsParams.Default; // 予算=兵力／補給0.3・訓練0.4・整備0.25・哨戒0.35・休養0.1

        [Test]
        public void Budget_ScalesWithStrength()
        {
            Assert.AreEqual(10000, FleetOperationsRules.Budget(10000, P));
            Assert.AreEqual(0, FleetOperationsRules.Budget(-5, P)); // 非負
        }

        [Test]
        public void TaskCost_PerActionWeights()
        {
            Assert.AreEqual(3000, FleetOperationsRules.TaskCost(FleetTask.補給, 10000, P));
            Assert.AreEqual(4000, FleetOperationsRules.TaskCost(FleetTask.訓練, 10000, P));
            Assert.AreEqual(2500, FleetOperationsRules.TaskCost(FleetTask.整備, 10000, P));
            Assert.AreEqual(3500, FleetOperationsRules.TaskCost(FleetTask.哨戒, 10000, P));
            Assert.AreEqual(1000, FleetOperationsRules.TaskCost(FleetTask.休養, 10000, P));
        }

        [Test]
        public void TotalCost_SumsUniqueTasks()
        {
            var two = new List<FleetTask> { FleetTask.補給, FleetTask.訓練 };
            Assert.AreEqual(7000, FleetOperationsRules.TotalCost(two, 10000, P));
            // 重複は1回だけ。
            var dup = new List<FleetTask> { FleetTask.補給, FleetTask.補給 };
            Assert.AreEqual(3000, FleetOperationsRules.TotalCost(dup, 10000, P));
            Assert.AreEqual(0, FleetOperationsRules.TotalCost(null, 10000, P));
        }

        [Test]
        public void CanAfford_And_Remaining()
        {
            var ok = new List<FleetTask> { FleetTask.補給, FleetTask.訓練 }; // 7000 ≤ 10000
            Assert.IsTrue(FleetOperationsRules.CanAfford(ok, 10000, P));
            Assert.AreEqual(3000, FleetOperationsRules.Remaining(ok, 10000, P));

            // 全部（合計1.4×兵力＝14000）は予算10000を超過＝選べない・残は負。
            var all = new List<FleetTask> { FleetTask.補給, FleetTask.訓練, FleetTask.整備, FleetTask.哨戒, FleetTask.休養 };
            Assert.IsFalse(FleetOperationsRules.CanAfford(all, 10000, P));
            Assert.AreEqual(-4000, FleetOperationsRules.Remaining(all, 10000, P));
        }
    }
}
