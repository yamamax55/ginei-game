using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 触媒の非対称性（CEND-3 #2361）の純ロジックを固定する：触媒ボーナス（同盟成長係数の上乗せ）、
    /// 自己進展ペナルティ、超越除外閾値での CanTranscend 境界、CEND-1 IsEligible との AND ゲート、
    /// catalystRatio の 0..1 Clamp と NaN 安全、null 安全。
    /// </summary>
    public class CatalystRulesTests
    {
        // 全 proxy 満点で超越適格スコアを満たす健全国家（CEND-1 と同じ）。
        static FactionState Healthy() => new FactionState(Faction.帝国);

        // ---- CatalystBonus（同盟勢力の成長係数を加速） ----

        [Test]
        public void CatalystBonus_ZeroRatio_ReturnsTargetUnchanged()
        {
            Assert.AreEqual(1.0f, CatalystRules.CatalystBonus(0f, 1.0f), 1e-4f);
        }

        [Test]
        public void CatalystBonus_HigherRatio_GivesLargerBonus()
        {
            float low = CatalystRules.CatalystBonus(0.2f, 1.0f);
            float high = CatalystRules.CatalystBonus(0.8f, 1.0f);
            Assert.Greater(high, low);
            Assert.Greater(low, 1.0f); // 触媒は常に上乗せ
        }

        [Test]
        public void CatalystBonus_FullRatio_UsesCatalystStrength()
        {
            // ratio=1 → targetFactor×(1+catalystStrength)=1×1.3
            Assert.AreEqual(1.3f, CatalystRules.CatalystBonus(1f, 1.0f), 1e-4f);
        }

        [Test]
        public void CatalystBonus_ClampsRatioAndHandlesNegativeTarget()
        {
            // ratio>1 は 1 へ Clamp（=full と一致）
            Assert.AreEqual(CatalystRules.CatalystBonus(1f, 2.0f),
                            CatalystRules.CatalystBonus(5f, 2.0f), 1e-4f);
            // 負の targetFactor は 0 扱い
            Assert.AreEqual(0f, CatalystRules.CatalystBonus(0.5f, -3.0f), 1e-4f);
        }

        // ---- SelfAdvancementPenalty（触媒特化が自勢力の進展を削る） ----

        [Test]
        public void SelfAdvancementPenalty_ZeroRatio_NoPenalty()
        {
            Assert.AreEqual(1.0f, CatalystRules.SelfAdvancementPenalty(0f), 1e-4f);
        }

        [Test]
        public void SelfAdvancementPenalty_DecreasesWithRatio()
        {
            float low = CatalystRules.SelfAdvancementPenalty(0.2f);
            float high = CatalystRules.SelfAdvancementPenalty(0.8f);
            Assert.Less(high, low);
            Assert.Less(high, 1.0f);
        }

        [Test]
        public void SelfAdvancementPenalty_FullRatio_UsesPenaltyStrength()
        {
            // ratio=1 → 1 - penaltyStrength = 1 - 0.4 = 0.6
            Assert.AreEqual(0.6f, CatalystRules.SelfAdvancementPenalty(1f), 1e-4f);
        }

        [Test]
        public void SelfAdvancementPenalty_StaysInUnitRange()
        {
            Assert.GreaterOrEqual(CatalystRules.SelfAdvancementPenalty(5f), 0f);
            Assert.LessOrEqual(CatalystRules.SelfAdvancementPenalty(5f), 1f);
            Assert.AreEqual(1.0f, CatalystRules.SelfAdvancementPenalty(float.NaN), 1e-4f); // NaN→0 比率
        }

        // ---- CanTranscend（除外閾値の境界） ----

        [Test]
        public void CanTranscend_BelowThreshold_True()
        {
            Assert.IsTrue(CatalystRules.CanTranscend(0.5f)); // 既定閾値 0.7 未満
        }

        [Test]
        public void CanTranscend_AtOrAboveThreshold_False()
        {
            Assert.IsFalse(CatalystRules.CanTranscend(0.7f)); // 閾値以上は不可
            Assert.IsFalse(CatalystRules.CanTranscend(0.9f));
        }

        [Test]
        public void CanTranscend_BoundaryIsExclusiveAtThreshold()
        {
            float t = CatalystParams.Default.exclusionThreshold; // 0.7
            Assert.IsTrue(CatalystRules.CanTranscend(t - 0.01f, t));
            Assert.IsFalse(CatalystRules.CanTranscend(t, t));
        }

        [Test]
        public void CanTranscend_ClampsNegativeAndNaN()
        {
            Assert.IsTrue(CatalystRules.CanTranscend(-1f));        // <0 → 0 → 適格
            Assert.IsTrue(CatalystRules.CanTranscend(float.NaN));  // NaN → 0 → 適格
        }

        // ---- IsEligibleWithCatalyst（CEND-1 IsEligible への接続） ----

        [Test]
        public void IsEligibleWithCatalyst_HighScoreLowCatalyst_Eligible()
        {
            // 健全国家はスコア適格・低触媒 → 超越可
            Assert.IsTrue(CatalystRules.IsEligibleWithCatalyst(Healthy(), 0.2f));
        }

        [Test]
        public void IsEligibleWithCatalyst_HighScoreHighCatalyst_ExcludedByCatalyst()
        {
            // スコアは適格でも触媒特化が閾値以上 → 変容できない（フェザーン・ジレンマ）
            Assert.IsTrue(TranscendenceRules.IsEligible(TranscendenceRules.EligibilityScore(Healthy())));
            Assert.IsFalse(CatalystRules.IsEligibleWithCatalyst(Healthy(), 0.85f));
        }

        [Test]
        public void IsEligibleWithCatalyst_LowScore_NeverEligible()
        {
            // スコア不足は触媒が低くても不適格（CEND-1 の判定に委譲）
            Assert.IsFalse(CatalystRules.IsEligibleWithCatalyst(0.1f, 0.0f));
        }

        [Test]
        public void IsEligibleWithCatalyst_MatchesAndOfBothGates()
        {
            float score = TranscendenceRules.EligibilityScore(Healthy());
            bool expected = TranscendenceRules.IsEligible(score) && CatalystRules.CanTranscend(0.3f);
            Assert.AreEqual(expected, CatalystRules.IsEligibleWithCatalyst(score, 0.3f));
        }

        [Test]
        public void IsEligibleWithCatalyst_NullState_False()
        {
            Assert.IsFalse(CatalystRules.IsEligibleWithCatalyst((FactionState)null, 0.1f));
        }

        // ---- パラメータ既定値 ----

        [Test]
        public void Defaults_MatchSpec()
        {
            var d = CatalystParams.Default;
            Assert.AreEqual(0.3f, d.catalystStrength, 1e-4f);
            Assert.AreEqual(0.4f, d.penaltyStrength, 1e-4f);
            Assert.AreEqual(0.7f, d.exclusionThreshold, 1e-4f);
            Assert.AreEqual(0.3f, CatalystRules.DefaultCatalystStrength, 1e-4f);
            Assert.AreEqual(0.4f, CatalystRules.DefaultPenaltyStrength, 1e-4f);
            Assert.AreEqual(0.7f, CatalystRules.DefaultExclusionThreshold, 1e-4f);
        }
    }
}
