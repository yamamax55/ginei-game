using System;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦隊メニューからの移動命令の可否（<see cref="FleetOrderRules"/>）を固定する。
    /// 移動命令の入口をメニューへ集約し MAP を表示専用にする変更なので、
    /// <b>命令を出せる/出せないの判定</b>と<b>行先の記述</b>が崩れないことが最優先。
    /// </summary>
    public class FleetOrderRulesTests
    {
        // 1-2-3-4 の一直線マップ（回廊 length=1）。owner は既定で 同盟。
        private static GalaxyMap Line(Faction owner = Faction.同盟)
        {
            var map = new GalaxyMap();
            for (int i = 1; i <= 4; i++)
                map.AddSystem(new StarSystem(i, "S" + i, new Vector2(i, 0f), owner));
            map.AddCorridor(new Corridor(1, 2, 1f));
            map.AddCorridor(new Corridor(2, 3, 1f));
            map.AddCorridor(new Corridor(3, 4, 1f));
            return map;
        }

        private static StrategicFleet Fleet(int startId = 1, Faction f = Faction.同盟)
            => new StrategicFleet(1, startId, f);

        // ---------------- CanOrder ----------------

        [Test]
        public void CanOrder_OwnIdleFleet_IsAllowed()
        {
            Assert.AreEqual(MoveOrderRejection.なし, FleetOrderRules.CanOrder(Fleet(), Faction.同盟));
        }

        [Test]
        public void CanOrder_EnemyFleet_IsRejected()
        {
            Assert.AreEqual(MoveOrderRejection.敵軍艦隊,
                FleetOrderRules.CanOrder(Fleet(1, Faction.帝国), Faction.同盟));
        }

        [Test]
        public void CanOrder_Reinforcement_IsRejected()
        {
            var f = Fleet();
            f.warpingAsReinforcement = true;
            Assert.AreEqual(MoveOrderRejection.増援航行中, FleetOrderRules.CanOrder(f, Faction.同盟));
        }

        [Test]
        public void CanOrder_Engaged_IsRejected()
        {
            var f = Fleet();
            f.engaged = true;
            Assert.AreEqual(MoveOrderRejection.交戦中, FleetOrderRules.CanOrder(f, Faction.同盟));
        }

        [Test]
        public void CanOrder_ReinforcementTakesPriorityOverEngaged()
        {
            var f = Fleet();
            f.warpingAsReinforcement = true;
            f.engaged = true;
            Assert.AreEqual(MoveOrderRejection.増援航行中, FleetOrderRules.CanOrder(f, Faction.同盟));
        }

        [Test]
        public void CanOrder_NullFleet_IsRejectedWithoutThrowing()
        {
            Assert.AreEqual(MoveOrderRejection.敵軍艦隊, FleetOrderRules.CanOrder(null, Faction.同盟));
        }

        // ---------------- CanMoveTo ----------------

        [Test]
        public void CanMoveTo_NormalGoal_IsAllowed()
        {
            var map = Line();
            Assert.AreEqual(MoveOrderRejection.なし,
                FleetOrderRules.CanMoveTo(map, Fleet(), Faction.同盟, 4));
        }

        [Test]
        public void CanMoveTo_UnknownSystem_HasNoCorridor()
        {
            var map = Line();
            Assert.AreEqual(MoveOrderRejection.回廊が無い,
                FleetOrderRules.CanMoveTo(map, Fleet(), Faction.同盟, 999));
        }

        [Test]
        public void CanMoveTo_NullMap_HasNoCorridor()
        {
            Assert.AreEqual(MoveOrderRejection.回廊が無い,
                FleetOrderRules.CanMoveTo(null, Fleet(), Faction.同盟, 2));
        }

        [Test]
        public void CanMoveTo_SameSystemWhileDocked_IsRejected()
        {
            var map = Line();
            Assert.AreEqual(MoveOrderRejection.目的地が現在地,
                FleetOrderRules.CanMoveTo(map, Fleet(1), Faction.同盟, 1));
        }

        [Test]
        public void CanMoveTo_Unreachable_IsRejected()
        {
            var map = Line();
            map.AddSystem(new StarSystem(9, "孤立", new Vector2(9f, 9f), Faction.同盟)); // 回廊なし
            Assert.AreEqual(MoveOrderRejection.到達不能,
                FleetOrderRules.CanMoveTo(map, Fleet(), Faction.同盟, 9));
        }

        [Test]
        public void CanMoveTo_PreconditionBeatsGoalChecks()
        {
            var map = Line();
            var f = Fleet();
            f.engaged = true;
            // 目的地が現在地でもあるが、事前判定（交戦中）が優先される
            Assert.AreEqual(MoveOrderRejection.交戦中,
                FleetOrderRules.CanMoveTo(map, f, Faction.同盟, 1));
        }

        // 要塞：1-2 の回廊を敵（帝国）要塞が扼する。迂回路の有無で結果が変わる。
        private static GalaxyMap FortressMap(bool withBypass, Faction fortressOwner)
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(1, "起点", new Vector2(0f, 0f), Faction.同盟));
            map.AddSystem(new StarSystem(2, "対岸", new Vector2(2f, 0f), Faction.同盟));
            var choke = new Corridor(1, 2, 1f, CorridorType.要衝);
            choke.fortress = new Fortress(1000f, 100f) { owner = fortressOwner };
            map.AddCorridor(choke);
            if (withBypass)
            {
                map.AddSystem(new StarSystem(3, "迂回", new Vector2(1f, 3f), Faction.同盟));
                map.AddCorridor(new Corridor(1, 3, 5f));
                map.AddCorridor(new Corridor(3, 2, 5f));
            }
            return map;
        }

        [Test]
        public void CanMoveTo_EnemyFortressWithoutBypass_WarnsBlockade()
        {
            var map = FortressMap(false, Faction.帝国);
            // 素の経路はあるが、要塞を避けた経路が無い＝「落とすしかない」警告
            Assert.AreEqual(MoveOrderRejection.要塞封鎖,
                FleetOrderRules.CanMoveTo(map, Fleet(1, Faction.同盟), Faction.同盟, 2));
        }

        [Test]
        public void CanMoveTo_EnemyFortressWithBypass_IsAllowed()
        {
            var map = FortressMap(true, Faction.帝国);
            Assert.AreEqual(MoveOrderRejection.なし,
                FleetOrderRules.CanMoveTo(map, Fleet(1, Faction.同盟), Faction.同盟, 2));
        }

        [Test]
        public void CanMoveTo_OwnFortress_IsAllowed()
        {
            var map = FortressMap(false, Faction.同盟); // 自分の要塞は自分を止めない
            Assert.AreEqual(MoveOrderRejection.なし,
                FleetOrderRules.CanMoveTo(map, Fleet(1, Faction.同盟), Faction.同盟, 2));
        }

        [Test]
        public void CanMoveTo_MovingFleet_PlansFromNextSystem()
        {
            var map = Line();
            var f = Fleet(1);
            Assert.IsTrue(f.BeginWarp(map, 2), "回廊へ入れていない");
            // 到達予定(2)を起点に判定する＝出発元(1)へ引き返す命令も受理される
            Assert.AreEqual(MoveOrderRejection.なし, FleetOrderRules.CanMoveTo(map, f, Faction.同盟, 1));
            Assert.AreEqual(MoveOrderRejection.なし, FleetOrderRules.CanMoveTo(map, f, Faction.同盟, 4));
        }

        // ---------------- RejectionText ----------------

        [Test]
        public void RejectionText_NoneIsEmpty_OthersAreJapanese()
        {
            Assert.AreEqual("", FleetOrderRules.RejectionText(MoveOrderRejection.なし));
            foreach (MoveOrderRejection r in Enum.GetValues(typeof(MoveOrderRejection)))
            {
                if (r == MoveOrderRejection.なし) continue;
                string text = FleetOrderRules.RejectionText(r);
                Assert.IsFalse(string.IsNullOrEmpty(text), r + " の説明文が空");
            }
        }

        // ---------------- StateLabel ----------------

        [Test]
        public void StateLabel_Docked()
        {
            Assert.AreEqual("停泊中", FleetOrderRules.StateLabel(Fleet()));
        }

        [Test]
        public void StateLabel_Moving()
        {
            var map = Line();
            var f = Fleet(1);
            f.BeginWarp(map, 2);
            Assert.AreEqual("移動中", FleetOrderRules.StateLabel(f));
        }

        [Test]
        public void StateLabel_Engaged()
        {
            var map = Line();
            var f = Fleet(1);
            f.BeginWarp(map, 2);
            f.engaged = true;
            Assert.AreEqual("交戦中", FleetOrderRules.StateLabel(f));
        }

        [Test]
        public void StateLabel_Reinforcement()
        {
            var f = Fleet();
            f.warpingAsReinforcement = true;
            Assert.AreEqual("増援航行中", FleetOrderRules.StateLabel(f));
        }

        [Test]
        public void StateLabel_BlockadedByFortress()
        {
            var map = FortressMap(false, Faction.帝国);
            var f = Fleet(1, Faction.同盟);
            Assert.IsTrue(f.BeginWarp(map, 2));
            Assert.AreEqual("要塞に足止め", FleetOrderRules.StateLabel(f));
        }

        [Test]
        public void StateLabel_Null_IsEmpty()
        {
            Assert.AreEqual("", FleetOrderRules.StateLabel(null));
        }

        // ---------------- TryDescribeRoute / IsMultiHop ----------------

        [Test]
        public void TryDescribeRoute_Docked_AllThreeAreCurrentSystem()
        {
            var f = Fleet(3);
            Assert.IsTrue(FleetOrderRules.TryDescribeRoute(f, out int from, out int hop, out int final));
            Assert.AreEqual(3, from);
            Assert.AreEqual(3, hop);
            Assert.AreEqual(3, final);
            Assert.IsFalse(FleetOrderRules.IsMultiHop(f));
        }

        [Test]
        public void TryDescribeRoute_SingleHop_HopEqualsFinal()
        {
            var map = Line();
            var f = Fleet(1);
            Assert.IsTrue(f.BeginWarp(map, 2));
            Assert.IsTrue(FleetOrderRules.TryDescribeRoute(f, out int from, out int hop, out int final));
            Assert.AreEqual(1, from, "出発元が現在星系になっていない");
            Assert.AreEqual(2, hop);
            Assert.AreEqual(2, final);
            Assert.IsFalse(FleetOrderRules.IsMultiHop(f));
        }

        [Test]
        public void TryDescribeRoute_MultiHop_HopDiffersFromFinal()
        {
            var map = Line(); // 全星系が同盟領＝経路を通り抜けられる
            var f = Fleet(1);
            Assert.IsTrue(f.WarpTo(map, 4), "多ホップ経路が引けていない");
            Assert.IsTrue(FleetOrderRules.TryDescribeRoute(f, out int from, out int hop, out int final));
            Assert.AreEqual(1, from);
            Assert.AreEqual(2, hop, "いま向かっている隣の星系が違う");
            Assert.AreEqual(4, final, "最終目的地が違う");
            Assert.AreNotEqual(hop, final);
            Assert.IsTrue(FleetOrderRules.IsMultiHop(f));
        }

        [Test]
        public void TryDescribeRoute_Null_ReturnsFalse()
        {
            Assert.IsFalse(FleetOrderRules.TryDescribeRoute(null, out int from, out int hop, out int final));
            Assert.AreEqual(0, from);
            Assert.AreEqual(0, hop);
            Assert.AreEqual(0, final);
            Assert.IsFalse(FleetOrderRules.IsMultiHop(null));
        }
    }
}
