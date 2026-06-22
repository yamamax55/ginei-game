using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>艦隊管理サマリ：過大指揮判定・空席/現役集計・平均兵力・null安全。</summary>
    public class FleetCommandSummaryRulesTests
    {
        [Test]
        public void IsOverCapacity_OnlyWhenStrengthExceedsPositiveCapacity()
        {
            Assert.IsTrue(FleetCommandSummaryRules.IsOverCapacity(15000, 12000));
            Assert.IsFalse(FleetCommandSummaryRules.IsOverCapacity(12000, 12000)); // 同値は可
            Assert.IsFalse(FleetCommandSummaryRules.IsOverCapacity(15000, 0));      // 規模0（空席）は超過扱いしない
        }

        [Test]
        public void Summarize_Empty_IsAllZero()
        {
            var s = FleetCommandSummaryRules.Summarize(null);
            Assert.AreEqual(0, s.totalFleets);
            Assert.AreEqual(0, s.activeFleets);
            Assert.AreEqual(0f, s.averageStrength, 1e-4f);

            var s2 = FleetCommandSummaryRules.Summarize(new List<FleetCommandEntry>());
            Assert.AreEqual(0, s2.totalFleets);
        }

        [Test]
        public void Summarize_CountsActiveStrengthVacantAndOverCapacity()
        {
            var entries = new List<FleetCommandEntry>
            {
                new FleetCommandEntry(10000, true,  true,  12000), // 適正
                new FleetCommandEntry(15000, true,  true,  12000), // 過大指揮
                new FleetCommandEntry( 8000, true,  false, 0),     // 空席
                new FleetCommandEntry( 5000, false, true,  12000), // 解隊＝集計対象外
            };
            var s = FleetCommandSummaryRules.Summarize(entries);
            Assert.AreEqual(4, s.totalFleets);
            Assert.AreEqual(3, s.activeFleets);
            Assert.AreEqual(33000, s.totalStrength);       // 10000+15000+8000（解隊は除外）
            Assert.AreEqual(1, s.vacantCommands);
            Assert.AreEqual(1, s.overCapacity);
            Assert.AreEqual(11000f, s.averageStrength, 1e-4f); // 33000/3
        }

        [Test]
        public void Summarize_VacantIsNotCountedAsOverCapacity()
        {
            var entries = new List<FleetCommandEntry>
            {
                new FleetCommandEntry(99999, true, false, 0), // 空席（規模0）＝過大指揮には数えない
            };
            var s = FleetCommandSummaryRules.Summarize(entries);
            Assert.AreEqual(1, s.vacantCommands);
            Assert.AreEqual(0, s.overCapacity);
        }
    }
}
