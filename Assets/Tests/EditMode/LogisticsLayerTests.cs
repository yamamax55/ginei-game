using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦時兵站レイヤー（#92 L-1〜L-3）を固定する：資源備蓄と類型別生産（L-1）、補給線の到達とZOC遮断・前線の枯渇（L-2）、
    /// 通商破壊の迎撃と補給の途絶（L-3）。すべて純ロジック。
    /// </summary>
    public class LogisticsLayerTests
    {
        // ===== L-1 資源と生産 =====

        [Test]
        public void Stockpile_AddConsume_FloorsAtZero()
        {
            var s = new ResourceStockpile(10, 0, 0);
            Assert.IsFalse(s.TryConsume(ResourceType.物資, 20)); // 足りない＝消費しない
            Assert.AreEqual(10f, s.supplies, 1e-4f);
            Assert.IsTrue(s.TryConsume(ResourceType.物資, 4));
            Assert.AreEqual(6f, s.supplies, 1e-4f);
            s.Add(ResourceType.物資, -100); // 枯渇は0で止まる
            Assert.AreEqual(0f, s.supplies, 1e-4f);
            Assert.IsTrue(s.IsDepleted); // 弾薬/燃料も0
        }

        [Test]
        public void Production_IndustryMakesAmmoAndSupplies()
        {
            var s = new ResourceStockpile();
            ResourceProductionRules.Produce(s, SystemType.工業, factor: 1f, dt: 1f);
            Assert.AreEqual(ResourceProductionRules.IndustryAmmo, s.ammo, 1e-4f);
            Assert.AreEqual(ResourceProductionRules.IndustrySupplies, s.supplies, 1e-4f);
            Assert.AreEqual(0f, s.fuel, 1e-4f);
        }

        [Test]
        public void Production_TypeSpecific_AndStabilityScaled()
        {
            var farm = new ResourceStockpile();
            ResourceProductionRules.Produce(farm, SystemType.農業, 1f, 1f);
            Assert.AreEqual(ResourceProductionRules.AgricultureSupplies, farm.supplies, 1e-4f);
            Assert.AreEqual(0f, farm.fuel, 1e-4f);

            var mine = new ResourceStockpile();
            ResourceProductionRules.Produce(mine, SystemType.鉱業, 0.5f, 1f); // 安定度半分で半減
            Assert.AreEqual(ResourceProductionRules.MiningFuel * 0.5f, mine.fuel, 1e-4f);
        }

        // ===== 惑星単位の産出（#767 ハイブリッド：惑星が産出の単一の真実・星系は集約）=====

        [Test]
        public void Production_FromProvince_UsesPlanetTypeAndStability()
        {
            var s = new ResourceStockpile();
            // 安定度MAX＝OutputFactor 1.0 → 鉱業惑星は燃料率そのまま、他資源は0
            var mine = new Province(1, "民主", 100f) { systemType = SystemType.鉱業, stability = GovernanceRules.MaxStability };
            ResourceProductionRules.ProduceFromProvince(s, mine, 1f);
            Assert.AreEqual(ResourceProductionRules.MiningFuel, s.fuel, 1e-3f);
            Assert.AreEqual(0f, s.supplies, 1e-4f);
            // ProvinceRate は備蓄を変えず実効率を返す（表示用）
            Assert.AreEqual(ResourceProductionRules.MiningFuel, ResourceProductionRules.ProvinceRate(mine, ResourceType.燃料), 1e-3f);
        }

        [Test]
        public void Production_LowStabilityPlanet_ProducesLess()
        {
            // 安定度0でも MinOutputFactor で最低限は出る（支配≠即産出＝完全停止はしない）
            var s = new ResourceStockpile();
            var farm = new Province(2, "専制", 100f) { systemType = SystemType.農業, stability = 0f };
            ResourceProductionRules.ProduceFromProvince(s, farm, 1f);
            Assert.AreEqual(ResourceProductionRules.AgricultureSupplies * GovernanceRules.MinOutputFactor, s.supplies, 1e-3f);
        }

        [Test]
        public void Production_FromSystem_AggregatesPlanets()
        {
            // 星系＝惑星の集約：工業惑星＋農業惑星（ともに安定度MAX）の産出を合算
            var s = new ResourceStockpile();
            var planets = new List<Province>
            {
                new Province(3, "民主", 100f) { systemType = SystemType.工業, stability = GovernanceRules.MaxStability },
                new Province(3, "民主", 100f) { systemType = SystemType.農業, stability = GovernanceRules.MaxStability },
            };
            ResourceProductionRules.ProduceFromSystem(s, planets, 1f);
            Assert.AreEqual(ResourceProductionRules.IndustrySupplies + ResourceProductionRules.AgricultureSupplies, s.supplies, 1e-3f);
            Assert.AreEqual(ResourceProductionRules.IndustryAmmo, s.ammo, 1e-3f);
        }

        [Test]
        public void Production_DefaultProvince_IsHabitation()
        {
            // 既定の Province は居住類型＝少量物資のみ（後方互換）
            var s = new ResourceStockpile();
            var p = new Province(4, "民主", 100f) { stability = GovernanceRules.MaxStability };
            Assert.AreEqual(SystemType.居住, p.systemType);
            ResourceProductionRules.ProduceFromProvince(s, p, 1f);
            Assert.AreEqual(ResourceProductionRules.HabitationSupplies, s.supplies, 1e-3f);
            Assert.AreEqual(0f, s.fuel, 1e-4f);
        }

        // ===== L-2 補給線 =====

        private static GalaxyMap LineMap(params Faction[] owners)
        {
            // 0-1-2-3 の一直線。owners[i] が星系 i の所有勢力。
            var m = new GalaxyMap();
            for (int i = 0; i < owners.Length; i++)
                m.AddSystem(new StarSystem(i, "S" + i, Vector2.zero, owners[i]));
            for (int i = 0; i < owners.Length - 1; i++)
                m.AddCorridor(new Corridor(i, i + 1, 1f));
            return m;
        }

        [Test]
        public void Supply_FlowsAlongOwnedCorridors()
        {
            // 全部帝国＝後方0から前線3まで補給が届く
            var m = LineMap(Faction.帝国, Faction.帝国, Faction.帝国, Faction.帝国);
            Assert.IsTrue(SupplyRules.IsSupplied(m, Faction.帝国, new[] { 0 }, target: 3));
        }

        [Test]
        public void Supply_CutByEnemyOwnedSystem()
        {
            // 星系2が同盟＝補給線が途切れる → 前線3へ届かない
            var m = LineMap(Faction.帝国, Faction.帝国, Faction.同盟, Faction.帝国);
            Assert.IsFalse(SupplyRules.IsSupplied(m, Faction.帝国, new[] { 0 }, target: 3));
            Assert.IsTrue(SupplyRules.IsSupplied(m, Faction.帝国, new[] { 0 }, target: 1)); // 手前までは届く
        }

        [Test]
        public void Supply_CutByEnemyZoc_BlockedSystem()
        {
            // 全部帝国だが星系2が敵ZOC下（blocked）＝そこを通せない → 前線3へ届かない
            var m = LineMap(Faction.帝国, Faction.帝国, Faction.帝国, Faction.帝国);
            var zoc = new HashSet<int> { 2 };
            Assert.IsFalse(SupplyRules.IsSupplied(m, Faction.帝国, new[] { 0 }, 3, zoc));
        }

        [Test]
        public void TickFront_StarvesWhenCut_RecoversWhenSupplied()
        {
            var front = new ResourceStockpile(10, 10, 10);
            SupplyRules.TickFront(front, supplied: false, resupplyRate: 5f, consumeRate: 4f, dt: 1f); // -4 each
            Assert.AreEqual(6f, front.supplies, 1e-4f);
            SupplyRules.TickFront(front, supplied: true, 5f, 4f, 1f); // +5 each
            Assert.AreEqual(11f, front.supplies, 1e-4f);

            // 断たれ続ければ枯渇＝飢餓
            for (int i = 0; i < 10; i++) SupplyRules.TickFront(front, false, 5f, 4f, 1f);
            Assert.IsTrue(front.IsDepleted);
        }

        // ===== L-3 通商破壊 =====

        [Test]
        public void Convoy_DestroyedWhenRaiderOutgunsEscort()
        {
            Assert.IsTrue(CommerceRaidingRules.ConvoyDestroyed(raiderStrength: 100, escortStrength: 50));
            Assert.IsFalse(CommerceRaidingRules.ConvoyDestroyed(100, 100)); // 護衛が拮抗＝守り切る
            Assert.IsFalse(CommerceRaidingRules.ConvoyDestroyed(100, 80, convoySelfDefense: 30)); // 自衛込みで守る
        }

        [Test]
        public void EscortNeeded_AccountsForSelfDefense()
        {
            Assert.AreEqual(70f, CommerceRaidingRules.EscortNeeded(100, convoySelfDefense: 30), 1e-4f);
        }

        [Test]
        public void ResolveInterception_StarvesFrontWhenConvoyDestroyed()
        {
            var front = new ResourceStockpile(0, 0, 0);

            // 護衛不足→船団撃破→補給0（前線は干上がったまま）
            float d1 = CommerceRaidingRules.ResolveInterception(front, convoyPayload: 50, raiderStrength: 100, escortStrength: 20);
            Assert.AreEqual(0f, d1, 1e-4f);
            Assert.IsTrue(front.IsDepleted);

            // 護衛十分→船団到達→補給が届く
            float d2 = CommerceRaidingRules.ResolveInterception(front, 50, 100, 120);
            Assert.AreEqual(50f, d2, 1e-4f);
            Assert.IsFalse(front.IsDepleted);
        }
    }
}
