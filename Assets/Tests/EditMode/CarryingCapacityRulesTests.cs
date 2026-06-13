using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>食糧天井（収容力）と食糧ストレス比の純ロジック（MALT-1 #1574）の担保。既定 Params の具体値で期待値固定。</summary>
    public class CarryingCapacityRulesTests
    {
        /// <summary>収容力＝農業産出×肥沃度倍率×技術倍率。肥沃度1・技術1なら産出そのまま、0なら倍率0.25で目減り。</summary>
        [Test]
        public void CarryingCapacity_AgricultureScaledByFertilityAndTech()
        {
            // 既定：scale1・肥沃度重み0.5・技術重み0.5
            Assert.AreEqual(0.8f, CarryingCapacityRules.CarryingCapacity(0.8f, 1f, 1f), 1e-4f);
            // 肥沃度0・技術0＝それぞれ倍率0.5＝0.8×0.5×0.5
            Assert.AreEqual(0.2f, CarryingCapacityRules.CarryingCapacity(0.8f, 0f, 0f), 1e-4f);
            // 農業産出0＝養えない＝収容力0
            Assert.AreEqual(0f, CarryingCapacityRules.CarryingCapacity(0f, 1f, 1f), 1e-4f);
        }

        /// <summary>FoodStressRatio＝人口÷収容力。1未満は余裕・1超は逼迫。収容力ほぼ0でも0割せず大きな値。</summary>
        [Test]
        public void FoodStressRatio_PopulationOverCapacity()
        {
            Assert.AreEqual(0.625f, CarryingCapacityRules.FoodStressRatio(0.5f, 0.8f), 1e-4f); // 天井内
            Assert.AreEqual(2.0f, CarryingCapacityRules.FoodStressRatio(1.0f, 0.5f), 1e-4f);   // 天井超過
            Assert.Greater(CarryingCapacityRules.FoodStressRatio(0.5f, 0f), 100f);             // 0割回避＝巨大値
        }

        /// <summary>食糧余剰＝収容力−人口。正なら余裕・負なら天井超過の不足。</summary>
        [Test]
        public void FoodSurplus_SignReflectsSlackOrShortage()
        {
            Assert.AreEqual(0.3f, CarryingCapacityRules.FoodSurplus(0.8f, 0.5f), 1e-4f);   // 余裕
            Assert.AreEqual(-0.3f, CarryingCapacityRules.FoodSurplus(0.5f, 0.8f), 1e-4f);  // 不足
        }

        /// <summary>生存圧は天井内（比≤1）で0、超過分が2乗で非線形に効く＝天井を超えると急に苦しい。</summary>
        [Test]
        public void SubsistencePressure_NonlinearAboveCeiling()
        {
            Assert.AreEqual(0f, CarryingCapacityRules.SubsistencePressure(0.9f), 1e-4f); // 天井内＝圧なし
            Assert.AreEqual(0f, CarryingCapacityRules.SubsistencePressure(1.0f), 1e-4f); // ぴったり＝圧なし
            Assert.AreEqual(0.25f, CarryingCapacityRules.SubsistencePressure(1.5f), 1e-4f); // (0.5)^2
            Assert.AreEqual(1.0f, CarryingCapacityRules.SubsistencePressure(2.0f), 1e-4f);  // (1.0)^2
            // 非線形＝超過2倍で圧は4倍（線形なら2倍のはず）
            float a = CarryingCapacityRules.SubsistencePressure(1.5f);
            float b = CarryingCapacityRules.SubsistencePressure(2.0f);
            Assert.Greater(b, a * 2f);
        }

        /// <summary>マルサスの罠＝天井近接で深まるが技術成長が緩める。技術成長0で最大・成長で出口。</summary>
        [Test]
        public void MalthusianTrap_TechGrowthEases()
        {
            Assert.AreEqual(1.0f, CarryingCapacityRules.MalthusianTrap(1.0f, 0f), 1e-4f);  // 天井×技術停滞＝罠最大
            Assert.AreEqual(0.5f, CarryingCapacityRules.MalthusianTrap(1.0f, 0.5f), 1e-4f); // 技術が半分緩める
            Assert.AreEqual(0f, CarryingCapacityRules.MalthusianTrap(1.0f, 1f), 1e-4f);     // 技術が天井を押し上げ＝罠消失
            Assert.AreEqual(0.5f, CarryingCapacityRules.MalthusianTrap(0.5f, 0f), 1e-4f);   // 天井に余裕＝罠浅い
        }

        /// <summary>収容力の漸増＝投資比例で等差的にゆっくり上がる（食糧は等差級数）。</summary>
        [Test]
        public void CapacityGrowthTick_SlowLinearIncrease()
        {
            // 0.5 + 0.05×投資1×dt2 = 0.6
            Assert.AreEqual(0.6f, CarryingCapacityRules.CapacityGrowthTick(0.5f, 1f, 2f), 1e-4f);
            // 投資0＝増えない
            Assert.AreEqual(0.5f, CarryingCapacityRules.CapacityGrowthTick(0.5f, 0f, 2f), 1e-4f);
        }

        /// <summary>過剰人口判定＝ストレス比が既定しきい値1.1以上で飢餓リスク。</summary>
        [Test]
        public void IsOvercapacity_ThresholdGate()
        {
            Assert.IsFalse(CarryingCapacityRules.IsOvercapacity(1.0f)); // 天井ぴったりは許容
            Assert.IsFalse(CarryingCapacityRules.IsOvercapacity(1.05f)); // 余裕しきい値1.1未満
            Assert.IsTrue(CarryingCapacityRules.IsOvercapacity(1.2f));  // 恒常的過剰
        }

        /// <summary>安全人口＝収容力×(1−バッファ)。天井いっぱいは危険＝余裕を残す。</summary>
        [Test]
        public void OptimalPopulation_LeavesBuffer()
        {
            Assert.AreEqual(0.8f, CarryingCapacityRules.OptimalPopulation(1.0f, 0.2f), 1e-4f); // 2割の備え
            Assert.AreEqual(1.0f, CarryingCapacityRules.OptimalPopulation(1.0f, 0f), 1e-4f);   // バッファ0＝天井ぴったり
        }
    }
}
