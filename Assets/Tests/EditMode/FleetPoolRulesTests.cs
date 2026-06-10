using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>勢力の艦隊プール配分（FleetPoolRules・#148 編成）の EditMode テスト。</summary>
    public class FleetPoolRulesTests
    {
        [SetUp]
        public void Reset() => FleetRoster.Clear();

        private static FleetUnitData Fleet(Faction f, int strength)
        {
            var u = FleetRoster.CreateFleet(f);
            u.baseStrength = strength;
            return u;
        }

        [Test]
        public void Allocated_SumsActiveFleetsBaseStrength()
        {
            Fleet(Faction.帝国, 3000);
            Fleet(Faction.帝国, 2000);
            Assert.AreEqual(5000, FleetPoolRules.Allocated(Faction.帝国));
            // 別勢力は独立
            Assert.AreEqual(0, FleetPoolRules.Allocated(Faction.同盟));
        }

        [Test]
        public void Allocated_IgnoresDisbandedAndZero()
        {
            var a = Fleet(Faction.帝国, 3000);
            Fleet(Faction.帝国, 0); // 0は数えない
            FleetRoster.Disband(Faction.帝国, a.fleetNumber); // 解隊は数えない
            Assert.AreEqual(0, FleetPoolRules.Allocated(Faction.帝国));
        }

        [Test]
        public void Available_IsPoolMinusAllocated_ClampedAtZero()
        {
            Fleet(Faction.帝国, 8000);
            Assert.AreEqual(2000, FleetPoolRules.Available(Faction.帝国, 10000));
            // 過剰割当でも負は返さない
            Assert.AreEqual(0, FleetPoolRules.Available(Faction.帝国, 5000));
        }

        [Test]
        public void CanAllocate_RespectsPoolAcrossOtherFleets()
        {
            Fleet(Faction.帝国, 6000);
            var b = Fleet(Faction.帝国, 1000);
            // b を 4000 にすると 6000+4000=10000 ≤ 10000 ＝可
            Assert.IsTrue(FleetPoolRules.CanAllocate(b, 4000, 10000));
            // b を 4001 にすると 10001 > 10000 ＝不可
            Assert.IsFalse(FleetPoolRules.CanAllocate(b, 4001, 10000));
            // 負は不可
            Assert.IsFalse(FleetPoolRules.CanAllocate(b, -1, 10000));
        }

        [Test]
        public void SetAllocation_AppliesWhenWithinPool_RejectsOverflow()
        {
            var a = Fleet(Faction.帝国, 1000);
            Assert.IsTrue(FleetPoolRules.SetAllocation(a, 9000, 10000));
            Assert.AreEqual(9000, a.baseStrength);
            // 超過は現状維持
            Assert.IsFalse(FleetPoolRules.SetAllocation(a, 11000, 10000));
            Assert.AreEqual(9000, a.baseStrength);
        }

        [Test]
        public void Adjust_IncrementsAndClampsAtZero()
        {
            var a = Fleet(Faction.帝国, 1000);
            Assert.IsTrue(FleetPoolRules.Adjust(a, 500, 10000));
            Assert.AreEqual(1500, a.baseStrength);
            // 0未満は0クランプ
            Assert.IsTrue(FleetPoolRules.Adjust(a, -9999, 10000));
            Assert.AreEqual(0, a.baseStrength);
            // プール超過の増分は拒否
            var b = Fleet(Faction.帝国, 9000);
            Assert.IsFalse(FleetPoolRules.Adjust(b, 2000, 10000));
            Assert.AreEqual(9000, b.baseStrength);
        }

        [Test]
        public void NullUnit_Safe()
        {
            Assert.IsFalse(FleetPoolRules.CanAllocate(null, 100, 10000));
            Assert.IsFalse(FleetPoolRules.SetAllocation(null, 100, 10000));
            Assert.IsFalse(FleetPoolRules.Adjust(null, 100, 10000));
        }
    }
}
