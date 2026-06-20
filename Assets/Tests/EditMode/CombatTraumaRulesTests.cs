using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// CombatTraumaRules（戦闘トラウマ＝兵士の長期的精神的損傷）の EditMode テスト。
    /// 既定 Params の期待値は手計算で固定。Pow/Sqrt 箇所のみ 1e-3f 許容。
    /// </summary>
    public class CombatTraumaRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        // --- TraumaAccumulation -----------------------------------------

        [Test]
        public void TraumaAccumulation_MaxInputs_IsOne()
        {
            // weighted=(0.4+0.4+0.3)/1.1=1.0, Pow(1,1.5)=1.0
            Assert.AreEqual(1f, CombatTraumaRules.TraumaAccumulation(1f, 1f, 1f), Eps);
        }

        [Test]
        public void TraumaAccumulation_HalfInputs_FollowsExponentCurve()
        {
            // weighted=0.5, Pow(0.5,1.5)=0.3535534
            Assert.AreEqual(0.3535534f, CombatTraumaRules.TraumaAccumulation(0.5f, 0.5f, 0.5f), PowEps);
        }

        [Test]
        public void TraumaAccumulation_NoExposure_IsZero()
        {
            Assert.AreEqual(0f, CombatTraumaRules.TraumaAccumulation(0f, 0f, 0f), Eps);
        }

        [Test]
        public void TraumaAccumulation_ClampsNegativeInputs()
        {
            Assert.AreEqual(0f, CombatTraumaRules.TraumaAccumulation(-5f, -2f, -9f), Eps);
        }

        // --- UnitSupport / IsolationFactor ------------------------------

        [Test]
        public void UnitSupport_FullBondsAndCare_IsOne()
        {
            Assert.AreEqual(1f, CombatTraumaRules.UnitSupport(1f, 1f), Eps);
        }

        [Test]
        public void UnitSupport_WeightedAverage()
        {
            // (0.5*0.6 + 0.25*0.4)/1.0 = 0.3+0.1 = 0.4
            Assert.AreEqual(0.4f, CombatTraumaRules.UnitSupport(0.5f, 0.25f), Eps);
        }

        [Test]
        public void IsolationFactor_IsComplementOfSupport()
        {
            // 1 - 0.4 = 0.6
            Assert.AreEqual(0.6f, CombatTraumaRules.IsolationFactor(0.4f), Eps);
        }

        // --- Chronification ---------------------------------------------

        [Test]
        public void Chronification_LongUntreated_TimeSaturates()
        {
            // timeFactor=clamp01(0.5*4=2)=1, 0.8*0.5*1=0.4
            Assert.AreEqual(0.4f, CombatTraumaRules.Chronification(0.8f, 0.5f, 4f), Eps);
        }

        [Test]
        public void Chronification_ShortUntreated_PartialTime()
        {
            // timeFactor=0.5, 0.8*0.5*0.5=0.2
            Assert.AreEqual(0.2f, CombatTraumaRules.Chronification(0.8f, 0.5f, 1f), Eps);
        }

        // --- CombatImpairment -------------------------------------------

        [Test]
        public void CombatImpairment_FullTraumaAndChronic_HitsMax()
        {
            // severity=1*(1+1)*0.5=1, 0.6*1=0.6
            Assert.AreEqual(0.6f, CombatTraumaRules.CombatImpairment(1f, 1f), Eps);
        }

        [Test]
        public void CombatImpairment_AcuteOnly_NoChronic()
        {
            // severity=0.5*(1+0)*0.5=0.25, 0.6*0.25=0.15
            Assert.AreEqual(0.15f, CombatTraumaRules.CombatImpairment(0.5f, 0f), Eps);
        }

        // --- RecoveryProgress -------------------------------------------

        [Test]
        public void RecoveryProgress_AllResources_IsOne()
        {
            // (0.5+0.3+0.2)/1.0 = 1.0
            Assert.AreEqual(1f, CombatTraumaRules.RecoveryProgress(1f, 1f, 1f), Eps);
        }

        [Test]
        public void RecoveryProgress_RestOnly_HalfProgress()
        {
            // (1*0.5)/1.0 = 0.5
            Assert.AreEqual(0.5f, CombatTraumaRules.RecoveryProgress(1f, 0f, 0f), Eps);
        }

        // --- TraumaContagion --------------------------------------------

        [Test]
        public void TraumaContagion_FullTraumaDenseUnit()
        {
            // 1*1*0.5 = 0.5
            Assert.AreEqual(0.5f, CombatTraumaRules.TraumaContagion(1f, 1f), Eps);
        }

        [Test]
        public void TraumaContagion_PartialValues()
        {
            // 0.6*0.5*0.5 = 0.15
            Assert.AreEqual(0.15f, CombatTraumaRules.TraumaContagion(0.6f, 0.5f), Eps);
        }

        // --- PsychologicalReadiness -------------------------------------

        [Test]
        public void PsychologicalReadiness_TraumatizedNoRecovery_IsLow()
        {
            // base=1-0.8=0.2, restored=0.2+0*0.8=0.2
            Assert.AreEqual(0.2f, CombatTraumaRules.PsychologicalReadiness(0.8f, 0f), Eps);
        }

        [Test]
        public void PsychologicalReadiness_FullRecovery_RestoresToOne()
        {
            // base=0.2, restored=0.2+1*0.8=1.0
            Assert.AreEqual(1f, CombatTraumaRules.PsychologicalReadiness(0.8f, 1f), Eps);
        }

        // --- Params -----------------------------------------------------

        [Test]
        public void Params_Ctor_ClampsOutOfRange()
        {
            var p = new CombatTraumaParams(
                exposureWeight: 5f,
                atrocityWeight: -1f,
                comradeLossWeight: 0.3f,
                accumulationExponent: 100f,
                bondsWeight: 0.6f,
                leadershipWeight: 0.4f,
                chronificationRate: 2f,
                maxImpairment: 9f,
                restWeight: 0.5f,
                treatmentWeight: 0.3f,
                supportWeight: 0.2f,
                contagionRate: -3f);

            Assert.AreEqual(1f, p.exposureWeight, Eps);
            Assert.AreEqual(0f, p.atrocityWeight, Eps);
            Assert.AreEqual(8f, p.accumulationExponent, Eps);
            Assert.AreEqual(1f, p.maxImpairment, Eps);
            Assert.AreEqual(0f, p.contagionRate, Eps);
        }

        // --- 物語テスト：惨事の生存者が孤立し慢性化、休養と戦友の支えで立ち直る ----

        [Test]
        public void Narrative_AtrocitySurvivor_IsolatesChronifiesThenHeals()
        {
            var p = CombatTraumaParams.Default;

            // 凄惨な会戦：高い曝露・惨事の目撃・多くの戦友を喪失 → 深いトラウマ。
            float trauma = CombatTraumaRules.TraumaAccumulation(0.9f, 0.9f, 0.9f, p);
            Assert.Greater(trauma, 0.7f, "凄惨な会戦は深いトラウマを残す");

            // 支えの乏しい部隊（絆も配慮も薄い）→ 孤立する。
            float weakSupport = CombatTraumaRules.UnitSupport(0.1f, 0.1f, p);
            float isolation = CombatTraumaRules.IsolationFactor(weakSupport, p);
            Assert.Greater(isolation, 0.8f, "支えのない兵は孤立する");

            // 孤立 × 長い未治療 → 慢性化が進む。
            float chronic = CombatTraumaRules.Chronification(trauma, isolation, 4f, p);
            Assert.Greater(chronic, 0.5f, "孤立し放置されると慢性化する");

            // 慢性化したトラウマは戦闘能力を深く蝕む。
            float impair = CombatTraumaRules.CombatImpairment(trauma, chronic, p);
            Assert.Greater(impair, 0.4f, "慢性トラウマは戦闘能力を蝕む");

            // 慢性化前と比べて、慢性化したほうが能力低下が大きい。
            float acuteImpair = CombatTraumaRules.CombatImpairment(trauma, 0f, p);
            Assert.Greater(impair, acuteImpair, "慢性化は急性より深く効く");

            // 精神的即応性は低い。
            float readinessBefore = CombatTraumaRules.PsychologicalReadiness(trauma, 0f, p);
            Assert.Less(readinessBefore, 0.3f, "回復前の即応性は低い");

            // 十分な休養・治療・戦友の支えで回復が進む。
            float strongSupport = CombatTraumaRules.UnitSupport(0.9f, 0.9f, p);
            float recovery = CombatTraumaRules.RecoveryProgress(1f, 1f, strongSupport, p);
            Assert.Greater(recovery, 0.8f, "休養と治療と支えで回復が進む");

            // 回復で即応性が立ち直る。
            float readinessAfter = CombatTraumaRules.PsychologicalReadiness(trauma, recovery, p);
            Assert.Greater(readinessAfter, readinessBefore, "回復で即応性が立ち直る");
        }
    }
}
