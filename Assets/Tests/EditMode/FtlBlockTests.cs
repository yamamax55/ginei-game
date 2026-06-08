using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 前線回廊（自勢力↔敵対勢力をつなぐ回廊）の FTL 不可制約を固定する（C-1 #34）。
    /// 前線はワープで通り抜けられず、経路探索は回避し、敵地は前線越しでしか行けないなら不可。
    /// </summary>
    public class FtlBlockTests
    {
        // 0(帝国) —(5)— 3(帝国) 友軍長経路。0 —(1)— 2(同盟) —(1)— 3 短いが前線2本。
        private GalaxyMap MakeMap()
        {
            var m = new GalaxyMap();
            m.AddSystem(new StarSystem(0, "A", Vector2.zero, Faction.帝国));
            m.AddSystem(new StarSystem(2, "B", Vector2.right, Faction.同盟));
            m.AddSystem(new StarSystem(3, "C", Vector2.up, Faction.帝国));
            m.AddCorridor(new Corridor(0, 3, 5f)); // 帝国-帝国 友軍
            m.AddCorridor(new Corridor(0, 2, 1f)); // 帝国-同盟 前線
            m.AddCorridor(new Corridor(2, 3, 1f)); // 同盟-帝国 前線
            return m;
        }

        [Test]
        public void IsFtlBlocked_HostileOwners_True_FriendlyFalse()
        {
            var m = MakeMap();
            Assert.IsTrue(StrategyRules.IsFtlBlocked(m, m.GetCorridor(0, 2)));
            Assert.IsFalse(StrategyRules.IsFtlBlocked(m, m.GetCorridor(0, 3)));
        }

        [Test]
        public void BeginWarp_AcrossFrontline_Fails_FriendlyOk()
        {
            var m = MakeMap();
            Assert.IsFalse(new StrategicFleet(1, 0, Faction.帝国).BeginWarp(m, 2)); // 前線
            Assert.IsTrue(new StrategicFleet(2, 0, Faction.帝国).BeginWarp(m, 3));  // 友軍
        }

        [Test]
        public void FindPath_AvoidFtlBlocked_RoutesAroundFrontline()
        {
            var m = MakeMap();
            CollectionAssert.AreEqual(new[] { 0, 2, 3 }, GalaxyPathfinder.FindPath(m, 0, 3, false)); // 安い前線経由
            CollectionAssert.AreEqual(new[] { 0, 3 }, GalaxyPathfinder.FindPath(m, 0, 3, true));     // 前線回避＝友軍長経路
        }

        [Test]
        public void WarpTo_RoutesAroundFrontline()
        {
            var m = MakeMap();
            var f = new StrategicFleet(1, 0, Faction.帝国);
            Assert.IsTrue(f.WarpTo(m, 3));                 // 友軍経路で直接 0→3
            Assert.AreEqual(3, f.destinationSystemId);
        }

        [Test]
        public void WarpTo_EnemyReachableOnlyViaFrontline_Fails()
        {
            var m = MakeMap();
            var f = new StrategicFleet(1, 0, Faction.帝国);
            Assert.IsFalse(f.WarpTo(m, 2)); // 2 は前線越しのみ＝FTL不可
            Assert.IsFalse(f.IsMoving);
        }
    }
}
