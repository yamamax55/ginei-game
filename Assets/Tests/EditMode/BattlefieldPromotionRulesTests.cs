using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>戦場昇進（武勲による抜擢・論功行賞）の純ロジックテスト。抜擢の妥当性・年功と実力・士気/忠誠/不満を担保。</summary>
    public class BattlefieldPromotionRulesTests
    {
        private const float Tol = 1e-4f;
        private const float TolPow = 1e-3f;

        /// <summary>武勲評価＝戦功×難度×独断の功。難しい戦を独断で勝った功ほど高く評価される。</summary>
        [Test]
        public void MeritScore_DifficultyAndInitiativeRaiseMerit()
        {
            // base=0.6, bonus=1+0.8×0.5+0.5×0.3=1.55, 0.6×1.55=0.93
            float m = BattlefieldPromotionRules.MeritScore(0.6f, 0.8f, 0.5f);
            Assert.AreEqual(0.93f, m, Tol);
        }

        /// <summary>難度・独断が無ければ戦功そのまま（bonus=1.0）。</summary>
        [Test]
        public void MeritScore_NoBonus_IsBaseContribution()
        {
            float m = BattlefieldPromotionRules.MeritScore(0.5f, 0f, 0f);
            Assert.AreEqual(0.5f, m, Tol);
        }

        /// <summary>抜擢の妥当性＝武勲/(1+現階級)。下位者ほど同じ武勲で上がりやすい。</summary>
        [Test]
        public void PromotionMerit_LowerRankPromotesEasier()
        {
            // 下士官 tier1: 0.6/2×3.0=0.9
            float low = BattlefieldPromotionRules.PromotionMerit(0.6f, 1f);
            Assert.AreEqual(0.9f, low, Tol);
            // 将官 tier5: 0.6/6×3.0=0.3
            float high = BattlefieldPromotionRules.PromotionMerit(0.6f, 5f);
            Assert.AreEqual(0.3f, high, Tol);
            Assert.Greater(low, high);
        }

        /// <summary>年功の主張＝勤続年数（Sqrtで逓減）。在籍年数だけで昇進を求める力。</summary>
        [Test]
        public void SeniorityClaim_GrowsWithYearsButDiminishes()
        {
            // sqrt(4)=2×0.4=0.8
            float s = BattlefieldPromotionRules.SeniorityClaim(4f);
            Assert.AreEqual(0.8f, s, TolPow);
        }

        /// <summary>昇進度＝実力と年功を実力主義度で混合。実力主義の組織ほど武勲が物を言う。</summary>
        [Test]
        public void PromotionDecision_MixesMeritAndSeniorityByBias()
        {
            // Lerp(0.3,0.8,0.75)=0.3+0.5×0.75=0.675
            float d = BattlefieldPromotionRules.PromotionDecision(0.8f, 0.3f, 0.75f);
            Assert.AreEqual(0.675f, d, Tol);
        }

        /// <summary>顕彰による士気高揚＝昇進×顕彰の可視性（陰の論功は効きが薄い）。</summary>
        [Test]
        public void MoraleBoostFromRecognition_ScalesWithVisibility()
        {
            // 0.6×0.5×0.5=0.15
            float morale = BattlefieldPromotionRules.MoraleBoostFromRecognition(0.6f, 0.5f);
            Assert.AreEqual(0.15f, morale, Tol);
        }

        /// <summary>引き立てへの忠誠＝抜擢×恩義（引き上げてくれた上官への恩義が忠誠を生む）。</summary>
        [Test]
        public void LoyaltyFromPatronage_ScalesWithGratitude()
        {
            // 0.6×0.8×0.6=0.288
            float loyalty = BattlefieldPromotionRules.LoyaltyFromPatronage(0.6f, 0.8f);
            Assert.AreEqual(0.288f, loyalty, Tol);
            // 恩義がなければ忠誠に変わらない
            float none = BattlefieldPromotionRules.LoyaltyFromPatronage(0.6f, 0f);
            Assert.AreEqual(0f, none, Tol);
        }

        /// <summary>不満＝武勲があるのに昇進しない乖離（功を立てたのに報われない憤り）。</summary>
        [Test]
        public void Resentment_RisesWhenMeritExceedsPromotion()
        {
            // gap=0.9-0.3=0.6×0.7=0.42
            float r = BattlefieldPromotionRules.Resentment(0.9f, 0.3f);
            Assert.AreEqual(0.42f, r, Tol);
            // 処遇が武勲に見合えば不満なし
            float fair = BattlefieldPromotionRules.Resentment(0.5f, 0.7f);
            Assert.AreEqual(0f, fair, Tol);
        }

        /// <summary>昇進に値するか＝既定閾値0.5を超えれば true。</summary>
        [Test]
        public void DeservesPromotion_UsesThreshold()
        {
            Assert.IsTrue(BattlefieldPromotionRules.DeservesPromotion(0.6f));
            Assert.IsFalse(BattlefieldPromotionRules.DeservesPromotion(0.4f));
            // 明示閾値版
            Assert.IsTrue(BattlefieldPromotionRules.DeservesPromotion(0.3f, 0.2f));
        }

        /// <summary>
        /// 物語テスト＝ラインハルトの抜擢人事。難しい戦を独断で勝った下級士官（大武勲）が、年功を飛び越え
        /// 実力主義の組織で抜擢される。公の顕彰で士気が上がり、引き上げた上官への恩義が忠誠を生む。
        /// もし報われなければ（昇進せず）功と処遇の乖離が不満を生んでいた、ことを対比で確かめる。
        /// </summary>
        [Test]
        public void Narrative_ReinhardStylePromotionRewardsMeritAndBreedsLoyalty()
        {
            // 下級士官（tier1）が難戦を独断で勝った大武勲
            float merit = BattlefieldPromotionRules.MeritScore(0.8f, 0.9f, 0.8f); // 0.8×(1+0.45+0.24)=0.8×1.69=1.352→clamp1.0
            Assert.AreEqual(1.0f, merit, Tol);

            float promMerit = BattlefieldPromotionRules.PromotionMerit(merit, 1f); // 1/2×3=1.5→1.0
            float seniority = BattlefieldPromotionRules.SeniorityClaim(1f);        // sqrt1×0.4=0.4

            // 実力主義の組織（bias=0.9）では武勲が物を言い抜擢される
            float decision = BattlefieldPromotionRules.PromotionDecision(promMerit, seniority, 0.9f);
            Assert.IsTrue(BattlefieldPromotionRules.DeservesPromotion(decision));

            // 公の顕彰で士気が上がり、恩義が忠誠を生む
            float morale = BattlefieldPromotionRules.MoraleBoostFromRecognition(decision, 1f);
            float loyalty = BattlefieldPromotionRules.LoyaltyFromPatronage(decision, 0.9f);
            Assert.Greater(morale, 0f);
            Assert.Greater(loyalty, 0f);

            // 抜擢されたので不満はない
            float content = BattlefieldPromotionRules.Resentment(merit, decision);

            // 対比＝もし年功主義の組織（bias=0.1）で報われなければ
            float snubbed = BattlefieldPromotionRules.PromotionDecision(promMerit, seniority, 0.1f);
            float resentment = BattlefieldPromotionRules.Resentment(merit, snubbed);
            Assert.Greater(resentment, content); // 報われない方が不満が大きい
        }
    }
}
