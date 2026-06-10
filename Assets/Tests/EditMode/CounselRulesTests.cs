using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>献策システム（#1104）の純ロジックを既定Paramsの具体値で固定する。</summary>
    public class CounselRulesTests
    {
        // ===== 献策の質 =====

        /// <summary>知略×状況が高いと神算、低いと愚策に段階化する。</summary>
        [Test]
        public void StratagemQuality_知略と状況で段階化()
        {
            Assert.AreEqual(CounselQuality.神算, CounselRules.StratagemQuality(0.9f, 0.9f)); // 0.81
            Assert.AreEqual(CounselQuality.良策, CounselRules.StratagemQuality(0.8f, 0.8f)); // 0.64
            Assert.AreEqual(CounselQuality.凡策, CounselRules.StratagemQuality(0.5f, 0.5f)); // 0.25
            Assert.AreEqual(CounselQuality.愚策, CounselRules.StratagemQuality(0.3f, 0.3f)); // 0.09
            // 同じ知略でも状況が悪いと策が冴えない（神算→愚策へ）。
            Assert.AreEqual(CounselQuality.愚策, CounselRules.StratagemQuality(0.9f, 0.05f));
        }

        // ===== 採否：暗愚な君主は良策を退ける =====

        /// <summary>同じ良策でも暗愚な君主では採択確率が地に落ち、英明な君主では高い（袁紹/田豊型）。</summary>
        [Test]
        public void AdoptionLikelihood_暗君は良策を退ける()
        {
            // 良策(quality=2/3) × 受容力。
            float dumb = CounselRules.AdoptionLikelihood(CounselQuality.良策, 0.1f, 0.5f); // recept=0.26 → 0.1733
            float wise = CounselRules.AdoptionLikelihood(CounselQuality.良策, 0.9f, 0.9f); // recept=0.9  → 0.6
            Assert.AreEqual(0.17333f, dumb, 1e-4f);
            Assert.AreEqual(0.6f, wise, 1e-4f);
            Assert.Less(dumb, wise);
        }

        /// <summary>信頼ある参謀の策は通りやすい（信頼差で採択確率が上がる）。</summary>
        [Test]
        public void AdoptionLikelihood_信頼で通りやすくなる()
        {
            float low = CounselRules.AdoptionLikelihood(CounselQuality.良策, 0.5f, 0.0f);
            float high = CounselRules.AdoptionLikelihood(CounselQuality.良策, 0.5f, 1.0f);
            Assert.Less(low, high);
        }

        /// <summary>採択判定は roll で決定論（確率未満で採択）。</summary>
        [Test]
        public void IsAdopted_rollで決定論()
        {
            // wise の良策＝採択確率0.6。
            Assert.IsTrue(CounselRules.IsAdopted(CounselQuality.良策, 0.9f, 0.9f, 0.5f));
            Assert.IsFalse(CounselRules.IsAdopted(CounselQuality.良策, 0.9f, 0.9f, 0.7f));
        }

        // ===== 帰結 =====

        /// <summary>良策採択は成功率↑(>1)・愚策採択は失敗(<1)・却下は無効(=1)。</summary>
        [Test]
        public void OutcomeModifier_採否で帰結が変わる()
        {
            Assert.AreEqual(1.13333f, CounselRules.OutcomeModifier(CounselQuality.良策, true), 1e-4f);
            Assert.AreEqual(0.6f, CounselRules.OutcomeModifier(CounselQuality.愚策, true), 1e-4f);
            // 退けた策は帰結に効かない。
            Assert.AreEqual(1f, CounselRules.OutcomeModifier(CounselQuality.神算, false), 1e-6f);
        }

        /// <summary>献策無視の代償＝神算を退けるほど後悔が大きく、採択や凡策却下では0。</summary>
        [Test]
        public void MissedOpportunityCost_神算却下が最大の後悔()
        {
            float god = CounselRules.MissedOpportunityCost(CounselQuality.神算, false);   // 1-1/3
            float good = CounselRules.MissedOpportunityCost(CounselQuality.良策, false);  // 2/3-1/3
            Assert.AreEqual(0.66667f, god, 1e-4f);
            Assert.AreEqual(0.33333f, good, 1e-4f);
            Assert.Greater(god, good);
            // 採択すれば後悔なし・凡策却下も惜しくない。
            Assert.AreEqual(0f, CounselRules.MissedOpportunityCost(CounselQuality.神算, true), 1e-6f);
            Assert.AreEqual(0f, CounselRules.MissedOpportunityCost(CounselQuality.凡策, false), 1e-6f);
        }

        // ===== 信用更新 =====

        /// <summary>的中で信用が増し外れで減る＝実績が発言力を作る（0..1クランプ）。</summary>
        [Test]
        public void CounselorCredibilityTick_的中で信頼が増す()
        {
            Assert.AreEqual(0.7f, CounselRules.CounselorCredibilityTick(0.5f, true, 1f), 1e-4f);
            Assert.AreEqual(0.3f, CounselRules.CounselorCredibilityTick(0.5f, false, 1f), 1e-4f);
            // 上下限でクランプ。
            Assert.AreEqual(1f, CounselRules.CounselorCredibilityTick(1f, true, 1f), 1e-6f);
            Assert.AreEqual(0f, CounselRules.CounselorCredibilityTick(0f, false, 1f), 1e-6f);
        }

        // ===== 参謀の不満 =====

        /// <summary>良策を退けられ続けるほど不満が嵩む（陳宮の離反型）が凡策却下は不満を生まない。</summary>
        [Test]
        public void RejectionFrustration_却下の累積で不満が嵩む()
        {
            float once = CounselRules.RejectionFrustration(CounselQuality.良策, 1);  // 0.3333*0.4
            float thrice = CounselRules.RejectionFrustration(CounselQuality.良策, 3);
            Assert.AreEqual(0.13333f, once, 1e-4f);
            Assert.Greater(thrice, once);
            // 凡策の却下・回数0は不満なし。
            Assert.AreEqual(0f, CounselRules.RejectionFrustration(CounselQuality.凡策, 5), 1e-6f);
            Assert.AreEqual(0f, CounselRules.RejectionFrustration(CounselQuality.神算, 0), 1e-6f);
        }
    }
}
