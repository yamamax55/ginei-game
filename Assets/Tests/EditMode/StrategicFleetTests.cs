using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦略マップ 艦隊の時間制ワープ移動（C-1 #34）を固定する。
    /// 回廊以外へは行けない／ワープに時間がかかる／到着で星系が切り替わる。
    /// </summary>
    public class StrategicFleetTests
    {
        // 0 —(4)— 1。2 は 0 と非接続。
        private GalaxyMap MakeMap()
        {
            var m = new GalaxyMap();
            m.AddSystem(new StarSystem(0, "A", Vector2.zero));
            m.AddSystem(new StarSystem(1, "B", Vector2.right));
            m.AddSystem(new StarSystem(2, "C", Vector2.up));
            m.AddCorridor(new Corridor(0, 1, 4f));
            return m;
        }

        [Test]
        public void BeginWarp_ToConnected_StartsMoving_WithEta()
        {
            var m = MakeMap();
            var f = new StrategicFleet(0, 0) { warpSpeed = 2f };
            Assert.IsTrue(f.BeginWarp(m, 1));
            Assert.IsTrue(f.IsMoving);
            Assert.AreEqual(2f, f.Eta, 1e-4f); // length4 / speed2 = 2s
        }

        [Test]
        public void BeginWarp_ToUnconnected_Fails()
        {
            var m = MakeMap();
            var f = new StrategicFleet(0, 0);
            Assert.IsFalse(f.BeginWarp(m, 2)); // 0-2 に回廊なし＝移動不可
            Assert.IsFalse(f.IsMoving);
        }

        [Test]
        public void BeginWarp_SameSystem_Fails()
        {
            var m = MakeMap();
            var f = new StrategicFleet(0, 0);
            Assert.IsFalse(f.BeginWarp(m, 0));
        }

        [Test]
        public void Tick_AdvancesProgress_NoArrivalMidway()
        {
            var m = MakeMap();
            var f = new StrategicFleet(0, 0) { warpSpeed = 2f };
            f.BeginWarp(m, 1);             // length4, speed2 → 2s
            bool arrived = f.Tick(1f);      // 進行 2/4
            Assert.IsFalse(arrived);
            Assert.IsTrue(f.IsMoving);
            Assert.AreEqual(0.5f, f.Progress, 1e-4f);
            Assert.AreEqual(0, f.currentSystemId); // まだ出発元
        }

        [Test]
        public void Tick_ReachesDestination_SwitchesSystem()
        {
            var m = MakeMap();
            var f = new StrategicFleet(0, 0) { warpSpeed = 2f };
            f.BeginWarp(m, 1);
            f.Tick(1f);
            bool arrived = f.Tick(1.5f);    // 合計2.5s ≥ 2s
            Assert.IsTrue(arrived);
            Assert.IsFalse(f.IsMoving);
            Assert.AreEqual(1, f.currentSystemId);
            Assert.AreEqual(1f, f.Progress, 1e-4f);
        }

        [Test]
        public void Tick_WhenStationary_NoOp()
        {
            var f = new StrategicFleet(0, 0);
            Assert.IsFalse(f.Tick(1f));
        }

        [Test]
        public void CannotBeginWarp_WhileMoving()
        {
            var m = MakeMap();
            var f = new StrategicFleet(0, 0) { warpSpeed = 1f };
            f.BeginWarp(m, 1);
            Assert.IsFalse(f.BeginWarp(m, 1)); // 既に移動中は不可
        }
    }
}
