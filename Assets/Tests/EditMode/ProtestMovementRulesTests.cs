using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class ProtestMovementRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void CauseResonance_WeightsLegitimacyAndAppeal()
        {
            // 0.8*0.6 + 0.6*0.4 = 0.48 + 0.24 = 0.72
            float r = ProtestMovementRules.CauseResonance(0.8f, 0.6f);
            Assert.AreEqual(0.72f, r, Eps);
        }

        [Test]
        public void Mobilization_ScalesResonanceByOrganizing()
        {
            // factor = 0.5 + 0.5*0.8 = 0.9; 0.72*0.9 = 0.648
            float m = ProtestMovementRules.Mobilization(0.72f, 0.8f);
            Assert.AreEqual(0.648f, m, Eps);
        }

        [Test]
        public void Mobilization_ZeroOrganizing_StillSelfStartsFromResonance()
        {
            // factor = 0.5 + 0.5*0 = 0.5; 0.72*0.5 = 0.36
            float m = ProtestMovementRules.Mobilization(0.72f, 0f);
            Assert.AreEqual(0.36f, m, Eps);
        }

        [Test]
        public void NonviolentDiscipline_AveragesLeadershipAndResistance()
        {
            // 0.8*0.5 + 0.6*0.5 = 0.7
            float d = ProtestMovementRules.NonviolentDiscipline(0.8f, 0.6f);
            Assert.AreEqual(0.7f, d, Eps);
        }

        [Test]
        public void MovementSustainability_GeometricMeanThenExponent()
        {
            // geo = (0.8*0.8*0.8)^(1/3) = 0.8; 0.8^1.5 = 0.8*sqrt(0.8) = 0.715542
            float s = ProtestMovementRules.MovementSustainability(0.8f, 0.8f, 0.8f);
            Assert.AreEqual(0.715542f, s, PowEps);
        }

        [Test]
        public void MovementSustainability_AnyZero_KillsMovement()
        {
            // 資源が尽きれば積がゼロ＝持続しない
            float s = ProtestMovementRules.MovementSustainability(0.9f, 0f, 0.9f);
            Assert.AreEqual(0f, s, Eps);
        }

        [Test]
        public void RepressionOrConcession_StrongMovementLeansToConcession()
        {
            // 0.6/(0.6+0.4) = 0.6 弾圧寄り（強硬体制）
            float a = ProtestMovementRules.RepressionOrConcession(0.6f, 0.4f);
            Assert.AreEqual(0.6f, a, Eps);
            // 運動が強いほど譲歩寄り（< 0.5）
            float b = ProtestMovementRules.RepressionOrConcession(0.3f, 0.9f);
            Assert.AreEqual(0.25f, b, Eps);
            Assert.Less(b, 0.5f);
        }

        [Test]
        public void RepressionOrConcession_NoConflict_IsNeutral()
        {
            float a = ProtestMovementRules.RepressionOrConcession(0f, 0f);
            Assert.AreEqual(0.5f, a, Eps);
        }

        [Test]
        public void MovementFragmentation_DiversityAndDisunity()
        {
            // 0.5*0.6 + (1-0.4)*0.4 = 0.30 + 0.24 = 0.54
            float f = ProtestMovementRules.MovementFragmentation(0.5f, 0.4f);
            Assert.AreEqual(0.54f, f, Eps);
        }

        [Test]
        public void PublicSympathy_OverreachOnPeacefulProtestRaisesSympathy()
        {
            // 0.7*0.6 + 0.5*0.4 = 0.42 + 0.20 = 0.62
            float s = ProtestMovementRules.PublicSympathy(0.7f, 0.5f);
            Assert.AreEqual(0.62f, s, Eps);
        }

        [Test]
        public void PoliticalOutcome_IsSignedAndClamped()
        {
            // sustain=0.715542, sympathy=0.62, concession=1-0.6=0.4
            // product = 0.715542*0.62*0.4 = 0.1774544; geo = ^(1/3) = 0.562049
            // achievement = 0.562049^1.5 = 0.421349; signed = *2-1 = -0.157302
            float o = ProtestMovementRules.PoliticalOutcome(0.715542f, 0.62f, 0.6f);
            Assert.AreEqual(-0.157302f, o, PowEps);
            Assert.GreaterOrEqual(o, -1f);
            Assert.LessOrEqual(o, 1f);
        }

        [Test]
        public void PoliticalOutcome_ConcessionVictoryIsPositive()
        {
            // 体制が完全に折れる（repression=0 → concession=1）持続・共感とも高い＝勝利
            float o = ProtestMovementRules.PoliticalOutcome(0.9f, 0.9f, 0f);
            Assert.Greater(o, 0f);
        }

        [Test]
        public void Inputs_AreClamped_NoNaN()
        {
            float r = ProtestMovementRules.CauseResonance(5f, -3f);
            Assert.AreEqual(0.6f, r, Eps); // 1*0.6 + 0*0.4 = 0.6
            float o = ProtestMovementRules.PoliticalOutcome(2f, 2f, -1f);
            Assert.False(float.IsNaN(o));
            Assert.LessOrEqual(o, 1f);
        }

        // 物語テスト：天安門型（強硬体制が非暴力デモを弾圧）vs ビロード革命型（体制が折れる）。
        [Test]
        public void Narrative_PeacefulMassMovement_WinsWhenRegimeYieldsLosesWhenCrushed()
        {
            // 共通の運動：正当な大義・大衆の支持・組織化
            float resonance = ProtestMovementRules.CauseResonance(0.85f, 0.8f);
            float mobilization = ProtestMovementRules.Mobilization(resonance, 0.7f);
            float discipline = ProtestMovementRules.NonviolentDiscipline(0.8f, 0.8f);
            float sustain = ProtestMovementRules.MovementSustainability(mobilization, 0.7f, 0.8f);
            float sympathy = ProtestMovementRules.PublicSympathy(discipline, 0.7f);

            // ケースA：強硬体制が圧倒的（弾圧寄り）＝非暴力でも力で潰される
            float crush = ProtestMovementRules.RepressionOrConcession(0.9f, mobilization);
            float crushedOutcome = ProtestMovementRules.PoliticalOutcome(sustain, sympathy, crush);

            // ケースB：体制が運動の力に折れる（譲歩寄り）＝同じ運動が勝つ
            float yield = ProtestMovementRules.RepressionOrConcession(0.2f, mobilization);
            float wonOutcome = ProtestMovementRules.PoliticalOutcome(sustain, sympathy, yield);

            Assert.Greater(crush, 0.5f, "強硬体制では弾圧寄り");
            Assert.Greater(crush, yield, "強硬体制ほど弾圧寄り");
            Assert.Greater(wonOutcome, crushedOutcome, "体制が折れた方が政治的成果が高い");
        }
    }
}
