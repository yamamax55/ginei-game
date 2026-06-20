using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>彼我戦力評価 IntelligenceEstimateRules の EditMode テスト（既定Params期待値を手計算で固定）。</summary>
    public class IntelligenceEstimateRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void StrengthEstimate_InflatesObservedByCoverGap()
        {
            // observed=180, gapFactor=1-0.5*(1-0.6)=0.8, 180/0.8=225
            float est = IntelligenceEstimateRules.StrengthEstimate(new[] { 100f, 50f, 30f }, 0.6f);
            Assert.AreEqual(225f, est, Eps);
        }

        [Test]
        public void StrengthEstimate_ZeroCoverInflatesMost_NegativesClamped()
        {
            // observed=100 (負値は0扱い), gapFactor=1-0.5*1=0.5, 100/0.5=200
            float est = IntelligenceEstimateRules.StrengthEstimate(new[] { 100f, -40f }, 0f);
            Assert.AreEqual(200f, est, Eps);
        }

        [Test]
        public void StrengthEstimate_NullAndEmptyAreZero()
        {
            Assert.AreEqual(0f, IntelligenceEstimateRules.StrengthEstimate(null, 0.5f), Eps);
            Assert.AreEqual(0f, IntelligenceEstimateRules.StrengthEstimate(new float[0], 0.5f), Eps);
        }

        [Test]
        public void UncertaintyBand_BlendsCoverGapAndAge()
        {
            // gap=0.5, blend=0.5*0.5+0.5*0.5=0.5, 0.6*0.5=0.3
            Assert.AreEqual(0.3f, IntelligenceEstimateRules.UncertaintyBand(0.5f, 0.5f), Eps);
            // gap=1, blend=1, 0.6*1=0.6 (最大幅)
            Assert.AreEqual(0.6f, IntelligenceEstimateRules.UncertaintyBand(0f, 1f), Eps);
        }

        [Test]
        public void OverestimateBias_ThreatTimesCaution()
        {
            // 0.4*0.8*0.5=0.16
            Assert.AreEqual(0.16f, IntelligenceEstimateRules.OverestimateBias(0.8f, 0.5f), Eps);
        }

        [Test]
        public void UnderestimateBias_OverconfidenceTimesDeception()
        {
            // 0.4*0.5*0.6=0.12
            Assert.AreEqual(0.12f, IntelligenceEstimateRules.UnderestimateBias(0.5f, 0.6f), Eps);
        }

        [Test]
        public void WorstAndBestCase_SpreadAroundEstimate()
        {
            // band=0.3: worst=200*1.3=260, best=200*0.7=140
            Assert.AreEqual(260f, IntelligenceEstimateRules.WorstCaseEstimate(200f, 0.3f), Eps);
            Assert.AreEqual(140f, IntelligenceEstimateRules.BestCaseEstimate(200f, 0.3f), Eps);
        }

        [Test]
        public void BestCase_NeverNegative_BandClampedToOne()
        {
            // band>1 はクランプ→1, best=200*(1-1)=0
            Assert.AreEqual(0f, IntelligenceEstimateRules.BestCaseEstimate(200f, 5f), Eps);
        }

        [Test]
        public void IntentInference_SkillPullsTowardRawFromNeutral()
        {
            // raw=(0.5*0.8+0.5*0.4)/1=0.6
            // skill=1: Lerp(0.5,0.6,1)=0.6
            Assert.AreEqual(0.6f, IntelligenceEstimateRules.IntentInference(0.8f, 0.4f, 1f), Eps);
            // skill=0.5: Lerp(0.5,0.6,0.5)=0.55
            Assert.AreEqual(0.55f, IntelligenceEstimateRules.IntentInference(0.8f, 0.4f, 0.5f), Eps);
            // skill=0: 判断保留=0.5
            Assert.AreEqual(0.5f, IntelligenceEstimateRules.IntentInference(0.8f, 0.4f, 0f), Eps);
        }

        [Test]
        public void EstimateBias_OverMinusUnder_ClampedSigned()
        {
            // 0.16-0.12=0.04 (わずかに悲観)
            Assert.AreEqual(0.04f, IntelligenceEstimateRules.EstimateBias(0.16f, 0.12f), Eps);
            // 過小のみ→楽観(負)
            Assert.AreEqual(-0.3f, IntelligenceEstimateRules.EstimateBias(0f, 0.3f), Eps);
            // 範囲外入力でも -1..1 に収まる
            float b = IntelligenceEstimateRules.EstimateBias(5f, 0f);
            Assert.That(b, Is.InRange(-1f, 1f));
            Assert.AreEqual(1f, b, Eps);
        }

        [Test]
        public void Narrative_CautiousAdmiralWithStaleIntelOverestimatesAndBracesForWorstCase()
        {
            // 慎重な提督が古く薄い索敵で敵艦隊を観測：実戦力を過大視し、最悪ケースに備えて慎重になる物語。
            var p = IntelligenceEstimateParams.Default;
            float[] observed = { 60f, 40f }; // 見えているのは100だけ
            float coverage = 0.4f;           // 索敵カバーは薄い
            float age = 0.8f;                // 情報は古い

            float est = IntelligenceEstimateRules.StrengthEstimate(observed, coverage, p);
            // gapFactor=1-0.5*(1-0.4)=0.7, 100/0.7≈142.857 → 観測より多く見積もる
            Assert.Greater(est, 100f);

            float band = IntelligenceEstimateRules.UncertaintyBand(coverage, age, p);
            float worst = IntelligenceEstimateRules.WorstCaseEstimate(est, band, p);
            float best = IntelligenceEstimateRules.BestCaseEstimate(est, band, p);
            // 不確実性ゆえ最悪は推定を上回り、最良は下回り、幅が開く
            Assert.Greater(worst, est);
            Assert.Less(best, est);
            Assert.Greater(worst - best, 0f);

            // 脅威認識が高く慎重→過大評価、敵は侮っておらず秘匿も低い→過小評価は小さい
            float over = IntelligenceEstimateRules.OverestimateBias(0.9f, 0.8f, p);
            float under = IntelligenceEstimateRules.UnderestimateBias(0.1f, 0.2f, p);
            float bias = IntelligenceEstimateRules.EstimateBias(over, under, p);
            // 正味は悲観（敵を過大視）に振れる
            Assert.Greater(bias, 0f);
            Assert.That(bias, Is.InRange(-1f, 1f));

            // 敵態勢が前進的・過去も攻勢的・分析力もそこそこ→攻勢の意図と読む（中庸0.5超）
            float intent = IntelligenceEstimateRules.IntentInference(0.8f, 0.7f, 0.6f, p);
            Assert.Greater(intent, 0.5f);
        }
    }
}
