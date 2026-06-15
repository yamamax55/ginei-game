using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>星系上（惑星上）での接敵＝fleet-vs-fleet（BeginSystemEngagements / GatherEngagementAtSystem / ResolveSystemEncounters）の EditMode テスト。</summary>
    public class SystemEngagementTests
    {
        // 停泊艦隊（onCorridor=false 既定＝IsOnCorridor false）。星系 sysId に在席。
        private static StrategicFleet Stationed(int id, Faction f, int strength, int sysId)
            => new StrategicFleet { id = id, faction = f, strength = strength, currentSystemId = sysId };

        [TearDown]
        public void Cleanup() => BattleHandoff.Clear();

        [Test]
        public void BeginSystemEngagements_MarksHostileSameSystemStationed()
        {
            var reg = new StrategicFleetRegistry();
            reg.Add(Stationed(1, Faction.帝国, 100, 5));
            reg.Add(Stationed(2, Faction.同盟, 120, 5)); // 同一星系・敵対
            reg.Add(Stationed(3, Faction.帝国, 80, 5));  // 同盟と敵対（追加で交戦）
            reg.Add(Stationed(4, Faction.同盟, 60, 7));  // 別星系＝無関係

            int n = StrategyRules.BeginSystemEngagements(reg);
            Assert.Greater(n, 0);
            Assert.IsTrue(reg.GetFleet(1).engaged);
            Assert.IsTrue(reg.GetFleet(2).engaged);
            Assert.IsTrue(reg.GetFleet(3).engaged);
            Assert.IsFalse(reg.GetFleet(4).engaged); // 別星系
        }

        [Test]
        public void BeginSystemEngagements_IgnoresSameFactionOnly()
        {
            var reg = new StrategicFleetRegistry();
            reg.Add(Stationed(1, Faction.帝国, 100, 5));
            reg.Add(Stationed(2, Faction.帝国, 120, 5)); // 同盟が居ない＝交戦しない
            Assert.AreEqual(0, StrategyRules.BeginSystemEngagements(reg));
            Assert.IsFalse(reg.GetFleet(1).engaged);
        }

        [Test]
        public void GatherEngagementAtSystem_SplitsByHostility()
        {
            var reg = new StrategicFleetRegistry();
            reg.Add(Stationed(1, Faction.帝国, 100, 5));
            reg.Add(Stationed(2, Faction.帝国, 80, 5));
            reg.Add(Stationed(3, Faction.同盟, 120, 5));
            StrategyRules.BeginSystemEngagements(reg);

            var sideA = new List<StrategicFleet>();
            var sideB = new List<StrategicFleet>();
            Assert.IsTrue(StrategyRules.GatherEngagementAtSystem(reg, 5, sideA, sideB));
            Assert.AreEqual(2, sideA.Count); // 帝国2隊
            Assert.AreEqual(1, sideB.Count); // 同盟1隊
        }

        [Test]
        public void ResolveSystemEncounters_WinnerKeepsSurvivors_LoserRemoved()
        {
            var reg = new StrategicFleetRegistry();
            reg.Add(Stationed(1, Faction.帝国, 300, 5));
            reg.Add(Stationed(2, Faction.同盟, 100, 5));
            StrategyRules.BeginSystemEngagements(reg);

            var outcomes = new List<EncounterOutcome>();
            int c = StrategyRules.ResolveSystemEncounters(reg, outcomes, null);
            Assert.AreEqual(1, c);
            // 帝国(300) > 同盟(100) → 帝国勝利・同盟除去・残存=300*(300-100)/300=200・固着解除
            Assert.IsNull(reg.GetFleet(2));
            Assert.IsNotNull(reg.GetFleet(1));
            Assert.AreEqual(200, reg.GetFleet(1).strength);
            Assert.IsFalse(reg.GetFleet(1).engaged);
            Assert.AreEqual(1, outcomes.Count);
        }
    }
}
