using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦略マップ仕上げ（C-1 #34）：回廊での会戦トリガー検知と星系占領を固定する。
    /// 敵対判定は FactionRelations（enum フォールバック＝帝国≠同盟＝敵）。
    /// </summary>
    public class StrategyRulesTests
    {
        // 0 —(4)— 1 、0 —(4)— 2。全星系の初期所有は帝国。
        private GalaxyMap MakeMap()
        {
            var m = new GalaxyMap();
            m.AddSystem(new StarSystem(0, "A", Vector2.zero, Faction.帝国));
            m.AddSystem(new StarSystem(1, "B", Vector2.right, Faction.帝国));
            m.AddSystem(new StarSystem(2, "C", Vector2.up, Faction.帝国));
            m.AddCorridor(new Corridor(0, 1, 4f));
            m.AddCorridor(new Corridor(0, 2, 4f));
            return m;
        }

        // ───────── 会戦トリガー（FindEncounters）─────────

        [Test]
        public void FindEncounters_HostileOnSameCorridor_IsDetected()
        {
            var m = MakeMap();
            var imp = new StrategicFleet(1, 0, Faction.帝国, 1f); imp.BeginWarp(m, 1); // 0→1
            var ally = new StrategicFleet(2, 1, Faction.同盟, 1f); ally.BeginWarp(m, 0); // 1→0（同一回廊）
            var reg = new StrategicFleetRegistry(m); reg.Add(imp); reg.Add(ally);

            var enc = StrategyRules.FindEncounters(reg);
            Assert.AreEqual(1, enc.Count);
            Assert.IsTrue(StrategyRules.AnyEncounter(reg));
        }

        [Test]
        public void FindEncounters_SameFaction_NotHostile_NoEncounter()
        {
            var m = MakeMap();
            var a = new StrategicFleet(1, 0, Faction.帝国, 1f); a.BeginWarp(m, 1);
            var b = new StrategicFleet(2, 1, Faction.帝国, 1f); b.BeginWarp(m, 0);
            var reg = new StrategicFleetRegistry(m); reg.Add(a); reg.Add(b);

            Assert.AreEqual(0, StrategyRules.FindEncounters(reg).Count);
            Assert.IsFalse(StrategyRules.AnyEncounter(reg));
        }

        [Test]
        public void FindEncounters_DifferentCorridors_NoEncounter()
        {
            var m = MakeMap();
            var imp = new StrategicFleet(1, 0, Faction.帝国, 1f); imp.BeginWarp(m, 1);  // 回廊 0-1
            var ally = new StrategicFleet(2, 0, Faction.同盟, 1f); ally.BeginWarp(m, 2); // 回廊 0-2
            var reg = new StrategicFleetRegistry(m); reg.Add(imp); reg.Add(ally);

            Assert.AreEqual(0, StrategyRules.FindEncounters(reg).Count);
        }

        [Test]
        public void FindEncounters_StationedFleet_NotCounted()
        {
            var m = MakeMap();
            var imp = new StrategicFleet(1, 0, Faction.帝国, 1f); imp.BeginWarp(m, 1); // 移動中
            var ally = new StrategicFleet(2, 1, Faction.同盟, 1f);                     // 停泊（1に在席）
            var reg = new StrategicFleetRegistry(m); reg.Add(imp); reg.Add(ally);

            Assert.AreEqual(0, StrategyRules.FindEncounters(reg).Count);
        }

        // ───────── 占領（ResolveOccupation）─────────

        [Test]
        public void ResolveOccupation_EnemyUndefended_FlipsOwner()
        {
            var m = MakeMap();
            var ally = new StrategicFleet(5, 1, Faction.同盟); // 帝国星系1に同盟艦が停泊
            var reg = new StrategicFleetRegistry(m); reg.Add(ally);

            Assert.IsTrue(StrategyRules.ResolveOccupation(m, reg, 1));
            Assert.AreEqual(Faction.同盟, m.GetSystem(1).owner);
        }

        [Test]
        public void ResolveOccupation_SameFaction_NoFlip()
        {
            var m = MakeMap();
            var imp = new StrategicFleet(5, 1, Faction.帝国); // 帝国星系に帝国艦
            var reg = new StrategicFleetRegistry(m); reg.Add(imp);

            Assert.IsFalse(StrategyRules.ResolveOccupation(m, reg, 1));
            Assert.AreEqual(Faction.帝国, m.GetSystem(1).owner);
        }

        [Test]
        public void ResolveOccupation_Contested_NoFlip()
        {
            var m = MakeMap();
            var imp = new StrategicFleet(5, 1, Faction.帝国);
            var ally = new StrategicFleet(6, 1, Faction.同盟); // 同一星系に両勢力＝占領未確定
            var reg = new StrategicFleetRegistry(m); reg.Add(imp); reg.Add(ally);

            Assert.IsFalse(StrategyRules.ResolveOccupation(m, reg, 1));
            Assert.AreEqual(Faction.帝国, m.GetSystem(1).owner);
        }

        [Test]
        public void ResolveOccupation_OnlyMovingFleet_NoFlip()
        {
            var m = MakeMap();
            var ally = new StrategicFleet(5, 0, Faction.同盟, 1f); ally.BeginWarp(m, 1); // 1へ移動中（未到着）
            var reg = new StrategicFleetRegistry(m); reg.Add(ally);

            Assert.IsFalse(StrategyRules.ResolveOccupation(m, reg, 1));
            Assert.AreEqual(Faction.帝国, m.GetSystem(1).owner);
        }

        [Test]
        public void ResolveAllOccupations_CountsFlips()
        {
            var m = MakeMap();
            var a1 = new StrategicFleet(5, 1, Faction.同盟); // 星系1占領
            var a2 = new StrategicFleet(6, 2, Faction.同盟); // 星系2占領
            var reg = new StrategicFleetRegistry(m); reg.Add(a1); reg.Add(a2);

            Assert.AreEqual(2, StrategyRules.ResolveAllOccupations(m, reg));
            Assert.AreEqual(Faction.同盟, m.GetSystem(1).owner);
            Assert.AreEqual(Faction.同盟, m.GetSystem(2).owner);
            Assert.AreEqual(Faction.帝国, m.GetSystem(0).owner); // 0 は無人＝据え置き
        }
    }
}
