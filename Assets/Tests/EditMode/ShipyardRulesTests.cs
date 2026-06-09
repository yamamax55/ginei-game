using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 造船・建艦（#884 BUILD-1〜4）を固定する：艦種×役割のコスト表、建造の進捗と並行建造、生産力係数、
    /// 完成オーダーの就役（新造艦隊登録／既存艦隊の損耗補充）。FleetRoster は static のため Clear する。
    /// </summary>
    public class ShipyardRulesTests
    {
        [SetUp]
        public void Reset() => FleetRoster.Clear();

        // ===== BUILD-3 コスト表 =====

        [Test]
        public void Cost_CombatByClass_NonCombatByRole()
        {
            Assert.AreEqual(ShipyardRules.CostBattleship, ShipyardRules.Cost(ShipClass.戦艦, ShipRole.戦闘艦), 1e-4f);
            Assert.AreEqual(ShipyardRules.CostDestroyer, ShipyardRules.Cost(ShipClass.駆逐艦, ShipRole.戦闘艦), 1e-4f);
            Assert.AreEqual(ShipyardRules.CostTransport, ShipyardRules.Cost(ShipClass.巡航艦, ShipRole.輸送艦), 1e-4f);
            // 戦艦 ＞ 巡航 ＞ 駆逐（コスト差で作り分けに意味）
            Assert.Greater(ShipyardRules.Cost(ShipClass.戦艦, ShipRole.戦闘艦), ShipyardRules.Cost(ShipClass.駆逐艦, ShipRole.戦闘艦));
        }

        // ===== BUILD-1 キュー・進捗 =====

        [Test]
        public void Enqueue_AddsOrderWithCostFromTable()
        {
            var yard = new Shipyard(1, Faction.帝国);
            var order = ShipyardRules.Enqueue(yard, ShipClass.戦艦, ShipRole.戦闘艦, "第1艦隊");
            Assert.AreEqual(1, yard.queue.Count);
            Assert.AreEqual(ShipyardRules.CostBattleship, order.cost, 1e-4f);
            Assert.AreEqual(Faction.帝国, order.faction);
        }

        [Test]
        public void Tick_AdvancesAndCompletes()
        {
            var yard = new Shipyard(1, Faction.帝国, parallelCapacity: 1, buildPower: 10f);
            ShipyardRules.Enqueue(yard, ShipClass.駆逐艦, ShipRole.戦闘艦); // cost 35

            var done1 = ShipyardRules.Tick(yard, dt: 1f, productionFactor: 1f); // +10 → 10
            Assert.AreEqual(0, done1.Count);
            ShipyardRules.Tick(yard, 2f, 1f); // +20 → 30
            var done2 = ShipyardRules.Tick(yard, 1f, 1f); // +10 → 40 ≥ 35 完成
            Assert.AreEqual(1, done2.Count);
            Assert.AreEqual(0, yard.queue.Count); // キューから外れる
        }

        [Test]
        public void Tick_ParallelCapacity_LimitsConcurrentBuilds()
        {
            var yard = new Shipyard(1, Faction.帝国, parallelCapacity: 1, buildPower: 100f);
            var a = ShipyardRules.Enqueue(yard, ShipClass.駆逐艦, ShipRole.戦闘艦); // cost 35
            var b = ShipyardRules.Enqueue(yard, ShipClass.駆逐艦, ShipRole.戦闘艦);

            ShipyardRules.Tick(yard, 1f, 1f); // 先頭1件だけ進む（+100 → a 完成）
            // a は完成して外れ、b は未着手のまま
            Assert.IsTrue(a.IsComplete);
            Assert.AreEqual(0f, b.progress, 1e-4f);
        }

        // ===== BUILD-2 生産力連動 =====

        [Test]
        public void ProductionFactor_FromProvinceStability()
        {
            var low = new Province(1, "", 100f); low.stability = 0f;
            var high = new Province(2, "", 100f); high.stability = 100f;
            Assert.Less(ShipyardRules.ProductionFactor(low), ShipyardRules.ProductionFactor(high));
            Assert.AreEqual(1f, ShipyardRules.ProductionFactor(null), 1e-4f); // 無しは1.0
        }

        [Test]
        public void Tick_SlowerUnderLowProduction()
        {
            var yard = new Shipyard(1, Faction.帝国, 1, 10f);
            var o = ShipyardRules.Enqueue(yard, ShipClass.巡航艦, ShipRole.戦闘艦);
            ShipyardRules.Tick(yard, 1f, 0.5f); // 生産力半分 → +5
            Assert.AreEqual(5f, o.progress, 1e-4f);
        }

        // ===== BUILD-4 就役（FleetRoster 連携） =====

        [Test]
        public void Commission_NewFleet_RegistersWithRoleAndStrength()
        {
            var order = new BuildOrder(ShipClass.戦艦, ShipRole.戦闘艦, Faction.帝国,
                ShipyardRules.CostBattleship, ShipyardRules.YieldBattleship, "新鋭艦隊") { progress = 999f };

            FleetUnitData unit = ShipyardRules.Commission(order);
            Assert.IsNotNull(unit);
            Assert.AreEqual(ShipRole.戦闘艦, unit.shipRole);
            Assert.AreEqual(ShipyardRules.YieldBattleship, unit.baseStrength);
            Assert.AreSame(unit, FleetRoster.GetFleet(Faction.帝国, unit.fleetNumber)); // 台帳に登録
        }

        [Test]
        public void Commission_Reinforcement_AddsStrengthToExistingFleet()
        {
            var existing = FleetRoster.CreateFleet(Faction.同盟, 5);
            existing.baseStrength = 100;

            var order = new BuildOrder(ShipClass.駆逐艦, ShipRole.戦闘艦, Faction.同盟,
                ShipyardRules.CostDestroyer, 25) { reinforceFleetNumber = 5, progress = 999f };

            FleetUnitData unit = ShipyardRules.Commission(order);
            Assert.AreSame(existing, unit);
            Assert.AreEqual(125, existing.baseStrength); // 損耗補充で +25
        }

        [Test]
        public void Commission_RejectsIncompleteOrder()
        {
            var order = new BuildOrder(ShipClass.巡航艦, ShipRole.戦闘艦, Faction.帝国, 60f, 40);
            order.progress = 10f; // 未完成
            Assert.IsNull(ShipyardRules.Commission(order));
        }

        [Test]
        public void EndToEnd_BuildThenCommission()
        {
            var yard = new Shipyard(1, Faction.帝国, 1, 50f);
            ShipyardRules.Enqueue(yard, ShipClass.巡航艦, ShipRole.戦闘艦, "量産巡航艦隊"); // cost 60

            List<BuildOrder> done = ShipyardRules.Tick(yard, 2f, 1f); // +100 → 完成
            Assert.AreEqual(1, done.Count);
            FleetUnitData unit = ShipyardRules.Commission(done[0]);
            Assert.IsNotNull(unit);
            Assert.AreEqual("量産巡航艦隊", unit.fleetName);
        }
    }
}
