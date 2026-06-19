using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// IntelAnalysisRules（情報分析）の EditMode テスト。既定 Params 期待値を手計算で固定。
    /// </summary>
    public class IntelAnalysisRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void SourceReliability_TrackRecordBoostedByCorroboration()
        {
            // 0.8 * (1 - 0.5 + 0.5*0.6) = 0.8 * 0.8 = 0.64
            float r = IntelAnalysisRules.SourceReliability(0.8f, 0.6f);
            Assert.AreEqual(0.64f, r, Eps);
            // 裏付け満点なら実績そのもの
            Assert.AreEqual(0.8f, IntelAnalysisRules.SourceReliability(0.8f, 1f), Eps);
        }

        [Test]
        public void FragmentSynthesis_MoreFragmentsBuildPicture_WithDiminishingReturns()
        {
            // 0.8 * (1 - (1/4)^0.5) = 0.8 * 0.5 = 0.4
            Assert.AreEqual(0.4f, IntelAnalysisRules.FragmentSynthesis(3, 0.8f), PowEps);
            // 断片0なら像は結ばない
            Assert.AreEqual(0f, IntelAnalysisRules.FragmentSynthesis(0, 0.9f), Eps);
            // 断片が増えるほど像が濃くなる（逓減はするが単調増加）
            float few = IntelAnalysisRules.FragmentSynthesis(2, 0.8f);
            float many = IntelAnalysisRules.FragmentSynthesis(8, 0.8f);
            Assert.Greater(many, few);
        }

        [Test]
        public void ContradictionResolution_NoConflictIsPerfect_ManyConflictsLeanOnSkill()
        {
            // 矛盾なし＝1.0
            Assert.AreEqual(1f, IntelAnalysisRules.ContradictionResolution(0, 0.6f), Eps);
            // 0.6 + 0.4/(1+3) = 0.6 + 0.1 = 0.7
            Assert.AreEqual(0.7f, IntelAnalysisRules.ContradictionResolution(3, 0.6f), Eps);
        }

        [Test]
        public void ConfirmationBias_PriorTimesUndiscipline()
        {
            // 0.5 * 0.8 * (1-0.6) = 0.5 * 0.8 * 0.4 = 0.16
            Assert.AreEqual(0.16f, IntelAnalysisRules.ConfirmationBias(0.8f, 0.6f), Eps);
            // 規律満点ならバイアス消える
            Assert.AreEqual(0f, IntelAnalysisRules.ConfirmationBias(0.8f, 1f), Eps);
        }

        [Test]
        public void AnalysisConfidence_GeometricMeanOfThree()
        {
            // (0.64 * 0.4 * 0.7)^(1/3) = 0.1792^(1/3) ≈ 0.56357
            float c = IntelAnalysisRules.AnalysisConfidence(0.64f, 0.4f, 0.7f);
            Assert.AreEqual(0.56357f, c, PowEps);
            // どれか0なら確度0
            Assert.AreEqual(0f, IntelAnalysisRules.AnalysisConfidence(0.64f, 0f, 0.7f), Eps);
        }

        [Test]
        public void DeceptionDetection_SkillVsSophistication()
        {
            // 0.7 / (0.7 + 0.3) = 0.7
            Assert.AreEqual(0.7f, IntelAnalysisRules.DeceptionDetection(0.7f, 0.3f), Eps);
            // 双方0なら看破不能
            Assert.AreEqual(0f, IntelAnalysisRules.DeceptionDetection(0f, 0f), Eps);
            // 巧妙な欺瞞ほど看破は下がる
            Assert.Greater(IntelAnalysisRules.DeceptionDetection(0.6f, 0.2f),
                           IntelAnalysisRules.DeceptionDetection(0.6f, 0.8f));
        }

        [Test]
        public void EstimateAccuracy_ConfidenceMinusBiasAndDeceptionHarm()
        {
            // 0.56357 - 0.16 - 0.5*(1-0.7) = 0.56357 - 0.16 - 0.15 = 0.25357
            float a = IntelAnalysisRules.EstimateAccuracy(0.56357f, 0.16f, 0.7f);
            Assert.AreEqual(0.25357f, a, PowEps);
            // 看破満点なら欺瞞ペナルティ0
            float clean = IntelAnalysisRules.EstimateAccuracy(0.8f, 0f, 1f);
            Assert.AreEqual(0.8f, clean, Eps);
            // 強い欺瞞＋強いバイアスで負（誤った敵情判断）かつ -1 下限
            float wrong = IntelAnalysisRules.EstimateAccuracy(0.2f, 1f, 0f);
            Assert.AreEqual(-1f, wrong, Eps);
        }

        [Test]
        public void IsEstimateReliable_Threshold()
        {
            Assert.IsTrue(IntelAnalysisRules.IsEstimateReliable(0.5f, 0.4f));
            Assert.IsFalse(IntelAnalysisRules.IsEstimateReliable(0.3f, 0.4f));
            // 既定閾値委譲版（DefaultReliabilityThreshold=0.4）
            Assert.IsTrue(IntelAnalysisRules.IsEstimateReliable(0.4f));
            Assert.IsFalse(IntelAnalysisRules.IsEstimateReliable(0.39f));
        }

        [Test]
        public void NullAndExtremeInputs_AreSafe()
        {
            // 負の断片数・負の矛盾数は0扱い
            Assert.AreEqual(0f, IntelAnalysisRules.FragmentSynthesis(-5, 0.8f), Eps);
            Assert.AreEqual(1f, IntelAnalysisRules.ContradictionResolution(-3, 0.5f), Eps);
            // clamp 確認：オーバー入力でも 0..1 / -1..1 に収まる
            float r = IntelAnalysisRules.SourceReliability(5f, 5f);
            Assert.LessOrEqual(r, 1f);
            Assert.GreaterOrEqual(r, 0f);
            float acc = IntelAnalysisRules.EstimateAccuracy(2f, 0f, 1f);
            Assert.LessOrEqual(acc, 1f);
        }

        [Test]
        public void Story_VeteranAnalystSeesThrough_RookieFooledByDeception()
        {
            // 物語：同じ生情報（実績ある情報源・断片豊富・矛盾少）を、
            // 練達の分析官（高技量・高規律）と新米（低技量・低規律＝先入観に流される）が分析する。
            float track = 0.8f, corr = 0.7f;
            int fragments = 6; float fragQuality = 0.8f;
            int conflicts = 2;
            float prior = 0.7f;
            float deceptionSoph = 0.6f; // 巧妙な敵欺瞞

            float reliability = IntelAnalysisRules.SourceReliability(track, corr);
            float synthesis = IntelAnalysisRules.FragmentSynthesis(fragments, fragQuality);

            // 練達：技量0.9・規律0.85
            float vetRes = IntelAnalysisRules.ContradictionResolution(conflicts, 0.9f);
            float vetConf = IntelAnalysisRules.AnalysisConfidence(reliability, synthesis, vetRes);
            float vetBias = IntelAnalysisRules.ConfirmationBias(prior, 0.85f);
            float vetDetect = IntelAnalysisRules.DeceptionDetection(0.9f, deceptionSoph);
            float vetAcc = IntelAnalysisRules.EstimateAccuracy(vetConf, vetBias, vetDetect);

            // 新米：技量0.3・規律0.2
            float rookieRes = IntelAnalysisRules.ContradictionResolution(conflicts, 0.3f);
            float rookieConf = IntelAnalysisRules.AnalysisConfidence(reliability, synthesis, rookieRes);
            float rookieBias = IntelAnalysisRules.ConfirmationBias(prior, 0.2f);
            float rookieDetect = IntelAnalysisRules.DeceptionDetection(0.3f, deceptionSoph);
            float rookieAcc = IntelAnalysisRules.EstimateAccuracy(rookieConf, rookieBias, rookieDetect);

            // 練達の方が推定精度が高く、敵情判断を信頼でき、新米は欺瞞に騙される
            Assert.Greater(vetAcc, rookieAcc);
            Assert.IsTrue(IntelAnalysisRules.IsEstimateReliable(vetAcc));
            Assert.IsFalse(IntelAnalysisRules.IsEstimateReliable(rookieAcc));
        }
    }
}
