using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 自律分散チームワーク（#544-550）を固定する：全員好成績でのみ創発し、傑物前提で機能、
    /// ドクトリンが反応/天井/分散・信頼/結束を分ける。
    /// </summary>
    public class AutonomyRulesTests
    {
        private static AutonomyParams P => AutonomyParams.Default;

        // --- DoctrineFactor ---

        [Test]
        public void DoctrineFactor_Autonomy_HighReactivityCeilingVariance()
        {
            var prof = AutonomyRules.DoctrineFactor(CommandDoctrine.自律分散, P);
            Assert.AreEqual(1.3f, prof.Reactivity, 1e-4f);
            Assert.AreEqual(1.5f, prof.Ceiling, 1e-4f);
            Assert.AreEqual(0.6f, prof.Variance, 1e-4f);
            Assert.AreEqual(0.2f, prof.MoraleFloor, 1e-4f);
        }

        [Test]
        public void DoctrineFactor_Dependent_LowVarianceHighMoraleFloor()
        {
            var prof = AutonomyRules.DoctrineFactor(CommandDoctrine.集団依存, P);
            // 集団依存は分散が低く（手堅い）、士気底上げが高い（凡庸でも崩れにくい）
            Assert.Less(prof.Variance, AutonomyRules.DoctrineFactor(CommandDoctrine.自律分散, P).Variance);
            Assert.Greater(prof.MoraleFloor, AutonomyRules.DoctrineFactor(CommandDoctrine.自律分散, P).MoraleFloor);
        }

        // --- EmergentSynergy ---

        [Test]
        public void EmergentSynergy_AllExcellent_BonusStands()
        {
            var perf = new List<float> { 0.8f, 0.9f, 1.0f };
            // 平均 0.9・自律1.0・gain0.5 → 0.45
            Assert.AreEqual(0.45f, AutonomyRules.EmergentSynergy(perf, 1f, P), 1e-4f);
        }

        [Test]
        public void EmergentSynergy_OneMediocre_Zero()
        {
            var perf = new List<float> { 0.9f, 0.6f, 1.0f }; // 0.6 が閾値0.7未満
            Assert.AreEqual(0f, AutonomyRules.EmergentSynergy(perf, 1f, P), 1e-4f);
        }

        [Test]
        public void EmergentSynergy_AtThreshold_StillStands()
        {
            // 閾値ちょうど(0.7)は創発する（未満のみ崩れる）
            var perf = new List<float> { 0.7f, 0.7f };
            Assert.AreEqual(0.35f, AutonomyRules.EmergentSynergy(perf, 1f, P), 1e-4f); // 平均0.7×0.5
        }

        [Test]
        public void EmergentSynergy_ScalesWithAutonomy_AndClampsEmpty()
        {
            var perf = new List<float> { 1.0f, 1.0f };
            Assert.AreEqual(0.25f, AutonomyRules.EmergentSynergy(perf, 0.5f, P), 1e-4f); // 自律0.5で半減
            Assert.AreEqual(0f, AutonomyRules.EmergentSynergy(new List<float>(), 1f, P), 1e-4f); // 空は0
            Assert.AreEqual(0f, AutonomyRules.EmergentSynergy(null, 1f, P), 1e-4f);             // null は0
        }

        // --- IsFunctional ---

        [Test]
        public void IsFunctional_HighCapabilityAndAutonomy_PositiveBonus()
        {
            // capability1×autonomy1=1.0 → 閾値0.5から最大 → +0.4
            Assert.AreEqual(0.4f, AutonomyRules.IsFunctional(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void IsFunctional_LowDrive_NegativeDysfunction()
        {
            // capability0.2×autonomy0.5=0.1 → 閾値0.5未満＝逆機能（負）
            float result = AutonomyRules.IsFunctional(0.2f, 0.5f, P);
            Assert.Less(result, 0f);
            // u=(0.5-0.1)/0.5=0.8 → -0.3×0.8 = -0.24
            Assert.AreEqual(-0.24f, result, 1e-4f);
        }

        [Test]
        public void IsFunctional_AtThreshold_Zero()
        {
            // drive=0.5 ちょうど＝機能/逆機能の境目＝0
            Assert.AreEqual(0f, AutonomyRules.IsFunctional(1f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void IsFunctional_ClampsInputs()
        {
            // 過大入力でも drive は1.0でクランプ＝上限ボーナス
            Assert.AreEqual(0.4f, AutonomyRules.IsFunctional(2f, 2f, P), 1e-4f);
            // 負入力は0クランプ＝最大逆機能 -0.3
            Assert.AreEqual(-0.3f, AutonomyRules.IsFunctional(-1f, 1f, P), 1e-4f);
        }

        // --- TrustVsCohesion ---

        [Test]
        public void TrustVsCohesion_Autonomy_TrustOverCohesion()
        {
            AutonomyRules.TrustVsCohesion(CommandDoctrine.自律分散, P, out float trust, out float cohesion);
            Assert.Greater(trust, cohesion); // 自律型は信頼で動く
            Assert.AreEqual(0.85f, trust, 1e-4f);
        }

        [Test]
        public void TrustVsCohesion_Dependent_CohesionOverTrust()
        {
            AutonomyRules.TrustVsCohesion(CommandDoctrine.集団依存, P, out float trust, out float cohesion);
            Assert.Greater(cohesion, trust); // 依存型は結束でまとまる
            Assert.AreEqual(0.85f, cohesion, 1e-4f);
        }
    }
}
