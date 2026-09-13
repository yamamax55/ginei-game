using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 援軍の到着時間（#38）の回帰。実機QAで「戦場の端に停泊させた予備を派遣したら、戦術マップへ入った
    /// 直後にワープインした」＝所要時間が 0 になる不具合が出た。
    ///
    /// 原因：宛先の端が艦隊の現在地と同じだと経路が1要素になり所要 0 秒。
    /// 端に着いただけでは戦場に着いたことにならないので、<b>端から戦っている地点まで回廊を進む</b>ぶんを足す。
    /// </summary>
    public class WarpReinforcementArrivalTimingTests
    {
        private const float Speed = 1f;
        private const float Sublight = 0.35f;

        /// <summary>0 —(len)— 1 の1本道。両端の所有はどちらも同盟＝前線ではない（FTL 可）。</summary>
        private static GalaxyMap OneCorridor(float length)
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem { id = 0, systemName = "A", owner = Faction.同盟 });
            map.AddSystem(new StarSystem { id = 1, systemName = "B", owner = Faction.同盟 });
            map.AddCorridor(new Corridor(0, 1, length, CorridorType.通商));
            return map;
        }

        // ── 端に居ても 0 秒にならない ──

        [Test]
        public void FleetAtEndpoint_StillTakesTimeToReachTheFight()
        {
            GalaxyMap map = OneCorridor(10f);
            var key = BattlefieldKey.Corridor(0, 1);

            float t = WarpReinforcementRules.TravelSecondsToBattlefield(map, 0, key, Speed, Sublight, out int entry);

            Assert.AreEqual(0, entry, "自分が居る端から入るはず");
            Assert.Greater(t, 0f, "端に停泊しているだけで所要0秒になってはいけない");
            // 回廊長10 の半分＝5 を速度1で進む＝5秒。
            Assert.AreEqual(5f, t, 1e-3f);
        }

        [Test]
        public void FleetAtOtherEndpoint_AlsoTakesTime()
        {
            GalaxyMap map = OneCorridor(10f);
            var key = BattlefieldKey.Corridor(0, 1);

            float t = WarpReinforcementRules.TravelSecondsToBattlefield(map, 1, key, Speed, Sublight, out int entry);

            Assert.AreEqual(1, entry);
            Assert.AreEqual(5f, t, 1e-3f);
        }

        [Test]
        public void FleetOneHopAway_AddsBothLegs()
        {
            // 2 —(4)— 0 —(10)— 1。2 から戦場(0-1)へ：0 まで4秒＋回廊内5秒＝9秒。
            var map = OneCorridor(10f);
            map.AddSystem(new StarSystem { id = 2, systemName = "C", owner = Faction.同盟 });
            map.AddCorridor(new Corridor(2, 0, 4f, CorridorType.通商));

            float t = WarpReinforcementRules.TravelSecondsToBattlefield(
                map, 2, BattlefieldKey.Corridor(0, 1), Speed, Sublight, out int entry);

            Assert.AreEqual(0, entry, "近いほうの端から入るはず");
            Assert.AreEqual(9f, t, 1e-3f);
        }

        [Test]
        public void InCorridorSeconds_ScalesWithLength()
        {
            Assert.AreEqual(5f, WarpReinforcementRules.InCorridorSeconds(
                OneCorridor(10f), BattlefieldKey.Corridor(0, 1), Speed, Sublight), 1e-3f);
            Assert.AreEqual(10f, WarpReinforcementRules.InCorridorSeconds(
                OneCorridor(20f), BattlefieldKey.Corridor(0, 1), Speed, Sublight), 1e-3f);
        }

        [Test]
        public void InCorridorSeconds_IsZeroForSystemBattle()
        {
            GalaxyMap map = OneCorridor(10f);
            Assert.AreEqual(0f, WarpReinforcementRules.InCorridorSeconds(
                map, BattlefieldKey.System(0), Speed, Sublight), 1e-4f);
        }

        [Test]
        public void InCorridorSeconds_NullSafe()
        {
            Assert.AreEqual(0f, WarpReinforcementRules.InCorridorSeconds(
                null, BattlefieldKey.Corridor(0, 1), Speed, Sublight), 1e-4f);
            Assert.AreEqual(0f, WarpReinforcementRules.InCorridorSeconds(
                OneCorridor(10f), default, Speed, Sublight), 1e-4f);
        }

        [Test]
        public void FrontlineCorridor_UsesSublightSpeed()
        {
            // 両端が敵対＝前線＝FTL 不可なので回廊内の移動が遅くなる。
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem { id = 0, owner = Faction.同盟 });
            map.AddSystem(new StarSystem { id = 1, owner = Faction.帝国 });
            map.AddCorridor(new Corridor(0, 1, 10f, CorridorType.要衝));

            float t = WarpReinforcementRules.InCorridorSeconds(
                map, BattlefieldKey.Corridor(0, 1), Speed, Sublight);

            Assert.AreEqual(5f / Sublight, t, 1e-2f, "前線回廊は亜光速ぶん遅いはず");
        }

        // ── 派遣した瞬間に到着扱いにならない ──

        [Test]
        public void DispatchFromEndpoint_DoesNotArriveImmediately()
        {
            GalaxyMap map = OneCorridor(10f);
            var key = BattlefieldKey.Corridor(0, 1);
            var ledger = new WarpReinforcementLedger();

            float t = WarpReinforcementRules.TravelSecondsToBattlefield(map, 0, key, Speed, Sublight, out _);
            long id = ledger.Dispatch(key, Faction.同盟, 6, 159, t);
            Assert.Greater(id, 0L);

            var arrived = new List<WarpReinforcement>();
            Assert.AreEqual(0, ledger.TakeArrived(key, arrived), "派遣した瞬間に到着してはいけない");

            ledger.SyncTo(t - 0.01);
            Assert.AreEqual(0, ledger.TakeArrived(key, arrived), "所要時間の手前で着いてはいけない");

            ledger.SyncTo(t);
            Assert.AreEqual(1, ledger.TakeArrived(key, arrived), "所要時間ちょうどで着くこと");
        }
    }
}
