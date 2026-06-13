using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>艦隊自動編成（#146/#147/#148 拡張）：推奨数/配分/司令割付/一括編成。</summary>
    public class FleetAutoOrganizeTests
    {
        [Test]
        public void RecommendAndAllocate()
        {
            Assert.AreEqual(4, FleetAutoOrganizeRules.RecommendFleetCount(48000, 12000)); // ちょうど4
            Assert.AreEqual(4, FleetAutoOrganizeRules.RecommendFleetCount(1000, 300));    // ceil(3.33)
            Assert.AreEqual(0, FleetAutoOrganizeRules.RecommendFleetCount(0, 300));
            Assert.AreEqual(1, FleetAutoOrganizeRules.RecommendFleetCount(500, 0));

            var even = FleetAutoOrganizeRules.AllocateStrength(48000, 4);
            CollectionAssert.AreEqual(new[] { 12000, 12000, 12000, 12000 }, even);
            var rem = FleetAutoOrganizeRules.AllocateStrength(1003, 4); // 余り3を先頭へ
            CollectionAssert.AreEqual(new[] { 251, 251, 251, 250 }, rem);
        }

        [Test]
        public void AssignCommanders_HighRankToLargeFleet_GatedByCapacity()
        {
            // 兵力12000の艦隊×4、司令は元帥/大将/中将/准将。准将(cap3000)は12000を率いられない。
            var strengths = new[] { 12000, 12000, 12000, 12000 };
            var commanders = new List<CommanderSlot>
            {
                new CommanderSlot(1, 10), // 元帥 cap60000
                new CommanderSlot(2, 8),  // 大将 cap15000
                new CommanderSlot(3, 7),  // 中将 cap12000（ちょうど可）
                new CommanderSlot(4, 5),  // 准将 cap3000（不可）
            };
            var cmd = FleetAutoOrganizeRules.AssignCommanders(strengths, commanders);
            // 上位から順に配属、准将は12000を指揮できず空席。
            Assert.AreEqual(1, cmd[0]);
            Assert.AreEqual(2, cmd[1]);
            Assert.AreEqual(3, cmd[2]);
            Assert.AreEqual(-1, cmd[3]); // 准将は過大兵力で配属されない
        }

        [Test]
        public void AutoOrganize_FullPlan()
        {
            var commanders = new List<CommanderSlot>
            {
                new CommanderSlot(1, 10),
                new CommanderSlot(2, 8),
                new CommanderSlot(3, 7),
                new CommanderSlot(4, 5),
            };
            var plans = FleetAutoOrganizeRules.AutoOrganize(48000, 12000, commanders);
            Assert.AreEqual(4, plans.Count);
            Assert.AreEqual(12000, plans[0].strength);
            Assert.AreEqual(1, plans[0].commanderId);
            Assert.AreEqual(2, plans[1].commanderId);
            Assert.AreEqual(3, plans[2].commanderId);
            Assert.AreEqual(-1, plans[3].commanderId);

            // プール0は空編成
            Assert.AreEqual(0, FleetAutoOrganizeRules.AutoOrganize(0, 12000, commanders).Count);
        }
    }
}
