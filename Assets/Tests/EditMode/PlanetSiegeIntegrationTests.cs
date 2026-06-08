using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 惑星攻城の戦略配線（#131）：StarSystem.planet 有無での占領経路の分岐と、
    /// TickSieges による制空権制圧→侵略→占領＋所有同期、係争中（防衛艦在席）は進めない、を固定する。
    /// </summary>
    public class PlanetSiegeIntegrationTests
    {
        // 0(帝国)・1(同盟) の2星系。0 を攻めるための回廊で接続。
        private GalaxyMap TwoSystems(bool defend0)
        {
            var m = new GalaxyMap();
            var s0 = new StarSystem(0, "Emp", Vector2.zero, Faction.帝国);
            if (defend0) s0.planet = new Planet(0, Faction.帝国, maxOrbitalDefense: 10f, invasionThreshold: 10f);
            m.AddSystem(s0);
            m.AddSystem(new StarSystem(1, "Ally", Vector2.right, Faction.同盟));
            m.AddCorridor(new Corridor(0, 1, 2f));
            return m;
        }

        [Test]
        public void ResolveOccupation_DefendedSystem_DoesNotInstantFlip()
        {
            var m = TwoSystems(defend0: true);
            var reg = new StrategicFleetRegistry(m);
            reg.Add(new StrategicFleet(1, 0, Faction.同盟) { strength = 50 }); // 帝国星系0に同盟艦が停泊

            Assert.IsFalse(StrategyRules.ResolveOccupation(m, reg, 0)); // 防衛惑星は停泊だけでは落ちない
            Assert.AreEqual(Faction.帝国, m.GetSystem(0).owner);
        }

        [Test]
        public void ResolveOccupation_UndefendedSystem_FlipsAsBefore()
        {
            var m = TwoSystems(defend0: false); // planet 無し＝従来動作
            var reg = new StrategicFleetRegistry(m);
            reg.Add(new StrategicFleet(1, 0, Faction.同盟) { strength = 50 });

            Assert.IsTrue(StrategyRules.ResolveOccupation(m, reg, 0)); // 無防備は停泊で占領（後方互換）
            Assert.AreEqual(Faction.同盟, m.GetSystem(0).owner);
        }

        [Test]
        public void TickSieges_HostileBesieger_SuppressesThenCaptures_SyncsSystemOwner()
        {
            var m = TwoSystems(defend0: true);
            var reg = new StrategicFleetRegistry(m);
            reg.Add(new StrategicFleet(1, 0, Faction.同盟) { strength = 5 }); // S-AV戦力5（既定係数）
            Planet p = m.GetSystem(0).planet;

            StrategyRules.TickSieges(m, reg, 1f); // 制圧：def 10→5
            Assert.AreEqual(5f, p.orbitalDefense, 1e-4f);
            Assert.IsFalse(p.DomainDown);

            StrategyRules.TickSieges(m, reg, 1f); // def 5→0 ＝ドメイン・ダウン
            Assert.IsTrue(p.DomainDown);
            Assert.AreEqual(Faction.帝国, m.GetSystem(0).owner); // まだ占領前

            StrategyRules.TickSieges(m, reg, 1f); // 侵略 0→5
            int caps = StrategyRules.TickSieges(m, reg, 1f); // 侵略 5→10 ＝占領
            Assert.AreEqual(1, caps);
            Assert.AreEqual(Faction.同盟, p.owner);
            Assert.AreEqual(Faction.同盟, m.GetSystem(0).owner); // 星系所有も同期
            // 占領で新所有者が再建：制空権 max・侵略値0（再び攻城が要る＝陥落の永続バグ防止）
            Assert.AreEqual(10f, p.orbitalDefense, 1e-4f);
            Assert.AreEqual(0f, p.invasionProgress, 1e-4f);
            Assert.IsFalse(p.Captured);
        }

        [Test]
        public void TickSieges_Contested_DefenderPresent_NoProgress()
        {
            var m = TwoSystems(defend0: true);
            var reg = new StrategicFleetRegistry(m);
            reg.Add(new StrategicFleet(1, 0, Faction.同盟) { strength = 5 }); // 攻撃側
            reg.Add(new StrategicFleet(2, 0, Faction.帝国) { strength = 5 }); // 防衛側（在席）
            Planet p = m.GetSystem(0).planet;

            StrategyRules.TickSieges(m, reg, 1f);
            Assert.AreEqual(10f, p.orbitalDefense, 1e-4f); // 係争中＝攻城は進まない（宇宙の会戦が先）
        }

        [Test]
        public void TickSieges_NoAttacker_NoCapture()
        {
            var m = TwoSystems(defend0: true);
            var reg = new StrategicFleetRegistry(m);
            reg.Add(new StrategicFleet(2, 0, Faction.帝国) { strength = 5 }); // 防衛側のみ
            Planet p = m.GetSystem(0).planet;

            StrategyRules.TickSieges(m, reg, 1f);
            Assert.AreEqual(10f, p.orbitalDefense, 1e-4f);
            Assert.IsFalse(p.Captured);
        }
    }
}
