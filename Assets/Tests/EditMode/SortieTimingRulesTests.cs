using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// SortieTimingRules（出撃・仕掛けのタイミング）の純ロジックテスト。既定 Params で期待値固定。
    /// </summary>
    public class SortieTimingRulesTests
    {
        const float Tol = 1e-4f;

        [Test]
        public void Readiness_BothMaxed_IsOne()
        {
            Assert.AreEqual(1f, SortieTimingRules.Readiness(1f, 1f), Tol);
        }

        [Test]
        public void Readiness_MoraleZero_HalvedBySynergy()
        {
            // weighted=0.5, synergy=0 → Lerp(0.5,0,0.5)=0.25
            Assert.AreEqual(0.25f, SortieTimingRules.Readiness(1f, 0f), Tol);
        }

        [Test]
        public void Opportunity_IsProductOfVulnerabilityAndDisorder()
        {
            Assert.AreEqual(0.4f, SortieTimingRules.Opportunity(0.8f, 0.5f), Tol);
        }

        [Test]
        public void TimingScore_IsProductOfReadinessAndOpportunity()
        {
            Assert.AreEqual(0.24f, SortieTimingRules.TimingScore(0.6f, 0.4f), Tol);
        }

        [Test]
        public void PrematureLoss_AtThreshold_NoPenalty()
        {
            Assert.AreEqual(0f, SortieTimingRules.PrematureLoss(0.5f), Tol);
        }

        [Test]
        public void PrematureLoss_LowReadiness_Penalized()
        {
            // shortfall=(0.5-0.2)/0.5=0.6, *0.6 = 0.36
            Assert.AreEqual(0.36f, SortieTimingRules.PrematureLoss(0.2f), Tol);
        }

        [Test]
        public void MissedWindowLoss_PassesThroughDecayClamped()
        {
            Assert.AreEqual(0.7f, SortieTimingRules.MissedWindowLoss(0.7f), Tol);
            Assert.AreEqual(1f, SortieTimingRules.MissedWindowLoss(1.5f), Tol);
        }

        [Test]
        public void InitiativeBonus_HighScoreAndSlowEnemy_GivesBonus()
        {
            // 0.8*0.5*0.5 = 0.2
            Assert.AreEqual(0.2f, SortieTimingRules.InitiativeBonus(0.8f, 0.5f), Tol);
        }

        [Test]
        public void OptimalDelay_Unprepared_TellsToWait()
        {
            // prepGap=0.8-0.4=0.4, urgency=0 → 0.4*10 = +4 (待つ)
            Assert.AreEqual(4f, SortieTimingRules.OptimalDelay(0.4f, 0f), Tol);
        }

        [Test]
        public void OptimalDelay_ReadyWithOpportunity_TellsToStrikeNow()
        {
            // prepGap=0, urgency=0.5*6=3 → -3 (今動く)
            Assert.AreEqual(-3f, SortieTimingRules.OptimalDelay(0.8f, 0.5f), Tol);
        }

        [Test]
        public void IsSortieFavorable_RespectsThreshold()
        {
            Assert.IsTrue(SortieTimingRules.IsSortieFavorable(0.6f));
            Assert.IsFalse(SortieTimingRules.IsSortieFavorable(0.3f));
        }

        [Test]
        public void ParamsCtor_ClampsOutOfRange()
        {
            var p = new SortieTimingParams(
                preparationWeight: 5f, synergyBlend: -1f,
                prematureThreshold: 2f, maxPrematurePenalty: 9f,
                maxInitiativeBonus: -3f, optimalReadiness: 4f,
                delayPerReadiness: -2f, opportunityUrgency: -1f,
                maxDelay: -10f, favorableThreshold: 8f);
            Assert.AreEqual(1f, p.preparationWeight, Tol);
            Assert.AreEqual(0f, p.synergyBlend, Tol);
            Assert.AreEqual(1f, p.prematureThreshold, Tol);
            Assert.AreEqual(1f, p.maxPrematurePenalty, Tol);
            Assert.AreEqual(0f, p.maxInitiativeBonus, Tol);
            Assert.AreEqual(1f, p.optimalReadiness, Tol);
            Assert.AreEqual(0f, p.delayPerReadiness, Tol);
            Assert.AreEqual(0f, p.opportunityUrgency, Tol);
            Assert.AreEqual(0f, p.maxDelay, Tol);
            Assert.AreEqual(1f, p.favorableThreshold, Tol);
        }

        /// <summary>
        /// 物語テスト：準備と好機が噛み合う瞬間が最大効果。早撃ち（準備不足）は損し、
        /// 待ちすぎる（好機が閉じる）と隙を逃す。三者を1本で確かめる。
        /// </summary>
        [Test]
        public void Narrative_PeakWhenPreparationAndOpportunityAlign()
        {
            // 噛み合う瞬間：準備度高(0.9)×好機高(脆弱0.9×混乱0.8=0.72)
            float readyNow = SortieTimingRules.Readiness(0.9f, 0.9f);
            float oppNow = SortieTimingRules.Opportunity(0.9f, 0.8f);
            float scorePeak = SortieTimingRules.TimingScore(readyNow, oppNow);

            // 早撃ち：準備不足(0.2)で同じ好機 → スコアが低く、ペナルティも発生
            float readyEarly = SortieTimingRules.Readiness(0.2f, 0.2f);
            float scoreEarly = SortieTimingRules.TimingScore(readyEarly, oppNow);
            Assert.Less(scoreEarly, scorePeak, "早撃ちは準備不足でスコアが落ちる");
            Assert.Greater(SortieTimingRules.PrematureLoss(readyEarly), 0f, "早撃ちは準備不足の損を被る");
            Assert.AreEqual(0f, SortieTimingRules.PrematureLoss(readyNow), Tol, "万全の準備なら早撃ち損なし");

            // 待ちすぎ：準備は整っても好機が閉じる(脆弱0.2×混乱0.1=0.02) → スコア激減＋逸機損
            float oppLate = SortieTimingRules.Opportunity(0.2f, 0.1f);
            float scoreLate = SortieTimingRules.TimingScore(readyNow, oppLate);
            Assert.Less(scoreLate, scorePeak, "待ちすぎると好機が閉じてスコアが落ちる");
            Assert.Greater(SortieTimingRules.MissedWindowLoss(0.8f), 0f, "閉じた隙のぶん逸機損");

            // 噛み合う瞬間は出撃機が来たと判定され、先手の主導権も得る
            Assert.IsTrue(SortieTimingRules.IsSortieFavorable(scorePeak), "噛み合う瞬間は出撃機");
            Assert.Greater(SortieTimingRules.InitiativeBonus(scorePeak, 0.6f), 0f, "先手で主導権");
        }
    }
}
