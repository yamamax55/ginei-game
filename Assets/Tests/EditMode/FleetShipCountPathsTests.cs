using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦艇数が<b>戦略艦隊を更新するすべての経路</b>で取り残されないことの回帰。
    ///
    /// レビュー指摘：要塞戦だけでなく、補給損耗・通常会戦・撤退・按分でも
    /// <c>strength</c> だけ減って艦艇数が置き去りになっていた。
    /// 実損（実際に艦を失う）は <see cref="FleetShipCountRules.ApplyPhysicalLoss"/> を通す、が唯一の約束。
    /// </summary>
    public class FleetShipCountPathsTests
    {
        private static StrategicFleet Fleet(int id, int strength, int ships)
        {
            var f = new StrategicFleet { id = id, faction = Faction.同盟, strength = strength, supply = 1f };
            f.SetShips(ships);
            return f;
        }

        // ===== 窓口そのもの =====

        [Test]
        public void ApplyPhysicalLoss_ReducesBothStrengthAndShips()
        {
            StrategicFleet f = Fleet(1, 200, 8000);
            FleetShipCountRules.ApplyPhysicalLoss(f, 100);

            Assert.AreEqual(100, f.strength);
            Assert.AreEqual(4000, f.Ships, "兵力が半分なら艦艇も半分");
        }

        [Test]
        public void ApplyPhysicalLoss_ZeroMeansAnnihilated()
        {
            StrategicFleet f = Fleet(1, 200, 8000);
            FleetShipCountRules.ApplyPhysicalLoss(f, 0);

            Assert.AreEqual(0, f.strength);
            Assert.AreEqual(0, f.Ships);
            Assert.IsTrue(f.shipCountSet, "0隻が未初期化と誤解されないこと");
        }

        [Test]
        public void ApplyPhysicalLoss_NoLoss_KeepsShips()
        {
            StrategicFleet f = Fleet(1, 200, 8000);
            FleetShipCountRules.ApplyPhysicalLoss(f, 200);
            Assert.AreEqual(8000, f.Ships);
        }

        [Test]
        public void ApplyPhysicalLoss_NullSafe()
        {
            Assert.DoesNotThrow(() => FleetShipCountRules.ApplyPhysicalLoss(null, 10));
        }

        // ===== 経路①：補給切れの損耗 =====

        [Test]
        public void SupplyAttrition_AlsoReducesShips()
        {
            StrategicFleet f = Fleet(1, 1000, 40000);
            f.supply = 0f;   // 完全に枯渇

            int before = f.Ships;
            int lost = MilitarySupplyTickRules.TickFleet(f, supplied: false);

            Assert.Greater(lost, 0, "前提：損耗が起きること");
            Assert.Less(f.strength, 1000);
            Assert.Less(f.Ships, before, "補給切れで艦を失ったのに艦艇数が減っていない");
        }

        [Test]
        public void SupplyRecovery_DoesNotTouchShips()
        {
            // 補給が回復するだけなら艦は増えも減りもしない（戦闘力と艦艇数の区別）。
            StrategicFleet f = Fleet(1, 1000, 40000);
            f.supply = 0.5f;

            MilitarySupplyTickRules.TickFleet(f, supplied: true);

            Assert.AreEqual(1000, f.strength);
            Assert.AreEqual(40000, f.Ships);
        }

        // ===== 経路②：通常会戦の按分（ScaleStrength 経由）=====

        [Test]
        public void EncounterAttrition_ScalesShipsPerFleet()
        {
            // 勝者2隊（100/300）が残存200になる＝50/150。艦艇も各隊の比率どおり。
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem { id = 0, owner = Faction.同盟 });
            map.AddSystem(new StarSystem { id = 1, owner = Faction.帝国 });
            map.AddCorridor(new Corridor(0, 1, 5f, CorridorType.通商));

            var reg = new StrategicFleetRegistry(map);
            StrategicFleet a = Fleet(1, 100, 4000);
            StrategicFleet b = Fleet(2, 300, 12000);
            reg.Add(a); reg.Add(b);

            var fleets = new List<StrategicFleet> { a, b };
            foreach (var f in fleets) FleetShipCountRules.ApplyPhysicalLoss(f, f.strength / 2);

            Assert.AreEqual(50, a.strength);
            Assert.AreEqual(2000, a.Ships);
            Assert.AreEqual(150, b.strength);
            Assert.AreEqual(6000, b.Ships);
        }

        // ===== 経路③：新規艦隊の初期化 =====

        [Test]
        public void InitializeShips_FixesCountAfterStrengthIsDecided()
        {
            var f = new StrategicFleet { id = 1, strength = 130 };
            Assert.IsFalse(f.shipCountSet);

            f.strength = 195;                       // 難易度補正で兵力が変わったあと…
            FleetShipCountRules.InitializeShips(f); // …ここで確定させる

            Assert.IsTrue(f.shipCountSet);
            Assert.AreEqual(FleetShipCountRules.FromStrength(195), f.Ships,
                            "補正前の兵力ではなく、確定した兵力から作られること");
        }

        [Test]
        public void RegistryAdd_InitializesUnsetFleets()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem { id = 0, owner = Faction.同盟 });
            var reg = new StrategicFleetRegistry(map);

            var f = new StrategicFleet { id = 7, strength = 250 };
            reg.Add(f);

            Assert.IsTrue(f.shipCountSet, "盤面に加わった時点で確定していること");
            Assert.AreEqual(FleetShipCountRules.FromStrength(250), f.Ships);
        }

        [Test]
        public void RegistryAdd_DoesNotOverwriteRestoredFleets()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem { id = 0, owner = Faction.同盟 });
            var reg = new StrategicFleetRegistry(map);

            StrategicFleet f = Fleet(8, 250, 123);   // セーブから復元した想定
            reg.Add(f);

            Assert.AreEqual(123, f.Ships, "確定済みの艦艇数を上書きしてはいけない");
        }

        [Test]
        public void RegistryAdd_KeepsAnnihilatedZero()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem { id = 0, owner = Faction.同盟 });
            var reg = new StrategicFleetRegistry(map);

            StrategicFleet f = Fleet(9, 10, 0);   // 0隻（確定済み）
            reg.Add(f);

            Assert.AreEqual(0, f.Ships, "0隻が盤面追加で復活してはいけない");
        }
    }
}
