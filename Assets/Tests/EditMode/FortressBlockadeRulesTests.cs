using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 回廊要塞＝固定拠点の通行判定（#40 C-7）。
    /// 戦略グラフ側＝「敵の要塞は制圧するまで通れない／所有者は通れる／要塞なしの通商回廊は素通り」。
    /// 迂回については、<b>別の通商回廊による戦略的な回り道は許す</b>（銀河を一本道にしない）。
    /// 戦術マップでの局所的な回り込み禁止は <see cref="CorridorArenaRulesTests"/> が担保する。
    /// </summary>
    public class FortressBlockadeRulesTests
    {
        private static StarSystem Sys(int id, Faction owner) => new StarSystem { id = id, owner = owner };

        /// <summary>0–1 を要塞つき要衝で結び、0–2–1 を通商回廊で迂回できるマップ。</summary>
        private static GalaxyMap MapWithFortressAndBypass(Faction fortressOwner, float garrison)
        {
            var map = new GalaxyMap();
            map.AddSystem(Sys(0, Faction.同盟));
            map.AddSystem(Sys(1, Faction.帝国));
            map.AddSystem(Sys(2, Faction.同盟));

            var choke = new Corridor(0, 1, 1f, CorridorType.要衝);
            choke.fortress = new Fortress(garrison, 100f, 1f, true) { owner = fortressOwner, fortressName = "要塞" };
            map.AddCorridor(choke);
            map.AddCorridor(new Corridor(0, 2, 5f, CorridorType.通商));
            map.AddCorridor(new Corridor(2, 1, 5f, CorridorType.通商));
            return map;
        }

        private static Corridor Choke(GalaxyMap map) => map.GetCorridor(0, 1);

        // ── 型の識別（イゼルローン型 vs フェザーン型）──

        [Test]
        public void FortifiedChoke_And_CommerceCorridor_AreMutuallyExclusive()
        {
            var map = MapWithFortressAndBypass(Faction.帝国, 1000f);
            Corridor choke = Choke(map);
            Corridor commerce = map.GetCorridor(0, 2);

            Assert.IsTrue(FortressBlockadeRules.IsFortifiedChoke(choke));      // 要塞あり＝イゼルローン型
            Assert.IsFalse(FortressBlockadeRules.IsCommerceCorridor(choke));
            Assert.IsTrue(FortressBlockadeRules.IsCommerceCorridor(commerce)); // 要塞なし＝フェザーン型
            Assert.IsFalse(FortressBlockadeRules.IsFortifiedChoke(commerce));
        }

        [Test]
        public void CommerceCorridor_NeverBlocks()
        {
            var map = MapWithFortressAndBypass(Faction.帝国, 1000f);
            Assert.IsFalse(FortressBlockadeRules.Blocks(map.GetCorridor(0, 2), Faction.帝国));
            Assert.IsFalse(FortressBlockadeRules.Blocks(map.GetCorridor(0, 2), Faction.同盟));
        }

        // ── 通行の可否 ──

        [Test]
        public void Blocks_HostileOnly_OwnerPassesFreely()
        {
            var map = MapWithFortressAndBypass(Faction.帝国, 1000f);
            Assert.IsTrue(FortressBlockadeRules.Blocks(map, 0, 1, Faction.同盟));  // 敵対＝通れない
            Assert.IsFalse(FortressBlockadeRules.Blocks(map, 0, 1, Faction.帝国)); // 所有者＝自由通行
        }

        [Test]
        public void MaxAdvanceFraction_StopsHostileShortOfFortress()
        {
            var map = MapWithFortressAndBypass(Faction.帝国, 1000f);
            Corridor choke = Choke(map);
            Assert.AreEqual(FortressBlockadeRules.StandoffFraction,
                            FortressBlockadeRules.MaxAdvanceFraction(choke, Faction.同盟), 1e-4f);
            Assert.AreEqual(1f, FortressBlockadeRules.MaxAdvanceFraction(choke, Faction.帝国), 1e-4f);
            Assert.Less(FortressBlockadeRules.StandoffFraction, 1f); // 反対側へは抜けない
        }

        [Test]
        public void CapturedFortress_OpensPassage_ForEveryone()
        {
            var map = MapWithFortressAndBypass(Faction.帝国, 1000f);
            Corridor choke = Choke(map);
            StrategyRules.AssaultFortress(choke.fortress, Faction.同盟, 100000); // 圧倒的兵力で制圧

            Assert.IsFalse(FortressBlockadeRules.Blocks(choke, Faction.同盟));
            Assert.IsFalse(FortressBlockadeRules.Blocks(choke, Faction.帝国)); // 守備0＝誰も止められない
            Assert.AreEqual(1f, FortressBlockadeRules.MaxAdvanceFraction(choke, Faction.同盟), 1e-4f);
        }

        // ── 再占領の整合（守備を置き直すとまた封鎖できる）──

        [Test]
        public void Regarrison_RestoresBlockade_AgainstPreviousOwner()
        {
            var map = MapWithFortressAndBypass(Faction.帝国, 1000f);
            Corridor choke = Choke(map);
            StrategyRules.AssaultFortress(choke.fortress, Faction.同盟, 100000);

            FortressBlockadeRules.Regarrison(choke.fortress, Faction.同盟, 800f);

            Assert.IsTrue(choke.fortress.controlsCorridor);
            Assert.AreEqual(Faction.同盟, choke.fortress.owner);
            Assert.IsTrue(FortressBlockadeRules.Blocks(choke, Faction.帝国)); // 今度は帝国が締め出される
            Assert.IsFalse(FortressBlockadeRules.Blocks(choke, Faction.同盟));
        }

        [Test]
        public void Regarrison_WithZeroGarrison_LeavesCorridorOpen()
        {
            var map = MapWithFortressAndBypass(Faction.帝国, 1000f);
            Corridor choke = Choke(map);
            FortressBlockadeRules.Regarrison(choke.fortress, Faction.同盟, 0f);

            Assert.IsFalse(choke.fortress.controlsCorridor);              // 守備を残せなければ扼せない
            Assert.IsFalse(FortressBlockadeRules.Blocks(choke, Faction.帝国));
        }

        // ── 迂回の照会（戦略側は回り道を許す＝路線は消さない）──

        [Test]
        public void HasBypass_FindsCommerceDetour_StrategyAllowsGoingAround()
        {
            var map = MapWithFortressAndBypass(Faction.帝国, 1000f);
            Assert.IsTrue(FortressBlockadeRules.HasBypass(map, Choke(map), Faction.同盟, out List<int> route));
            CollectionAssert.AreEqual(new[] { 0, 2, 1 }, route); // 通商回廊で回り込める＝銀河は一本道でない
        }

        [Test]
        public void HasBypass_False_WhenChokeIsTheOnlyLink()
        {
            var map = new GalaxyMap();
            map.AddSystem(Sys(0, Faction.同盟));
            map.AddSystem(Sys(1, Faction.帝国));
            var choke = new Corridor(0, 1, 1f, CorridorType.要衝);
            choke.fortress = new Fortress(1000f, 100f, 1f, true) { owner = Faction.帝国 };
            map.AddCorridor(choke);

            Assert.IsFalse(FortressBlockadeRules.HasBypass(map, choke, Faction.同盟));
            Assert.IsTrue(FortressBlockadeRules.IsCutEdge(map, choke));
        }

        [Test]
        public void IsCutEdge_False_WhenAlternateRouteExists()
        {
            var map = MapWithFortressAndBypass(Faction.帝国, 1000f);
            Assert.IsFalse(FortressBlockadeRules.IsCutEdge(map, Choke(map)));
        }

        // ── 経路探索（#40：封鎖回廊を避ける経路が引ける）──

        [Test]
        public void FindPath_AvoidingFortresses_TakesCommerceDetour()
        {
            var map = MapWithFortressAndBypass(Faction.帝国, 1000f);

            // 既定（従来）＝最短＝要塞回廊を通ってしまう
            CollectionAssert.AreEqual(new[] { 0, 1 }, GalaxyPathfinder.FindPath(map, 0, 1));

            // 要塞を避ける＝遠回りでも通商回廊を選ぶ
            var q = GalaxyPathfinder.PathQuery.AvoidingFortresses(Faction.同盟);
            CollectionAssert.AreEqual(new[] { 0, 2, 1 }, GalaxyPathfinder.FindPath(map, 0, 1, q));
        }

        [Test]
        public void FindPath_AvoidingFortresses_EmptyWhenOnlyRouteIsBlocked()
        {
            var map = new GalaxyMap();
            map.AddSystem(Sys(0, Faction.同盟));
            map.AddSystem(Sys(1, Faction.帝国));
            var choke = new Corridor(0, 1, 1f, CorridorType.要衝);
            choke.fortress = new Fortress(1000f, 100f, 1f, true) { owner = Faction.帝国 };
            map.AddCorridor(choke);

            var q = GalaxyPathfinder.PathQuery.AvoidingFortresses(Faction.同盟);
            Assert.AreEqual(0, GalaxyPathfinder.FindPath(map, 0, 1, q).Count); // 回り道は無い＝制圧するしかない
            CollectionAssert.AreEqual(new[] { 0, 1 }, GalaxyPathfinder.FindPath(map, 0, 1)); // 素の探索は通す
        }

        [Test]
        public void FindPath_OwnerIsNotBlocked_TakesShortestThroughOwnFortress()
        {
            var map = MapWithFortressAndBypass(Faction.帝国, 1000f);
            var q = GalaxyPathfinder.PathQuery.AvoidingFortresses(Faction.帝国); // 自分の要塞は障害にならない
            CollectionAssert.AreEqual(new[] { 0, 1 }, GalaxyPathfinder.FindPath(map, 0, 1, q));
        }
    }
}
