using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 回廊要塞の<b>移動実行</b>とセーブ往復（#40 C-7）。
    /// 「経路探索だけ直しても、実際に動かすと素通りできる」を防ぐための統合的な回帰。
    /// </summary>
    public class FortressPassageIntegrationTests
    {
        private static GalaxyMap TwoSystems(Faction fortressOwner, float garrison, out Corridor choke)
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem { id = 0, owner = Faction.同盟 });
            map.AddSystem(new StarSystem { id = 1, owner = Faction.帝国 });
            choke = new Corridor(0, 1, 10f, CorridorType.要衝);
            choke.fortress = new Fortress(garrison, 100f, 1f, true)
            { owner = fortressOwner, fortressName = "イゼルローン要塞" };
            map.AddCorridor(choke);
            return map;
        }

        private static StrategicFleet Fleet(Faction f, int systemId, int strength = 5000)
            => new StrategicFleet { id = 1, faction = f, currentSystemId = systemId, strength = strength };

        // ── 敵は素通りできない ──

        [Test]
        public void HostileFleet_NeverArrives_WhileFortressHolds()
        {
            var map = TwoSystems(Faction.帝国, 1000f, out _);
            StrategicFleet f = Fleet(Faction.同盟, 0);
            Assert.IsTrue(f.BeginWarp(map, 1));

            // 十分すぎる時間を進めても反対側の星系には着かない。
            for (int i = 0; i < 2000; i++) f.Tick(map, 0.5f);

            Assert.AreEqual(0, f.currentSystemId, "要塞を無視して反対側へ着いてしまった");
            Assert.IsTrue(f.IsOnCorridor);
            Assert.IsTrue(f.IsBlockadedByFortress);
        }

        [Test]
        public void HostileFleet_IsNotStoppedByHugeSingleStep()
        {
            // 1回の巨大な dt（ヒッチ・倍速）でも回廊を飛び越えられない。
            var map = TwoSystems(Faction.帝国, 1000f, out _);
            StrategicFleet f = Fleet(Faction.同盟, 0);
            f.BeginWarp(map, 1);

            f.Tick(map, 100000f);

            Assert.AreEqual(0, f.currentSystemId);
            Assert.IsTrue(f.IsOnCorridor);
        }

        [Test]
        public void OwnerFleet_PassesFreely()
        {
            var map = TwoSystems(Faction.帝国, 1000f, out _);
            StrategicFleet f = Fleet(Faction.帝国, 0);
            Assert.IsTrue(f.BeginWarp(map, 1));

            bool arrived = false;
            for (int i = 0; i < 2000 && !arrived; i++) arrived = f.Tick(map, 0.5f);

            Assert.IsTrue(arrived);
            Assert.AreEqual(1, f.currentSystemId);
            Assert.IsFalse(f.IsBlockadedByFortress);
        }

        [Test]
        public void CommerceCorridor_NoFortress_PassesFreely()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem { id = 0, owner = Faction.同盟 });
            map.AddSystem(new StarSystem { id = 1, owner = Faction.帝国 });
            map.AddCorridor(new Corridor(0, 1, 10f, CorridorType.通商)); // フェザーン型＝要塞なし

            StrategicFleet f = Fleet(Faction.同盟, 0);
            f.BeginWarp(map, 1);
            bool arrived = false;
            for (int i = 0; i < 2000 && !arrived; i++) arrived = f.Tick(map, 0.5f);

            Assert.IsTrue(arrived);
            Assert.AreEqual(1, f.currentSystemId);
        }

        // ── 制圧すると同じ艦隊がその場から先へ進める ──

        [Test]
        public void CapturingFortress_ReleasesTheStalledFleet()
        {
            var map = TwoSystems(Faction.帝国, 1000f, out Corridor choke);
            StrategicFleet f = Fleet(Faction.同盟, 0, 100000);
            f.BeginWarp(map, 1);
            for (int i = 0; i < 200; i++) f.Tick(map, 0.5f);
            Assert.AreEqual(0, f.currentSystemId, "前提：まだ足止めされている");

            StrategyRules.AssaultFortress(choke.fortress, Faction.同盟, f.strength); // 制圧

            bool arrived = false;
            for (int i = 0; i < 2000 && !arrived; i++) arrived = f.Tick(map, 0.5f);

            Assert.IsTrue(arrived, "制圧後も通れないままだった");
            Assert.AreEqual(1, f.currentSystemId);
        }

        [Test]
        public void RegarrisonMidTransit_StopsAFleetThatWasPassing()
        {
            // 通っている最中に要塞が敵の手に渡れば、その場で止まる（通行状態の更新が移動へ効く）。
            var map = TwoSystems(Faction.同盟, 1000f, out Corridor choke); // 最初は自軍の要塞＝通れる
            StrategicFleet f = Fleet(Faction.同盟, 0);
            f.BeginWarp(map, 1);
            for (int i = 0; i < 3; i++) f.Tick(map, 0.5f);
            Assert.IsFalse(f.IsBlockadedByFortress);

            FortressBlockadeRules.Regarrison(choke.fortress, Faction.帝国, 1000f); // 敵が奪って守備を置く

            for (int i = 0; i < 2000; i++) f.Tick(map, 0.5f);
            Assert.AreEqual(0, f.currentSystemId, "奪われた要塞を素通りしてしまった");
            Assert.IsTrue(f.IsBlockadedByFortress);
        }

        // ── 経路計画（迂回路があるならそちらを通り、無ければ制圧しに行く）──

        [Test]
        public void WarpTo_PrefersCommerceDetour_WhenOneExists()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem { id = 0, owner = Faction.同盟 });
            map.AddSystem(new StarSystem { id = 1, owner = Faction.帝国 });
            map.AddSystem(new StarSystem { id = 2, owner = Faction.同盟 });
            var choke = new Corridor(0, 1, 1f, CorridorType.要衝);
            choke.fortress = new Fortress(1000f, 100f, 1f, true) { owner = Faction.帝国 };
            map.AddCorridor(choke);
            map.AddCorridor(new Corridor(0, 2, 5f, CorridorType.通商));
            map.AddCorridor(new Corridor(2, 1, 5f, CorridorType.通商));

            StrategicFleet f = Fleet(Faction.同盟, 0);
            Assert.IsTrue(f.WarpTo(map, 1));
            Assert.AreEqual(2, f.destinationSystemId, "最短でも要塞回廊ではなく通商回廊を選ぶこと");
        }

        [Test]
        public void WarpTo_StillAcceptsOrder_WhenOnlyRouteIsTheFortress()
        {
            // 迂回路が無ければ命令は受理し、要塞へ向かわせる（そこで力攻めが起きる）。
            var map = TwoSystems(Faction.帝国, 1000f, out _);
            StrategicFleet f = Fleet(Faction.同盟, 0);
            Assert.IsTrue(f.WarpTo(map, 1));
            Assert.AreEqual(1, f.destinationSystemId);
            Assert.IsTrue(f.IsOnCorridor);
        }

        // ── セーブ往復（要塞が消えない・旧セーブは要塞なしで読める）──

        [Test]
        public void SaveRoundTrip_KeepsFortressState()
        {
            var map = TwoSystems(Faction.帝国, 777f, out Corridor choke);
            choke.fortress.shieldIntegrity = 0.4f;
            choke.fortress.mainGunPower = 640f;

            var campaign = new CampaignState(map);
            CampaignSaveData save = CampaignSerializer.ToSaveData(campaign);
            CampaignState restored = CampaignSerializer.FromSaveData(save);

            Corridor rc = restored.map.GetCorridor(0, 1);
            Assert.IsNotNull(rc.fortress, "ロードで要塞が消えた＝封鎖が失われる");
            Assert.AreEqual(Faction.帝国, rc.fortress.owner);
            Assert.AreEqual(777f, rc.fortress.garrisonStrength, 1e-3f);
            Assert.AreEqual(0.4f, rc.fortress.shieldIntegrity, 1e-3f);
            Assert.AreEqual(640f, rc.fortress.mainGunPower, 1e-3f);
            Assert.AreEqual("イゼルローン要塞", rc.fortress.fortressName);
            Assert.IsTrue(FortressBlockadeRules.Blocks(rc, Faction.同盟));
        }

        [Test]
        public void SaveRoundTrip_LegacySaveWithoutFortress_LoadsAsCommerceCorridor()
        {
            // 旧セーブ（hasFortress を持たない）＝要塞なしとして読め、通行を妨げない。
            var save = new CampaignSaveData();
            save.systems.Add(new StarSystemSave { id = 0, owner = (int)Faction.同盟, name = "A" });
            save.systems.Add(new StarSystemSave { id = 1, owner = (int)Faction.帝国, name = "B" });
            save.corridors.Add(new CorridorSave { aId = 0, bId = 1, length = 10f, type = (int)CorridorType.要衝 });

            CampaignState restored = CampaignSerializer.FromSaveData(save);
            Corridor rc = restored.map.GetCorridor(0, 1);

            Assert.IsNull(rc.fortress);
            Assert.IsTrue(FortressBlockadeRules.IsCommerceCorridor(rc));
            Assert.IsFalse(FortressBlockadeRules.Blocks(rc, Faction.同盟));
        }
    }
}
