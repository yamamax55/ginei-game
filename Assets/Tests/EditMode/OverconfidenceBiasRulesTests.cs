using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 過信バイアスと計画錯誤を固定する：成功確率の水増し、所要見積もりの楽観割引、見積もりギャップ、
    /// WYSIATI の未知軽視、外部視点による引き戻し、踏み込み判定、フィードバック校正、代償。境界を担保。
    /// </summary>
    public class OverconfidenceBiasRulesTests
    {
        private static readonly OverconfidenceParams P = OverconfidenceParams.Default;
        // 水増し0.3/計画割引0.4/外部視点重み0.5/校正率0.5

        [Test]
        public void InflatedSuccessEstimate_RaisesTowardOne()
        {
            // 過信ゼロ＝真値そのまま
            Assert.AreEqual(0.5f, OverconfidenceBiasRules.InflatedSuccessEstimate(0.5f, 0f, P), 1e-4f);
            // 真0.5・過信1.0＝0.5 + 0.5×1×0.3 = 0.65
            Assert.AreEqual(0.65f, OverconfidenceBiasRules.InflatedSuccessEstimate(0.5f, 1f, P), 1e-4f);
            // 真0.5・過信0.5＝0.5 + 0.5×0.5×0.3 = 0.575
            Assert.AreEqual(0.575f, OverconfidenceBiasRules.InflatedSuccessEstimate(0.5f, 0.5f, P), 1e-4f);
            // 真1.0は天井＝水増ししても1のまま
            Assert.AreEqual(1f, OverconfidenceBiasRules.InflatedSuccessEstimate(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void PlanningFallacyFactor_OptimisticUnderEstimate()
        {
            // 過信ゼロ＝見積もりは正確（倍率1.0）
            Assert.AreEqual(1f, OverconfidenceBiasRules.PlanningFallacyFactor(0f, P), 1e-4f);
            // 過信1.0＝1 − 1×0.4 = 0.6（所要を6割に見積もる＝楽観）
            Assert.AreEqual(0.6f, OverconfidenceBiasRules.PlanningFallacyFactor(1f, P), 1e-4f);
            // 過信0.5＝0.8
            Assert.AreEqual(0.8f, OverconfidenceBiasRules.PlanningFallacyFactor(0.5f, P), 1e-4f);
        }

        [Test]
        public void EstimateGap_SubjectiveMinusReal()
        {
            // 真の所要10を主観6と見積もった＝ギャップ −4（楽観し過ぎ）
            Assert.AreEqual(-4f, OverconfidenceBiasRules.EstimateGap(6f, 10f), 1e-4f);
            // 正確なら0
            Assert.AreEqual(0f, OverconfidenceBiasRules.EstimateGap(10f, 10f), 1e-4f);
        }

        [Test]
        public void WysiatiNeglect_IgnoresUnknownsWhenConfident()
        {
            // 未知の重み0.8・過信1.0＝0.8（見えないものを全面的に無視）
            Assert.AreEqual(0.8f, OverconfidenceBiasRules.WysiatiNeglect(0.8f, 1f), 1e-4f);
            // 過信ゼロなら未知を無視しない
            Assert.AreEqual(0f, OverconfidenceBiasRules.WysiatiNeglect(0.8f, 0f), 1e-4f);
            // 半々＝0.8×0.5
            Assert.AreEqual(0.4f, OverconfidenceBiasRules.WysiatiNeglect(0.8f, 0.5f), 1e-4f);
        }

        [Test]
        public void OutsideViewCorrection_PullsTowardBaseRate()
        {
            // 内部0.9・基準率0.3・重み0.5＝Lerp = 0.6
            Assert.AreEqual(0.6f, OverconfidenceBiasRules.OutsideViewCorrection(0.9f, 0.3f, P), 1e-4f);
            // 内部と基準率が一致＝動かない
            Assert.AreEqual(0.5f, OverconfidenceBiasRules.OutsideViewCorrection(0.5f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void OverreachDecision_GoesWhenInflatedClearsThreshold()
        {
            // 主観0.65が閾値0.6を超える＝踏み込む
            Assert.IsTrue(OverconfidenceBiasRules.OverreachDecision(0.65f, 0.6f));
            // 主観0.55は閾値に届かず＝見送る
            Assert.IsFalse(OverconfidenceBiasRules.OverreachDecision(0.55f, 0.6f));
        }

        [Test]
        public void ConfidenceCalibration_GoodFeedbackLowersOverconfidence()
        {
            // 過信0.8・良いフィードバック1.0＝0.8×(1 − 1×0.5) = 0.4
            Assert.AreEqual(0.4f, OverconfidenceBiasRules.ConfidenceCalibration(0.8f, 1f, P), 1e-4f);
            // 質の悪い反省（0）では過信は維持
            Assert.AreEqual(0.8f, OverconfidenceBiasRules.ConfidenceCalibration(0.8f, 0f, P), 1e-4f);
        }

        [Test]
        public void CostOfOverconfidence_GapTimesStakes()
        {
            // ギャップ −4・賭け金100＝|−4|×100 = 400
            Assert.AreEqual(400f, OverconfidenceBiasRules.CostOfOverconfidence(-4f, 100f), 1e-4f);
            // 正確なら代償ゼロ
            Assert.AreEqual(0f, OverconfidenceBiasRules.CostOfOverconfidence(0f, 100f), 1e-4f);
        }

        [Test]
        public void IsOverconfident_AboveThreshold()
        {
            Assert.IsTrue(OverconfidenceBiasRules.IsOverconfident(0.7f, 0.6f));
            Assert.IsFalse(OverconfidenceBiasRules.IsOverconfident(0.5f, 0.6f));
        }

        // 物語テスト：過信した提督が成功確率を水増しして本来見送るべき作戦に踏み切り、所要を楽観視して
        // 代償を払うが、敗北という質の良いフィードバックで過信が校正され、外部視点を入れて踏みとどまる。
        [Test]
        public void Narrative_OverreachThenCalibrate()
        {
            float trueChance = 0.45f;   // 本当は五分に届かない賭け
            float overconfidence = 1f;  // 慢心の極み
            float goThreshold = 0.6f;

            // 過信が成功確率を水増し＝0.45 + 0.55×1×0.3 = 0.615
            float inflated = OverconfidenceBiasRules.InflatedSuccessEstimate(trueChance, overconfidence, P);
            Assert.AreEqual(0.615f, inflated, 1e-4f);
            // 水増しのせいで閾値を超え、本来見送るべき作戦に踏み込む
            Assert.IsTrue(OverconfidenceBiasRules.OverreachDecision(inflated, goThreshold));

            // 所要を楽観視（真の所要10 を 6割に見積もる）→ ギャップ→ 代償
            float trueTime = 10f;
            float subjTime = trueTime * OverconfidenceBiasRules.PlanningFallacyFactor(overconfidence, P);
            Assert.AreEqual(6f, subjTime, 1e-4f);
            float gap = OverconfidenceBiasRules.EstimateGap(subjTime, trueTime);
            float cost = OverconfidenceBiasRules.CostOfOverconfidence(gap, 50f);
            Assert.Greater(cost, 0f); // 楽観の代償を払う

            // 敗北＝質の良いフィードバックで過信が校正される（0.5へ）
            float calibrated = OverconfidenceBiasRules.ConfidenceCalibration(overconfidence, 1f, P);
            Assert.AreEqual(0.5f, calibrated, 1e-4f);

            // 校正後の主観確率に外部視点（基準率＝真値）を混ぜると閾値を割り、今度は踏みとどまる
            float inflated2 = OverconfidenceBiasRules.InflatedSuccessEstimate(trueChance, calibrated, P);
            float corrected = OverconfidenceBiasRules.OutsideViewCorrection(inflated2, trueChance, P);
            Assert.IsFalse(OverconfidenceBiasRules.OverreachDecision(corrected, goThreshold));
        }
    }
}
