using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 全永続化（continue の土台）：財政（国庫/税率/予算/債務）・戦略艦隊（盤面の駒）・統一時間が往復で保存復元されるか。
    /// </summary>
    public class CampaignSaveFullPersistTests
    {
        // ===== 財政（FactionState） =====

        [Test]
        public void Fiscal_RoundTrips()
        {
            var c = new CampaignState();
            var fs = new FactionState(Faction.帝国, 0.5f);
            fs.treasury = 500f;
            fs.taxRate = 0.4f;
            fs.budget.military = 120f; fs.budget.welfare = 30f;
            fs.fiscal.debt = 250f;
            c.states.Add(fs);

            CampaignState r = CampaignSerializer.FromSaveData(CampaignSerializer.ToSaveData(c));
            FactionState rf = r.states[0];
            Assert.AreEqual(500f, rf.treasury, 1e-3f);
            Assert.AreEqual(0.4f, rf.taxRate, 1e-3f);
            Assert.AreEqual(120f, rf.budget.military, 1e-3f);
            Assert.AreEqual(30f, rf.budget.welfare, 1e-3f);
            Assert.AreEqual(250f, rf.fiscal.debt, 1e-3f);
        }

        // ===== 戦略艦隊（盤面の駒） =====

        private static GalaxyMap TwoSystemMap()
        {
            var m = new GalaxyMap();
            m.AddSystem(new StarSystem(0, "A", Vector2.zero, Faction.帝国));
            m.AddSystem(new StarSystem(1, "B", new Vector2(1f, 0f), Faction.同盟));
            m.AddCorridor(new Corridor(0, 1, 4f));
            return m;
        }

        [Test]
        public void Fleet_StationaryRoundTrips()
        {
            var map = TwoSystemMap();
            var reg = new StrategicFleetRegistry(map);
            reg.Add(new StrategicFleet(7, 1, Faction.同盟, 1.5f) { strength = 80, supply = 0.5f, engaged = true });

            var save = new CampaignSaveData();
            CampaignSerializer.WriteFleets(save, reg);
            StrategicFleetRegistry r = CampaignSerializer.ReadFleets(save, map);

            Assert.AreEqual(1, r.fleets.Count);
            StrategicFleet f = r.GetFleet(7);
            Assert.IsNotNull(f);
            Assert.AreEqual(Faction.同盟, f.faction);
            Assert.AreEqual(80, f.strength);
            Assert.AreEqual(0.5f, f.supply, 1e-3f);
            Assert.AreEqual(1, f.currentSystemId);
            Assert.IsTrue(f.engaged);
            Assert.IsFalse(f.IsOnCorridor); // 停泊
        }

        [Test]
        public void Fleet_MovingReWarpsOnLoad()
        {
            var map = TwoSystemMap();
            var reg = new StrategicFleetRegistry(map);
            var f = new StrategicFleet(3, 0, Faction.帝国, 1f) { strength = 60 };
            f.WarpTo(map, 1); // 移動中
            reg.Add(f);
            Assert.IsTrue(f.IsMoving);

            var save = new CampaignSaveData();
            CampaignSerializer.WriteFleets(save, reg);
            StrategicFleet r = CampaignSerializer.ReadFleets(save, map).GetFleet(3);
            Assert.IsNotNull(r);
            Assert.AreEqual(60, r.strength);
            Assert.IsTrue(r.IsOnCorridor); // ロードで目的地へ再ワープ
        }

        // ===== 統一時間 =====

        [Test]
        public void Clock_RoundTrips()
        {
            var clock = new GameClock { elapsedSeconds = 12345.0, speed = 3f };
            var save = new CampaignSaveData();
            CampaignSerializer.WriteClock(save, clock);
            GameClock r = CampaignSerializer.ReadClock(save);
            Assert.AreEqual(12345.0, r.elapsedSeconds, 1e-6);
            Assert.AreEqual(3f, r.speed, 1e-3f);
        }

        // ===== 全部入りの JSON 往復 =====

        [Test]
        public void FullFile_RoundTrips_ThroughJson()
        {
            var c = new CampaignState(TwoSystemMap());
            var fs = new FactionState(Faction.帝国, 0.6f) { treasury = 999f, governmentForm = GovernmentForm.共産主義 };
            fs.fiscal.debt = 42f;
            c.states.Add(fs);
            var reg = new StrategicFleetRegistry(c.map);
            reg.Add(new StrategicFleet(1, 0, Faction.帝国, 1f) { strength = 50 });
            var people = new System.Collections.Generic.List<Person> { new Person(9, "提督", Faction.帝国, PersonRole.軍人) { rankTier = 8 } };

            var save = CampaignSerializer.ToSaveData(c);
            CampaignSerializer.WritePeople(save, people);
            CampaignSerializer.WriteFleets(save, reg);
            CampaignSerializer.WriteClock(save, new GameClock { elapsedSeconds = 777.0, speed = 2f });

            string json = JsonUtility.ToJson(save);
            CampaignSaveData parsed = CampaignSerializer.Parse(json);
            CampaignState rc = CampaignSerializer.FromSaveData(parsed);

            Assert.AreEqual(GovernmentForm.共産主義, rc.states[0].governmentForm);
            Assert.AreEqual(999f, rc.states[0].treasury, 1e-3f);
            Assert.AreEqual(42f, rc.states[0].fiscal.debt, 1e-3f);
            Assert.AreEqual(1, CampaignSerializer.ReadPeople(parsed).Count);
            Assert.AreEqual(1, CampaignSerializer.ReadFleets(parsed, rc.map).fleets.Count);
            Assert.AreEqual(777.0, CampaignSerializer.ReadClock(parsed).elapsedSeconds, 1e-6);
        }
    }
}
