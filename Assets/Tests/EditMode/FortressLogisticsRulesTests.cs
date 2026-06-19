using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 要塞兵站：備蓄容量・消費率・攻囲下の持久日数・主砲エネルギー消費・補給途絶度・実効備蓄・生活維持の持続・自力持久判定。
    /// 既定 Params で期待値固定（1e-4f）。
    /// </summary>
    public class FortressLogisticsRulesTests
    {
        [Test]
        public void StockpileCapacity_VolumeTimesInfra()
        {
            Assert.AreEqual(500f, FortressLogisticsRules.StockpileCapacity(100f, 5f), 1e-4f); // 100×5×1.0
            Assert.AreEqual(0f, FortressLogisticsRules.StockpileCapacity(-10f, 5f), 1e-4f);   // 負入力は0
        }

        [Test]
        public void ConsumptionRate_GarrisonGunAndTempo()
        {
            Assert.AreEqual(10f, FortressLogisticsRules.ConsumptionRate(1000f, 0f, 0f), 1e-4f); // 1000×0.01×1
            Assert.AreEqual(60f, FortressLogisticsRules.ConsumptionRate(1000f, 1f, 1f), 1e-4f); // 1000×0.03×2
        }

        [Test]
        public void SiegeEndurance_StockOverConsumption()
        {
            Assert.AreEqual(60f, FortressLogisticsRules.SiegeEndurance(600f, 10f), 1e-4f); // 600/10
            // 消費0は実質無限＝極大
            Assert.Greater(FortressLogisticsRules.SiegeEndurance(600f, 0f), 1e6f);
        }

        [Test]
        public void MainGunEnergyDrain_FrequencyTimesYield()
        {
            Assert.AreEqual(100f, FortressLogisticsRules.MainGunEnergyDrain(2f, 50f), 1e-4f); // 2×50×1.0
            Assert.AreEqual(0f, FortressLogisticsRules.MainGunEnergyDrain(-1f, 50f), 1e-4f);  // 負入力は0
        }

        [Test]
        public void SupplyInterdiction_BlockadeRatio()
        {
            Assert.AreEqual(0.75f, FortressLogisticsRules.SupplyInterdiction(75f, 25f), 1e-4f); // 75/100
            Assert.AreEqual(0f, FortressLogisticsRules.SupplyInterdiction(0f, 0f), 1e-4f);      // 両者0は途絶なし
        }

        [Test]
        public void EffectiveStockpile_ReducedByInterdiction()
        {
            Assert.AreEqual(125f, FortressLogisticsRules.EffectiveStockpile(500f, 0.75f), 1e-4f); // 500×0.25
            Assert.AreEqual(500f, FortressLogisticsRules.EffectiveStockpile(500f, 0f), 1e-4f);    // 途絶なしは全量
        }

        [Test]
        public void LifeSupportSustainment_StockOverGarrison()
        {
            Assert.AreEqual(500f, FortressLogisticsRules.LifeSupportSustainment(500f, 200f), 1e-4f); // 500/(200×0.005)
            // 人員0は実質無限＝極大
            Assert.Greater(FortressLogisticsRules.LifeSupportSustainment(500f, 0f), 1e6f);
        }

        [Test]
        public void IsSelfSustaining_AgainstThreshold()
        {
            Assert.IsFalse(FortressLogisticsRules.IsSelfSustaining(60f));  // 60<180
            Assert.IsTrue(FortressLogisticsRules.IsSelfSustaining(200f));  // 200>=180
            Assert.IsTrue(FortressLogisticsRules.IsSelfSustaining(50f, 30f)); // 委譲版＝閾値30
        }

        [Test]
        public void Narrative_IserlohnHoldsSixMonthSiegeButNotSelfSustaining()
        {
            // 大要塞：貯蔵容積200×兵站インフラ10＝備蓄2000。
            float cap = FortressLogisticsRules.StockpileCapacity(200f, 10f);
            Assert.AreEqual(2000f, cap, 1e-4f);

            // 駐留2000・主砲未使用・平時活動＝消費20/日。
            float consume = FortressLogisticsRules.ConsumptionRate(2000f, 0f, 0f);
            Assert.AreEqual(20f, consume, 1e-4f);

            // 攻囲下の持久＝2000/20＝100日。半年(180日)には届かず自力持久ではない。
            float endurance = FortressLogisticsRules.SiegeEndurance(cap, consume);
            Assert.AreEqual(100f, endurance, 1e-4f);
            Assert.IsFalse(FortressLogisticsRules.IsSelfSustaining(endurance));

            // 強固な封鎖(封鎖80 vs 突破20)＝途絶0.8。外部から使える実効備蓄は2割へ。
            float interdiction = FortressLogisticsRules.SupplyInterdiction(80f, 20f);
            Assert.AreEqual(0.8f, interdiction, 1e-4f);
            float effective = FortressLogisticsRules.EffectiveStockpile(cap, interdiction);
            Assert.AreEqual(400f, effective, 1e-4f); // 2000×0.2

            // 主砲(トゥール・ハンマー)を撃てばエネルギー消費は跳ね上がる。
            float drain = FortressLogisticsRules.MainGunEnergyDrain(1f, 500f);
            Assert.AreEqual(500f, drain, 1e-4f);
        }
    }
}
