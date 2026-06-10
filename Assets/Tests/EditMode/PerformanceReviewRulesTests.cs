using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>人事評価9-box（実績×潜在）の純ロジック検証（#995）。既定Paramsで期待値固定。</summary>
    public class PerformanceReviewRulesTests
    {
        private static readonly PerformanceReviewParams P = PerformanceReviewParams.Default; // 低0.33/高0.66

        /// <summary>高実績×高潜在＝スター人材／高実績×低潜在＝高位プロ（専門職留任）。</summary>
        [Test]
        public void Box_高実績の区分が潜在で分かれる()
        {
            Assert.AreEqual(TalentBox.スター人材, PerformanceReviewRules.Box(0.9f, 0.9f, P));
            Assert.AreEqual(TalentBox.熟練者, PerformanceReviewRules.Box(0.9f, 0.5f, P));
            Assert.AreEqual(TalentBox.高位プロ, PerformanceReviewRules.Box(0.9f, 0.1f, P));
        }

        /// <summary>低実績×高潜在＝有望株（育成投資の最優先）。</summary>
        [Test]
        public void Box_低実績高潜在は有望株()
        {
            Assert.AreEqual(TalentBox.有望株, PerformanceReviewRules.Box(0.1f, 0.9f, P));
            Assert.AreEqual(TalentBox.要改善, PerformanceReviewRules.Box(0.1f, 0.5f, P));
            Assert.AreEqual(TalentBox.問題児, PerformanceReviewRules.Box(0.1f, 0.1f, P));
        }

        /// <summary>中実績の3区分と軸の境界（高=0.66ちょうどは高バンド）。</summary>
        [Test]
        public void Box_中実績と境界値()
        {
            Assert.AreEqual(TalentBox.中核人材, PerformanceReviewRules.Box(0.5f, 0.5f, P));
            Assert.AreEqual(TalentBox.安定貢献者, PerformanceReviewRules.Box(0.5f, 0.9f, P));
            Assert.AreEqual(TalentBox.平凡, PerformanceReviewRules.Box(0.5f, 0.1f, P));
            // 境界＝0.66ちょうどは高、0.33ちょうどは中、0.32は低。
            Assert.AreEqual(TalentBox.スター人材, PerformanceReviewRules.Box(0.66f, 0.66f, P));
            Assert.AreEqual(TalentBox.中核人材, PerformanceReviewRules.Box(0.33f, 0.33f, P));
            Assert.AreEqual(TalentBox.問題児, PerformanceReviewRules.Box(0.32f, 0.32f, P));
        }

        /// <summary>スター人材（高実績×高潜在）の昇進適性が最も高い＝専門職留任型を上回る。</summary>
        [Test]
        public void PromotionReadiness_スター人材が最優先()
        {
            float star = PerformanceReviewRules.PromotionReadiness(0.9f, 0.9f);
            float specialist = PerformanceReviewRules.PromotionReadiness(0.9f, 0.1f); // 高位プロ
            float lowPerf = PerformanceReviewRules.PromotionReadiness(0.1f, 0.9f);    // 有望株
            // star = 0.6*0.9+0.4*0.9 - 0.3*0.9*0.1 = 0.9 - 0.027 = 0.873
            Assert.That(star, Is.EqualTo(0.873f).Within(1e-4f));
            Assert.Greater(star, specialist);
            Assert.Greater(star, lowPerf);
            // specialist = 0.6*0.9+0.4*0.1 - 0.3*0.9*0.9 = 0.58 - 0.243 = 0.337
            Assert.That(specialist, Is.EqualTo(0.337f).Within(1e-4f));
        }

        /// <summary>有望株（低実績×高潜在）の育成優先度が最大＝高実績×低潜在は最小。</summary>
        [Test]
        public void DevelopmentPriority_有望株が最大()
        {
            float promising = PerformanceReviewRules.DevelopmentPriority(0.0f, 1.0f); // 1.0*(0.5+0.5*1)=1.0
            float specialist = PerformanceReviewRules.DevelopmentPriority(1.0f, 0.0f); // 0
            Assert.That(promising, Is.EqualTo(1.0f).Within(1e-4f));
            Assert.That(specialist, Is.EqualTo(0.0f).Within(1e-4f));
            Assert.Greater(promising, specialist);
        }

        /// <summary>離職リスク＝スター人材ほど市場需要に引かれる（同需要で他区分を上回る）。</summary>
        [Test]
        public void RetentionRisk_スター人材が引かれやすい()
        {
            float star = PerformanceReviewRules.RetentionRisk(TalentBox.スター人材, 1.0f, P);
            float ordinary = PerformanceReviewRules.RetentionRisk(TalentBox.平凡, 1.0f, P);
            // star = 0.6*1.0*1.0 = 0.6
            Assert.That(star, Is.EqualTo(0.6f).Within(1e-4f));
            Assert.Greater(star, ordinary);
            // 需要ゼロなら引かれない。
            Assert.That(PerformanceReviewRules.RetentionRisk(TalentBox.スター人材, 0f, P), Is.EqualTo(0f).Within(1e-4f));
        }

        /// <summary>後継者準備度＝潜在×経験。経験ゼロは潜在の半分・潜在ゼロは0。</summary>
        [Test]
        public void SuccessionReadiness_潜在と経験の合成()
        {
            // 1.0*(0.5+0.5*1)=1.0
            Assert.That(PerformanceReviewRules.SuccessionReadiness(1.0f, 1.0f), Is.EqualTo(1.0f).Within(1e-4f));
            // 経験ゼロ＝潜在の半分。
            Assert.That(PerformanceReviewRules.SuccessionReadiness(1.0f, 0.0f), Is.EqualTo(0.5f).Within(1e-4f));
            // 潜在ゼロ＝0。
            Assert.That(PerformanceReviewRules.SuccessionReadiness(0.0f, 1.0f), Is.EqualTo(0.0f).Within(1e-4f));
        }

        /// <summary>甘辛補正＝甘い評価者の点は引き下げ・辛い評価者は引き上げ・公平(0.5)は不変。</summary>
        [Test]
        public void CalibrationAdjustment_評価者の偏りを補正()
        {
            // 公平＝不変。
            Assert.That(PerformanceReviewRules.CalibrationAdjustment(0.7f, 0.5f, P), Is.EqualTo(0.7f).Within(1e-4f));
            // 甘い評価者(1.0)＝bias+0.5＝0.5*0.5=0.25 引き下げ → 0.7-0.25=0.45
            Assert.That(PerformanceReviewRules.CalibrationAdjustment(0.7f, 1.0f, P), Is.EqualTo(0.45f).Within(1e-4f));
            // 辛い評価者(0.0)＝bias-0.5＝0.25 引き上げ → 0.7+0.25=0.95
            Assert.That(PerformanceReviewRules.CalibrationAdjustment(0.7f, 0.0f, P), Is.EqualTo(0.95f).Within(1e-4f));
        }
    }
}
