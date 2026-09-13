using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 艦艇数のセーブ往復と、旧セーブの読込互換。
    /// 旧セーブのファイル自体は書き換えず、<b>読み込み時に兵力から導出して埋める</b>方針を固定する。
    /// </summary>
    public class FleetShipCountSaveTests
    {
        private static GalaxyMap TwoSystems()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem { id = 0, systemName = "A", owner = Faction.同盟 });
            map.AddSystem(new StarSystem { id = 1, systemName = "B", owner = Faction.同盟 });
            map.AddCorridor(new Corridor(0, 1, 5f, CorridorType.通商));
            return map;
        }

        [Test]
        public void RoundTrip_KeepsPerFleetShipCount()
        {
            GalaxyMap map = TwoSystems();
            var reg = new StrategicFleetRegistry(map);
            reg.Add(new StrategicFleet(5, 0, Faction.同盟) { strength = 228, shipCount = 9120 });
            reg.Add(new StrategicFleet(6, 1, Faction.同盟) { strength = 159, shipCount = 4321 });

            var save = new CampaignSaveData();
            CampaignSerializer.WriteFleets(save, reg);
            StrategicFleetRegistry restored = CampaignSerializer.ReadFleets(save, map);

            Assert.AreEqual(9120, restored.GetFleet(5).Ships);
            Assert.AreEqual(4321, restored.GetFleet(6).Ships, "艦隊ごとに別々の隻数が保たれること");
            Assert.AreEqual(228, restored.GetFleet(5).strength);
            Assert.AreEqual(159, restored.GetFleet(6).strength);
        }

        [Test]
        public void LegacySave_WithoutShipCount_DerivesFromStrength()
        {
            // 旧セーブ＝shipCount フィールドが無い（JsonUtility が 0 で埋める）。
            var save = new CampaignSaveData();
            save.fleets.Add(new StrategicFleetSave
            {
                id = 5, faction = (int)Faction.同盟, strength = 228,
                warpSpeed = 1f, sublightFactor = 0.35f, currentSystemId = 0,
            });

            StrategicFleetRegistry restored = CampaignSerializer.ReadFleets(save, TwoSystems());
            StrategicFleet f = restored.GetFleet(5);

            Assert.AreEqual(FleetShipCountRules.FromStrength(228), f.Ships, "兵力から導出して埋める");
            Assert.Greater(f.Ships, 0, "旧セーブを読んで0隻になってはいけない");
        }

        [Test]
        public void UninitializedFleet_ReportsDerivedShips()
        {
            // 盤面で新しく作った艦隊（shipCount 未設定）も 0 隻にならない。
            var f = new StrategicFleet(9, 0, Faction.帝国) { strength = 130 };
            Assert.AreEqual(FleetShipCountRules.FromStrength(130), f.Ships);
        }

        [Test]
        public void Write_PersistsDerivedValue_ForUninitializedFleet()
        {
            GalaxyMap map = TwoSystems();
            var reg = new StrategicFleetRegistry(map);
            reg.Add(new StrategicFleet(7, 0, Faction.同盟) { strength = 100 });  // shipCount 未設定

            var save = new CampaignSaveData();
            CampaignSerializer.WriteFleets(save, reg);

            Assert.AreEqual(FleetShipCountRules.FromStrength(100), save.fleets[0].shipCount,
                            "保存時点で導出値を書き出す＝次回以降は導出に頼らない");
        }
    }
}
