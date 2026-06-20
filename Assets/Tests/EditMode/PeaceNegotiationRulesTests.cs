using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>PeaceNegotiationRules（講和交渉の純ロジック）の EditMode テスト。既定 Params の期待値を手計算で固定。</summary>
    public class PeaceNegotiationRulesTests
    {
        const float Tol = 1e-4f;     // 一般
        const float TolPow = 1e-3f;  // Sqrt/Pow を含む箇所

        // NegotiationIncentive = ww*0.5 + st*0.3 + es*0.2
        [Test]
        public void NegotiationIncentive_WeightedSum_Default()
        {
            // 0.8*0.5 + 0.6*0.3 + 0.5*0.2 = 0.4+0.18+0.1 = 0.68
            Assert.AreEqual(0.68f, PeaceNegotiationRules.NegotiationIncentive(0.8f, 0.6f, 0.5f), Tol);
        }

        [Test]
        public void NegotiationIncentive_ClampsInputs()
        {
            // 全要素最大 → 0.5+0.3+0.2 = 1.0 で頭打ち
            Assert.AreEqual(1f, PeaceNegotiationRules.NegotiationIncentive(2f, 2f, 2f), Tol);
            // 負入力は 0 へ
            Assert.AreEqual(0f, PeaceNegotiationRules.NegotiationIncentive(-1f, -1f, -1f), Tol);
        }

        // DemandStrength = bp*(1 + lv*0.5)
        [Test]
        public void DemandStrength_LeverageAmplifies_Default()
        {
            // 0.7*(1+0.6*0.5) = 0.7*1.3 = 0.91
            Assert.AreEqual(0.91f, PeaceNegotiationRules.DemandStrength(0.7f, 0.6f), Tol);
            // てこゼロなら戦況優位そのまま
            Assert.AreEqual(0.7f, PeaceNegotiationRules.DemandStrength(0.7f, 0f), Tol);
        }

        // ConcessionWillingness = ww*0.5 + dp*0.4
        [Test]
        public void ConcessionWillingness_WearinessAndPressure_Default()
        {
            // 0.6*0.5 + 0.5*0.4 = 0.3+0.2 = 0.5
            Assert.AreEqual(0.5f, PeaceNegotiationRules.ConcessionWillingness(0.6f, 0.5f), Tol);
        }

        // FaceSavingNeed = sqrt(pr*dp)*0.6
        [Test]
        public void FaceSavingNeed_GeometricMean_Default()
        {
            // sqrt(0.81*0.64)*0.6 = sqrt(0.5184)*0.6 = 0.72*0.6 = 0.432
            Assert.AreEqual(0.432f, PeaceNegotiationRules.FaceSavingNeed(0.81f, 0.64f), TolPow);
            // 片方ゼロなら面子は崩れる
            Assert.AreEqual(0f, PeaceNegotiationRules.FaceSavingNeed(1f, 0f), TolPow);
        }

        // ZoneOfAgreement = max(0, cb - da)
        [Test]
        public void ZoneOfAgreement_OverlapOnly_Default()
        {
            // 0.7-0.4 = 0.3
            Assert.AreEqual(0.3f, PeaceNegotiationRules.ZoneOfAgreement(0.4f, 0.7f), Tol);
            // 譲歩が要求に届かなければ域は閉じる
            Assert.AreEqual(0f, PeaceNegotiationRules.ZoneOfAgreement(0.8f, 0.3f), Tol);
        }

        // DealAttractiveness = z*(1 - f)
        [Test]
        public void DealAttractiveness_FaceConstrains_Default()
        {
            // 0.5*(1-0.4) = 0.3
            Assert.AreEqual(0.3f, PeaceNegotiationRules.DealAttractiveness(0.5f, 0.4f), Tol);
            // 面子最大なら魅力ゼロ
            Assert.AreEqual(0f, PeaceNegotiationRules.DealAttractiveness(0.9f, 1f), Tol);
        }

        // BreakdownRisk = dg*(1 + md*0.5)
        [Test]
        public void BreakdownRisk_DistrustAmplifies_Default()
        {
            // 0.5*(1+0.6*0.5) = 0.5*1.3 = 0.65
            Assert.AreEqual(0.65f, PeaceNegotiationRules.BreakdownRisk(0.5f, 0.6f), Tol);
        }

        [Test]
        public void IsPeaceReachable_ThresholdAndDefault()
        {
            // 0.6*(1-0.4)=0.36 >= 0.3 → true
            Assert.IsTrue(PeaceNegotiationRules.IsPeaceReachable(0.6f, 0.4f, 0.3f));
            // 0.6*(1-0.6)=0.24 < 0.3 → false
            Assert.IsFalse(PeaceNegotiationRules.IsPeaceReachable(0.6f, 0.6f, 0.3f));
            // 既定閾値委譲版（DefaultReachThreshold=0.3）
            Assert.IsTrue(PeaceNegotiationRules.IsPeaceReachable(0.6f, 0.4f));
            Assert.AreEqual(0.3f, PeaceNegotiationRules.DefaultReachThreshold, Tol);
        }

        // ── 物語テスト：拮抗が泥沼化し、双方疲弊で講和卓が開く ──
        [Test]
        public void Narrative_StalematedAndExhausted_PeaceBecomesReachable()
        {
            // 序盤：帝国が優位・疲弊薄く・国内も強硬。動機は乏しく要求は強い＝講和に届かない。
            float earlyIncentive = PeaceNegotiationRules.NegotiationIncentive(0.2f, 0.1f, 0.1f);
            float strongDemand = PeaceNegotiationRules.DemandStrength(0.9f, 0.8f); // 0.9*1.4=1.0頭打ち
            float lowConcession = PeaceNegotiationRules.ConcessionWillingness(0.15f, 0.1f);
            float earlyZopa = PeaceNegotiationRules.ZoneOfAgreement(strongDemand, lowConcession);
            float earlyFace = PeaceNegotiationRules.FaceSavingNeed(0.8f, 0.8f);
            float earlyDeal = PeaceNegotiationRules.DealAttractiveness(earlyZopa, earlyFace);
            float earlyRisk = PeaceNegotiationRules.BreakdownRisk(0.8f, 0.7f);
            Assert.IsFalse(PeaceNegotiationRules.IsPeaceReachable(earlyDeal, earlyRisk),
                "序盤の強硬・低譲歩では講和は到達不能のはず");

            // 終盤：長期戦で膠着し双方疲弊・国内は和平を望む。動機高く要求は控えめ・譲歩は厚い。
            float lateIncentive = PeaceNegotiationRules.NegotiationIncentive(0.85f, 0.8f, 0.7f);
            float moderateDemand = PeaceNegotiationRules.DemandStrength(0.2f, 0.2f);
            float highConcession = PeaceNegotiationRules.ConcessionWillingness(0.9f, 0.95f);
            float lateZopa = PeaceNegotiationRules.ZoneOfAgreement(moderateDemand, highConcession);
            float lateFace = PeaceNegotiationRules.FaceSavingNeed(0.2f, 0.2f);
            float lateDeal = PeaceNegotiationRules.DealAttractiveness(lateZopa, lateFace);
            float lateRisk = PeaceNegotiationRules.BreakdownRisk(0.15f, 0.2f);

            Assert.Greater(lateIncentive, earlyIncentive, "疲弊と膠着で交渉動機は高まるはず");
            Assert.Greater(lateZopa, earlyZopa, "譲歩が厚くなり合意可能域が開くはず");
            Assert.IsTrue(PeaceNegotiationRules.IsPeaceReachable(lateDeal, lateRisk),
                "双方疲弊・低面子・低リスクなら講和に到達できるはず");
        }
    }
}
