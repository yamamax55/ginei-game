using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦犯裁判（AtrocityRules の後段＝裁きの政治）を固定する：勝者の裁きの正統性割引、
    /// 区切り（公正な裁きだけが過去を閉じる）、苛烈量刑の殉教リスク、不処罰の不満、
    /// 真実和解型の選択肢。境界を担保。
    /// </summary>
    public class TribunalRulesTests
    {
        private static readonly TribunalParams P = TribunalParams.Default;
        // 勝者裁き割引0.5/区切り0.5/殉教0.6/不処罰0.4/和解0.5

        [Test]
        public void PerceivedFairness_VictorOnlyJusticeIsDiscounted()
        {
            // 双方の罪を裁く公正な法廷＝手続きどおりの正統性
            Assert.AreEqual(1f, TribunalRules.PerceivedFairness(1f, false, P), 1e-5f);
            // 勝者の罪は裁かない法廷＝同じ手続きでも半分＝復讐の劇場
            Assert.AreEqual(0.5f, TribunalRules.PerceivedFairness(1f, true, P), 1e-5f);
            Assert.AreEqual(0.4f, TribunalRules.PerceivedFairness(0.8f, true, P), 1e-5f);
            // 入力クランプ（範囲外は0..1へ）
            Assert.AreEqual(1f, TribunalRules.PerceivedFairness(2f, false, P), 1e-5f);
            Assert.AreEqual(0f, TribunalRules.PerceivedFairness(-1f, false, P), 1e-5f);
        }

        [Test]
        public void ClosureEffect_OnlyFairJusticeClosesThePast()
        {
            // 公正な裁き×全員有罪＝最大の区切り0.5
            Assert.AreEqual(0.5f, TribunalRules.ClosureEffect(1f, 1f, P), 1e-5f);
            // 不公正な法廷は全員を有罪にしても過去を閉じられない（fairness=0でゼロ）
            Assert.AreEqual(0f, TribunalRules.ClosureEffect(0f, 1f, P), 1e-5f);
            // 裁かなければ区切りなし
            Assert.AreEqual(0f, TribunalRules.ClosureEffect(1f, 0f, P), 1e-5f);
        }

        [Test]
        public void ClosureEffect_VictorJusticeHalvesClosureEvenAtFullConviction()
        {
            // 勝者の裁き（手続き完璧でも fairness=0.5）＝全員有罪でも区切りは半分0.25
            float fairness = TribunalRules.PerceivedFairness(1f, true, P);
            Assert.AreEqual(0.25f, TribunalRules.ClosureEffect(fairness, 1f, P), 1e-5f);
        }

        [Test]
        public void MartyrdomRisk_ExecutingPopularDefendantBackfires()
        {
            // 人望ある被告への極刑＝最大殉教リスク0.6
            Assert.AreEqual(0.6f, TribunalRules.MartyrdomRisk(1f, 1f, P), 1e-5f);
            // 無名の被告なら吊るしてもリスクなし
            Assert.AreEqual(0f, TribunalRules.MartyrdomRisk(1f, 0f, P), 1e-5f);
            // 寛大な量刑なら人望があっても殉教者は立たない
            Assert.AreEqual(0f, TribunalRules.MartyrdomRisk(0f, 1f, P), 1e-5f);
            Assert.AreEqual(0.24f, TribunalRules.MartyrdomRisk(0.5f, 0.8f, P), 1e-5f);
        }

        [Test]
        public void ImpunityGrievance_LeniencyForGreatCrimesWoundsTwice()
        {
            // 大罪を全員無罪放免＝最大不満0.4（被害者を二度殺す）
            Assert.AreEqual(0.4f, TribunalRules.ImpunityGrievance(0f, 1f, P), 1e-5f);
            // 全員有罪なら不処罰の不満なし
            Assert.AreEqual(0f, TribunalRules.ImpunityGrievance(1f, 1f, P), 1e-5f);
            // 罪が小さければ寛大も呑める
            Assert.AreEqual(0f, TribunalRules.ImpunityGrievance(0f, 0f, P), 1e-5f);
            Assert.AreEqual(0.2f, TribunalRules.ImpunityGrievance(0.5f, 1f, P), 1e-5f);
        }

        [Test]
        public void ReconciliationPath_TruthOverPunishment()
        {
            // 公正な場×全面的な真相究明＝最大の和解0.5（南ア型）
            Assert.AreEqual(0.5f, TribunalRules.ReconciliationPath(1f, 1f, P), 1e-5f);
            // 真実が語られなければ和解は開かない
            Assert.AreEqual(0f, TribunalRules.ReconciliationPath(1f, 0f, P), 1e-5f);
            // 不公正な場での「真実」は告白ショー＝和解ゼロ
            Assert.AreEqual(0f, TribunalRules.ReconciliationPath(0f, 1f, P), 1e-5f);
            // 勝者の裁きの場では真相究明の効果も半減
            float fairness = TribunalRules.PerceivedFairness(1f, true, P);
            Assert.AreEqual(0.25f, TribunalRules.ReconciliationPath(fairness, 1f, P), 1e-5f);
        }

        [Test]
        public void DefaultOverloads_MatchExplicitParams()
        {
            // 既定Params省略オーバーロードの一致
            Assert.AreEqual(TribunalRules.PerceivedFairness(0.7f, true, P), TribunalRules.PerceivedFairness(0.7f, true), 1e-6f);
            Assert.AreEqual(TribunalRules.ClosureEffect(0.7f, 0.6f, P), TribunalRules.ClosureEffect(0.7f, 0.6f), 1e-6f);
            Assert.AreEqual(TribunalRules.MartyrdomRisk(0.7f, 0.6f, P), TribunalRules.MartyrdomRisk(0.7f, 0.6f), 1e-6f);
            Assert.AreEqual(TribunalRules.ImpunityGrievance(0.7f, 0.6f, P), TribunalRules.ImpunityGrievance(0.7f, 0.6f), 1e-6f);
            Assert.AreEqual(TribunalRules.ReconciliationPath(0.7f, 0.6f, P), TribunalRules.ReconciliationPath(0.7f, 0.6f), 1e-6f);
        }
    }
}
