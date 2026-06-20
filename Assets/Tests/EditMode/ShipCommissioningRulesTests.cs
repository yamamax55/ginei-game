using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>艦の就役（戦力化）：艤装・初期不具合・乗員習熟・慣熟航海・戦力化・就役所要・量産習熟。</summary>
    public class ShipCommissioningRulesTests
    {
        [Test]
        public void FittingOut_HullTimesEquipment()
        {
            // 0.9 × 0.8 = 0.72。
            Assert.AreEqual(0.72f, ShipCommissioningRules.FittingOutProgress(0.9f, 0.8f), 1e-4f);
            // 入力 clamp：負/超過は 0..1 へ。
            Assert.AreEqual(0f, ShipCommissioningRules.FittingOutProgress(-1f, 0.5f), 1e-4f);
            Assert.AreEqual(1f, ShipCommissioningRules.FittingOutProgress(2f, 2f), 1e-4f);
        }

        [Test]
        public void Teething_LowMaturityLowQualityRaisesProblems()
        {
            // (1-0.5)×(1-0.4) = 0.5×0.6 = 0.30。
            Assert.AreEqual(0.30f, ShipCommissioningRules.TeethingProblems(0.5f, 0.4f), 1e-4f);
            // 成熟も品質も完璧なら不具合ゼロ。
            Assert.AreEqual(0f, ShipCommissioningRules.TeethingProblems(1f, 1f), 1e-4f);
            // 最悪でも上限0.8でクランプ（(1-0)×(1-0)=1.0 だが上限）。
            Assert.AreEqual(0.8f, ShipCommissioningRules.TeethingProblems(0f, 0f), 1e-4f);
        }

        [Test]
        public void CrewFamiliarization_DiminishingReturns()
        {
            // 1×1=1 → 1^0.5 = 1.0。
            Assert.AreEqual(1.0f, ShipCommissioningRules.CrewFamiliarization(1f, 1f), 1e-3f);
            // 0.64×1=0.64 → √0.64 = 0.8（逓減で線形0.64より高い）。
            Assert.AreEqual(0.8f, ShipCommissioningRules.CrewFamiliarization(0.64f, 1f), 1e-3f);
            // 訓練ゼロなら習熟ゼロ。
            Assert.AreEqual(0f, ShipCommissioningRules.CrewFamiliarization(1f, 0f), 1e-4f);
        }

        [Test]
        public void Shakedown_FittingMinusTeething()
        {
            // 0.72 − 0.30×0.7(=0.21) = 0.51。
            Assert.AreEqual(0.51f, ShipCommissioningRules.ShakedownProgress(0.72f, 0.30f), 1e-4f);
            // 不具合が大きいと進捗は0でクランプ。
            Assert.AreEqual(0f, ShipCommissioningRules.ShakedownProgress(0.2f, 1f), 1e-4f);
        }

        [Test]
        public void CombatReadiness_ShakedownTimesCrew()
        {
            // 0.8 × 0.5 = 0.40。
            Assert.AreEqual(0.40f, ShipCommissioningRules.CombatReadiness(0.8f, 0.5f), 1e-4f);
            // どちらかが0なら戦力化0。
            Assert.AreEqual(0f, ShipCommissioningRules.CombatReadiness(0f, 1f), 1e-4f);
        }

        [Test]
        public void CommissioningTime_ImmaturityAndEfficiency()
        {
            // 成熟1.0・効率1.0 → 30×(1+0)/1 = 30。
            Assert.AreEqual(30f, ShipCommissioningRules.CommissioningTime(1f, 1f), 1e-4f);
            // 成熟0.5・効率2.0 → 30×1.5/2 = 22.5。
            Assert.AreEqual(22.5f, ShipCommissioningRules.CommissioningTime(0.5f, 2f), 1e-4f);
            // 効率0は下限0.01で0割回避（巨大だが有限）。
            Assert.Greater(ShipCommissioningRules.CommissioningTime(0f, 0f), 1000f);
        }

        [Test]
        public void SerialLearning_GrowsToCapWithUnits()
        {
            // 1隻目＝習熟1.0（学習なし）。
            Assert.AreEqual(1.0f, ShipCommissioningRules.SerialProductionLearning(1), 1e-4f);
            Assert.AreEqual(1.0f, ShipCommissioningRules.SerialProductionLearning(0), 1e-4f);
            // 11隻＝extra10・x=0.1×10=1.0・saturate=0.5 → 1+(0.5)×0.5 = 1.25。
            Assert.AreEqual(1.25f, ShipCommissioningRules.SerialProductionLearning(11), 1e-3f);
            // 大量生産は上限1.5へ漸近（超えない）。
            Assert.LessOrEqual(ShipCommissioningRules.SerialProductionLearning(100000), 1.5f);
            Assert.Greater(ShipCommissioningRules.SerialProductionLearning(100000), 1.45f);
        }

        [Test]
        public void IsCommissioned_ThresholdGate()
        {
            // 既定閾値0.7。
            Assert.IsTrue(ShipCommissioningRules.IsCommissioned(0.7f));
            Assert.IsTrue(ShipCommissioningRules.IsCommissioned(0.9f));
            Assert.IsFalse(ShipCommissioningRules.IsCommissioned(0.69f));
            // 明示閾値版。
            Assert.IsTrue(ShipCommissioningRules.IsCommissioned(0.5f, 0.5f));
            Assert.IsFalse(ShipCommissioningRules.IsCommissioned(0.4f, 0.5f));
        }

        [Test]
        public void Narrative_NewClassWorksUpThenSerialBuildMatures()
        {
            // 物語：新型艦の一番艦は設計が未成熟・乗員も不慣れで就役に手間取り戦力化が遅い。
            // 量産が進むと設計が成熟し品質も上がり、習熟効果で同型艦が早く戦力化する。

            // 一番艦：設計成熟0.4・品質0.5・乗員経験0.3・訓練0.5。
            float teeth1 = ShipCommissioningRules.TeethingProblems(0.4f, 0.5f);   // 0.6×0.5=0.30
            float fit1 = ShipCommissioningRules.FittingOutProgress(1f, 0.9f);      // 0.90
            float shake1 = ShipCommissioningRules.ShakedownProgress(fit1, teeth1); // 0.90-0.21=0.69
            float crew1 = ShipCommissioningRules.CrewFamiliarization(0.3f, 0.5f);  // √0.15≈0.387
            float ready1 = ShipCommissioningRules.CombatReadiness(shake1, crew1);  // ≈0.267
            Assert.IsFalse(ShipCommissioningRules.IsCommissioned(ready1)); // 一番艦は未就役
            float time1 = ShipCommissioningRules.CommissioningTime(0.4f, 1f);
            Assert.AreEqual(48f, time1, 1e-3f); // 30×(1+0.6)=48 ＝ 手間取る

            // 30番艦：設計成熟0.9・品質0.9・熟練乗員(経験0.9/訓練0.9)。
            float teeth30 = ShipCommissioningRules.TeethingProblems(0.9f, 0.9f);   // 0.1×0.1=0.01
            float fit30 = ShipCommissioningRules.FittingOutProgress(1f, 1f);        // 1.0
            float shake30 = ShipCommissioningRules.ShakedownProgress(fit30, teeth30);// 1-0.007=0.993
            float crew30 = ShipCommissioningRules.CrewFamiliarization(0.9f, 0.9f);  // √0.81=0.9
            float ready30 = ShipCommissioningRules.CombatReadiness(shake30, crew30);// ≈0.894
            Assert.IsTrue(ShipCommissioningRules.IsCommissioned(ready30)); // 後期艦は就役

            // 戦力化は後期艦が高い・所要は短い・量産習熟は伸びる。
            Assert.Greater(ready30, ready1);
            float time30 = ShipCommissioningRules.CommissioningTime(0.9f, 1f); // 30×1.1=33
            Assert.Less(time30, time1);
            Assert.Greater(
                ShipCommissioningRules.SerialProductionLearning(30),
                ShipCommissioningRules.SerialProductionLearning(1));
        }
    }
}
