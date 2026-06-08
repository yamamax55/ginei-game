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

        // ───────── ResolveEncounters（回廊で出会った敵対艦隊が戦う）─────────

        private GalaxyMap LineMap()
        {
            var m = new GalaxyMap();
            m.AddSystem(new StarSystem(0, "A", Vector2.zero, Faction.帝国));
            m.AddSystem(new StarSystem(1, "B", Vector2.right, Faction.帝国));
            m.AddCorridor(new Corridor(0, 1, 4f));
            return m;
        }

        [Test]
        public void ResolveEncounters_HostileOnSameCorridor_LoserRemoved_WinnerContinues()
        {
            var m = LineMap();
            var reg = new StrategicFleetRegistry(m);
            var imp = new StrategicFleet(1, 0, Faction.帝国) { strength = 300 }; imp.BeginWarp(m, 1);  // 0→1
            var ally = new StrategicFleet(2, 1, Faction.同盟) { strength = 200 }; ally.BeginWarp(m, 0); // 1→0（同一回廊）
            reg.Add(imp); reg.Add(ally);

            // 入った直後はまだ回廊内で接触していない（複数艦が同じ回廊に同時に居られる）
            Assert.AreEqual(0, StrategyRules.ResolveEncounters(reg));

            // 中間まで進めると交差＝接触して戦闘（len4・speed1 → 双方 progress 0.5 で交差）
            reg.Tick(2f);
            Assert.AreEqual(1, StrategyRules.ResolveEncounters(reg));
            Assert.IsNull(reg.GetFleet(2));      // 敗者(同盟200)除去
            Assert.AreEqual(100, imp.strength);  // 勝者 残存100
            Assert.IsTrue(imp.IsMoving);         // 勝者は回廊を進み続ける
        }

        [Test]
        public void ResolveEncounters_SameFaction_NoBattle()
        {
            var m = LineMap();
            var reg = new StrategicFleetRegistry(m);
            var a = new StrategicFleet(1, 0, Faction.帝国) { strength = 300 }; a.BeginWarp(m, 1);
            var b = new StrategicFleet(2, 1, Faction.帝国) { strength = 200 }; b.BeginWarp(m, 0);
            reg.Add(a); reg.Add(b);

            Assert.AreEqual(0, StrategyRules.ResolveEncounters(reg)); // 同勢力は戦わない
        }
    }
}
