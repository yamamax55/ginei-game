using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>野戦築城・二重包囲の純ロジック（GAL-4・カエサル＝アレシア）の EditMode テスト。</summary>
    public class MilitaryEngineeringRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void EngineeringSpeed_SlowsOnDifficultTerrain()
        {
            // 100工兵×0.01 / (1+2×0.5) = 1.0/2.0 = 0.5
            Assert.AreEqual(0.5f, MilitaryEngineeringRules.EngineeringSpeed(100, 2f), Eps);
            // 平地（難易度0）＝50×0.01/1 = 0.5
            Assert.AreEqual(0.5f, MilitaryEngineeringRules.EngineeringSpeed(50, 0f), Eps);
            // 負の工兵数は0でクランプ
            Assert.AreEqual(0f, MilitaryEngineeringRules.EngineeringSpeed(-10, 0f), Eps);
        }

        [Test]
        public void FortificationProgress_AdvancesAndClampsToOne()
        {
            Assert.AreEqual(0.8f, MilitaryEngineeringRules.FortificationProgress(0.5f, 1f, 0.3f), Eps);
            // 上限1.0でクランプ
            Assert.AreEqual(1f, MilitaryEngineeringRules.FortificationProgress(0.5f, 10f, 0.9f), Eps);
        }

        [Test]
        public void LineLengthDilution_ThinsOnLongLines()
        {
            // 基準長以下は1.0
            Assert.AreEqual(1f, MilitaryEngineeringRules.LineLengthDilution(5f), Eps);
            // 倍の長さで半減
            Assert.AreEqual(0.5f, MilitaryEngineeringRules.LineLengthDilution(20f), Eps);
        }

        [Test]
        public void ContravallationAndCircumvallation_AreProgressTimesDilution()
        {
            // 0.8×(10/20)=0.4
            Assert.AreEqual(0.4f, MilitaryEngineeringRules.ContravallationStrength(0.8f, 20f), Eps);
            // 対外線も同型
            Assert.AreEqual(0.4f, MilitaryEngineeringRules.CircumvallationStrength(0.8f, 20f), Eps);
        }

        [Test]
        public void ForceDivisionStrain_RisesWhenThinlyStretched()
        {
            // (40×0.5)/100 = 0.2
            Assert.AreEqual(0.2f, MilitaryEngineeringRules.ForceDivisionStrain(100f, 40f), Eps);
            // 兵力0で線があれば最大負担1
            Assert.AreEqual(1f, MilitaryEngineeringRules.ForceDivisionStrain(0f, 10f), Eps);
            // 兵力0でも線が無ければ負担0
            Assert.AreEqual(0f, MilitaryEngineeringRules.ForceDivisionStrain(0f, 0f), Eps);
        }

        [Test]
        public void DoubleSiegeViability_LimitedByWeakerLineAndStrain()
        {
            // min(0.6,0.4)×(1−0.2) = 0.4×0.8 = 0.32
            Assert.AreEqual(0.32f, MilitaryEngineeringRules.DoubleSiegeViability(0.6f, 0.4f, 100f, 40f), Eps);
        }

        [Test]
        public void Repulse_And_BreakoutResistance_AreStrengthMinusPressure()
        {
            // 0.8 − clamp01(50/100) = 0.8−0.5 = 0.3
            Assert.AreEqual(0.3f, MilitaryEngineeringRules.ReliefArmyRepulse(0.8f, 50f, 100f), Eps);
            // 0.7 − clamp01(30/100) = 0.7−0.3 = 0.4
            Assert.AreEqual(0.4f, MilitaryEngineeringRules.BesiegedBreakoutResistance(0.7f, 30f, 100f), Eps);
            // 救援軍が守備の倍なら圧は1.0で飽和＝撥ね返し負（突破される）
            Assert.AreEqual(0.5f - 1f, MilitaryEngineeringRules.ReliefArmyRepulse(0.5f, 200f, 100f), Eps);
        }

        [Test]
        public void Story_AlesiaDoubleSiegeHoldsButBreaksWhenSpreadTooThin()
        {
            // 十分な兵力（守備200）で短い二線＝両線が厚く築ける。
            float fort = MilitaryEngineeringRules.FortificationProgress(
                MilitaryEngineeringRules.EngineeringSpeed(400, 0f), 1f, 0f); // 4.0/1 → 1.0でクランプ
            Assert.AreEqual(1f, fort, Eps);

            float contra = MilitaryEngineeringRules.ContravallationStrength(fort, 10f); // 1.0×1.0
            float circum = MilitaryEngineeringRules.CircumvallationStrength(fort, 10f);
            Assert.AreEqual(1f, contra, Eps);
            Assert.AreEqual(1f, circum, Eps);

            // 救援軍も包囲された敵も守備より小さい＝両線とも撥ね返し優位。
            float repulse = MilitaryEngineeringRules.ReliefArmyRepulse(circum, 120f, 200f);   // 1−0.6=0.4
            float resist = MilitaryEngineeringRules.BesiegedBreakoutResistance(contra, 100f, 200f); // 1−0.5=0.5
            Assert.IsTrue(repulse > 0f && resist > 0f);
            Assert.IsTrue(MilitaryEngineeringRules.IsDoubleSiegeHolding(repulse, resist),
                "両線とも撥ね返せば二重包囲は成立する");

            // だが同じ兵を長大な二線へ薄く伸ばすと割兵負担が跳ね、維持余力が痩せる。
            float tightViability = MilitaryEngineeringRules.DoubleSiegeViability(contra, circum, 200f, 40f);
            float thinViability = MilitaryEngineeringRules.DoubleSiegeViability(contra, circum, 200f, 800f);
            Assert.IsTrue(thinViability < tightViability,
                "二線に薄く伸ばすほど兵力が割れて維持余力が落ちる");

            // 弱い対外線では大救援軍を撥ね返せず＝二重包囲が崩れる。
            float weakCircum = MilitaryEngineeringRules.CircumvallationStrength(0.2f, 40f); // 0.2×0.25=0.05
            float weakRepulse = MilitaryEngineeringRules.ReliefArmyRepulse(weakCircum, 300f, 200f); // 0.05−1.0=負
            Assert.IsFalse(MilitaryEngineeringRules.IsDoubleSiegeHolding(weakRepulse, resist),
                "対外線が破られれば二重包囲は崩れる");
        }
    }
}
