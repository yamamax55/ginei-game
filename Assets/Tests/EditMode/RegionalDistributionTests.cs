using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 勢力内供給配分（#2112）：需給(DIST-1)/連結(DIST-2)/プール(DIST-3)/配分(DIST-4)/適用(DIST-5)/配送(DIST-6)。
    /// </summary>
    public class RegionalDistributionTests
    {
        // --- DIST-1 需給バランス ---
        [Test]
        public void Balance_SurplusDeficit()
        {
            Assert.AreEqual(60f, SupplyBalanceRules.Surplus(100f, 40f), 1e-3f);
            Assert.AreEqual(0f, SupplyBalanceRules.Surplus(20f, 50f), 1e-3f);
            Assert.AreEqual(30f, SupplyBalanceRules.Deficit(20f, 50f), 1e-3f);
            Assert.AreEqual(60f, SupplyBalanceRules.NetPosition(100f, 40f), 1e-3f);
            Assert.AreEqual(-30f, SupplyBalanceRules.NetPosition(70f, 100f), 1e-3f);
        }

        // --- DIST-2 連結成分（通商破壊で分断） ---
        [Test]
        public void Reachability_ConnectedComponentsAndBlocking()
        {
            var map = new GalaxyMap();
            map.AddSystem(new StarSystem(0, "A", Vector2.zero, Faction.帝国));
            map.AddSystem(new StarSystem(1, "B", Vector2.zero, Faction.帝国));
            map.AddSystem(new StarSystem(2, "C", Vector2.zero, Faction.帝国));
            map.AddSystem(new StarSystem(3, "D", Vector2.zero, Faction.同盟)); // 敵領
            map.AddCorridor(new Corridor(0, 1));
            map.AddCorridor(new Corridor(1, 2));
            map.AddCorridor(new Corridor(2, 3));

            var comp = RegionReachabilityRules.ConnectedComponent(map, Faction.帝国, 0, null);
            Assert.AreEqual(3, comp.Count); // 0,1,2（3は同盟で除外）
            Assert.IsTrue(comp.Contains(2));
            Assert.IsFalse(comp.Contains(3));

            // 星系1を遮断（通商破壊）→ 0から1へ行けず分断
            var blocked = new System.Collections.Generic.HashSet<int> { 1 };
            var comp2 = RegionReachabilityRules.ConnectedComponent(map, Faction.帝国, 0, blocked);
            Assert.AreEqual(1, comp2.Count); // {0} のみ

            Assert.AreEqual(1, RegionReachabilityRules.Components(map, Faction.帝国, null).Count);       // 一体
            Assert.AreEqual(2, RegionReachabilityRules.Components(map, Faction.帝国, blocked).Count);    // {0}と{2}に分断
        }

        // --- DIST-3 プール集計 ---
        [Test]
        public void Pool_SurplusDeficitTransportableDelivered()
        {
            var prod = new[] { 100f, 20f, 0f };
            var demand = new[] { 40f, 50f, 30f };
            Assert.AreEqual(60f, DistributionPoolRules.TotalSurplus(prod, demand), 1e-3f);
            Assert.AreEqual(60f, DistributionPoolRules.TotalDeficit(prod, demand), 1e-3f);
            Assert.AreEqual(60f, DistributionPoolRules.Transportable(60f, 100f), 1e-3f);
            Assert.AreEqual(50f, DistributionPoolRules.Transportable(60f, 50f), 1e-3f); // 回廊容量律速
            Assert.AreEqual(54f, DistributionPoolRules.Delivered(60f, 0.1f), 1e-3f);    // 輸送ロス
            Assert.AreEqual(0.9f, DistributionPoolRules.FillRate(54f, 60f), 1e-4f);
        }

        // --- DIST-4 配分 ---
        [Test]
        public void Allocation_PullsAndReceives()
        {
            var pulls = PoolAllocationRules.Pulls(new[] { 60f, 0f, 0f }, 60f);
            Assert.AreEqual(60f, pulls[0], 1e-3f);
            Assert.AreEqual(0f, pulls[1], 1e-3f);
            var recv = PoolAllocationRules.Receives(new[] { 0f, 30f, 30f }, 54f);
            Assert.AreEqual(0f, recv[0], 1e-3f);
            Assert.AreEqual(27f, recv[1], 1e-3f); // 54×0.5
            Assert.AreEqual(27f, recv[2], 1e-3f);
        }

        // --- DIST-5 適用 ---
        [Test]
        public void Apply_PullReceiveAndMove()
        {
            const int c = 1;
            var s0 = new CommodityStock(); s0.Add(c, 100f);
            var s1 = new CommodityStock(); s1.Add(c, 20f);
            var s2 = new CommodityStock(); // 0
            RedistributionApplyRules.Apply(new[] { s0, s1, s2 }, c, new[] { 60f, 0f, 0f }, new[] { 0f, 27f, 27f });
            Assert.AreEqual(40f, s0.Get(c), 1e-3f);
            Assert.AreEqual(47f, s1.Get(c), 1e-3f);
            Assert.AreEqual(27f, s2.Get(c), 1e-3f);

            var from = new CommodityStock(); from.Add(c, 100f);
            var to = new CommodityStock();
            RedistributionApplyRules.Move(from, to, c, 40f, 0.1f);
            Assert.AreEqual(60f, from.Get(c), 1e-3f);
            Assert.AreEqual(36f, to.Get(c), 1e-3f); // 40×0.9
        }

        // --- DIST-6 配送（領域1パス） ---
        [Test]
        public void Distribute_RegionPass()
        {
            const int c = 1;
            var s0 = new CommodityStock(); s0.Add(c, 100f); // 余剰60（需要40）
            var s1 = new CommodityStock(); s1.Add(c, 20f);  // 不足30（需要50）
            var s2 = new CommodityStock();                  // 不足30（需要30）
            var stocks = new[] { s0, s1, s2 };
            var demand = new[] { 40f, 50f, 30f };
            var r = RegionalDistributionTickRules.Distribute(stocks, c, demand, float.MaxValue, 0.1f);

            Assert.AreEqual(60f, r.totalSurplus, 1e-3f);
            Assert.AreEqual(60f, r.totalDeficit, 1e-3f);
            Assert.AreEqual(60f, r.transportable, 1e-3f);
            Assert.AreEqual(54f, r.delivered, 1e-3f);
            Assert.AreEqual(0.9f, r.fillRate, 1e-4f);
            // 余剰惑星から引かれ、不足惑星へ配送された（輸送ロス6は消える）。
            Assert.AreEqual(40f, s0.Get(c), 1e-3f);
            Assert.AreEqual(47f, s1.Get(c), 1e-3f);
            Assert.AreEqual(27f, s2.Get(c), 1e-3f);
        }
    }
}
