using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// TacticalSurpriseRules（戦術的奇襲）の EditMode テスト。既定 Params で期待値固定。
    /// </summary>
    public class TacticalSurpriseRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void SurpriseLevel_UnexpectedAndConcealed_High()
        {
            // (1-0.2)=0.8 × concealment 0.9 × 1.0 = 0.72
            Assert.AreEqual(0.72f, TacticalSurpriseRules.SurpriseLevel(0.2f, 0.9f), Eps);
        }

        [Test]
        public void FirstStrikeBonus_ScalesWithSurprise()
        {
            // 1 + 0.72 × 1.0 = 1.72
            Assert.AreEqual(1.72f, TacticalSurpriseRules.FirstStrikeBonus(0.72f), Eps);
        }

        [Test]
        public void EnemyReactionDelay_StrongSurpriseSlowEnemy()
        {
            // surprise 0.72 × (1-0.5) × maxDelay 4.0 = 1.44
            Assert.AreEqual(1.44f, TacticalSurpriseRules.EnemyReactionDelay(0.72f, 0.5f), Eps);
        }

        [Test]
        public void SurpriseDecay_FadesOverTime()
        {
            // surprise 0.72 × (1 - 2.5/5.0)=0.5 → 0.36
            Assert.AreEqual(0.36f, TacticalSurpriseRules.SurpriseDecay(0.72f, 2.5f), Eps);
            // 立て直し時間を過ぎれば完全に消える
            Assert.AreEqual(0f, TacticalSurpriseRules.SurpriseDecay(0.72f, 10f), Eps);
        }

        [Test]
        public void WindowOfOpportunity_TracksReactionDelay()
        {
            // reactionDelay 1.44 × 1.0 = 1.44
            Assert.AreEqual(1.44f, TacticalSurpriseRules.WindowOfOpportunity(1.44f), Eps);
        }

        [Test]
        public void DetectionNullification_ReconErasesConcealment()
        {
            // concealment 0.9 − recon 0.6 × 1.0 = 0.3
            Assert.AreEqual(0.3f, TacticalSurpriseRules.DetectionNullification(0.9f, 0.6f), Eps);
            // 偵察が隠密を上回れば 0（奇襲不成立の素地）
            Assert.AreEqual(0f, TacticalSurpriseRules.DetectionNullification(0.5f, 0.9f), Eps);
        }

        [Test]
        public void CompoundedShock_LowMoraleAmplifies()
        {
            // bonus 1.72 × (1 + (1-0.4)×0.5) = 1.72 × 1.3 = 2.236
            Assert.AreEqual(2.236f, TacticalSurpriseRules.CompoundedShock(1.72f, 0.4f), Eps);
            // 士気満タンの敵は心理的増幅なし＝ボーナスそのまま
            Assert.AreEqual(1.72f, TacticalSurpriseRules.CompoundedShock(1.72f, 1f), Eps);
        }

        [Test]
        public void IsSurpriseAchieved_ThresholdGate()
        {
            Assert.IsTrue(TacticalSurpriseRules.IsSurpriseAchieved(0.72f, 0.5f));
            Assert.IsFalse(TacticalSurpriseRules.IsSurpriseAchieved(0.3f, 0.5f));
        }

        [Test]
        public void Narrative_SurpriseStrikeFadesAndCanBeDetected()
        {
            // 不意打ち：敵は予期せず(0.2)・こちらは隠れて寄れた(0.9)→奇襲成立
            float surprise = TacticalSurpriseRules.SurpriseLevel(0.2f, 0.9f);
            Assert.AreEqual(0.72f, surprise, Eps);
            Assert.IsTrue(TacticalSurpriseRules.IsSurpriseAchieved(surprise, 0.5f));

            // 初撃に大きなボーナス＋敵の反応が遅れる
            float bonus = TacticalSurpriseRules.FirstStrikeBonus(surprise);
            float delay = TacticalSurpriseRules.EnemyReactionDelay(surprise, 0.5f);
            Assert.Greater(bonus, 1f);
            Assert.Greater(delay, 0f);
            // その遅延ぶんが好機の窓になる
            Assert.AreEqual(delay, TacticalSurpriseRules.WindowOfOpportunity(delay), Eps);

            // だが時間が経つと敵が態勢を立て直し、奇襲効果は薄れる
            float later = TacticalSurpriseRules.SurpriseDecay(surprise, 4f);
            Assert.Less(later, surprise);

            // そして偵察に見つかっていれば、そもそも隠密が消え奇襲は成立しない
            float concealedAfterRecon = TacticalSurpriseRules.DetectionNullification(0.9f, 0.95f);
            float detectedSurprise = TacticalSurpriseRules.SurpriseLevel(0.2f, concealedAfterRecon);
            Assert.IsFalse(TacticalSurpriseRules.IsSurpriseAchieved(detectedSurprise, 0.5f));
        }
    }
}
