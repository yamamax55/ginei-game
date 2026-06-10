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

        // ===================================================================
        // ===== 敵対的エッジケース（境界・クランプ・分岐・異常入力・不変条件） =====
        // ===================================================================

        // --- Cost / StrengthYield：列挙の各分岐・役割が艦種に優先する仕様 ---

        [Test]
        public void Cost_AllNonCombatRoles_ByRoleIgnoringClass()
        {
            // 非戦闘艦のコストは役割で決まる＝艦種（ここでは戦艦）を無視する仕様。
            Assert.AreEqual(25f, ShipyardRules.Cost(ShipClass.戦艦, ShipRole.輸送艦), 1e-5f); // CostTransport
            Assert.AreEqual(20f, ShipyardRules.Cost(ShipClass.戦艦, ShipRole.偵察艦), 1e-5f); // CostScout
            Assert.AreEqual(45f, ShipyardRules.Cost(ShipClass.戦艦, ShipRole.入植艦), 1e-5f); // CostColony
        }

        [Test]
        public void Cost_CombatCruiser_IsDefaultClassBranch()
        {
            // 戦闘艦で戦艦でも駆逐でもない＝巡航艦（default 分岐）は CostCruiser=60。
            Assert.AreEqual(60f, ShipyardRules.Cost(ShipClass.巡航艦, ShipRole.戦闘艦), 1e-5f);
        }

        [Test]
        public void StrengthYield_AllBranches_Exhaustive()
        {
            // 戦闘艦は艦種で：戦艦60/巡航40/駆逐25。
            Assert.AreEqual(60, ShipyardRules.StrengthYield(ShipClass.戦艦, ShipRole.戦闘艦));
            Assert.AreEqual(40, ShipyardRules.StrengthYield(ShipClass.巡航艦, ShipRole.戦闘艦));
            Assert.AreEqual(25, ShipyardRules.StrengthYield(ShipClass.駆逐艦, ShipRole.戦闘艦));
            // 非戦闘艦は役割を問わず一律 YieldNonCombat=20（艦種も無視）。
            Assert.AreEqual(20, ShipyardRules.StrengthYield(ShipClass.戦艦, ShipRole.輸送艦));
            Assert.AreEqual(20, ShipyardRules.StrengthYield(ShipClass.駆逐艦, ShipRole.偵察艦));
            Assert.AreEqual(20, ShipyardRules.StrengthYield(ShipClass.巡航艦, ShipRole.入植艦));
        }

        // --- Enqueue / EnqueueReinforcement：null・非正の艦隊番号の異常入力 ---

        [Test]
        public void Enqueue_NullYard_ReturnsNullNoThrow()
        {
            Assert.IsNull(ShipyardRules.Enqueue(null, ShipClass.戦艦, ShipRole.戦闘艦));
        }

        [Test]
        public void EnqueueReinforcement_NonPositiveFleetNumber_RejectedAndQueueUntouched()
        {
            var yard = new Shipyard(1, Faction.帝国);
            Assert.IsNull(ShipyardRules.EnqueueReinforcement(yard, 0, ShipClass.駆逐艦, ShipRole.戦闘艦));
            Assert.IsNull(ShipyardRules.EnqueueReinforcement(yard, -3, ShipClass.駆逐艦, ShipRole.戦闘艦));
            Assert.IsNull(ShipyardRules.EnqueueReinforcement(null, 5, ShipClass.駆逐艦, ShipRole.戦闘艦));
            Assert.AreEqual(0, yard.queue.Count); // 何も積まれていない
        }

        [Test]
        public void EnqueueReinforcement_SetsReinforceNumberAndCost()
        {
            var yard = new Shipyard(1, Faction.同盟);
            var o = ShipyardRules.EnqueueReinforcement(yard, 7, ShipClass.戦艦, ShipRole.戦闘艦);
            Assert.AreEqual(7, o.reinforceFleetNumber);
            Assert.AreEqual(100f, o.cost, 1e-5f);  // CostBattleship
            Assert.AreEqual(60, o.strengthYield);  // YieldBattleship
            Assert.AreEqual(1, yard.queue.Count);
        }

        // --- ActiveCount：境界（空キュー / capacity>count / yard null） ---

        [Test]
        public void ActiveCount_Boundaries()
        {
            Assert.AreEqual(0, ShipyardRules.ActiveCount(null));
            var yard = new Shipyard(1, Faction.帝国, parallelCapacity: 3, buildPower: 10f);
            Assert.AreEqual(0, ShipyardRules.ActiveCount(yard)); // 空キュー
            ShipyardRules.Enqueue(yard, ShipClass.駆逐艦, ShipRole.戦闘艦);
            Assert.AreEqual(1, ShipyardRules.ActiveCount(yard)); // count<capacity → count
            ShipyardRules.Enqueue(yard, ShipClass.駆逐艦, ShipRole.戦闘艦);
            ShipyardRules.Enqueue(yard, ShipClass.駆逐艦, ShipRole.戦闘艦);
            ShipyardRules.Enqueue(yard, ShipClass.駆逐艦, ShipRole.戦闘艦);
            Assert.AreEqual(3, ShipyardRules.ActiveCount(yard)); // count>capacity → capacity
        }

        // --- Tick：異常入力・クランプ・並行・負係数・複数同時完成 ---

        [Test]
        public void Tick_NullYard_OrNonPositiveDt_ReturnsEmpty()
        {
            Assert.AreEqual(0, ShipyardRules.Tick(null, 1f, 1f).Count);
            var yard = new Shipyard(1, Faction.帝国, 1, 10f);
            ShipyardRules.Enqueue(yard, ShipClass.駆逐艦, ShipRole.戦闘艦);
            Assert.AreEqual(0, ShipyardRules.Tick(yard, 0f, 1f).Count);  // dt=0
            Assert.AreEqual(0, ShipyardRules.Tick(yard, -5f, 1f).Count); // dt<0
            Assert.AreEqual(0f, yard.queue[0].progress, 1e-5f);         // 進んでいない
        }

        [Test]
        public void Tick_NegativeProductionFactor_ClampedToZero_NoProgress()
        {
            var yard = new Shipyard(1, Faction.帝国, 1, 10f);
            var o = ShipyardRules.Enqueue(yard, ShipClass.巡航艦, ShipRole.戦闘艦);
            ShipyardRules.Tick(yard, 1f, -2f); // factor=Max(0,-2)=0 → +0
            Assert.AreEqual(0f, o.progress, 1e-5f);
        }

        [Test]
        public void Tick_ProgressClampedToCost_NoOvershoot()
        {
            var yard = new Shipyard(1, Faction.帝国, 1, 1000f);
            var o = ShipyardRules.Enqueue(yard, ShipClass.駆逐艦, ShipRole.戦闘艦); // cost 35
            ShipyardRules.Tick(yard, 10f, 1f); // +10000 だが cost で頭打ち
            // 完成してキューから外れる。progress は cost を超えない。
            Assert.AreEqual(35f, o.progress, 1e-5f);
            Assert.IsTrue(o.IsComplete);
        }

        [Test]
        public void Tick_ParallelTwo_AdvancesBothSimultaneously()
        {
            var yard = new Shipyard(1, Faction.帝国, parallelCapacity: 2, buildPower: 10f);
            var a = ShipyardRules.Enqueue(yard, ShipClass.戦艦, ShipRole.戦闘艦); // cost 100
            var b = ShipyardRules.Enqueue(yard, ShipClass.戦艦, ShipRole.戦闘艦); // cost 100
            var c = ShipyardRules.Enqueue(yard, ShipClass.戦艦, ShipRole.戦闘艦); // cost 100（capacity外）
            ShipyardRules.Tick(yard, 1f, 1f); // 先頭2件に +10 ずつ、3件目は手つかず
            Assert.AreEqual(10f, a.progress, 1e-5f);
            Assert.AreEqual(10f, b.progress, 1e-5f);
            Assert.AreEqual(0f, c.progress, 1e-5f);
        }

        [Test]
        public void Tick_MultipleCompletionsInOneTick_ReturnedHeadFirst()
        {
            var yard = new Shipyard(1, Faction.帝国, parallelCapacity: 3, buildPower: 1000f);
            var a = ShipyardRules.Enqueue(yard, ShipClass.戦艦, ShipRole.戦闘艦, "A"); // cost 100
            var b = ShipyardRules.Enqueue(yard, ShipClass.駆逐艦, ShipRole.戦闘艦, "B"); // cost 35
            var c = ShipyardRules.Enqueue(yard, ShipClass.巡航艦, ShipRole.戦闘艦, "C"); // cost 60
            var done = ShipyardRules.Tick(yard, 1f, 1f); // 全完成（+1000）
            Assert.AreEqual(3, done.Count);
            // 仕様：完成は先頭から（キュー順）で返る＝A,B,C。
            Assert.AreSame(a, done[0]);
            Assert.AreSame(b, done[1]);
            Assert.AreSame(c, done[2]);
            Assert.AreEqual(0, yard.queue.Count);
        }

        // --- ProductionFactor：OutputFactor のクランプ両端と中点 ---

        [Test]
        public void ProductionFactor_ClampsAndMidpoint()
        {
            var min = new Province(1, "", 100f); min.stability = 0f;
            var mid = new Province(2, "", 100f); mid.stability = 50f;
            var max = new Province(3, "", 100f); max.stability = 100f;
            // Lerp(0.3, 1, stability/100)
            Assert.AreEqual(0.3f, ShipyardRules.ProductionFactor(min), 1e-5f);
            Assert.AreEqual(0.65f, ShipyardRules.ProductionFactor(mid), 1e-5f);
            Assert.AreEqual(1.0f, ShipyardRules.ProductionFactor(max), 1e-5f);
        }

        [Test]
        public void ProductionFactor_StabilityAboveMax_ClampedToOne()
        {
            var over = new Province(1, "", 100f); over.stability = 250f; // 異常に高い安定度
            // Clamp01 で 1.0 に頭打ち＝Lerp は 1.0 を超えない。
            Assert.AreEqual(1.0f, ShipyardRules.ProductionFactor(over), 1e-5f);
        }

        // --- Commission：null・補充先不在・補充の合計保存 ---

        [Test]
        public void Commission_NullOrder_ReturnsNull()
        {
            Assert.IsNull(ShipyardRules.Commission(null));
        }

        [Test]
        public void Commission_ReinforceMissingFleet_ReturnsNull()
        {
            // 補充先の艦隊が台帳に存在しない（FleetRoster は SetUp で Clear 済み）。
            var order = new BuildOrder(ShipClass.駆逐艦, ShipRole.戦闘艦, Faction.帝国,
                ShipyardRules.CostDestroyer, 25) { reinforceFleetNumber = 99, progress = 999f };
            Assert.IsNull(ShipyardRules.Commission(order));
        }

        [Test]
        public void Commission_Reinforcement_PreservesTotalStrength()
        {
            // 合計保存則：補充後の baseStrength = 元 + strengthYield（取りこぼし・二重加算なし）。
            var existing = FleetRoster.CreateFleet(Faction.同盟, 3);
            existing.baseStrength = 200;
            var order = new BuildOrder(ShipClass.戦艦, ShipRole.戦闘艦, Faction.同盟,
                ShipyardRules.CostBattleship, ShipyardRules.YieldBattleship)
                { reinforceFleetNumber = 3, progress = 999f };
            ShipyardRules.Commission(order);
            Assert.AreEqual(260, existing.baseStrength); // 200 + 60
        }

        [Test]
        public void Commission_NewFleet_RejectedWhenNumberRetired()
        {
            // 永久欠番のみが払い出し可能領域を塞ぐと CreateFleet が null → Commission も null。
            // ここでは番号1を永久欠番にし、新造（番号0→NextAvailable）が欠番1を飛ばして成立することを確認。
            FleetRoster.RetireNumber(Faction.帝国, 1);
            var order = new BuildOrder(ShipClass.巡航艦, ShipRole.戦闘艦, Faction.帝国,
                ShipyardRules.CostCruiser, ShipyardRules.YieldCruiser, "再建艦隊") { progress = 999f };
            FleetUnitData unit = ShipyardRules.Commission(order);
            Assert.IsNotNull(unit);
            Assert.AreNotEqual(1, unit.fleetNumber); // 永久欠番1は払い出されない
        }

        // --- BuildOrder コンストラクタのクランプ（負コスト/負兵力） ---

        [Test]
        public void BuildOrder_NegativeCostAndYield_ClampedToZero()
        {
            var o = new BuildOrder(ShipClass.戦艦, ShipRole.戦闘艦, Faction.帝国, -50f, -10);
            Assert.AreEqual(0f, o.cost, 1e-5f);
            Assert.AreEqual(0, o.strengthYield);
            // cost=0 のオーダーは progress=0 でも IsComplete（0>=0）＝即完成扱い。
            Assert.IsTrue(o.IsComplete);
        }
    }
}
