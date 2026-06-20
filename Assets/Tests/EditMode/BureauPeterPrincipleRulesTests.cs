using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// ピーターの法則による官僚機構の能力ドリフトを固定する：能力転移/実力相関/罷免が下がるほどミスマッチが増え、
    /// 収束目標は人材プールをミスマッチぶん下回り、ドリフトは目標へ単調収束し、各APIは bounded・null安全（参照なし）・決定論。
    /// </summary>
    public class BureauPeterPrincipleRulesTests
    {
        private const float Eps = 1e-5f;
        private static BureauPeterPrincipleRules.BureauPeterParams Def
            => BureauPeterPrincipleRules.BureauPeterParams.Default;

        // --- Params のクランプ ---

        [Test]
        public void Params_ClampsAllInputs_0to1()
        {
            var p = new BureauPeterPrincipleRules.BureauPeterParams(2f, -1f, 5f, -3f, -2f);
            Assert.AreEqual(1f, p.meritCorrelation, Eps);
            Assert.AreEqual(0f, p.skillTransfer, Eps);
            Assert.AreEqual(1f, p.maxIncompetenceDrop, Eps);
            Assert.AreEqual(0f, p.demotionEfficacy, Eps);
            Assert.AreEqual(0f, p.driftRate, Eps); // driftRate は下限0
        }

        // --- IncompetenceMismatch ---

        [Test]
        public void IncompetenceMismatch_Bounds_0to1()
        {
            float m = BureauPeterPrincipleRules.IncompetenceMismatch(Def);
            Assert.GreaterOrEqual(m, 0f);
            Assert.LessOrEqual(m, 1f);
        }

        [Test]
        public void IncompetenceMismatch_Zero_WhenSkillTransfersFully()
        {
            // 能力転移1＝下の能力が上で完全に通用＝ピーターの法則が起きない
            var p = new BureauPeterPrincipleRules.BureauPeterParams(0.6f, 1f, 0.4f, 0.3f, 0.1f);
            Assert.AreEqual(0f, BureauPeterPrincipleRules.IncompetenceMismatch(p), Eps);
        }

        [Test]
        public void IncompetenceMismatch_Zero_WhenDemotionPerfect()
        {
            // 罷免の効き1＝無能を即更迭できる＝ミスマッチが残らない
            var p = new BureauPeterPrincipleRules.BureauPeterParams(0.6f, 0.5f, 0.4f, 1f, 0.1f);
            Assert.AreEqual(0f, BureauPeterPrincipleRules.IncompetenceMismatch(p), Eps);
        }

        [Test]
        public void IncompetenceMismatch_DecreasesWithSkillTransfer()
        {
            // 能力転移が高いほどミスマッチは単調に減る
            var lo = new BureauPeterPrincipleRules.BureauPeterParams(0.6f, 0.2f, 0.4f, 0.3f, 0.1f);
            var hi = new BureauPeterPrincipleRules.BureauPeterParams(0.6f, 0.8f, 0.4f, 0.3f, 0.1f);
            Assert.Greater(BureauPeterPrincipleRules.IncompetenceMismatch(lo),
                           BureauPeterPrincipleRules.IncompetenceMismatch(hi));
        }

        [Test]
        public void IncompetenceMismatch_DecreasesWithDemotionEfficacy()
        {
            var lo = new BureauPeterPrincipleRules.BureauPeterParams(0.6f, 0.5f, 0.4f, 0.1f, 0.1f);
            var hi = new BureauPeterPrincipleRules.BureauPeterParams(0.6f, 0.5f, 0.4f, 0.9f, 0.1f);
            Assert.Greater(BureauPeterPrincipleRules.IncompetenceMismatch(lo),
                           BureauPeterPrincipleRules.IncompetenceMismatch(hi));
        }

        [Test]
        public void IncompetenceMismatch_DecreasesWithMeritCorrelation()
        {
            // 実力相関が高いほど選抜の質が上がり初期ミスマッチが下がる（単調）
            var lo = new BureauPeterPrincipleRules.BureauPeterParams(0.0f, 0.5f, 0.4f, 0.3f, 0.1f);
            var hi = new BureauPeterPrincipleRules.BureauPeterParams(1.0f, 0.5f, 0.4f, 0.3f, 0.1f);
            Assert.Greater(BureauPeterPrincipleRules.IncompetenceMismatch(lo),
                           BureauPeterPrincipleRules.IncompetenceMismatch(hi));
        }

        [Test]
        public void IncompetenceMismatch_DefaultOverload_MatchesDefaultParams()
        {
            Assert.AreEqual(BureauPeterPrincipleRules.IncompetenceMismatch(Def),
                            BureauPeterPrincipleRules.IncompetenceMismatch(), Eps);
        }

        // --- EquilibriumCompetence ---

        [Test]
        public void Equilibrium_NeverExceedsTalentPool()
        {
            // 人材プールより上には収束しない（ピーターの法則は能力を削る方向）
            for (float pool = 0f; pool <= 1f; pool += 0.25f)
                Assert.LessOrEqual(BureauPeterPrincipleRules.EquilibriumCompetence(pool, Def), pool + Eps);
        }

        [Test]
        public void Equilibrium_EqualsPool_WhenNoMismatch()
        {
            // ミスマッチ0（完全転移）なら人材プールがそのまま収束目標
            var p = new BureauPeterPrincipleRules.BureauPeterParams(0.6f, 1f, 0.4f, 0.3f, 0.1f);
            Assert.AreEqual(0.7f, BureauPeterPrincipleRules.EquilibriumCompetence(0.7f, p), Eps);
        }

        [Test]
        public void Equilibrium_MonotonicInTalentPool()
        {
            Assert.Greater(BureauPeterPrincipleRules.EquilibriumCompetence(0.9f, Def),
                           BureauPeterPrincipleRules.EquilibriumCompetence(0.3f, Def));
        }

        [Test]
        public void Equilibrium_Bounds_0to1_OnExtremeInputs()
        {
            Assert.AreEqual(0f, BureauPeterPrincipleRules.EquilibriumCompetence(-5f, Def), Eps);
            float hi = BureauPeterPrincipleRules.EquilibriumCompetence(5f, Def);
            Assert.GreaterOrEqual(hi, 0f);
            Assert.LessOrEqual(hi, 1f);
        }

        // --- DriftTick ---

        [Test]
        public void DriftTick_MovesTowardEquilibrium_FromAbove()
        {
            // 現在能力が目標より高い＝無能化で下がる
            float target = BureauPeterPrincipleRules.EquilibriumCompetence(0.8f, Def);
            float after = BureauPeterPrincipleRules.DriftTick(0.95f, 0.8f, 1f, Def);
            Assert.Less(after, 0.95f);
            Assert.GreaterOrEqual(after, target - Eps);
        }

        [Test]
        public void DriftTick_MovesTowardEquilibrium_FromBelow()
        {
            // 現在が目標より低い＝採用/淘汰で回復する
            float target = BureauPeterPrincipleRules.EquilibriumCompetence(0.8f, Def);
            float after = BureauPeterPrincipleRules.DriftTick(0.1f, 0.8f, 1f, Def);
            Assert.Greater(after, 0.1f);
            Assert.LessOrEqual(after, target + Eps);
        }

        [Test]
        public void DriftTick_ZeroDt_IsNoOp()
        {
            Assert.AreEqual(0.6f, BureauPeterPrincipleRules.DriftTick(0.6f, 0.9f, 0f, Def), Eps);
        }

        [Test]
        public void DriftTick_NegativeDt_TreatedAsZero()
        {
            Assert.AreEqual(0.6f, BureauPeterPrincipleRules.DriftTick(0.6f, 0.9f, -5f, Def), Eps);
        }

        [Test]
        public void DriftTick_DoesNotOvershoot_LargeDt()
        {
            // dt が大きくても目標を飛び越えない（割合は飽和）
            float target = BureauPeterPrincipleRules.EquilibriumCompetence(0.2f, Def);
            float after = BureauPeterPrincipleRules.DriftTick(1f, 0.2f, 1000f, Def);
            Assert.GreaterOrEqual(after, target - Eps);
            Assert.LessOrEqual(after, 1f + Eps);
        }

        [Test]
        public void DriftTick_ConvergesOverManyYears()
        {
            // 多年回すと収束目標へ寄る（劣化ドリフトの長期挙動）
            float target = BureauPeterPrincipleRules.EquilibriumCompetence(0.85f, Def);
            float c = 1f;
            for (int y = 0; y < 200; y++) c = BureauPeterPrincipleRules.DriftTick(c, 0.85f, 1f, Def);
            Assert.AreEqual(target, c, 1e-2f);
        }

        [Test]
        public void DriftTick_Deterministic()
        {
            float a = BureauPeterPrincipleRules.DriftTick(0.7f, 0.8f, 0.5f, Def);
            float b = BureauPeterPrincipleRules.DriftTick(0.7f, 0.8f, 0.5f, Def);
            Assert.AreEqual(a, b, 0f);
        }

        [Test]
        public void DriftTick_DefaultOverload_MatchesDefaultParams()
        {
            Assert.AreEqual(BureauPeterPrincipleRules.DriftTick(0.7f, 0.8f, 0.5f, Def),
                            BureauPeterPrincipleRules.DriftTick(0.7f, 0.8f, 0.5f), Eps);
        }

        // --- ヘルパ ---

        [Test]
        public void ThroughputFactor_ClampsAndDelegatesIdentity()
        {
            Assert.AreEqual(0.42f, BureauPeterPrincipleRules.ThroughputFactor(0.42f), Eps);
            Assert.AreEqual(1f, BureauPeterPrincipleRules.ThroughputFactor(9f), Eps);
            Assert.AreEqual(0f, BureauPeterPrincipleRules.ThroughputFactor(-9f), Eps);
        }

        [Test]
        public void IsHollowedOut_TrueBelowThreshold_FalseAtOrAbove()
        {
            Assert.IsTrue(BureauPeterPrincipleRules.IsHollowedOut(0.1f, 0.3f));
            Assert.IsFalse(BureauPeterPrincipleRules.IsHollowedOut(0.3f, 0.3f));
            Assert.IsFalse(BureauPeterPrincipleRules.IsHollowedOut(0.9f, 0.3f));
        }

        [Test]
        public void IsHollowedOut_ClampsInputs()
        {
            // 負の能力は0扱い・閾値>1 は1扱い＝常に hollow
            Assert.IsTrue(BureauPeterPrincipleRules.IsHollowedOut(-5f, 5f));
        }
    }
}
