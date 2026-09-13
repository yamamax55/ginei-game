using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 要塞の駐留艦隊（#40）のセーブ往復と旧セーブ互換。
    ///
    /// 要点：①艦隊と駐留先の対応が保たれる ②旧セーブは駐留なしで読め、施設の守備値が消えない
    /// ③施設の守備値と駐留艦隊を足し合わせない（別勘定）。
    /// </summary>
    public class FortressGarrisonSaveTests
    {
        private static GalaxyMap MapWithFortress(out Corridor choke)
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem { id = 0, systemName = "A", owner = Faction.同盟 });
            map.AddSystem(new StarSystem { id = 1, systemName = "B", owner = Faction.帝国 });
            choke = new Corridor(0, 1, 10f, CorridorType.要衝);
            choke.fortress = new Fortress(1000f, 500f, 1f, true)
            { owner = Faction.同盟, fortressName = "イゼルローン要塞" };
            map.AddCorridor(choke);
            return map;
        }

        [Test]
        public void RoundTrip_KeepsGarrisonRoster()
        {
            GalaxyMap map = MapWithFortress(out Corridor choke);
            choke.fortress.garrisonFleetIds.Add(5);
            choke.fortress.garrisonFleetIds.Add(6);

            var campaign = new CampaignState(map);
            CampaignSaveData save = CampaignSerializer.ToSaveData(campaign);
            CampaignState restored = CampaignSerializer.FromSaveData(save);

            Fortress rf = restored.map.GetCorridor(0, 1).fortress;
            Assert.IsNotNull(rf);
            CollectionAssert.AreEqual(new[] { 5, 6 }, rf.garrisonFleetIds, "駐留艦隊の対応が失われている");
            Assert.AreEqual(1000f, rf.garrisonStrength, 1e-3f, "施設の守備値も保たれること");
        }

        [Test]
        public void LegacySave_WithoutRoster_LoadsEmptyAndKeepsFacilityStrength()
        {
            // 旧セーブ＝駐留名簿のフィールドが無い（JsonUtility が null で返す）。
            var save = new CampaignSaveData();
            save.systems.Add(new StarSystemSave { id = 0, owner = (int)Faction.同盟, name = "A" });
            save.systems.Add(new StarSystemSave { id = 1, owner = (int)Faction.帝国, name = "B" });
            save.corridors.Add(new CorridorSave
            {
                aId = 0, bId = 1, length = 10f, type = (int)CorridorType.要衝,
                hasFortress = true, fortGarrison = 1200f, fortShield = 1f, fortMainGun = 740f,
                fortControlsCorridor = true, fortOwner = (int)Faction.帝国, fortName = "イゼルローン要塞",
                fortGarrisonFleetIds = null,   // ★旧セーブ
            });

            CampaignState restored = CampaignSerializer.FromSaveData(save);
            Fortress rf = restored.map.GetCorridor(0, 1).fortress;

            Assert.IsNotNull(rf.garrisonFleetIds, "null のままだと参照で落ちる");
            Assert.AreEqual(0, rf.garrisonFleetIds.Count, "旧セーブは駐留なしで読む");
            Assert.AreEqual(1200f, rf.garrisonStrength, 1e-3f, "初期守備戦力が黙って消えてはいけない");
            Assert.IsTrue(FortressRules.BlocksPassage(rf), "施設の守備値だけで従来どおり封鎖できること");
        }

        [Test]
        public void FacilityStrengthAndGarrisonFleetsAreCountedSeparately()
        {
            GalaxyMap map = MapWithFortress(out Corridor choke);
            var reg = new StrategicFleetRegistry(map);
            var f5 = new StrategicFleet(5, 0, Faction.同盟) { strength = 200 };
            f5.SetShips(8000);
            reg.Add(f5);
            choke.fortress.garrisonFleetIds.Add(5);

            int ships = FortressGarrisonRules.TotalGarrisonShips(choke.fortress, reg);
            Assert.AreEqual(8000, ships, "駐留艦隊の隻数だけを数えること");
            Assert.AreEqual(1000f, choke.fortress.garrisonStrength, 1e-3f,
                            "施設の守備値は艦隊の数と混ざらない");
        }

        [Test]
        public void RoundTrip_ThenPrune_DropsMissingFleets()
        {
            GalaxyMap map = MapWithFortress(out Corridor choke);
            choke.fortress.garrisonFleetIds.Add(5);
            choke.fortress.garrisonFleetIds.Add(99);   // 存在しない艦隊（全滅済みの想定）

            var reg = new StrategicFleetRegistry(map);
            reg.Add(new StrategicFleet(5, 0, Faction.同盟) { strength = 100 });

            FortressGarrisonRules.PruneMissing(choke.fortress, reg.GetFleet);

            CollectionAssert.AreEqual(new[] { 5 }, choke.fortress.garrisonFleetIds,
                                      "いない艦隊のIDが名簿に残ってはいけない");
        }

        [Test]
        public void EmptyGarrison_RoundTripsAsEmpty()
        {
            GalaxyMap map = MapWithFortress(out _);
            var campaign = new CampaignState(map);
            CampaignSaveData save = CampaignSerializer.ToSaveData(campaign);
            CampaignState restored = CampaignSerializer.FromSaveData(save);

            Fortress rf = restored.map.GetCorridor(0, 1).fortress;
            Assert.IsNotNull(rf.garrisonFleetIds);
            Assert.AreEqual(0, rf.garrisonFleetIds.Count);
        }
    }
}
