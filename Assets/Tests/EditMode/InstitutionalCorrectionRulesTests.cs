using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>制度的な誤りの蓄積と脆性崩壊（POPR-3 #1517）の純ロジック検証。</summary>
    public class InstitutionalCorrectionRulesTests
    {
        /// <summary>修正能力＝批判の自由×0.6＋フィードバック×0.4。批判の自由が主因（開かれた社会ほど高い）。</summary>
        [Test]
        public void CorrectionCapacity_批判の自由とフィードバック回路の加重和()
        {
            // 既定 criticismWeight=0.6, feedbackWeight=0.4。
            Assert.AreEqual(0.8f * 0.6f + 0.5f * 0.4f, InstitutionalCorrectionRules.CorrectionCapacity(0.8f, 0.5f), 1e-4f);
            // 両方最大なら 0.6+0.4=1.0。
            Assert.AreEqual(1f, InstitutionalCorrectionRules.CorrectionCapacity(1f, 1f), 1e-4f);
            // 両方ゼロなら能力ゼロ。
            Assert.AreEqual(0f, InstitutionalCorrectionRules.CorrectionCapacity(0f, 0f), 1e-4f);
        }

        /// <summary>誤りの蓄積＝(新たな誤り−修正能力)×0.1×dt。自己修正できれば誤りは減る（溜まらない）。</summary>
        [Test]
        public void ErrorAccumulationTick_修正能力が新たな誤りを削る()
        {
            // 既定 accumulationRate=0.1。新errors0.8, cap0.3, dt2 → +(0.8-0.3)*0.1*2=+0.1。
            Assert.AreEqual(0.3f + 0.1f, InstitutionalCorrectionRules.ErrorAccumulationTick(0.3f, 0.8f, 0.3f, 2f), 1e-4f);
            // 修正能力が新たな誤りを上回れば誤りは減る＝自己修正できれば溜まらない。
            // 新errors0.2, cap0.7, dt2 → +(0.2-0.7)*0.1*2=-0.1。
            Assert.AreEqual(0.5f - 0.1f, InstitutionalCorrectionRules.ErrorAccumulationTick(0.5f, 0.2f, 0.7f, 2f), 1e-4f);
        }

        /// <summary>脆性＝蓄積した誤り×1.0。修正されない歪みが溜まるほど脆くなる。</summary>
        [Test]
        public void BrittlenessFromErrors_蓄積した誤りが脆性を高める()
        {
            // 既定 brittlenessScale=1.0 ＝誤り蓄積がそのまま脆性。
            Assert.AreEqual(0.7f, InstitutionalCorrectionRules.BrittlenessFromErrors(0.7f), 1e-4f);
            Assert.AreEqual(0f, InstitutionalCorrectionRules.BrittlenessFromErrors(0f), 1e-4f);
        }

        /// <summary>崩壊確率＝誤りが臨界0.6超でのみ立ち上がる。臨界未満は衝撃が来ても崩れない（非線形）。</summary>
        [Test]
        public void CollapseProbability_誤り蓄積が臨界を超えると衝撃で非線形に崩壊()
        {
            // 既定 criticalThreshold=0.6。臨界未満は確率ゼロ。
            Assert.AreEqual(0f, InstitutionalCorrectionRules.CollapseProbability(0.5f, 1f), 1e-4f);
            // 臨界超過：stock0.8 → excess=(0.8-0.6)/(1-0.6)=0.5、shock0.6 → 0.5*0.6=0.3。
            Assert.AreEqual(0.3f, InstitutionalCorrectionRules.CollapseProbability(0.8f, 0.6f), 1e-4f);
            // 誤りが多いほど小さな衝撃でも崩れる：stock1.0 → excess=1.0、shock0.4 → 0.4。
            Assert.AreEqual(0.4f, InstitutionalCorrectionRules.CollapseProbability(1f, 0.4f), 1e-4f);
        }

        /// <summary>誤りの発見＝透明性×批判。隠蔽（どちらか欠ける）と発見されず溜まる。</summary>
        [Test]
        public void ErrorDetection_透明性と批判が誤りを早く発見する()
        {
            Assert.AreEqual(0.8f * 0.7f, InstitutionalCorrectionRules.ErrorDetection(0.8f, 0.7f), 1e-4f);
            // 批判が封じられれば（ゼロ）発見されない＝隠蔽されると溜まる。
            Assert.AreEqual(0f, InstitutionalCorrectionRules.ErrorDetection(1f, 0f), 1e-4f);
        }

        /// <summary>試行錯誤の学習＝修正能力×実験度×0.05×dt ぶん能力が伸びる。試して直すほど直せる力が育つ。</summary>
        [Test]
        public void TrialAndErrorLearning_試行錯誤で修正能力が育つ()
        {
            // 既定 learningRate=0.05。cap0.4, exp0.5, dt2 → +0.05*0.4*0.5*2=+0.02。
            Assert.AreEqual(0.4f + 0.02f, InstitutionalCorrectionRules.TrialAndErrorLearning(0.4f, 0.5f, 2f), 1e-4f);
            // 実験ゼロなら据え置き（積で表す）。
            Assert.AreEqual(0.6f, InstitutionalCorrectionRules.TrialAndErrorLearning(0.6f, 0f, 5f), 1e-4f);
        }

        /// <summary>隠蔽の蓄積＝抑圧×既存誤り×0.08×dt ぶん膨らむ。抑圧は誤りを覆い隠して増幅する。</summary>
        [Test]
        public void ConcealmentBacklog_抑圧が誤りを見えないまま溜める()
        {
            // 既定 concealmentRate=0.08。suppression0.5, stock0.4, dt2 → +0.08*0.5*0.4*2=+0.032。
            Assert.AreEqual(0.4f + 0.032f, InstitutionalCorrectionRules.ConcealmentBacklog(0.5f, 0.4f, 2f), 1e-4f);
            // 抑圧ゼロなら据え置き＝隠蔽は起きない。
            Assert.AreEqual(0.3f, InstitutionalCorrectionRules.ConcealmentBacklog(0f, 0.3f, 5f), 1e-4f);
        }

        /// <summary>崩壊予兆＝誤りが臨界0.6超かつ修正能力を上回る。修正が十分高ければ向かわない。</summary>
        [Test]
        public void IsHeadedForCollapse_誤りが修正能力を上回り崩壊へ向かう()
        {
            // 既定 criticalThreshold=0.6。誤り0.7≥0.6 かつ 0.7>修正能力0.3 → 崩壊へ。
            Assert.IsTrue(InstitutionalCorrectionRules.IsHeadedForCollapse(0.7f, 0.3f));
            // 誤りは多いが修正能力0.8がそれを上回る → 向かわない（自己修正で間に合う）。
            Assert.IsFalse(InstitutionalCorrectionRules.IsHeadedForCollapse(0.7f, 0.8f));
            // 誤りが臨界未満なら向かわない。
            Assert.IsFalse(InstitutionalCorrectionRules.IsHeadedForCollapse(0.5f, 0f));
        }
    }
}
