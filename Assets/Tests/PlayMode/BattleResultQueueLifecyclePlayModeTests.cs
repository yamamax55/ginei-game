using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class BattleResultQueueLifecyclePlayModeTests
    {
        private GameObject viewObject;

        [SetUp]
        public void SetUp()
        {
            BattleResultQueue.Clear();
            ReinforcementReturnQueue.Clear();
            BattleHandoff.Clear();
        }

        [Test]
        public void TwoResolvedBattles_AreAppliedInQueueOrderWithoutOverwritingEachOther()
        {
            var map = new GalaxyMap();
            for (int i = 0; i < 4; i++)
                map.AddSystem(new StarSystem(i, "S" + i, new Vector2(i * 3f, 0f), i % 2 == 0 ? Faction.同盟 : Faction.帝国));

            var registry = new StrategicFleetRegistry(map);
            registry.Add(new StrategicFleet(10, 0, Faction.同盟) { strength = 120 });
            registry.Add(new StrategicFleet(11, 1, Faction.帝国) { strength = 110 });
            registry.Add(new StrategicFleet(20, 2, Faction.同盟) { strength = 130 });
            registry.Add(new StrategicFleet(21, 3, Faction.帝国) { strength = 140 });

            BattleResultQueue.Push(Result(10, 11, true, 70));
            BattleResultQueue.Push(Result(20, 21, false, 80));

            viewObject = new GameObject("GalaxyView_BattleResultQa");
            GalaxyView view = viewObject.AddComponent<GalaxyView>();
            view.BindBattleRegistryForQa(registry);

            view.DrainOneBattleResultForQa();
            Assert.AreEqual(70, registry.GetFleet(10).strength, "先頭の勝者へ結果が反映されていない");
            Assert.AreEqual(130, registry.GetFleet(20).strength, "2件目を同じフレームで誤って上書きした");
            Assert.AreEqual(1, BattleResultQueue.Count);
            Assert.IsFalse(BattleHandoff.Pending, "反映済みglobal handoffが次の結果を塞いでいる");

            view.DrainOneBattleResultForQa();
            Assert.AreEqual(80, registry.GetFleet(21).strength, "2件目の勝者へ独立した結果が反映されていない");
            Assert.AreEqual(0, BattleResultQueue.Count);
            Assert.IsFalse(BattleHandoff.Pending);
        }

        private static BattleHandoff.State Result(int fleetA, int fleetB, bool sideAWon, int survivor)
        {
            return new BattleHandoff.State
            {
                Pending = true,
                Resolved = true,
                factionA = Faction.同盟,
                factionB = Faction.帝国,
                fleetIdA = fleetA,
                fleetIdB = fleetB,
                sideAWon = sideAWon,
                survivorStrength = survivor,
            };
        }

        [TearDown]
        public void TearDown()
        {
            BattleResultQueue.Clear();
            ReinforcementReturnQueue.Clear();
            BattleHandoff.Clear();
            if (viewObject != null) Object.DestroyImmediate(viewObject);
            viewObject = null;
        }

        [Test]
        public void CampaignReset_DiscardsPriorBattleResultsAndReinforcementReturns()
        {
            BattleResultQueue.Push(new BattleHandoff.State
            {
                Pending = true,
                Resolved = true,
                fleetIdA = 10,
                fleetIdB = 20,
                sideAWon = true,
                survivorStrength = 50,
            });
            ReinforcementReturnQueue.Push(new WarpReinforcement { id = 1, fleetId = 30 });
            ReinforcementReturnQueue.PushArrived(40);

            Assert.AreEqual(1, BattleResultQueue.Count);
            Assert.AreEqual(1, ReinforcementReturnQueue.Count);

            GalaxyView.ResetCampaignStatics();

            Assert.AreEqual(0, BattleResultQueue.Count, "前戦役の会戦結果が新戦役へ残った");
            Assert.AreEqual(0, ReinforcementReturnQueue.Count, "前戦役の援軍差し戻しが新戦役へ残った");
            var arrived = new List<int>();
            Assert.IsFalse(ReinforcementReturnQueue.TakeArrivedIds(arrived), "前戦役の到着済み援軍IDが新戦役へ残った");
            Assert.IsEmpty(arrived);
        }
    }
}
