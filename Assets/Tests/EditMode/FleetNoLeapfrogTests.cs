using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>飛び石移動の禁止＝必ず星系を占領してから移動する（StrategicFleet.WarpTo の経路打ち切り）の EditMode テスト。</summary>
    public class FleetNoLeapfrogTests
    {
        // 直線銀河 1-2-3-4（帝国1/帝国2/同盟3/同盟4）。
        private static GalaxyMap LinearMap()
        {
            var m = new GalaxyMap();
            m.systems.Add(new StarSystem(1, "S1", Vector2.zero, Faction.帝国));
            m.systems.Add(new StarSystem(2, "S2", Vector2.zero, Faction.帝国));
            m.systems.Add(new StarSystem(3, "S3", Vector2.zero, Faction.同盟));
            m.systems.Add(new StarSystem(4, "S4", Vector2.zero, Faction.同盟));
            m.corridors.Add(new Corridor(1, 2));
            m.corridors.Add(new Corridor(2, 3));
            m.corridors.Add(new Corridor(3, 4));
            return m;
        }

        [Test]
        public void WarpTo_StopsAtFirstEnemySystem_NoLeapfrog()
        {
            var m = LinearMap();
            var f = new StrategicFleet(1, 1, Faction.帝国); // 帝国艦隊 @1
            Assert.IsTrue(f.WarpTo(m, 4));            // 4(同盟)まで命令しても…
            Assert.AreEqual(2, f.destinationSystemId); // 自勢力2を通り
            Assert.AreEqual(3, f.FinalDestinationId);  // 最初の敵星系3で止まる（4へは飛ばない）
        }

        [Test]
        public void WarpTo_AdjacentEnemy_SingleHopNoRoute()
        {
            var m = LinearMap();
            var f = new StrategicFleet(2, 2, Faction.帝国); // 帝国艦隊 @2（3が隣接敵）
            Assert.IsTrue(f.WarpTo(m, 4));
            Assert.AreEqual(3, f.destinationSystemId); // 隣接敵3へ1ホップ
            Assert.IsFalse(f.HasRoute);                // 3を飛び越えない
            Assert.AreEqual(3, f.FinalDestinationId);
        }

        [Test]
        public void WarpTo_ThroughOwnedTerritory_AllowedToFirstEnemy()
        {
            var m = LinearMap();
            m.GetSystem(3).owner = Faction.帝国; // 1,2,3 帝国 / 4 同盟
            var f = new StrategicFleet(3, 1, Faction.帝国);
            Assert.IsTrue(f.WarpTo(m, 4));
            Assert.AreEqual(2, f.destinationSystemId); // 自勢力領2,3を通り抜け
            Assert.AreEqual(4, f.FinalDestinationId);  // 最初の敵星系4で止まる
        }

        [Test]
        public void WarpTo_AllOwned_ReachesGoal()
        {
            var m = LinearMap();
            m.GetSystem(3).owner = Faction.帝国;
            m.GetSystem(4).owner = Faction.帝国; // 全て帝国
            var f = new StrategicFleet(4, 1, Faction.帝国);
            Assert.IsTrue(f.WarpTo(m, 4));
            Assert.AreEqual(2, f.destinationSystemId);
            Assert.AreEqual(4, f.FinalDestinationId); // 自領内は目的地まで進める
        }

        [Test]
        public void WarpTo_AdjacentOwned_SingleHopNoRoute()
        {
            var m = LinearMap();
            var f = new StrategicFleet(5, 1, Faction.帝国);
            Assert.IsTrue(f.WarpTo(m, 2)); // 隣接の自領
            Assert.AreEqual(2, f.destinationSystemId);
            Assert.IsFalse(f.HasRoute);
        }
    }
}
