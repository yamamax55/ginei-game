using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>勢力プールのストア（FleetPool）＋造船供給（ShipyardRules.CommissionToPool）＋プール基準の配分の EditMode テスト（#148/#884）。</summary>
    public class FleetPoolTests
    {
        [SetUp]
        public void Reset() { FleetPool.Clear(); FleetRoster.Clear(); }

        // ───────── FleetPool ストア ─────────

        [Test]
        public void Get_DefaultsToZero_PerFaction()
        {
            Assert.AreEqual(0, FleetPool.Get(Faction.帝国));
            FleetPool.Set(Faction.帝国, 12000);
            Assert.AreEqual(12000, FleetPool.Get(Faction.帝国));
            Assert.AreEqual(0, FleetPool.Get(Faction.同盟)); // 勢力独立
        }

        [Test]
        public void Set_And_Add_ClampAtZero()
        {
            FleetPool.Set(Faction.帝国, -50);
            Assert.AreEqual(0, FleetPool.Get(Faction.帝国));
            FleetPool.Add(Faction.帝国, 500);
            Assert.AreEqual(500, FleetPool.Get(Faction.帝国));
            Assert.AreEqual(200, FleetPool.Add(Faction.帝国, -300)); // 500-300
            Assert.AreEqual(0, FleetPool.Add(Faction.帝国, -9999)); // 0下限
        }

        // ───────── 造船 → プール供給 ─────────

        [Test]
        public void CommissionToPool_AddsYield_WhenComplete()
        {
            var order = new BuildOrder(ShipClass.戦艦, ShipRole.戦闘艦, Faction.帝国,
                ShipyardRules.CostBattleship, ShipyardRules.YieldBattleship);
            order.progress = order.cost; // 完成
            int added = ShipyardRules.CommissionToPool(order);
            Assert.AreEqual(ShipyardRules.YieldBattleship, added);
            Assert.AreEqual(ShipyardRules.YieldBattleship, FleetPool.Get(Faction.帝国));
        }

        [Test]
        public void CommissionToPool_IncompleteOrNull_NoChange()
        {
            var order = new BuildOrder(ShipClass.戦艦, ShipRole.戦闘艦, Faction.帝国, 100f, 60);
            order.progress = 50f; // 未完成
            Assert.AreEqual(0, ShipyardRules.CommissionToPool(order));
            Assert.AreEqual(0, ShipyardRules.CommissionToPool(null));
            Assert.AreEqual(0, FleetPool.Get(Faction.帝国));
        }

        [Test]
        public void Shipyard_BuildToCompletion_ThenCommissionToPool()
        {
            // 船渠で建造→完成→プールへ就役（造船で総艦艇が増える、の一連）
            var yard = new Shipyard(1, Faction.帝国, 1, 1000f);
            ShipyardRules.Enqueue(yard, ShipClass.駆逐艦, ShipRole.戦闘艦);
            var done = ShipyardRules.Tick(yard, 1f, 1f); // buildPower 1000×dt1 ≥ コスト35 ＝完成
            Assert.AreEqual(1, done.Count);
            ShipyardRules.CommissionToPool(done[0]);
            Assert.AreEqual(ShipyardRules.YieldDestroyer, FleetPool.Get(Faction.帝国));
        }

        // ───────── プール基準の配分（FleetPoolRules ストア版） ─────────

        [Test]
        public void StoredOverloads_AvailableAndAdjust_UseFleetPool()
        {
            FleetPool.Set(Faction.帝国, 10000);
            var f = FleetRoster.CreateFleet(Faction.帝国); f.baseStrength = 3000;
            Assert.AreEqual(7000, FleetPoolRules.Available(Faction.帝国));
            // プール基準で増減
            Assert.IsTrue(FleetPoolRules.Adjust(f, 2000));
            Assert.AreEqual(5000, f.baseStrength);
            Assert.AreEqual(5000, FleetPoolRules.Available(Faction.帝国));
            // プール超過は拒否
            Assert.IsFalse(FleetPoolRules.SetAllocation(f, 11000));
            Assert.AreEqual(5000, f.baseStrength);
        }

        [Test]
        public void Building_IncreasesPool_AllowsMoreAllocation()
        {
            FleetPool.Set(Faction.帝国, 5000);
            var f = FleetRoster.CreateFleet(Faction.帝国); f.baseStrength = 5000; // 使い切り
            Assert.AreEqual(0, FleetPoolRules.Available(Faction.帝国));
            Assert.IsFalse(FleetPoolRules.Adjust(f, 500)); // 残0で増やせない

            // 造船で +60 → 配分可能になる
            var order = new BuildOrder(ShipClass.戦艦, ShipRole.戦闘艦, Faction.帝国, 100f, 60);
            order.progress = order.cost;
            ShipyardRules.CommissionToPool(order);
            Assert.AreEqual(60, FleetPoolRules.Available(Faction.帝国));
            Assert.IsTrue(FleetPoolRules.Adjust(f, 60));
            Assert.AreEqual(5060, f.baseStrength);
        }
    }
}
