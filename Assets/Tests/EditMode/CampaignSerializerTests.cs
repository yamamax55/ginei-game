using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦役セーブ（FND-2 #495・CampaignState ↔ バージョン付きJSON）を固定する：盤面（星系/回廊/惑星）と勢力国家状態の
    /// 往復が値を保つこと、schemaVersion が入ること、空/不正JSONが安全に null を返すこと。
    /// </summary>
    public class CampaignSerializerTests
    {
        private static CampaignState BuildSample()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(0, "首都星", new Vector2(1f, 2f), Faction.帝国)
            {
                habitable = true, isColonized = true, systemType = SystemType.工業
            });
            var frontier = new StarSystem(1, "辺境星", new Vector2(3f, 4f), Faction.同盟)
            {
                habitable = true, isColonized = false, systemType = SystemType.鉱業
            };
            frontier.planet = new Planet(1, Faction.同盟, maxOrbitalDefense: 100f, invasionThreshold: 50f)
            {
                orbitalDefense = 40f, invasionProgress = 10f
            };
            map.AddSystem(frontier);
            map.AddCorridor(new Corridor(0, 1, 2.5f, CorridorType.要衝));

            var c = new CampaignState(map);
            var fs = new FactionState(Faction.帝国, 0.7f);
            fs.regime.legitimacy = 0.8f; fs.regime.corruption = 0.2f; fs.regime.virtue = 0.6f;
            fs.polity.population = 500000; fs.polity.rulerForce = 8000;
            fs.polity.cooperation = 0.55f; fs.polity.oppression = 0.3f;
            fs.organization.cohesion = 0.9f; fs.organization.fragmented = false;
            fs.community.hope = 0.65f; fs.community.dissent = true;
            c.states.Add(fs);
            return c;
        }

        [Test]
        public void RoundTrip_PreservesGalaxyAndPlanet()
        {
            string json = CampaignSerializer.ToJson(BuildSample());
            CampaignState r = CampaignSerializer.FromJson(json);

            Assert.IsNotNull(r);
            Assert.AreEqual(2, r.map.systems.Count);
            StarSystem s0 = r.map.GetSystem(0);
            Assert.AreEqual("首都星", s0.systemName);
            Assert.AreEqual(Faction.帝国, s0.owner);
            Assert.AreEqual(SystemType.工業, s0.systemType);
            Assert.AreEqual(1f, s0.position.x, 1e-4f);

            StarSystem s1 = r.map.GetSystem(1);
            Assert.IsFalse(s1.isColonized);
            Assert.IsNotNull(s1.planet);
            Assert.AreEqual(40f, s1.planet.orbitalDefense, 1e-4f);
            Assert.AreEqual(50f, s1.planet.invasionThreshold, 1e-4f);

            Assert.AreEqual(1, r.map.corridors.Count);
            Assert.AreEqual(CorridorType.要衝, r.map.corridors[0].type);
            Assert.AreEqual(2.5f, r.map.corridors[0].length, 1e-4f);
        }

        [Test]
        public void RoundTrip_PreservesFactionState()
        {
            string json = CampaignSerializer.ToJson(BuildSample());
            CampaignState r = CampaignSerializer.FromJson(json);

            FactionState fs = CampaignRules.GetState(r, Faction.帝国);
            Assert.IsNotNull(fs);
            Assert.AreEqual(0.7f, fs.inclusiveness, 1e-4f);
            Assert.AreEqual(0.8f, fs.regime.legitimacy, 1e-4f);
            Assert.AreEqual(0.2f, fs.regime.corruption, 1e-4f);
            Assert.AreEqual(500000, fs.polity.population);
            Assert.AreEqual(0.3f, fs.polity.oppression, 1e-4f);
            Assert.AreEqual(0.9f, fs.organization.cohesion, 1e-4f);
            Assert.AreEqual(0.65f, fs.community.hope, 1e-4f);
            Assert.IsTrue(fs.community.dissent);
        }

        [Test]
        public void Save_IncludesSchemaVersion()
        {
            CampaignSaveData save = CampaignSerializer.ToSaveData(BuildSample());
            Assert.AreEqual(CampaignSerializer.SchemaVersion, save.schemaVersion);

            string json = CampaignSerializer.ToJson(BuildSample());
            StringAssert.Contains("schemaVersion", json);
        }

        [Test]
        public void RoundTrip_RestoredStateStillTicks()
        {
            // 復元した状態がそのまま CampaignRules で時間進行できる（モデルとして生きている）
            var r = CampaignSerializer.FromJson(CampaignSerializer.ToJson(BuildSample()));
            FactionState fs = CampaignRules.GetState(r, Faction.帝国);
            fs.regime.virtue = 0f;
            float before = fs.regime.corruption;
            CampaignRules.Tick(r, 1f);
            Assert.Greater(fs.regime.corruption, before); // 腐敗が進む＝生きた状態
        }

        [Test]
        public void Parse_EmptyOrInvalid_ReturnsNullSafely()
        {
            Assert.IsNull(CampaignSerializer.FromJson(null));
            Assert.IsNull(CampaignSerializer.FromJson(""));
            Assert.IsNull(CampaignSerializer.Parse(null));
        }

        [Test]
        public void FromSaveData_NullStillBuildsEmptyState()
        {
            CampaignState c = CampaignSerializer.FromSaveData(null);
            Assert.IsNotNull(c);
            Assert.IsNotNull(c.map);
            Assert.AreEqual(0, c.states.Count);
        }
    }
}
