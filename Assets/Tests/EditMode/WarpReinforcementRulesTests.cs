using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>援軍（ワープイン・#38 C-5）：戦場キーの同定と、ワープ所要時間/到着時刻の算出。</summary>
    public class WarpReinforcementRulesTests
    {
        // ===== BattlefieldKey =====

        [Test]
        public void BattlefieldKey_順不同で同一視される()
        {
            Assert.AreEqual(BattlefieldKey.Corridor(3, 7), BattlefieldKey.Corridor(7, 3));
            Assert.IsTrue(BattlefieldKey.Corridor(3, 7) == BattlefieldKey.Corridor(7, 3));
            Assert.AreEqual(BattlefieldKey.Corridor(3, 7).GetHashCode(),
                            BattlefieldKey.Corridor(7, 3).GetHashCode());
        }

        [Test]
        public void BattlefieldKey_別戦場は別キー()
        {
            Assert.AreNotEqual(BattlefieldKey.Corridor(3, 7), BattlefieldKey.Corridor(3, 8));
            Assert.IsTrue(BattlefieldKey.Corridor(3, 7) != BattlefieldKey.Corridor(4, 7));
        }

        [Test]
        public void BattlefieldKey_defaultは無効で星系0と区別される()
        {
            BattlefieldKey invalid = default;
            Assert.IsFalse(invalid.IsValid);
            Assert.IsTrue(BattlefieldKey.System(0).IsValid);
            Assert.AreNotEqual(invalid, BattlefieldKey.System(0));
        }

        [Test]
        public void BattlefieldKey_星系戦場と回廊戦場()
        {
            Assert.IsTrue(BattlefieldKey.System(5).IsSystemBattle);
            Assert.IsFalse(BattlefieldKey.Corridor(5, 6).IsSystemBattle);
            Assert.IsTrue(BattlefieldKey.Corridor(5, 6).Contains(6));
            Assert.IsFalse(BattlefieldKey.Corridor(5, 6).Contains(7));
            Assert.AreEqual(5, BattlefieldKey.Corridor(5, 6).Other(6));
            Assert.AreEqual(-1, BattlefieldKey.Corridor(5, 6).Other(9));
        }

        [Test]
        public void BattlefieldKey_longエンコードで往復できる()
        {
            BattlefieldKey k = BattlefieldKey.Corridor(12, 34);
            Assert.AreEqual(k, BattlefieldKey.Decode(k.Encode()));
            Assert.AreEqual(12L * 100000L + 34L, k.Encode());     // GalaxyView の CorridorKey と同規則
            Assert.IsFalse(BattlefieldKey.Decode(-1L).IsValid);
        }

        // ===== 所要時間 =====

        [Test]
        public void TravelSeconds_距離割る速度()
        {
            Assert.AreEqual(20f, WarpReinforcementRules.TravelSeconds(40f, 2f), 1e-4f);
            Assert.AreEqual(0f, WarpReinforcementRules.TravelSeconds(0f, 2f), 1e-4f);   // 距離0＝即時
        }

        [Test]
        public void TravelSeconds_速度0は到達不能()
        {
            Assert.IsTrue(float.IsPositiveInfinity(WarpReinforcementRules.TravelSeconds(40f, 0f)));
            Assert.AreEqual(WarpReinforcementRules.Unreachable, WarpReinforcementRules.TravelSeconds(40f, -1f));
        }

        [Test]
        public void TravelSeconds_亜光速は遅い()
        {
            float ftl = WarpReinforcementRules.TravelSeconds(40f, 2f, false, 0.5f);
            float sub = WarpReinforcementRules.TravelSeconds(40f, 2f, true, 0.5f);
            Assert.AreEqual(20f, ftl, 1e-4f);
            Assert.AreEqual(40f, sub, 1e-4f);   // 速度が半分＝倍かかる
            Assert.Greater(sub, ftl);
        }

        // ===== 経路に沿った所要時間 =====

        /// <summary>0-1-2 の一直線（同一勢力＝前線でない）。回廊長は 10 と 30。</summary>
        private static GalaxyMap LineMap()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(0, "A", Vector2.zero, Faction.帝国));
            map.AddSystem(new StarSystem(1, "B", new Vector2(1f, 0f), Faction.帝国));
            map.AddSystem(new StarSystem(2, "C", new Vector2(2f, 0f), Faction.帝国));
            map.AddCorridor(new Corridor(0, 1, 10f));
            map.AddCorridor(new Corridor(1, 2, 30f));
            return map;
        }

        [Test]
        public void TravelSecondsAlongRoute_ホップの合計()
        {
            GalaxyMap map = LineMap();
            var path = new List<int> { 0, 1, 2 };
            // (10+30)/2 = 20
            Assert.AreEqual(20f, WarpReinforcementRules.TravelSecondsAlongRoute(map, path, 2f, 0.35f), 1e-4f);
        }

        [Test]
        public void TravelSecondsAlongRoute_出発地と同じなら0()
        {
            GalaxyMap map = LineMap();
            Assert.AreEqual(0f, WarpReinforcementRules.TravelSecondsAlongRoute(map, new List<int> { 0 }, 2f, 0.35f), 1e-4f);
        }

        [Test]
        public void TravelSecondsAlongRoute_回廊が無ければ到達不能()
        {
            GalaxyMap map = LineMap();
            var path = new List<int> { 0, 2 };   // 0-2 の直通回廊は無い
            Assert.IsTrue(float.IsPositiveInfinity(
                WarpReinforcementRules.TravelSecondsAlongRoute(map, path, 2f, 0.35f)));
        }

        [Test]
        public void TravelSecondsAlongRoute_前線回廊は亜光速で遅い()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(0, "A", Vector2.zero, Faction.帝国));
            map.AddSystem(new StarSystem(1, "B", new Vector2(1f, 0f), Faction.同盟)); // 敵対＝FTL不可の前線
            map.AddCorridor(new Corridor(0, 1, 10f));

            float t = WarpReinforcementRules.TravelSecondsAlongRoute(map, new List<int> { 0, 1 }, 1f, 0.5f);
            Assert.AreEqual(20f, t, 1e-4f);   // 10 / (1×0.5)
        }

        // ===== 戦場までの所要時間 =====

        [Test]
        public void TravelSecondsToBattlefield_近いほうの端から入る()
        {
            GalaxyMap map = LineMap();
            // 星系0 から 回廊1-2 の戦場へ。端1（10/2=5秒）のほうが端2（40/2=20秒）より近い。
            // 端に着いたあと、戦っている地点（回廊の中ほど）まで回廊内を進むぶんが加わる：
            // 回廊1-2 の長さ30 × 0.5 ÷ 速度2 ＝ 7.5秒。合計 5 + 7.5 ＝ 12.5秒。
            float t = WarpReinforcementRules.TravelSecondsToBattlefield(
                map, 0, BattlefieldKey.Corridor(1, 2), 2f, 0.35f, out int entry);
            Assert.AreEqual(12.5f, t, 1e-4f);
            Assert.AreEqual(1, entry);
        }

        [Test]
        public void TravelSecondsToBattlefield_出発地が端でも回廊内を進むぶんはかかる()
        {
            // ★旧仕様は「端に居れば0秒」だったが、これは実機で不具合になった：
            // 戦場の端に停泊させた予備を派遣すると、戦術マップへ入った直後にワープインしてしまう。
            // 端に着いただけでは戦場に着いたことにならないので、回廊内を進むぶんが必ずかかる。
            GalaxyMap map = LineMap();
            float t = WarpReinforcementRules.TravelSecondsToBattlefield(
                map, 1, BattlefieldKey.Corridor(1, 2), 2f, 0.35f, out int entry);
            Assert.AreEqual(7.5f, t, 1e-4f);   // 長さ30 × 0.5 ÷ 速度2
            Assert.Greater(t, 0f, "端に停泊しているだけで即到着してはいけない");
            Assert.AreEqual(1, entry);
        }

        [Test]
        public void TravelSecondsToBattlefield_星系戦場()
        {
            GalaxyMap map = LineMap();
            float t = WarpReinforcementRules.TravelSecondsToBattlefield(
                map, 0, BattlefieldKey.System(2), 2f, 0.35f, out int entry);
            Assert.AreEqual(20f, t, 1e-4f);
            Assert.AreEqual(2, entry);
        }

        [Test]
        public void TravelSecondsToBattlefield_到達不能()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(0, "A", Vector2.zero, Faction.帝国));
            map.AddSystem(new StarSystem(9, "Z", new Vector2(9f, 0f), Faction.帝国)); // 孤立
            float t = WarpReinforcementRules.TravelSecondsToBattlefield(
                map, 0, BattlefieldKey.System(9), 1f, 0.35f, out int entry);
            Assert.IsTrue(float.IsPositiveInfinity(t));
            Assert.AreEqual(-1, entry);
        }

        // ===== 到着時刻 =====

        [Test]
        public void ArrivalTime_現在時刻に所要を足す()
        {
            Assert.AreEqual(130.0, WarpReinforcementRules.ArrivalTime(100.0, 30f), 1e-6);
            Assert.AreEqual(100.0, WarpReinforcementRules.ArrivalTime(100.0, -5f), 1e-6); // 負の所要は0扱い
            Assert.IsTrue(double.IsPositiveInfinity(
                WarpReinforcementRules.ArrivalTime(100.0, WarpReinforcementRules.Unreachable)));
        }

        [Test]
        public void HasArrived_到着時刻以上で真()
        {
            Assert.IsFalse(WarpReinforcementRules.HasArrived(130.0, 129.999));
            Assert.IsTrue(WarpReinforcementRules.HasArrived(130.0, 130.0));
            Assert.IsTrue(WarpReinforcementRules.HasArrived(130.0, 200.0));
        }

        [Test]
        public void RemainingSeconds_到着後は0()
        {
            Assert.AreEqual(30f, WarpReinforcementRules.RemainingSeconds(130.0, 100.0), 1e-4f);
            Assert.AreEqual(0f, WarpReinforcementRules.RemainingSeconds(130.0, 130.0), 1e-4f);
            Assert.AreEqual(0f, WarpReinforcementRules.RemainingSeconds(130.0, 999.0), 1e-4f);
        }

        [Test]
        public void ArrivalProgress_0から1へ()
        {
            Assert.AreEqual(0f, WarpReinforcementRules.ArrivalProgress(100.0, 200.0, 100.0), 1e-4f);
            Assert.AreEqual(0.5f, WarpReinforcementRules.ArrivalProgress(100.0, 200.0, 150.0), 1e-4f);
            Assert.AreEqual(1f, WarpReinforcementRules.ArrivalProgress(100.0, 200.0, 500.0), 1e-4f);
            Assert.AreEqual(1f, WarpReinforcementRules.ArrivalProgress(100.0, 100.0, 100.0), 1e-4f); // 所要0＝即時
        }
    }
}
