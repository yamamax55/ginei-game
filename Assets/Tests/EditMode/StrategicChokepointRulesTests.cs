using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>戦略的隘路（チョークポイント）の地政学的価値の純ロジックの検証。</summary>
    public class StrategicChokepointRulesTests
    {
        const float Eps = 1e-4f;
        const float EpsPow = 1e-3f; // Sqrt を含む箇所

        // PassageConstraint = (1-width)*difficulty
        [Test]
        public void PassageConstraint_NarrowAndDifficult_IsHigh()
        {
            // (1-0.2)*0.8 = 0.64
            Assert.AreEqual(0.64f, StrategicChokepointRules.PassageConstraint(0.2f, 0.8f), Eps);
        }

        [Test]
        public void PassageConstraint_WideCorridor_NoConstraint()
        {
            // 広さ1 → narrowness 0 → 制約0
            Assert.AreEqual(0f, StrategicChokepointRules.PassageConstraint(1f, 0.9f), Eps);
        }

        [Test]
        public void PassageConstraint_ClampsInputs()
        {
            // width=-1→0(clamp)、diff=5→1(clamp) → (1-0)*1 = 1
            Assert.AreEqual(1f, StrategicChokepointRules.PassageConstraint(-1f, 5f), Eps);
        }

        // IrreplaceabilityValue = constraint*(1-alt)
        [Test]
        public void IrreplaceabilityValue_NoBypass_KeepsValue()
        {
            // 0.64*(1-0.25) = 0.48
            Assert.AreEqual(0.48f, StrategicChokepointRules.IrreplaceabilityValue(0.64f, 0.25f), Eps);
        }

        [Test]
        public void IrreplaceabilityValue_FullBypass_ZeroValue()
        {
            // 迂回路1 → 代替が利く → 価値0
            Assert.AreEqual(0f, StrategicChokepointRules.IrreplaceabilityValue(0.9f, 1f), Eps);
        }

        // HoldingAdvantage = value*control
        [Test]
        public void HoldingAdvantage_ValueTimesControl()
        {
            // 0.48*0.5 = 0.24
            Assert.AreEqual(0.24f, StrategicChokepointRules.HoldingAdvantage(0.48f, 0.5f), Eps);
        }

        // FrontierTension = (n/(1+n))*contested
        [Test]
        public void FrontierTension_AdjacentPowersSaturate()
        {
            // 2/(1+2)*0.6 = 0.6667*0.6 = 0.4
            Assert.AreEqual(0.4f, StrategicChokepointRules.FrontierTension(2f, 0.6f), Eps);
        }

        [Test]
        public void FrontierTension_NoAdjacentPowers_NoTension()
        {
            // 隣接0 → 緊張0
            Assert.AreEqual(0f, StrategicChokepointRules.FrontierTension(0f, 1f), Eps);
        }

        // DefensiveSuitability = constraint*fortifiability
        [Test]
        public void DefensiveSuitability_ConstraintTimesFortifiability()
        {
            // 0.64*0.75 = 0.48
            Assert.AreEqual(0.48f, StrategicChokepointRules.DefensiveSuitability(0.64f, 0.75f), Eps);
        }

        // ContestIntensity = advantage*tension
        [Test]
        public void ContestIntensity_AdvantageTimesTension()
        {
            // 0.24*0.4 = 0.096
            Assert.AreEqual(0.096f, StrategicChokepointRules.ContestIntensity(0.24f, 0.4f), Eps);
        }

        // GeopoliticalLeverage = value*advantage
        [Test]
        public void GeopoliticalLeverage_ValueTimesAdvantage()
        {
            // 0.48*0.24 = 0.1152
            Assert.AreEqual(0.1152f, StrategicChokepointRules.GeopoliticalLeverage(0.48f, 0.24f), Eps);
        }

        // ChokepointDecisiveness = sqrt(leverage*defensive)
        [Test]
        public void ChokepointDecisiveness_GeometricMean()
        {
            // sqrt(0.1152*0.48) = sqrt(0.055296) = 0.235150...
            Assert.AreEqual(0.235150f, StrategicChokepointRules.ChokepointDecisiveness(0.1152f, 0.48f), EpsPow);
        }

        [Test]
        public void ChokepointDecisiveness_OneSideZero_NotDecisive()
        {
            // 攻めの価値があっても守れない（防御0）と決定性は0
            Assert.AreEqual(0f, StrategicChokepointRules.ChokepointDecisiveness(0.9f, 0f), EpsPow);
        }

        [Test]
        public void IsDecisiveChokepoint_ThresholdGate()
        {
            Assert.IsTrue(StrategicChokepointRules.IsDecisiveChokepoint(0.6f, 0.5f));
            Assert.IsFalse(StrategicChokepointRules.IsDecisiveChokepoint(0.4f, 0.5f));
            // 既定閾値0.5
            Assert.IsTrue(StrategicChokepointRules.IsDecisiveChokepoint(0.5f));
        }

        [Test]
        public void Params_ClampsNegativeScales()
        {
            var p = new StrategicChokepointParams(-1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f);
            Assert.AreEqual(0f, p.constraintScale, Eps);
            Assert.AreEqual(0f, p.decisivenessScale, Eps);
            // 係数0なら出力0
            Assert.AreEqual(0f, StrategicChokepointRules.PassageConstraint(0.1f, 0.9f, p), Eps);
        }

        /// <summary>
        /// 物語：イゼルローン回廊型の決定的要衝。極狭で通行困難、迂回路ほぼ無し、要塞で堅守、
        /// 二大勢力が接して係争中 → 高い迂回不能価値・握る優位・防御適性が揃い、戦局を左右する要衝になる。
        /// </summary>
        [Test]
        public void Narrative_IserlohnCorridor_IsDecisive()
        {
            float width = 0.1f, difficulty = 0.9f, alt = 0.1f;
            float control = 0.9f, fortifiability = 0.95f;
            float adjacentPowers = 2f, contested = 0.8f;

            float pc = StrategicChokepointRules.PassageConstraint(width, difficulty);
            Assert.AreEqual(0.81f, pc, Eps); // (1-0.1)*0.9

            float iv = StrategicChokepointRules.IrreplaceabilityValue(pc, alt);
            Assert.AreEqual(0.729f, iv, Eps); // 0.81*0.9

            float ha = StrategicChokepointRules.HoldingAdvantage(iv, control);
            Assert.AreEqual(0.6561f, ha, Eps); // 0.729*0.9

            float ft = StrategicChokepointRules.FrontierTension(adjacentPowers, contested);
            Assert.AreEqual(0.53333f, ft, Eps); // (2/3)*0.8

            float ds = StrategicChokepointRules.DefensiveSuitability(pc, fortifiability);
            Assert.AreEqual(0.76950f, ds, Eps); // 0.81*0.95

            float ci = StrategicChokepointRules.ContestIntensity(ha, ft);
            Assert.AreEqual(0.34992f, ci, Eps); // 0.6561*0.53333

            float gl = StrategicChokepointRules.GeopoliticalLeverage(iv, ha);
            Assert.AreEqual(0.478297f, gl, Eps); // 0.729*0.6561

            float cd = StrategicChokepointRules.ChokepointDecisiveness(gl, ds);
            Assert.AreEqual(0.606704f, cd, EpsPow); // sqrt(0.478297*0.7695)

            Assert.IsTrue(StrategicChokepointRules.IsDecisiveChokepoint(cd)); // > 0.5
        }
    }
}
