using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 回廊要塞の<b>駐留艦隊</b>（<see cref="FortressGarrisonRules"/>）：
    /// 要塞施設（<see cref="Fortress.garrisonStrength"/>）と実在の艦隊（<see cref="Fortress.garrisonFleetIds"/>）を
    /// 分けること・二重計上しないこと・占領で敵艦隊が味方にならないこと・旧セーブ互換を固定する。
    /// </summary>
    public class FortressGarrisonRulesTests
    {
        // ── 素材 ──

        private static Fortress Fort(Faction owner, float garrison = 1000f)
            => new Fortress(garrison, 100f, 1f, true) { owner = owner, fortressName = "イゼルローン" };

        /// <summary>0 ─ 1 の1本道。回廊に要塞を据える。</summary>
        private static GalaxyMap MapWithFortress(Fortress f, out Corridor corridor)
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem { id = 0, owner = Faction.帝国 });
            map.AddSystem(new StarSystem { id = 1, owner = Faction.同盟 });
            corridor = new Corridor(0, 1, 1f, CorridorType.要衝) { fortress = f };
            map.AddCorridor(corridor);
            return map;
        }

        private static StrategicFleet Fleet(int id, Faction faction, int systemId = 0, int strength = 100)
            => new StrategicFleet(id, systemId, faction) { strength = strength };

        private static System.Func<int, StrategicFleet> Resolver(params StrategicFleet[] fleets)
        {
            var dict = new Dictionary<int, StrategicFleet>();
            for (int i = 0; i < fleets.Length; i++) dict[fleets[i].id] = fleets[i];
            return id => dict.TryGetValue(id, out StrategicFleet f) ? f : null;
        }

        // ── 可否判定 ──

        [Test]
        public void CanGarrison_OwnFortress_Allowed()
        {
            Fortress f = Fort(Faction.帝国);
            StrategicFleet fleet = Fleet(1, Faction.帝国);
            Assert.AreEqual(GarrisonRejection.なし, FortressGarrisonRules.CanGarrison(f, fleet, Faction.帝国));
        }

        [Test]
        public void CanGarrison_EnemyFortress_Rejected()
        {
            Fortress f = Fort(Faction.同盟);
            StrategicFleet fleet = Fleet(1, Faction.帝国);
            Assert.AreEqual(GarrisonRejection.敵の要塞, FortressGarrisonRules.CanGarrison(f, fleet, Faction.帝国));
            Assert.IsFalse(FortressGarrisonRules.Garrison(f, fleet, Faction.帝国));
            Assert.AreEqual(0, FortressGarrisonRules.GarrisonFleetCount(f)); // 名簿は汚れない
        }

        [Test]
        public void CanGarrison_OwnFortress_AllowedEvenWhenFriendlyDisallowed()
        {
            // allowFriendly=false は「自勢力の要塞のみ」＝同一勢力なら通る。
            Fortress own = Fort(Faction.帝国);
            StrategicFleet mine = Fleet(2, Faction.帝国);
            var strict = new FortressGarrisonParams(4, false, true);
            Assert.AreEqual(GarrisonRejection.なし, FortressGarrisonRules.CanGarrison(own, mine, Faction.帝国, strict));

            // 2勢力 enum のみの盤面では他勢力＝敵対（FactionRelations の既定判定）＝どちらの設定でも拒否。
            Fortress others = Fort(Faction.同盟);
            Assert.AreEqual(GarrisonRejection.敵の要塞, FortressGarrisonRules.CanGarrison(others, mine, Faction.帝国, strict));
            Assert.AreEqual(GarrisonRejection.敵の要塞, FortressGarrisonRules.CanGarrison(others, mine, Faction.帝国));
        }

        [Test]
        public void CanGarrison_NotPlayersFleet_Rejected()
        {
            Fortress f = Fort(Faction.同盟);
            StrategicFleet enemy = Fleet(1, Faction.同盟);
            // 同盟の要塞に同盟の艦隊＝本来は駐留できるが、操作勢力は帝国＝他勢力の駒は動かせない。
            Assert.AreEqual(GarrisonRejection.他勢力の艦隊,
                            FortressGarrisonRules.CanGarrison(f, enemy, Faction.帝国));
        }

        [Test]
        public void CanGarrison_NullArguments_Safe()
        {
            Assert.AreEqual(GarrisonRejection.艦隊が無い, FortressGarrisonRules.CanGarrison(Fort(Faction.帝国), null, Faction.帝国));
            Assert.AreEqual(GarrisonRejection.要塞が無い, FortressGarrisonRules.CanGarrison(null, Fleet(1, Faction.帝国), Faction.帝国));
            Assert.AreEqual(0, FortressGarrisonRules.GarrisonFleetCount(null));
            Assert.AreEqual(0, FortressGarrisonRules.GarrisonFleetIds(null).Count);
            Assert.IsFalse(FortressGarrisonRules.IsGarrisonedIn(null, 1));
            Assert.IsFalse(FortressGarrisonRules.Sortie(null, 1));
            Assert.IsFalse(FortressGarrisonRules.SortieFrom(null, null));
            Assert.AreEqual(0, FortressGarrisonRules.TotalGarrisonShips(null, (System.Func<int, StrategicFleet>)null));
            Assert.AreEqual(0, FortressGarrisonRules.TotalGarrisonStrength(null, (System.Func<int, StrategicFleet>)null));
            Assert.AreEqual(0, FortressGarrisonRules.ClearGarrison(null).Count);
            Assert.AreEqual(0, FortressGarrisonRules.OnCaptured(null, Faction.帝国, null).Count);
            Assert.AreEqual(0f, FortressGarrisonRules.FacilityGarrisonStrength(null), 1e-4f);
            Assert.IsFalse(FortressGarrisonRules.IsLegacyGarrisonOnly(null));
            Assert.AreEqual("", FortressGarrisonRules.GarrisonSummaryText(null, null));
            Assert.IsNull(FortressGarrisonRules.FindCorridorOf(null, null));
            Assert.IsNull(FortressGarrisonRules.FindGarrison(null, 1));
            Assert.AreEqual(0, FortressGarrisonRules.ExcludeGarrisoned(null, null).Count);
            Assert.AreEqual(0, FortressGarrisonRules.PruneMissing(null, null));
        }

        [Test]
        public void CanGarrison_OnCorridor_Engaged_Reinforcing_Rejected()
        {
            Fortress f = Fort(Faction.帝国);
            GalaxyMap map = MapWithFortress(f, out _);

            // 回廊上（航行中）＝入港できない
            StrategicFleet moving = Fleet(1, Faction.帝国);
            Assert.IsTrue(moving.BeginWarp(map, 1));
            Assert.AreEqual(GarrisonRejection.回廊上にいる, FortressGarrisonRules.CanGarrison(f, moving, Faction.帝国));

            // 交戦中
            StrategicFleet engaged = Fleet(2, Faction.帝国);
            engaged.engaged = true;
            Assert.AreEqual(GarrisonRejection.交戦中, FortressGarrisonRules.CanGarrison(f, engaged, Faction.帝国));

            // 増援航行中（#38）
            StrategicFleet warping = Fleet(3, Faction.帝国);
            warping.warpingAsReinforcement = true;
            Assert.AreEqual(GarrisonRejection.増援航行中, FortressGarrisonRules.CanGarrison(f, warping, Faction.帝国));
        }

        [Test]
        public void CanGarrison_FarFromFortress_Rejected_WhenMapGiven()
        {
            Fortress f = Fort(Faction.帝国);
            GalaxyMap map = MapWithFortress(f, out _);
            map.AddSystem(new StarSystem { id = 2, owner = Faction.帝国 });
            map.AddCorridor(new Corridor(1, 2, 1f));

            StrategicFleet far = Fleet(1, Faction.帝国, 2);            // 要塞の回廊(0-1)に接していない
            Assert.AreEqual(GarrisonRejection.要塞から遠い, FortressGarrisonRules.CanGarrison(map, f, far, Faction.帝国));
            // map を渡さなければ隣接は見ない（要塞と艦隊だけの判定）。
            Assert.AreEqual(GarrisonRejection.なし, FortressGarrisonRules.CanGarrison(f, far, Faction.帝国));

            StrategicFleet near = Fleet(2, Faction.帝国, 1);            // 回廊の反対側の端でも可
            Assert.AreEqual(GarrisonRejection.なし, FortressGarrisonRules.CanGarrison(map, f, near, Faction.帝国));
        }

        [Test]
        public void CanGarrison_Full_Rejected()
        {
            Fortress f = Fort(Faction.帝国);
            var p = new FortressGarrisonParams(2, true, true);
            Assert.IsTrue(FortressGarrisonRules.Garrison(null, f, Fleet(1, Faction.帝国), Faction.帝国, p, out _));
            Assert.IsTrue(FortressGarrisonRules.Garrison(null, f, Fleet(2, Faction.帝国), Faction.帝国, p, out _));
            Assert.AreEqual(GarrisonRejection.満員,
                            FortressGarrisonRules.CanGarrison(null, f, Fleet(3, Faction.帝国), Faction.帝国, p));
            Assert.AreEqual(2, FortressGarrisonRules.GarrisonFleetCount(f));
        }

        [Test]
        public void Params_Default_And_Clamp()
        {
            FortressGarrisonParams d = FortressGarrisonParams.Default;
            Assert.AreEqual(4, d.maxFleets);
            Assert.IsTrue(d.allowFriendly);
            Assert.IsTrue(d.releaseHostileOnCapture);
            Assert.AreEqual(1, new FortressGarrisonParams(0, true, true).maxFleets);   // 下限1へクランプ
            Assert.AreEqual(1, new FortressGarrisonParams(-5, true, true).maxFleets);
        }

        // ── 駐留・出撃 ──

        [Test]
        public void Garrison_Twice_DoesNotDuplicate()
        {
            Fortress f = Fort(Faction.帝国);
            StrategicFleet fleet = Fleet(7, Faction.帝国);
            Assert.IsTrue(FortressGarrisonRules.Garrison(f, fleet, Faction.帝国));
            Assert.IsFalse(FortressGarrisonRules.Garrison(f, fleet, Faction.帝国, out GarrisonRejection reason));
            Assert.AreEqual(GarrisonRejection.すでに駐留中, reason);
            Assert.AreEqual(1, FortressGarrisonRules.GarrisonFleetCount(f));   // 二重登録されない
            Assert.AreEqual(1, FortressGarrisonRules.GarrisonFleetIds(f).Count);
            Assert.AreEqual(7, FortressGarrisonRules.GarrisonFleetIds(f)[0]);
        }

        [Test]
        public void Garrison_SecondFortress_Rejected_WhenMapGiven()
        {
            Fortress a = Fort(Faction.帝国);
            Fortress b = Fort(Faction.帝国);
            GalaxyMap map = MapWithFortress(a, out _);
            map.AddSystem(new StarSystem { id = 2, owner = Faction.帝国 });
            map.AddCorridor(new Corridor(0, 2, 1f, CorridorType.要衝) { fortress = b });

            StrategicFleet fleet = Fleet(1, Faction.帝国);
            Assert.IsTrue(FortressGarrisonRules.Garrison(map, a, fleet, Faction.帝国, out _));
            Assert.AreEqual(GarrisonRejection.他の要塞に駐留中,
                            FortressGarrisonRules.CanGarrison(map, b, fleet, Faction.帝国));
            Assert.AreEqual(0, FortressGarrisonRules.GarrisonFleetCount(b));
        }

        [Test]
        public void Sortie_RemovesFromGarrison_AndFleetCanMoveAgain()
        {
            Fortress f = Fort(Faction.帝国);
            GalaxyMap map = MapWithFortress(f, out _);
            StrategicFleet fleet = Fleet(1, Faction.帝国);

            Assert.IsTrue(FortressGarrisonRules.Garrison(map, f, fleet, Faction.帝国, out _));
            Assert.IsTrue(FortressGarrisonRules.IsGarrisoned(map, fleet));
            Assert.AreSame(f, FortressGarrisonRules.FindGarrison(map, fleet.id, out Corridor c));
            Assert.AreEqual(0, c.aId);
            Assert.AreEqual(1, c.bId);

            // 駐留中もその場で待機＝停泊したまま（艦隊側の状態は書き換えていない）。
            Assert.IsFalse(fleet.IsOnCorridor);
            Assert.AreEqual(Faction.帝国, fleet.faction);

            // 出撃＝名簿から外れ、以後は通常どおり移動できる。
            Assert.IsTrue(FortressGarrisonRules.Sortie(f, fleet));
            Assert.IsFalse(FortressGarrisonRules.IsGarrisoned(map, fleet));
            Assert.AreEqual(0, FortressGarrisonRules.GarrisonFleetCount(f));
            Assert.IsTrue(fleet.BeginWarp(map, 1));
            Assert.IsTrue(fleet.IsOnCorridor);

            Assert.IsFalse(FortressGarrisonRules.Sortie(f, fleet));   // 二度目は false
        }

        [Test]
        public void SortieFrom_FindsFortressAnywhereOnMap()
        {
            Fortress f = Fort(Faction.帝国);
            GalaxyMap map = MapWithFortress(f, out _);
            StrategicFleet fleet = Fleet(5, Faction.帝国);
            Assert.IsTrue(FortressGarrisonRules.Garrison(map, f, fleet, Faction.帝国, out _));

            Assert.IsTrue(FortressGarrisonRules.SortieFrom(map, fleet, out Fortress from));
            Assert.AreSame(f, from);
            Assert.IsFalse(FortressGarrisonRules.SortieFrom(map, fleet, out _)); // もう駐留していない
        }

        // ── 二重計上しない ──

        [Test]
        public void ExcludeGarrisoned_RemovesGarrisonedFleetsFromSystemCount()
        {
            Fortress f = Fort(Faction.帝国);
            GalaxyMap map = MapWithFortress(f, out _);
            StrategicFleet a = Fleet(1, Faction.帝国);
            StrategicFleet b = Fleet(2, Faction.帝国);
            Assert.IsTrue(FortressGarrisonRules.Garrison(map, f, a, Faction.帝国, out _));

            var atSystem = new List<StrategicFleet> { a, b, null };
            List<StrategicFleet> stationed = FortressGarrisonRules.ExcludeGarrisoned(map, atSystem);
            Assert.AreEqual(1, stationed.Count);            // 駐留中の a は星系の停泊艦から除かれる
            Assert.AreSame(b, stationed[0]);
            Assert.AreEqual(3, atSystem.Count);             // 元のリストは変更しない
        }

        // ── 集計 ──

        [Test]
        public void TotalGarrisonShips_SumsShips_NotFacilityGarrisonStrength()
        {
            Fortress f = Fort(Faction.帝国, 1000f);          // 施設の守備力 1000（混ぜない）
            StrategicFleet a = Fleet(1, Faction.帝国);
            StrategicFleet b = Fleet(2, Faction.帝国);
            a.SetShips(4000);
            b.SetShips(2500);
            Assert.IsTrue(FortressGarrisonRules.Garrison(f, a, Faction.帝国));
            Assert.IsTrue(FortressGarrisonRules.Garrison(f, b, Faction.帝国));

            var resolve = Resolver(a, b);
            Assert.AreEqual(6500, FortressGarrisonRules.TotalGarrisonShips(f, resolve));
            Assert.AreEqual(1000f, FortressGarrisonRules.FacilityGarrisonStrength(f), 1e-4f);
            Assert.AreEqual(200, FortressGarrisonRules.TotalGarrisonStrength(f, resolve)); // 兵力 100+100
            Assert.AreEqual(2, FortressGarrisonRules.GarrisonFleetCount(f));

            // 解決できない（resolve が null／盤面から消えた）艦隊は 0 として数える。
            Assert.AreEqual(0, FortressGarrisonRules.TotalGarrisonShips(f, (System.Func<int, StrategicFleet>)null));
            Assert.AreEqual(4000, FortressGarrisonRules.TotalGarrisonShips(f, Resolver(a)));
        }

        [Test]
        public void TotalGarrisonShips_UsesDerivedShipCount_WhenNotSet()
        {
            // 艦艇数が未設定なら兵力から導出（FleetShipCountRules.ShipsPerStrength=40）。
            Fortress f = Fort(Faction.帝国);
            StrategicFleet a = Fleet(1, Faction.帝国, 0, 100);
            Assert.IsTrue(FortressGarrisonRules.Garrison(f, a, Faction.帝国));
            Assert.AreEqual(4000, FortressGarrisonRules.TotalGarrisonShips(f, Resolver(a)));
        }

        [Test]
        public void TotalGarrisonShips_ViaRegistry()
        {
            Fortress f = Fort(Faction.帝国);
            var registry = new StrategicFleetRegistry();
            StrategicFleet a = Fleet(1, Faction.帝国);
            a.SetShips(1234);
            registry.Add(a);
            Assert.IsTrue(FortressGarrisonRules.Garrison(f, a, Faction.帝国));

            Assert.AreEqual(1234, FortressGarrisonRules.TotalGarrisonShips(f, registry));
            Assert.AreEqual(100, FortressGarrisonRules.TotalGarrisonStrength(f, registry));
            Assert.AreEqual(1, FortressGarrisonRules.GarrisonFleets(f, registry).Count);
            Assert.AreEqual(0, FortressGarrisonRules.GarrisonFleets(f, (StrategicFleetRegistry)null).Count);
            Assert.AreEqual(0, FortressGarrisonRules.TotalGarrisonShips(f, (StrategicFleetRegistry)null));
            Assert.AreEqual(0, FortressGarrisonRules.TotalGarrisonStrength(f, (StrategicFleetRegistry)null));
        }

        [Test]
        public void PruneMissing_RemovesDestroyedFleets_Only()
        {
            Fortress f = Fort(Faction.帝国);
            StrategicFleet a = Fleet(1, Faction.帝国);
            StrategicFleet b = Fleet(2, Faction.帝国);
            Assert.IsTrue(FortressGarrisonRules.Garrison(f, a, Faction.帝国));
            Assert.IsTrue(FortressGarrisonRules.Garrison(f, b, Faction.帝国));

            Assert.AreEqual(0, FortressGarrisonRules.PruneMissing(f, (System.Func<int, StrategicFleet>)null)); // 解決手段なし＝触らない
            Assert.AreEqual(2, FortressGarrisonRules.GarrisonFleetCount(f));

            Assert.AreEqual(1, FortressGarrisonRules.PruneMissing(f, Resolver(a)));   // b は盤面から消えた
            Assert.AreEqual(1, FortressGarrisonRules.GarrisonFleetCount(f));
            Assert.IsTrue(FortressGarrisonRules.IsGarrisonedIn(f, 1));
            Assert.IsFalse(FortressGarrisonRules.IsGarrisonedIn(f, 2));
        }

        // ── 占領 ──

        [Test]
        public void OnCaptured_DoesNotConvertEnemyGarrisonToNewOwner()
        {
            Fortress f = Fort(Faction.帝国);
            StrategicFleet defender = Fleet(1, Faction.帝国);
            StrategicFleet defender2 = Fleet(2, Faction.帝国);
            Assert.IsTrue(FortressGarrisonRules.Garrison(f, defender, Faction.帝国));
            Assert.IsTrue(FortressGarrisonRules.Garrison(f, defender2, Faction.帝国));

            List<int> released = FortressGarrisonRules.OnCaptured(f, Faction.同盟, Resolver(defender, defender2));

            Assert.AreEqual(2, released.Count);
            Assert.AreEqual(1, released[0]);                 // 名簿順（決定論）
            Assert.AreEqual(2, released[1]);
            Assert.AreEqual(0, FortressGarrisonRules.GarrisonFleetCount(f)); // 外すだけ
            Assert.AreEqual(Faction.帝国, defender.faction);   // 味方に変換されない
            Assert.AreEqual(Faction.帝国, defender2.faction);
            Assert.AreEqual(100, defender.strength);          // 複製・改変もされない
        }

        [Test]
        public void OnCaptured_KeepsFleetsFriendlyToNewOwner()
        {
            Fortress f = Fort(Faction.帝国);
            StrategicFleet imperial = Fleet(1, Faction.帝国);
            StrategicFleet allied = Fleet(2, Faction.同盟);
            // （通常経路では敵対勢力は駐留できないが、外交変化などで混在した名簿でも壊れないことを固定）
            f.garrisonFleetIds.Add(imperial.id);
            f.garrisonFleetIds.Add(allied.id);

            List<int> released = FortressGarrisonRules.OnCaptured(f, Faction.同盟, Resolver(imperial, allied));
            Assert.AreEqual(1, released.Count);
            Assert.AreEqual(1, released[0]);                                   // 帝国艦隊だけ外れる
            Assert.AreEqual(1, FortressGarrisonRules.GarrisonFleetCount(f));
            Assert.IsTrue(FortressGarrisonRules.IsGarrisonedIn(f, allied.id)); // 同盟艦隊は残る
        }

        [Test]
        public void OnCaptured_NullResolver_ReleasesAll()
        {
            Fortress f = Fort(Faction.帝国);
            f.garrisonFleetIds.Add(1);
            f.garrisonFleetIds.Add(2);
            List<int> released = FortressGarrisonRules.OnCaptured(f, Faction.同盟, null);
            Assert.AreEqual(2, released.Count);                                  // 素性不明は安全側で全員外す
            Assert.AreEqual(0, FortressGarrisonRules.GarrisonFleetCount(f));
        }

        [Test]
        public void OnCaptured_ParamOff_KeepsRoster()
        {
            Fortress f = Fort(Faction.帝国);
            StrategicFleet a = Fleet(1, Faction.帝国);
            Assert.IsTrue(FortressGarrisonRules.Garrison(f, a, Faction.帝国));
            var keep = new FortressGarrisonParams(4, true, false);
            Assert.AreEqual(0, FortressGarrisonRules.OnCaptured(f, Faction.同盟, Resolver(a), keep).Count);
            Assert.AreEqual(1, FortressGarrisonRules.GarrisonFleetCount(f));
        }

        [Test]
        public void ClearGarrison_ReturnsIdsAndEmpties()
        {
            Fortress f = Fort(Faction.帝国);
            StrategicFleet a = Fleet(3, Faction.帝国);
            Assert.IsTrue(FortressGarrisonRules.Garrison(f, a, Faction.帝国));
            List<int> released = FortressGarrisonRules.ClearGarrison(f);
            Assert.AreEqual(1, released.Count);
            Assert.AreEqual(3, released[0]);
            Assert.AreEqual(0, FortressGarrisonRules.GarrisonFleetCount(f));
            Assert.AreEqual(Faction.帝国, a.faction);   // 艦隊は無傷
        }

        // ── 旧セーブ互換 ──

        [Test]
        public void LegacySave_GarrisonStrengthOnly_StillWorks()
        {
            // 旧セーブ由来＝守備値だけ・駐留名簿は空。
            Fortress f = Fort(Faction.帝国, 1000f);
            Assert.IsTrue(FortressGarrisonRules.IsLegacyGarrisonOnly(f));
            Assert.IsFalse(FortressGarrisonRules.HasFleetGarrison(f));
            Assert.AreEqual(0, FortressGarrisonRules.TotalGarrisonShips(f, Resolver()));   // 艦隊へ変換しない
            Assert.AreEqual(1000f, FortressGarrisonRules.FacilityGarrisonStrength(f), 1e-4f); // 守備値は消えない
            Assert.IsTrue(FortressRules.BlocksPassage(f));                                 // 封鎖は従来どおり効く
            Assert.AreEqual("守備力 1000　駐留なし", FortressGarrisonRules.GarrisonSummaryText(f, null));

            // 艦隊を1つ入れても守備値と合算しない（別勘定）。
            StrategicFleet a = Fleet(1, Faction.帝国);
            a.SetShips(8000);
            Assert.IsTrue(FortressGarrisonRules.Garrison(f, a, Faction.帝国));
            Assert.IsFalse(FortressGarrisonRules.IsLegacyGarrisonOnly(f));
            Assert.AreEqual(1000f, FortressGarrisonRules.FacilityGarrisonStrength(f), 1e-4f);
            Assert.AreEqual(8000, FortressGarrisonRules.TotalGarrisonShips(f, Resolver(a)));
            Assert.AreEqual("守備力 1000　駐留 1部隊 8,000隻", FortressGarrisonRules.GarrisonSummaryText(f, Resolver(a)));
        }

        [Test]
        public void NullGarrisonList_IsRepaired()
        {
            // 旧セーブの復元でリストが null のまま来ても壊れない（照会で生成し直す）。
            Fortress f = Fort(Faction.帝国);
            f.garrisonFleetIds = null;
            Assert.AreEqual(0, FortressGarrisonRules.GarrisonFleetCount(f));
            Assert.IsNotNull(f.garrisonFleetIds);
            Assert.IsTrue(FortressGarrisonRules.Garrison(f, Fleet(1, Faction.帝国), Faction.帝国));
            Assert.AreEqual(1, FortressGarrisonRules.GarrisonFleetCount(f));
        }

        [Test]
        public void FortressConstructor_InitializesEmptyGarrisonList()
        {
            Assert.IsNotNull(new Fortress().garrisonFleetIds);
            Assert.AreEqual(0, new Fortress().garrisonFleetIds.Count);
            Assert.IsNotNull(new Fortress(100f, 10f).garrisonFleetIds);
            Assert.AreEqual(0, new Fortress(100f, 10f).garrisonFleetIds.Count);
        }

        // ── 理由文字列 ──

        [Test]
        public void RejectionText_CoversEveryReason()
        {
            Assert.AreEqual("", FortressGarrisonRules.RejectionText(GarrisonRejection.なし));
            var all = (GarrisonRejection[])System.Enum.GetValues(typeof(GarrisonRejection));
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == GarrisonRejection.なし) continue;
                string text = FortressGarrisonRules.RejectionText(all[i]);
                Assert.IsFalse(string.IsNullOrEmpty(text), all[i].ToString());
                Assert.AreNotEqual("駐留できません", text, all[i].ToString()); // 既定文へ落ちていない
            }
            Assert.AreEqual("駐留できません", FortressGarrisonRules.RejectionText((GarrisonRejection)999));
        }

        /// <summary>
        /// 短縮形（艦隊メニューの狭い列用）。全桁を出せない列に長文を入れると省略記号で理由が読めなくなる
        /// （実機QAで艦艇数が読めなかったのと同じ失敗）ので、行には必ず短い形を出す。
        /// </summary>
        [Test]
        public void ShortRejectionText_IsShortAndCoversEveryReason()
        {
            Assert.AreEqual("", FortressGarrisonRules.ShortRejectionText(GarrisonRejection.なし));
            var all = (GarrisonRejection[])System.Enum.GetValues(typeof(GarrisonRejection));
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == GarrisonRejection.なし) continue;
                string text = FortressGarrisonRules.ShortRejectionText(all[i]);
                Assert.IsFalse(string.IsNullOrEmpty(text), all[i].ToString());
                Assert.AreNotEqual("不可", text, all[i].ToString());          // 既定文へ落ちていない
                Assert.LessOrEqual(text.Length, 8, all[i].ToString());        // 狭い列でも切れない長さ
                // 全文より短い（同じなら短縮形の意味がない）。
                Assert.Less(text.Length, FortressGarrisonRules.RejectionText(all[i]).Length, all[i].ToString());
            }
            Assert.AreEqual("不可", FortressGarrisonRules.ShortRejectionText((GarrisonRejection)999));
        }

        // ── 一覧の短縮表示（施設の守備値と混ぜない） ──

        [Test]
        public void GarrisonCompactText_ShowsFleetsAndShips_NotFacilityStrength()
        {
            Fortress f = Fort(Faction.帝国, 1000f);
            StrategicFleet a = Fleet(5, Faction.帝国);
            a.SetShips(8000);
            StrategicFleet b = Fleet(6, Faction.帝国);
            b.SetShips(2000);
            FortressGarrisonRules.Garrison(f, a, Faction.帝国);
            FortressGarrisonRules.Garrison(f, b, Faction.帝国);

            string text = FortressGarrisonRules.GarrisonCompactText(f, Resolver(a, b));
            Assert.AreEqual("2隊 10,000隻", text);
            StringAssert.DoesNotContain("守備力", text);   // 施設の守備値は混ぜない
            StringAssert.DoesNotContain("兵力", text);     // プレイヤー向けは艦艇数だけ
        }

        [Test]
        public void GarrisonCompactText_EmptyRoster_SaysNone()
        {
            Fortress f = Fort(Faction.帝国, 1200f);
            Assert.AreEqual("駐留なし", FortressGarrisonRules.GarrisonCompactText(f, Resolver()));
            Assert.AreEqual("", FortressGarrisonRules.GarrisonCompactText(null, Resolver()));
            // 施設の守備値は残っている＝黙って消えない（旧セーブ互換）。
            Assert.IsTrue(FortressGarrisonRules.IsLegacyGarrisonOnly(f));
        }

        // ── 移動命令との整合（駐留したまま動かない・名簿が残らない） ──

        /// <summary>
        /// 「駐留中の通常移動は出撃と整合し、名簿が残らない」（作業票の合格条件）。
        /// 艦隊メニューの移動命令は <c>GalaxyView.OrderMove</c> で先に <see cref="FortressGarrisonRules.SortieFrom"/>
        /// を通す。その順序で名簿が確実に空になり、二度目の出撃が空振りすることを固定する。
        /// </summary>
        [Test]
        public void SortieBeforeMove_LeavesNoRosterEntry()
        {
            Fortress f = Fort(Faction.帝国);
            GalaxyMap map = MapWithFortress(f, out _);
            StrategicFleet fleet = Fleet(7, Faction.帝国);
            Assert.IsTrue(FortressGarrisonRules.Garrison(map, f, fleet, Faction.帝国, out _));

            // 移動命令の前段＝盤面のどこに駐留していても出撃させる。
            Assert.IsTrue(FortressGarrisonRules.SortieFrom(map, fleet, out Fortress from));
            Assert.AreSame(f, from);
            Assert.AreEqual(0, FortressGarrisonRules.GarrisonFleetCount(f), "名簿に残ってはいけない");
            Assert.IsFalse(FortressGarrisonRules.IsGarrisoned(map, fleet));
            // 二度目は空振り（同じ艦隊を二重に外そうとしても壊れない）。
            Assert.IsFalse(FortressGarrisonRules.SortieFrom(map, fleet, out _));

            // 艦艇数・所属は駐留と出撃で一切変わらない。
            Assert.AreEqual(Faction.帝国, fleet.faction);
            Assert.AreEqual(100, fleet.strength);
        }

        /// <summary>
        /// 駐留・出撃で<b>艦艇数と所属を保持</b>する（作業票の合格条件）。指揮官は艦隊台帳
        /// （<see cref="FleetRoster"/>）が持ち、本ルールは艦隊idを名簿に足し引きするだけなので触れない
        /// ＝艦隊側のフィールドが一切書き換わらないことをここで固定する。
        /// </summary>
        [Test]
        public void GarrisonAndSortie_DoNotTouchShipCountOrFaction()
        {
            Fortress f = Fort(Faction.帝国);
            GalaxyMap map = MapWithFortress(f, out _);
            StrategicFleet fleet = Fleet(9, Faction.帝国, 0, 300);
            fleet.SetShips(12345);

            FortressGarrisonRules.Garrison(map, f, fleet, Faction.帝国, out _);
            Assert.AreEqual(12345, fleet.Ships);
            Assert.AreEqual(300, fleet.strength);
            Assert.AreEqual(Faction.帝国, fleet.faction);
            Assert.AreEqual(0, fleet.currentSystemId, "駐留しても停泊している星系は変わらない");

            FortressGarrisonRules.Sortie(f, fleet);
            Assert.AreEqual(12345, fleet.Ships, "出撃で艦艇数が変わってはいけない");
            Assert.AreEqual(300, fleet.strength);
            Assert.AreEqual(Faction.帝国, fleet.faction);
            Assert.AreEqual(0, fleet.currentSystemId);
        }

        // ── 決定論 ──

        [Test]
        public void Deterministic_SameOperationsGiveSameRoster()
        {
            for (int trial = 0; trial < 3; trial++)
            {
                Fortress f = Fort(Faction.帝国);
                GalaxyMap map = MapWithFortress(f, out _);
                StrategicFleet a = Fleet(11, Faction.帝国);
                StrategicFleet b = Fleet(22, Faction.帝国);
                FortressGarrisonRules.Garrison(map, f, a, Faction.帝国, out _);
                FortressGarrisonRules.Garrison(map, f, b, Faction.帝国, out _);
                IReadOnlyList<int> ids = FortressGarrisonRules.GarrisonFleetIds(f);
                Assert.AreEqual(2, ids.Count);
                Assert.AreEqual(11, ids[0]);
                Assert.AreEqual(22, ids[1]);
            }
        }
    }
}
