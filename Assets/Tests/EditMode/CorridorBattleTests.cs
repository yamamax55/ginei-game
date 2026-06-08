using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 回廊戦闘（C-3 #36 抽象版）：兵力差による勝敗・消耗と、前線侵攻の書き戻し（占領/前進/除去）を固定する。
    /// </summary>
    public class CorridorBattleTests
    {
        // 0(同盟) —前線— 1(帝国)
        private GalaxyMap FrontlineMap()
        {
            var m = new GalaxyMap();
            m.AddSystem(new StarSystem(0, "Ally", Vector2.zero, Faction.同盟));
            m.AddSystem(new StarSystem(1, "Emp", Vector2.right, Faction.帝国));
            m.AddCorridor(new Corridor(0, 1, 2f));
            return m;
        }

        // ───────── ResolveCorridorBattle ─────────

        [Test]
        public void Resolve_AttackerSuperior_Wins_WithRemainder()
        {
            var r = StrategyRules.ResolveCorridorBattle(300, 200);
            Assert.IsTrue(r.attackerWon);
            Assert.AreEqual(100, r.survivorStrength);
        }

        [Test]
        public void Resolve_DefenderSuperior_DefenderWins()
        {
            var r = StrategyRules.ResolveCorridorBattle(100, 200);
            Assert.IsFalse(r.attackerWon);
            Assert.AreEqual(100, r.survivorStrength);
        }

        [Test]
        public void Resolve_Tie_DefenderHolds_ZeroSurvivor()
        {
            var r = StrategyRules.ResolveCorridorBattle(100, 100);
            Assert.IsFalse(r.attackerWon);
            Assert.AreEqual(0, r.survivorStrength);
        }

        // ───────── EngageFrontline ─────────

        [Test]
        public void Engage_AttackerWins_CapturesAndAdvances()
        {
            var m = FrontlineMap();
            var reg = new StrategicFleetRegistry(m);
            var atk = new StrategicFleet(1, 0, Faction.同盟) { strength = 300 };
            var def = new StrategicFleet(2, 1, Faction.帝国) { strength = 200 };
            reg.Add(atk); reg.Add(def);

            Assert.IsTrue(StrategyRules.EngageFrontline(m, reg, 0, 1, out var r));
            Assert.IsTrue(r.attackerWon);
            Assert.AreEqual(100, r.survivorStrength);
            Assert.IsNull(reg.GetFleet(2));                       // 防衛側除去
            Assert.AreEqual(100, atk.strength);                   // 残存（単独→そのまま）
            Assert.AreEqual(1, atk.currentSystemId);              // toSystem へ前進
            Assert.AreEqual(Faction.同盟, m.GetSystem(1).owner);  // 占領
        }

        [Test]
        public void Engage_DefenderWins_AttackerRemoved_SystemHeld()
        {
            var m = FrontlineMap();
            var reg = new StrategicFleetRegistry(m);
            var atk = new StrategicFleet(1, 0, Faction.同盟) { strength = 100 };
            var def = new StrategicFleet(2, 1, Faction.帝国) { strength = 300 };
            reg.Add(atk); reg.Add(def);

            Assert.IsTrue(StrategyRules.EngageFrontline(m, reg, 0, 1, out var r));
            Assert.IsFalse(r.attackerWon);
            Assert.AreEqual(200, r.survivorStrength);
            Assert.IsNull(reg.GetFleet(1));                       // 攻撃側除去
            Assert.AreEqual(200, def.strength);                   // 防衛側按分
            Assert.AreEqual(Faction.帝国, m.GetSystem(1).owner);  // 守り切る
        }

        [Test]
        public void Engage_NotFrontline_ReturnsFalse()
        {
            var m = new GalaxyMap();
            m.AddSystem(new StarSystem(0, "A", Vector2.zero, Faction.帝国));
            m.AddSystem(new StarSystem(1, "B", Vector2.right, Faction.帝国));
            m.AddCorridor(new Corridor(0, 1, 1f)); // 帝国-帝国 友軍
            var reg = new StrategicFleetRegistry(m);
            reg.Add(new StrategicFleet(1, 0, Faction.帝国) { strength = 100 });

            Assert.IsFalse(StrategyRules.EngageFrontline(m, reg, 0, 1, out _));
        }

        [Test]
        public void Engage_NoHostileAttacker_ReturnsFalse()
        {
            var m = FrontlineMap();
            var reg = new StrategicFleetRegistry(m);
            // 同盟星系0に帝国艦（toSystem所有者=帝国と敵対しない）＝攻撃側がいない
            reg.Add(new StrategicFleet(1, 0, Faction.帝国) { strength = 100 });

            Assert.IsFalse(StrategyRules.EngageFrontline(m, reg, 0, 1, out _));
        }
    }
}
