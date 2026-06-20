using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 超越（人類進化）の純ロジックを固定する（CEND-1 #2355）：適格スコアの 0..1 範囲、
    /// 安定/正統性/成熟（希望）に対する単調応答、閾値境界、進行/完了の状態遷移、
    /// 達成済みのみ勝利対象外、null 安全。
    /// </summary>
    public class TranscendenceRulesTests
    {
        // 全フィールド満点の健全な国家を作る（ctor で legitimacy/hope/cohesion/cooperation=1.0）。
        static FactionState Healthy() => new FactionState(Faction.帝国);

        // 全 proxy を底に落とす。
        static FactionState Degraded()
        {
            var s = new FactionState(Faction.帝国);
            s.regime.legitimacy = 0f;
            s.polity.cooperation = 0f;
            s.organization.cohesion = 0f;
            s.community.hope = 0f;
            return s;
        }

        [Test]
        public void EligibilityScore_IsWithinUnitRange()
        {
            Assert.GreaterOrEqual(TranscendenceRules.EligibilityScore(Healthy()), 0f);
            Assert.LessOrEqual(TranscendenceRules.EligibilityScore(Healthy()), 1f);
            Assert.GreaterOrEqual(TranscendenceRules.EligibilityScore(Degraded()), 0f);
            Assert.LessOrEqual(TranscendenceRules.EligibilityScore(Degraded()), 1f);
        }

        [Test]
        public void EligibilityScore_HealthyNearOne_DegradedNearZero()
        {
            Assert.AreEqual(1f, TranscendenceRules.EligibilityScore(Healthy()), 1e-4f);
            Assert.AreEqual(0f, TranscendenceRules.EligibilityScore(Degraded()), 1e-4f);
        }

        [Test]
        public void EligibilityScore_RespondsToLegitimacy()
        {
            var low = Degraded();
            var high = Degraded();
            high.regime.legitimacy = 1f;
            Assert.Greater(TranscendenceRules.EligibilityScore(high),
                           TranscendenceRules.EligibilityScore(low));
        }

        [Test]
        public void EligibilityScore_RespondsToMaturityHope()
        {
            var low = Degraded();
            var high = Degraded();
            high.community.hope = 1f;
            Assert.Greater(TranscendenceRules.EligibilityScore(high),
                           TranscendenceRules.EligibilityScore(low));
        }

        [Test]
        public void EligibilityScore_RespondsToStability()
        {
            // 安定度は正統性/合意/結束/希望の平均＝結束を上げると安定 proxy が上がりスコアも上がる。
            var low = Degraded();
            var high = Degraded();
            high.organization.cohesion = 1f;
            Assert.Greater(TranscendenceRules.EligibilityScore(high),
                           TranscendenceRules.EligibilityScore(low));
        }

        [Test]
        public void EligibilityScore_Null_IsZero()
        {
            Assert.AreEqual(0f, TranscendenceRules.EligibilityScore(null), 0f);
            Assert.AreEqual(0f, TranscendenceRules.EligibilityScore(null, TranscendenceParams.Default), 0f);
        }

        [Test]
        public void IsEligible_ThresholdBoundary()
        {
            var p = TranscendenceParams.Default; // threshold 0.9
            Assert.IsFalse(TranscendenceRules.IsEligible(p.threshold - 0.01f, p));
            Assert.IsTrue(TranscendenceRules.IsEligible(p.threshold, p));        // 境界は適格（>=）
            Assert.IsTrue(TranscendenceRules.IsEligible(p.threshold + 0.01f, p));
        }

        [Test]
        public void HealthyState_IsEligible_DegradedIsNot()
        {
            Assert.IsTrue(TranscendenceRules.IsEligible(TranscendenceRules.EligibilityScore(Healthy())));
            Assert.IsFalse(TranscendenceRules.IsEligible(TranscendenceRules.EligibilityScore(Degraded())));
        }

        [Test]
        public void BeginTranscendence_SetsTranscending_Idempotent()
        {
            var st = new TranscendenceState();
            TranscendenceRules.BeginTranscendence(st);
            Assert.IsTrue(st.isTranscending);
            Assert.IsFalse(st.isTranscended);
            // 冪等。
            TranscendenceRules.BeginTranscendence(st);
            Assert.IsTrue(st.isTranscending);
        }

        [Test]
        public void BeginTranscendence_AfterCompleted_DoesNotReopen()
        {
            var st = new TranscendenceState();
            TranscendenceRules.CompleteTranscendence(st);
            TranscendenceRules.BeginTranscendence(st);
            Assert.IsTrue(st.isTranscended);
            Assert.IsFalse(st.isTranscending);
        }

        [Test]
        public void CompleteTranscendence_SetsTranscended_ClearsTranscending()
        {
            var st = new TranscendenceState();
            TranscendenceRules.BeginTranscendence(st);
            TranscendenceRules.CompleteTranscendence(st);
            Assert.IsTrue(st.isTranscended);
            Assert.IsFalse(st.isTranscending);
        }

        [Test]
        public void IsExcludedFromVictory_OnlyWhenTranscended()
        {
            Assert.IsFalse(TranscendenceRules.IsExcludedFromVictory(new TranscendenceState()));

            var transcending = new TranscendenceState();
            TranscendenceRules.BeginTranscendence(transcending);
            Assert.IsFalse(TranscendenceRules.IsExcludedFromVictory(transcending));

            var done = new TranscendenceState();
            TranscendenceRules.CompleteTranscendence(done);
            Assert.IsTrue(TranscendenceRules.IsExcludedFromVictory(done));
        }

        [Test]
        public void NullState_IsSafe()
        {
            Assert.DoesNotThrow(() => TranscendenceRules.BeginTranscendence(null));
            Assert.DoesNotThrow(() => TranscendenceRules.CompleteTranscendence(null));
            Assert.IsFalse(TranscendenceRules.IsExcludedFromVictory(null));
        }
    }
}
