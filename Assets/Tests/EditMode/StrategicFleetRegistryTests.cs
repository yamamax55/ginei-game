using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦略艦隊レジストリの一括 Tick と、多ホップワープ（経路追従）を固定する（C-1 #34）。
    /// </summary>
    public class StrategicFleetRegistryTests
    {
        // 0 —(1)— 2 —(1)— 1 、0 —(10)— 1。0→1 の最短は 0→2→1。
        private GalaxyMap MakeMap()
        {
            var m = new GalaxyMap();
            m.AddSystem(new StarSystem(0, "A", Vector2.zero));
            m.AddSystem(new StarSystem(1, "B", Vector2.right));
            m.AddSystem(new StarSystem(2, "C", Vector2.up));
            m.AddCorridor(new Corridor(0, 1, 10f));
            m.AddCorridor(new Corridor(0, 2, 1f));
            m.AddCorridor(new Corridor(2, 1, 1f));
            return m;
        }

        [Test]
        public void WarpTo_MultiHop_FollowsShortestRoute()
        {
            var m = MakeMap();
            var f = new StrategicFleet(7, 0) { warpSpeed = 1f };
            Assert.IsTrue(f.WarpTo(m, 1));       // 経路 0→2→1
            Assert.AreEqual(1, f.FinalDestinationId);
            Assert.IsTrue(f.HasRoute);           // 残り経路 [1]

            var reg = new StrategicFleetRegistry(m);
            reg.Add(f);

            int final1 = reg.Tick(1f);           // 0→2 到着→自動で 2→1 へ継続
            Assert.AreEqual(0, final1);          // まだ最終ではない
            Assert.AreEqual(2, f.currentSystemId);
            Assert.IsTrue(f.IsMoving);
            Assert.IsFalse(f.HasRoute);

            int final2 = reg.Tick(1f);           // 2→1 到着＝最終
            Assert.AreEqual(1, final2);
            Assert.AreEqual(1, f.currentSystemId);
            Assert.IsFalse(f.IsMoving);
        }

        [Test]
        public void WarpTo_Unreachable_Fails()
        {
            var m = MakeMap();
            var iso = new StarSystem(9, "Z", Vector2.zero);
            m.AddSystem(iso); // 孤立星系（回廊なし）
            var f = new StrategicFleet(1, 0);
            Assert.IsFalse(f.WarpTo(m, 9));
            Assert.IsFalse(f.IsMoving);
        }

        [Test]
        public void Tick_AdvancesMultipleFleets()
        {
            var m = MakeMap();
            var f1 = new StrategicFleet(1, 0) { warpSpeed = 1f };
            var f2 = new StrategicFleet(2, 2) { warpSpeed = 1f };
            f1.BeginWarp(m, 2);  // 0→2（len1）
            f2.BeginWarp(m, 1);  // 2→1（len1）

            var reg = new StrategicFleetRegistry(m);
            reg.Add(f1);
            reg.Add(f2);

            int arrived = reg.Tick(1f);
            Assert.AreEqual(2, arrived);
            Assert.AreEqual(2, f1.currentSystemId);
            Assert.AreEqual(1, f2.currentSystemId);
        }

        [Test]
        public void FleetsAt_ReturnsOnlyStationed()
        {
            var m = MakeMap();
            var stay = new StrategicFleet(1, 0);
            var moving = new StrategicFleet(2, 0) { warpSpeed = 1f };
            moving.BeginWarp(m, 2);

            var reg = new StrategicFleetRegistry(m);
            reg.Add(stay);
            reg.Add(moving);

            var at0 = reg.FleetsAt(0);
            Assert.AreEqual(1, at0.Count);
            Assert.AreSame(stay, at0[0]); // 移動中の moving は含まれない
        }

        [Test]
        public void WarpTo_WhileMoving_RedirectsFromNextSystem()
        {
            // 0—(1)—1—(1)—2 の直線。0→1 移動中に 2 へ再指示すると、1 到着後に 1→2 へ継続する。
            var m = new GalaxyMap();
            m.AddSystem(new StarSystem(0, "A", Vector2.zero));
            m.AddSystem(new StarSystem(1, "B", Vector2.right));
            m.AddSystem(new StarSystem(2, "C", Vector2.up));
            m.AddCorridor(new Corridor(0, 1, 1f));
            m.AddCorridor(new Corridor(1, 2, 1f));

            var f = new StrategicFleet(1, 0) { warpSpeed = 1f };
            f.BeginWarp(m, 1);                  // 0→1 移動中
            Assert.IsTrue(f.WarpTo(m, 2));      // 移動中でも受理
            Assert.AreEqual(2, f.FinalDestinationId);

            var reg = new StrategicFleetRegistry(m);
            reg.Add(f);
            reg.Tick(1f);                       // 1到着→自動で 1→2 へ
            Assert.AreEqual(1, f.currentSystemId);
            Assert.IsTrue(f.IsMoving);
            reg.Tick(1f);                       // 2到着
            Assert.AreEqual(2, f.currentSystemId);
            Assert.IsFalse(f.IsMoving);
        }

        [Test]
        public void HoldOnCorridor_StopsAtFraction_StaysOnCorridor()
        {
            var m = new GalaxyMap();
            m.AddSystem(new StarSystem(0, "A", Vector2.zero));
            m.AddSystem(new StarSystem(1, "B", Vector2.right));
            m.AddCorridor(new Corridor(0, 1, 4f));
            var f = new StrategicFleet(1, 0, Faction.帝国) { warpSpeed = 1f };

            Assert.IsTrue(f.HoldOnCorridor(m, 1, 0.5f)); // 0→1 の中間で停止保持
            Assert.IsTrue(f.IsOnCorridor);

            var reg = new StrategicFleetRegistry(m);
            reg.Add(f);
            reg.Tick(10f);                            // いくら進めても保持位置(0.5)で止まる

            Assert.IsTrue(f.IsHolding);
            Assert.IsFalse(f.IsMoving);
            Assert.IsTrue(f.IsOnCorridor);
            Assert.AreEqual(0.5f, f.Progress, 1e-3f);
            Assert.AreEqual(0, f.currentSystemId);       // まだ回廊上（出発元のまま）
            Assert.AreEqual(0, reg.FleetsAt(0).Count);   // 回廊上の艦は星系在席に数えない
        }

        [Test]
        public void GetFleet_ById()
        {
            var reg = new StrategicFleetRegistry(MakeMap());
            var f = new StrategicFleet(42, 0);
            reg.Add(f);
            Assert.AreSame(f, reg.GetFleet(42));
            Assert.IsNull(reg.GetFleet(99));
        }
    }
}
