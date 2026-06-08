using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦略マップ 銀河グラフ（C-1 #34）の純ロジックを固定する。
    /// 「回廊で結ばれていなければ移動不可」「隣接列挙」「向き不問の回廊取得」。
    /// </summary>
    public class GalaxyMapTests
    {
        // 0 —(2)— 1 —(3)— 2 、加えて 1 —(1,要衝)— 3。0 と 2 の直接回廊は無い。
        private GalaxyMap MakeMap()
        {
            var m = new GalaxyMap();
            m.AddSystem(new StarSystem(0, "A", new Vector2(0, 0)));
            m.AddSystem(new StarSystem(1, "B", new Vector2(1, 0)));
            m.AddSystem(new StarSystem(2, "C", new Vector2(2, 0)));
            m.AddSystem(new StarSystem(3, "D", new Vector2(1, 1)));
            m.AddCorridor(new Corridor(0, 1, 2f));
            m.AddCorridor(new Corridor(1, 2, 3f));
            m.AddCorridor(new Corridor(1, 3, 1f, CorridorType.要衝));
            return m;
        }

        [Test]
        public void GetSystem_ReturnsById_OrNull()
        {
            var m = MakeMap();
            Assert.AreEqual("B", m.GetSystem(1).systemName);
            Assert.IsNull(m.GetSystem(99));
        }

        [Test]
        public void AreConnected_OnlyViaCorridor_Directionless()
        {
            var m = MakeMap();
            Assert.IsTrue(m.AreConnected(0, 1));
            Assert.IsTrue(m.AreConnected(1, 0));   // 向き不問
            Assert.IsFalse(m.AreConnected(0, 2));  // 直接の回廊なし＝移動不可
            Assert.IsFalse(m.AreConnected(0, 0));  // 同一星系は不可
        }

        [Test]
        public void Neighbors_ReturnsAllConnected()
        {
            var m = MakeMap();
            var n = m.Neighbors(1);
            Assert.AreEqual(3, n.Count);
            CollectionAssert.Contains(n, 0);
            CollectionAssert.Contains(n, 2);
            CollectionAssert.Contains(n, 3);
        }

        [Test]
        public void GetCorridor_BothDirections_ReturnSameEdge()
        {
            var m = MakeMap();
            var c1 = m.GetCorridor(1, 2);
            var c2 = m.GetCorridor(2, 1);
            Assert.AreSame(c1, c2);
            Assert.AreEqual(3f, c1.length);
        }

        [Test]
        public void Corridor_ConnectsAndOther()
        {
            var c = new Corridor(4, 7, 2f);
            Assert.IsTrue(c.Connects(4));
            Assert.IsTrue(c.Connects(7));
            Assert.IsFalse(c.Connects(5));
            Assert.AreEqual(7, c.Other(4));
            Assert.AreEqual(4, c.Other(7));
            Assert.AreEqual(-1, c.Other(5));
        }
    }
}
