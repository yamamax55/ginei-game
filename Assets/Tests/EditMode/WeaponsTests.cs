using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 兵器メーカー（防衛 contractor・#2020・<see cref="WeaponsRules"/>）を固定する：兵器開発と性能(WPN-1)、防衛調達(WPN-2)、
    /// 特需と平時(WPN-3)、武器輸出(WPN-4)、戦力供給(WPN-5)。
    /// </summary>
    public class WeaponsTests
    {
        // ===== WPN-1 兵器開発と性能 =====
        [Test]
        public void Development_PerformanceAndOverrun()
        {
            Assert.AreEqual(150f, WeaponsRules.WeaponPerformance(100f, 5f, 0.1f), 1e-3f); // R&D5で性能1.5倍
            Assert.AreEqual(1500f, WeaponsRules.DevelopmentOverrun(1000f, 0.5f, 1.0f), 1e-3f); // 複雑度で5割超過
        }

        // ===== WPN-2 防衛調達 =====
        [Test]
        public void Procurement_CostPlusVsCompetitive()
        {
            Assert.AreEqual(1150f, WeaponsRules.CostPlusPrice(1000f, 0.15f), 1e-3f); // 原価+15%（削減動機弱）
            Assert.AreEqual(1075f, WeaponsRules.CompetitiveBidPrice(1000f, 0.15f, 0.5f), 1e-3f); // 競争で利益圧縮
            Assert.AreEqual(11500f, WeaponsRules.ProcurementRevenue(10f, 1150f), 1e-3f);
        }

        // ===== WPN-3 特需と平時 =====
        [Test]
        public void Wartime_SurgeAndPeacetimeIdle()
        {
            Assert.AreEqual(300f, WeaponsRules.WartimeDemand(100f, 1.0f, 3f), 1e-3f); // 総力戦で3倍
            Assert.AreEqual(200f, WeaponsRules.WartimeDemand(100f, 0.5f, 3f), 1e-3f);
            Assert.AreEqual(300f, WeaponsRules.PeacetimeIdleCost(100f, 0.4f, 5f), 1e-3f); // 稼働4割→遊休コスト
        }

        // ===== WPN-4 武器輸出 =====
        [Test]
        public void Export_DualUseAndRestriction()
        {
            Assert.AreEqual(26000f, WeaponsRules.ExportRevenue(20f, 1300f), 1e-3f);
            Assert.AreEqual(26000f, WeaponsRules.DualUseSales(10f, 10f, 1300f), 1e-3f); // 両陣営に売る
            Assert.IsTrue(WeaponsRules.CanExport(false, true));  // 規制なし＝敵対先にも売れる
            Assert.IsFalse(WeaponsRules.CanExport(true, true));  // 規制下＝敵対先には売れない
            Assert.IsTrue(WeaponsRules.CanExport(true, false));  // 非敵対なら可
        }

        // ===== WPN-5 戦力供給 =====
        [Test]
        public void StrengthSupply_ToFleetPool()
        {
            // 高性能兵器ほど1機あたり戦力が高い：10機×(性能150/基準100)=15
            Assert.AreEqual(15f, WeaponsRules.WeaponStrengthYield(10f, 150f, 100f), 1e-3f);
            Assert.AreEqual(0f, WeaponsRules.WeaponStrengthYield(10f, 150f, 0f), 1e-3f);
            // 戦力プール（#148）へ供給
            FleetPool.Clear();
            Assert.AreEqual(15, WeaponsRules.CommissionWeapons(Faction.帝国, 15));
            Assert.AreEqual(15, FleetPool.Get(Faction.帝国));
            FleetPool.Clear();
        }
    }
}
