using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>複数艦隊での会戦受け渡し（接敵内容を会戦へ合わせる・GatherEngagementOnCorridor / ApplyMultiHandoffResult）の EditMode テスト。</summary>
    public class MultiFleetBattleHandoffTests
    {
        private static StrategicFleet Fleet(int id, Faction f, int strength, int sysFrom, int sysTo, bool engaged)
            => new StrategicFleet
            {
                id = id, faction = f, strength = strength,
                currentSystemId = sysFrom, destinationSystemId = sysTo, engaged = engaged,
            };

        [TearDown]
        public void Cleanup() => BattleHandoff.Clear();

        [Test]
        public void Gather_SplitsEngagedFleetsByHostility()
        {
            var reg = new StrategicFleetRegistry();
            reg.Add(Fleet(1, Faction.帝国, 100, 1, 2, true));
            reg.Add(Fleet(2, Faction.帝国, 80, 1, 2, true));
            reg.Add(Fleet(3, Faction.同盟, 120, 1, 2, true));
            reg.Add(Fleet(4, Faction.同盟, 50, 1, 2, false)); // 非交戦＝無視
            reg.Add(Fleet(5, Faction.同盟, 50, 3, 4, true));  // 別回廊＝無視

            var sideA = new List<StrategicFleet>();
            var sideB = new List<StrategicFleet>();
            Assert.IsTrue(StrategyRules.GatherEngagementOnCorridor(reg, 2, 1, sideA, sideB)); // 無向
            Assert.AreEqual(2, sideA.Count); // 帝国2隊
            Assert.AreEqual(1, sideB.Count); // 同盟1隊
        }

        [Test]
        public void Gather_FalseWhenOnlyOneSidePresent()
        {
            var reg = new StrategicFleetRegistry();
            reg.Add(Fleet(1, Faction.帝国, 100, 1, 2, true));
            reg.Add(Fleet(2, Faction.帝国, 80, 1, 2, true));
            var sideA = new List<StrategicFleet>();
            var sideB = new List<StrategicFleet>();
            Assert.IsFalse(StrategyRules.GatherEngagementOnCorridor(reg, 1, 2, sideA, sideB));
        }

        [Test]
        public void ApplyMulti_DistributesSurvivorsToWinner_RemovesLosers()
        {
            var reg = new StrategicFleetRegistry();
            reg.Add(Fleet(1, Faction.帝国, 100, 1, 2, true));
            reg.Add(Fleet(2, Faction.帝国, 300, 1, 2, true));
            reg.Add(Fleet(3, Faction.同盟, 150, 1, 2, true));

            BattleHandoff.fleets.Clear();
            BattleHandoff.fleets.Add(new BattleHandoff.HandoffFleet { faction = Faction.帝国, strategicStrength = 100, fleetId = 1, sideA = true });
            BattleHandoff.fleets.Add(new BattleHandoff.HandoffFleet { faction = Faction.帝国, strategicStrength = 300, fleetId = 2, sideA = true });
            BattleHandoff.fleets.Add(new BattleHandoff.HandoffFleet { faction = Faction.同盟, strategicStrength = 150, fleetId = 3, sideA = false });
            BattleHandoff.Pending = true;
            BattleHandoff.SetResult(aWon: true, survivors: 240);

            Assert.IsTrue(StrategyRules.ApplyHandoffResult(reg)); // IsMultiFleet ＝ 按分経路
            // 敗者は盤面から除去
            Assert.IsNull(reg.GetFleet(3));
            // 勝者は原戦力比で按分（100:300 → 60:180）＋交戦固着を解除
            Assert.AreEqual(60, reg.GetFleet(1).strength);
            Assert.AreEqual(180, reg.GetFleet(2).strength);
            Assert.IsFalse(reg.GetFleet(1).engaged);
            Assert.IsFalse(reg.GetFleet(2).engaged);
            // Handoff は消える
            Assert.IsFalse(BattleHandoff.IsMultiFleet);
            Assert.IsFalse(BattleHandoff.Pending);
        }

        [Test]
        public void ApplyMulti_BWinning_RemovesASide()
        {
            var reg = new StrategicFleetRegistry();
            reg.Add(Fleet(1, Faction.帝国, 100, 1, 2, true));
            reg.Add(Fleet(2, Faction.同盟, 200, 1, 2, true));

            BattleHandoff.fleets.Clear();
            BattleHandoff.fleets.Add(new BattleHandoff.HandoffFleet { faction = Faction.帝国, strategicStrength = 100, fleetId = 1, sideA = true });
            BattleHandoff.fleets.Add(new BattleHandoff.HandoffFleet { faction = Faction.同盟, strategicStrength = 200, fleetId = 2, sideA = false });
            BattleHandoff.Pending = true;
            BattleHandoff.SetResult(aWon: false, survivors: 130); // B 勝利

            Assert.IsTrue(StrategyRules.ApplyHandoffResult(reg));
            Assert.IsNull(reg.GetFleet(1));            // A 敗者除去
            Assert.AreEqual(130, reg.GetFleet(2).strength); // B 単独＝全残存
        }

        [Test]
        public void QueueMulti_SetsRepresentativesAndSideTotals()
        {
            var entries = new List<BattleHandoff.HandoffFleet>
            {
                new BattleHandoff.HandoffFleet { faction = Faction.帝国, strategicStrength = 100, fleetId = 1, sideA = true },
                new BattleHandoff.HandoffFleet { faction = Faction.帝国, strategicStrength = 300, fleetId = 2, sideA = true },
                new BattleHandoff.HandoffFleet { faction = Faction.同盟, strategicStrength = 150, fleetId = 3, sideA = false },
            };
            BattleHandoff.QueueMulti(entries, Faction.帝国, Faction.同盟, 1, 3, "Strategy");

            Assert.IsTrue(BattleHandoff.IsMultiFleet);
            Assert.IsTrue(BattleHandoff.Pending);
            Assert.AreEqual(Faction.帝国, BattleHandoff.factionA);
            Assert.AreEqual(Faction.同盟, BattleHandoff.factionB);
            Assert.AreEqual(400, BattleHandoff.strengthA); // A 陣営合計
            Assert.AreEqual(150, BattleHandoff.strengthB);
            Assert.AreEqual(1, BattleHandoff.fleetIdA);
            Assert.AreEqual(3, BattleHandoff.fleetIdB);
        }
    }
}
