using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 補給線 L-2（#94・<see cref="SupplyRules"/>）の未カバー挙動を固定する：補給源自身の扱い（所有/blocked 前提）、
    /// 分岐トポロジでの連結成分到達、不正入力（null map/sources）、TickFront の no-op 境界（dt&lt;=0/null）。
    /// 既存 <c>LogisticsLayerTests</c> は直線マップでの到達/遮断・枯渇/回復を担保済みなので、ここは重複させない。
    /// </summary>
    public class SupplyRulesExtraTests
    {
        // 0-1-2-3 の一直線マップ（owners[i]=星系iの所有勢力）。既存テストの LineMap と同形。
        private static GalaxyMap LineMap(params Faction[] owners)
        {
            var m = new GalaxyMap();
            for (int i = 0; i < owners.Length; i++)
                m.AddSystem(new StarSystem(i, "S" + i, Vector2.zero, owners[i]));
            for (int i = 0; i < owners.Length - 1; i++)
                m.AddCorridor(new Corridor(i, i + 1, 1f));
            return m;
        }

        /// <summary>補給源そのものが自勢力所有でなければ補給線の起点にならない（到達集合は空）。</summary>
        [Test]
        public void Supply_SourceNotOwned_YieldsEmpty()
        {
            // 起点0が同盟所有なのに帝国の補給源として渡す＝起点として無効＝何も補給されない
            var m = LineMap(Faction.同盟, Faction.帝国, Faction.帝国);
            var reached = SupplyRules.SuppliedSystems(m, Faction.帝国, new[] { 0 });
            Assert.AreEqual(0, reached.Count);
            Assert.IsFalse(SupplyRules.IsSupplied(m, Faction.帝国, new[] { 0 }, target: 1));
        }

        /// <summary>補給源自身が敵ZOC（blocked）下なら起点として使えず、その線は通らない。</summary>
        [Test]
        public void Supply_BlockedSource_CannotSeed()
        {
            // 全部帝国だが起点0が blocked＝供給開始点が断たれる→1にも届かない
            var m = LineMap(Faction.帝国, Faction.帝国, Faction.帝国);
            var zoc = new HashSet<int> { 0 };
            var reached = SupplyRules.SuppliedSystems(m, Faction.帝国, new[] { 0 }, zoc);
            Assert.AreEqual(0, reached.Count);
            Assert.IsFalse(SupplyRules.IsSupplied(m, Faction.帝国, new[] { 0 }, 1, zoc));
        }

        /// <summary>分岐トポロジ：自勢力で連結した枝だけ到達し、敵所有で分断された枝には届かない。</summary>
        [Test]
        public void Supply_BranchingMap_ReachesOnlyOwnedConnectedComponent()
        {
            // 星型: 中心0(帝国) - 1(帝国) / 2(同盟) / 3(帝国だが2経由でしか繋がらない)
            var m = new GalaxyMap();
            m.AddSystem(new StarSystem(0, "S0", Vector2.zero, Faction.帝国));
            m.AddSystem(new StarSystem(1, "S1", Vector2.zero, Faction.帝国));
            m.AddSystem(new StarSystem(2, "S2", Vector2.zero, Faction.同盟));
            m.AddSystem(new StarSystem(3, "S3", Vector2.zero, Faction.帝国));
            m.AddCorridor(new Corridor(0, 1, 1f));
            m.AddCorridor(new Corridor(0, 2, 1f));
            m.AddCorridor(new Corridor(2, 3, 1f));

            var reached = SupplyRules.SuppliedSystems(m, Faction.帝国, new[] { 0 });
            Assert.IsTrue(reached.Contains(0));
            Assert.IsTrue(reached.Contains(1));   // 直結の帝国枝には届く
            Assert.IsFalse(reached.Contains(2));  // 敵所有はそもそも辿れない
            Assert.IsFalse(reached.Contains(3));  // 敵星系の先の帝国星系には届かない（分断）
        }

        /// <summary>複数補給源：同じ連結成分の別起点からも到達でき、到達集合は和集合になる。</summary>
        [Test]
        public void Supply_MultipleSources_Union()
        {
            // 0(帝)-1(同)-2(帝)-3(帝): 0と2を補給源に。0からは0のみ、2からは2,3。
            var m = LineMap(Faction.帝国, Faction.同盟, Faction.帝国, Faction.帝国);
            var reached = SupplyRules.SuppliedSystems(m, Faction.帝国, new[] { 0, 2 });
            Assert.IsTrue(reached.Contains(0));
            Assert.IsTrue(reached.Contains(2));
            Assert.IsTrue(reached.Contains(3));
            Assert.IsFalse(reached.Contains(1)); // 敵星系は中継できない
        }

        /// <summary>不正入力（null map / null sources）は例外でなく空集合を返す（防御的）。</summary>
        [Test]
        public void Supply_NullInputs_ReturnEmptyNotThrow()
        {
            Assert.AreEqual(0, SupplyRules.SuppliedSystems(null, Faction.帝国, new[] { 0 }).Count);
            var m = LineMap(Faction.帝国, Faction.帝国);
            Assert.AreEqual(0, SupplyRules.SuppliedSystems(m, Faction.帝国, null).Count);
        }

        /// <summary>TickFront の no-op 境界：dt&lt;=0 と null 備蓄は何もせず副作用なし（負dtで増減もしない）。</summary>
        [Test]
        public void TickFront_NoOp_OnNonPositiveDt_AndNullStock()
        {
            var front = new ResourceStockpile(10, 10, 10);
            SupplyRules.TickFront(front, supplied: false, resupplyRate: 5f, consumeRate: 4f, dt: 0f);
            Assert.AreEqual(10f, front.supplies, 1e-4f); // dt=0 は無変化
            SupplyRules.TickFront(front, supplied: true, 5f, 4f, -1f);
            Assert.AreEqual(10f, front.supplies, 1e-4f); // 負dt も無変化（巻き戻さない）
            Assert.DoesNotThrow(() => SupplyRules.TickFront(null, true, 5f, 4f, 1f)); // null 安全
        }
    }
}
