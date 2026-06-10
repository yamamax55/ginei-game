using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 宥和政策を固定する：満足化＝譲歩×(1−拡張主義度)、食欲成長＝譲歩×拡張主義度
    /// （同じ譲歩が相手次第で逆に効く対比が核）、要求学習＝履歴×拡張主義度×0.5×dt、
    /// 評判コスト＝譲歩×同盟国0.1/国、読み違いの代償＝譲歩×過小評価分
    /// （正しく読めば代償ゼロ＝ミュンヘンの教訓）。クランプを担保。
    /// </summary>
    public class AppeasementRulesTests
    {
        private static readonly AppeasementParams P = AppeasementParams.Default;
        // 満足係数1.0/食欲係数1.0/学習速度0.5/同盟国毀損0.1/読み違い係数1.0

        [Test]
        public void SatiationEffect_OnlyStatusQuoIsSatisfied()
        {
            Assert.AreEqual(0.5f, AppeasementRules.SatiationEffect(0.5f, 0f, P), 1e-5f);   // 純粋な現状維持国＝譲歩がそのまま満足に
            Assert.AreEqual(0.4f, AppeasementRules.SatiationEffect(0.5f, 0.2f, P), 1e-5f);
            Assert.AreEqual(0f, AppeasementRules.SatiationEffect(0.5f, 1f, P), 1e-5f);     // 純粋な拡張主義者は満足しない
            Assert.AreEqual(0f, AppeasementRules.SatiationEffect(0f, 0f, P), 1e-5f);       // 譲らなければ何も起きない
            Assert.AreEqual(1f, AppeasementRules.SatiationEffect(5f, -1f, P), 1e-5f);      // 入力クランプ
        }

        [Test]
        public void AppetiteGrowth_OnlyRevisionistGrowsHungrier()
        {
            Assert.AreEqual(0.5f, AppeasementRules.AppetiteGrowth(0.5f, 1f, P), 1e-5f);    // 拡張主義者＝譲歩が食欲の頭金に
            Assert.AreEqual(0.1f, AppeasementRules.AppetiteGrowth(0.5f, 0.2f, P), 1e-5f);
            Assert.AreEqual(0f, AppeasementRules.AppetiteGrowth(0.5f, 0f, P), 1e-5f);      // 現状維持国の食欲は育たない
            Assert.AreEqual(1f, AppeasementRules.AppetiteGrowth(3f, 2f, P), 1e-5f);        // 入力クランプ
        }

        [Test]
        public void SameConcession_OppositeEffectByCharacter()
        {
            // 核となる対比：同じ譲歩0.6が、相手の性格で満足にも食欲にも化ける
            const float concession = 0.6f;

            // 現状維持国（revisionism=0.1）＝満足が支配し食欲はほぼ育たない＝平和を買えた
            Assert.AreEqual(0.54f, AppeasementRules.SatiationEffect(concession, 0.1f, P), 1e-5f);
            Assert.AreEqual(0.06f, AppeasementRules.AppetiteGrowth(concession, 0.1f, P), 1e-5f);

            // 拡張主義国（revisionism=0.9）＝同じ譲歩で満足はわずか、食欲が支配＝侵略を育てた
            Assert.AreEqual(0.06f, AppeasementRules.SatiationEffect(concession, 0.9f, P), 1e-5f);
            Assert.AreEqual(0.54f, AppeasementRules.AppetiteGrowth(concession, 0.9f, P), 1e-5f);
        }

        [Test]
        public void DemandTick_RevisionistLearnsToDemandMore()
        {
            Assert.AreEqual(0.55f, AppeasementRules.DemandTick(0.3f, 0.5f, 1f, 1f, P), 1e-5f); // 0.3+0.5×1×0.5×1
            Assert.AreEqual(0.3f, AppeasementRules.DemandTick(0.3f, 0.5f, 0f, 1f, P), 1e-5f);  // 現状維持国は譲られても要求を上げない
            Assert.AreEqual(0.3f, AppeasementRules.DemandTick(0.3f, 0f, 1f, 1f, P), 1e-5f);    // 譲歩の履歴が無ければ学習しない
            Assert.AreEqual(0.425f, AppeasementRules.DemandTick(0.3f, 0.5f, 1f, 0.5f, P), 1e-5f); // dt比例
            Assert.AreEqual(1f, AppeasementRules.DemandTick(0.9f, 1f, 1f, 1f, P), 1e-5f);      // 上限1で頭打ち
        }

        [Test]
        public void ReputationCost_AlliesWatchingErodeGuarantee()
        {
            Assert.AreEqual(0f, AppeasementRules.ReputationCost(1f, 0, P), 1e-5f);     // 誰も見ていなければ無料
            Assert.AreEqual(0.3f, AppeasementRules.ReputationCost(1f, 3, P), 1e-5f);   // 3国×0.1
            Assert.AreEqual(0.15f, AppeasementRules.ReputationCost(0.5f, 3, P), 1e-5f);
            Assert.AreEqual(1f, AppeasementRules.ReputationCost(1f, 20, P), 1e-5f);    // 露出は1で頭打ち
            Assert.AreEqual(0f, AppeasementRules.ReputationCost(1f, -5, P), 1e-5f);    // 負の同盟国数クランプ
        }

        [Test]
        public void MisjudgmentCost_SinIsMisreadingNotAppeasing()
        {
            // ミュンヘンの教訓＝宥和そのものでなく見誤りが罪：
            // 正しく読んだ譲歩は代償ゼロ（拡張主義者相手と知って譲るのは取引）
            Assert.AreEqual(0f, AppeasementRules.MisjudgmentCost(1f, 0.9f, 0.9f, P), 1e-5f);
            // 過大評価（警戒しすぎ）も破滅ではない
            Assert.AreEqual(0f, AppeasementRules.MisjudgmentCost(1f, 0.2f, 0.9f, P), 1e-5f);
            // 最悪＝現状維持と信じて本物の拡張主義者に全面譲歩
            Assert.AreEqual(1f, AppeasementRules.MisjudgmentCost(1f, 1f, 0f, P), 1e-5f);
            Assert.AreEqual(0.4f, AppeasementRules.MisjudgmentCost(0.5f, 0.9f, 0.1f, P), 1e-5f);
            // 譲らなければ読み違えても（この式の上では）代償は表面化しない
            Assert.AreEqual(0f, AppeasementRules.MisjudgmentCost(0f, 1f, 0f, P), 1e-5f);
        }

        [Test]
        public void Params_CtorClampsNegatives()
        {
            var p = new AppeasementParams(-1f, -1f, -1f, -1f, -1f);
            Assert.AreEqual(0f, p.satiationScale, 1e-5f);
            Assert.AreEqual(0f, p.appetiteScale, 1e-5f);
            Assert.AreEqual(0f, p.demandLearnRate, 1e-5f);
            Assert.AreEqual(0f, p.reputationCostPerAlly, 1e-5f);
            Assert.AreEqual(0f, p.misjudgmentScale, 1e-5f);
            Assert.AreEqual(0f, AppeasementRules.SatiationEffect(1f, 0f, p), 1e-5f); // 係数0＝何も生まない
        }
    }
}
