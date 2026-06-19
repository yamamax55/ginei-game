using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>戦略爆撃の純ロジックの回帰テスト（既定 Params で期待値固定）。</summary>
    public class StrategicBombingRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void BombingReach_SubtractsAirDefense()
        {
            // 0.8 - 0.3*1.0 = 0.5
            Assert.AreEqual(0.5f, StrategicBombingRules.BombingReach(0.8f, 0.3f), Eps);
        }

        [Test]
        public void BombingReach_HeavyDefenseBlocksBreakthrough()
        {
            // 0.3 - 0.9 < 0 → クランプで0
            Assert.AreEqual(0f, StrategicBombingRules.BombingReach(0.3f, 0.9f), Eps);
        }

        [Test]
        public void IndustrialDamage_ScalesWithConcentration()
        {
            // 0.5 * 0.6 = 0.30
            Assert.AreEqual(0.3f, StrategicBombingRules.IndustrialDamage(0.5f, 0.6f), Eps);
        }

        [Test]
        public void ProductionReduction_RedundancyResists()
        {
            // 0.3 * (1-0.4) = 0.18
            Assert.AreEqual(0.18f, StrategicBombingRules.ProductionReduction(0.3f, 0.4f), Eps);
            // 完全冗長なら生産低下ゼロ
            Assert.AreEqual(0f, StrategicBombingRules.ProductionReduction(0.9f, 1f), Eps);
        }

        [Test]
        public void MoraleEffect_ResilienceSoftensIt()
        {
            // 0.5 * (1-0.25) = 0.375
            Assert.AreEqual(0.375f, StrategicBombingRules.MoraleEffect(0.5f, 0.25f), Eps);
        }

        [Test]
        public void ResolveHardening_IndiscriminateStiffensEnemy()
        {
            // 0.6 * 0.5 = 0.30
            Assert.AreEqual(0.3f, StrategicBombingRules.ResolveHardening(0.6f), Eps);
            // 精密爆撃（無差別度0）なら逆効果なし
            Assert.AreEqual(0f, StrategicBombingRules.ResolveHardening(0f), Eps);
        }

        [Test]
        public void BomberAttrition_DefenseCostsBombers()
        {
            // 0.8 * 0.5 * 0.3 = 0.12
            Assert.AreEqual(0.12f, StrategicBombingRules.BomberAttrition(0.8f, 0.5f), Eps);
        }

        [Test]
        public void CumulativeWarEffort_SaturatesOverTime()
        {
            // 0.4 * (10/(10+10)) = 0.4*0.5 = 0.20
            Assert.AreEqual(0.2f, StrategicBombingRules.CumulativeWarEffort(0.4f, 10f), Eps);
            // 持続0なら累積なし
            Assert.AreEqual(0f, StrategicBombingRules.CumulativeWarEffort(0.4f, 0f), Eps);
            // 長く叩くほど瞬間値へ近づく（単調増加）
            float shortRun = StrategicBombingRules.CumulativeWarEffort(0.4f, 10f);
            float longRun = StrategicBombingRules.CumulativeWarEffort(0.4f, 100f);
            Assert.Less(shortRun, longRun);
        }

        [Test]
        public void IsWarEconomyCrippled_ThresholdBoolean()
        {
            Assert.IsTrue(StrategicBombingRules.IsWarEconomyCrippled(0.5f, 0.4f));
            Assert.IsFalse(StrategicBombingRules.IsWarEconomyCrippled(0.3f, 0.4f));
        }

        // 物語テスト：精密爆撃で敵の戦争経済を麻痺させる連鎖
        [Test]
        public void Narrative_PrecisionStrikeCripplesWarEconomy()
        {
            var p = StrategicBombingParams.Default;

            // 厚い爆撃機戦力が薄い防空を抜く
            float reach = StrategicBombingRules.BombingReach(0.9f, 0.2f, p); // 0.9-0.2=0.7
            Assert.AreEqual(0.7f, reach, Eps);

            // 一点に集中した敵産業を叩く
            float damage = StrategicBombingRules.IndustrialDamage(reach, 0.9f); // 0.63
            Assert.AreEqual(0.63f, damage, Eps);

            // 冗長性が低いので損害がそのまま生産低下に
            float reduction = StrategicBombingRules.ProductionReduction(damage, 0.1f); // 0.63*0.9=0.567
            Assert.AreEqual(0.567f, reduction, Eps);

            // 閾値0.5を越えて戦争経済は麻痺
            Assert.IsTrue(StrategicBombingRules.IsWarEconomyCrippled(reduction, 0.5f));

            // 精密爆撃（無差別度0）なら敵の結束硬化は起きない
            float hardening = StrategicBombingRules.ResolveHardening(0f, p);
            Assert.AreEqual(0f, hardening, Eps);

            // 叩き続ければ戦争遂行能力が累積的に削れる
            float cumulative = StrategicBombingRules.CumulativeWarEffort(reduction, 30f, p); // 0.567*0.75
            Assert.AreEqual(0.42525f, cumulative, 1e-3f);
        }
    }
}
