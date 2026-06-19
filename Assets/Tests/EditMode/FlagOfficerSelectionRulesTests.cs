using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>将官選抜＝提督登用の純ロジックのEditModeテスト（既定Params期待値固定）。</summary>
    public class FlagOfficerSelectionRulesTests
    {
        const float Tol = 1e-4f;     // 一般
        const float SqrtTol = 1e-3f; // Sqrt/Pow を含む箇所

        [Test]
        public void CommandAptitude_WeightedAverageOfThreeStats()
        {
            // (0.8*0.4 + 0.6*0.4 + 0.4*0.2)/1.0 = 0.64
            float a = FlagOfficerSelectionRules.CommandAptitude(80f, 60f, 40f);
            Assert.AreEqual(0.64f, a, Tol);
        }

        [Test]
        public void CommandAptitude_ClampsInputsAndStaysInUnitRange()
        {
            // 全部上限超過＝1.0、全部負＝0.0。
            Assert.AreEqual(1f, FlagOfficerSelectionRules.CommandAptitude(200f, 200f, 200f), Tol);
            Assert.AreEqual(0f, FlagOfficerSelectionRules.CommandAptitude(-50f, -50f, -50f), Tol);
        }

        [Test]
        public void MissionFit_SpecialtyMatchBoostsAndNeutralKeeps()
        {
            // 相性0.75＝倍率Lerp(1,1.5,0.5)=1.25 → 0.64*1.25=0.8。
            Assert.AreEqual(0.8f, FlagOfficerSelectionRules.MissionFit(0.64f, 0, 0.75f), Tol);
            // 相性0.5（中庸）＝倍率1.0 → 不変。
            Assert.AreEqual(0.64f, FlagOfficerSelectionRules.MissionFit(0.64f, 0, 0.5f), Tol);
        }

        [Test]
        public void TrustworthinessVetting_GeometricMeanWithFactionDrag()
        {
            // 忠誠100×清廉100、しがらみ無し＝1.0。
            Assert.AreEqual(1f, FlagOfficerSelectionRules.TrustworthinessVetting(100f, 100f, 0f), SqrtTol);
            // 64/64、しがらみ0.5＝sqrt(0.64*0.64)=0.64、drag=1-0.5*0.5=0.75 → 0.48。
            Assert.AreEqual(0.48f, FlagOfficerSelectionRules.TrustworthinessVetting(64f, 64f, 0.5f), SqrtTol);
        }

        [Test]
        public void TrustworthinessVetting_CorruptionCollapsesTrust()
        {
            // 忠誠100でも清廉0なら幾何平均0＝信頼ゼロ。
            float t = FlagOfficerSelectionRules.TrustworthinessVetting(100f, 0f, 0f);
            Assert.AreEqual(0f, t, SqrtTol);
        }

        [Test]
        public void SelectionScore_ProductRequiresBothAxes()
        {
            // 0.8 * 0.48 = 0.384。
            Assert.AreEqual(0.384f, FlagOfficerSelectionRules.SelectionScore(0.8f, 0.48f), Tol);
            // 片方ゼロなら積はゼロ（能力だけでは登用に値しない）。
            Assert.AreEqual(0f, FlagOfficerSelectionRules.SelectionScore(0.9f, 0f), Tol);
        }

        [Test]
        public void BoldPickRisk_UncertaintyTimesStakesCappedByMax()
        {
            // 0.5*0.5*0.4 = 0.1。
            Assert.AreEqual(0.1f, FlagOfficerSelectionRules.BoldPickRisk(0.5f, 0.5f), Tol);
            // 確かな将（不確かさ0）はリスクゼロ。
            Assert.AreEqual(0f, FlagOfficerSelectionRules.BoldPickRisk(0f, 1f), Tol);
            // 最大でも maxBoldRisk=0.4。
            Assert.AreEqual(0.4f, FlagOfficerSelectionRules.BoldPickRisk(1f, 1f), Tol);
        }

        [Test]
        public void CronyismPenalty_NoGapOrNoTiesMeansNoHarm()
        {
            // コネ0.8×実力差0.5×0.5 = 0.2。
            Assert.AreEqual(0.2f, FlagOfficerSelectionRules.CronyismPenalty(0.8f, 0.5f), Tol);
            // 実力差なし＝適材なので害ゼロ。
            Assert.AreEqual(0f, FlagOfficerSelectionRules.CronyismPenalty(1f, 0f), Tol);
            // コネなし＝実力で選んだので害ゼロ。
            Assert.AreEqual(0f, FlagOfficerSelectionRules.CronyismPenalty(0f, 1f), Tol);
        }

        [Test]
        public void NetSelectionValue_SubtractsRiskAndCronyismAndIsSuitable()
        {
            // 0.384 - 0.1 - 0.2 = 0.084。
            float net = FlagOfficerSelectionRules.NetSelectionValue(0.384f, 0.1f, 0.2f);
            Assert.AreEqual(0.084f, net, Tol);
            // 既定閾値0.1未満＝登用に適さない。
            Assert.IsFalse(FlagOfficerSelectionRules.IsSuitableAppointment(net));
            // 高い正味なら適任。
            Assert.IsTrue(FlagOfficerSelectionRules.IsSuitableAppointment(0.5f));
        }

        [Test]
        public void NetSelectionValue_ClampsToSignedUnitRange()
        {
            // スコア0・最大リスク・最大コネ害＝負側にクランプ。
            float net = FlagOfficerSelectionRules.NetSelectionValue(0f, 1f, 1f);
            Assert.AreEqual(-1f, net, Tol);
        }

        [Test]
        public void Narrative_GiftedButFactionBoundUpstartIsPassedOver()
        {
            // 物語：戦術の冴えた若手だが派閥に縛られ、実績薄く局面は重い。
            // 適性は高いが、信頼が派閥で割れ、抜擢リスクとコネ害が値打ちを食い潰し、登用は見送られる。
            var p = FlagOfficerSelectionParams.Default;

            float aptitude = FlagOfficerSelectionRules.CommandAptitude(90f, 70f, 30f, p);
            // (0.9*0.4 + 0.7*0.4 + 0.3*0.2)/1.0 = 0.36+0.28+0.06 = 0.70
            Assert.AreEqual(0.70f, aptitude, Tol);

            float fit = FlagOfficerSelectionRules.MissionFit(aptitude, 0, 0.6f, p);
            // 倍率 Lerp(1,1.2,0.5)=1.1 → 0.70*1.1 = 0.77
            Assert.AreEqual(0.77f, fit, Tol);

            float trust = FlagOfficerSelectionRules.TrustworthinessVetting(80f, 70f, 0.8f, p);
            // sqrt(0.8*0.7)=sqrt(0.56)≈0.748331、drag=1-0.8*0.5=0.6 → ≈0.448999
            Assert.AreEqual(Mathf.Sqrt(0.56f) * 0.6f, trust, SqrtTol);

            float score = FlagOfficerSelectionRules.SelectionScore(fit, trust, p);
            float risk = FlagOfficerSelectionRules.BoldPickRisk(0.7f, 0.9f, p);   // 0.7*0.9*0.4=0.252
            float crony = FlagOfficerSelectionRules.CronyismPenalty(0.8f, 0.4f, p); // 0.8*0.4*0.5=0.16
            Assert.AreEqual(0.252f, risk, Tol);
            Assert.AreEqual(0.16f, crony, Tol);

            float net = FlagOfficerSelectionRules.NetSelectionValue(score, risk, crony, p);
            // score≈0.77*0.448999≈0.345729 → net≈0.345729-0.252-0.16≈-0.066271
            Assert.Less(net, 0f);
            Assert.IsFalse(FlagOfficerSelectionRules.IsSuitableAppointment(net, p.suitableThreshold));
        }
    }
}
