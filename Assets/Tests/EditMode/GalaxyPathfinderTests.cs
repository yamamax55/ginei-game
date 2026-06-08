using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 銀河グラフ最短経路（C-1 #34・回廊 length 重みの Dijkstra）を固定する。
    /// 直行が高コストなら安い迂回を選ぶ／到達不能・未知ノードは空。
    /// </summary>
    public class GalaxyPathfinderTests
    {
        // 0 —(10)— 1 、0 —(1)— 2 —(1)— 1 、3 は孤立。
        private GalaxyMap MakeMap()
        {
            var m = new GalaxyMap();
            for (int i = 0; i < 4; i++) m.AddSystem(new StarSystem(i, "S" + i, Vector2.zero));
            m.AddCorridor(new Corridor(0, 1, 10f));
            m.AddCorridor(new Corridor(0, 2, 1f));
            m.AddCorridor(new Corridor(2, 1, 1f));
            return m;
        }

        [Test]
        public void FindPath_SameStartGoal_ReturnsSingle()
        {
            var p = GalaxyPathfinder.FindPath(MakeMap(), 0, 0);
            CollectionAssert.AreEqual(new[] { 0 }, p);
        }

        [Test]
        public void FindPath_Adjacent_ReturnsDirect()
        {
            var p = GalaxyPathfinder.FindPath(MakeMap(), 0, 2);
            CollectionAssert.AreEqual(new[] { 0, 2 }, p);
        }

        [Test]
        public void FindPath_PrefersCheaperDetour()
        {
            // 0→1 は直行コスト10より、0→2→1（コスト2）を選ぶ。
            var p = GalaxyPathfinder.FindPath(MakeMap(), 0, 1);
            CollectionAssert.AreEqual(new[] { 0, 2, 1 }, p);
        }

        [Test]
        public void FindPath_Unreachable_IsEmpty()
        {
            var p = GalaxyPathfinder.FindPath(MakeMap(), 0, 3); // 3 は孤立
            Assert.AreEqual(0, p.Count);
        }

        [Test]
        public void FindPath_UnknownNode_IsEmpty()
        {
            var p = GalaxyPathfinder.FindPath(MakeMap(), 0, 99);
            Assert.AreEqual(0, p.Count);
        }

        [Test]
        public void PathCost_SumsCorridorLengths()
        {
            var m = MakeMap();
            Assert.AreEqual(2f, GalaxyPathfinder.PathCost(m, new List<int> { 0, 2, 1 }), 1e-4f);
        }

        [Test]
        public void PathCost_BrokenAdjacency_ReturnsMinusOne()
        {
            var m = MakeMap();
            Assert.AreEqual(-1f, GalaxyPathfinder.PathCost(m, new List<int> { 0, 3 }));
        }
    }
}
