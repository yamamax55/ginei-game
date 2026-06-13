using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 出兵の政治（アムリッツァ型）を固定する：出兵誘因は低支持×選挙近接で立ち（健全政権は0）、
    /// 勝てば規模比例で支持回復・負ければ規模×損耗で崩落、崩壊閾値、博打の期待値。境界を担保。
    /// </summary>
    public class WarPoliticsRulesTests
    {
        private static readonly WarPoliticsParams P = WarPoliticsParams.Default;
        // 誘因閾値0.4/勝利回復0.3/敗北喪失0.5/崩壊閾値0.15

        [Test]
        public void WarIncentive_ZeroForHealthyGovernment()
        {
            Assert.AreEqual(0f, WarPoliticsRules.WarIncentive(0.4f, 1f, P), 1e-5f); // 閾値ちょうど＝0
            Assert.AreEqual(0f, WarPoliticsRules.WarIncentive(0.8f, 1f, P), 1e-5f);
        }

        [Test]
        public void WarIncentive_RisesWithDesperationAndElection()
        {
            // 支持0・選挙遠い＝不足1×0.5=0.5
            Assert.AreEqual(0.5f, WarPoliticsRules.WarIncentive(0f, 0f, P), 1e-5f);
            // 支持0・選挙目前＝不足1×1.0=1.0
            Assert.AreEqual(1f, WarPoliticsRules.WarIncentive(0f, 1f, P), 1e-5f);
            // 支持0.2＝不足0.5×0.5=0.25
            Assert.AreEqual(0.25f, WarPoliticsRules.WarIncentive(0.2f, 0f, P), 1e-5f);
        }

        [Test]
        public void SupportAfterVictory_BounceByScale()
        {
            Assert.AreEqual(0.5f, WarPoliticsRules.SupportAfterVictory(0.2f, 1f, P), 1e-5f);  // +0.3
            Assert.AreEqual(0.35f, WarPoliticsRules.SupportAfterVictory(0.2f, 0.5f, P), 1e-5f);
            Assert.AreEqual(1f, WarPoliticsRules.SupportAfterVictory(0.9f, 1f, P), 1e-5f);    // 上限1
        }

        [Test]
        public void SupportAfterDefeat_DeeperWithScaleAndCasualties()
        {
            // 全規模・全損耗＝−0.5×1×1=−0.5
            Assert.AreEqual(0.1f, WarPoliticsRules.SupportAfterDefeat(0.6f, 1f, 1f, P), 1e-5f);
            // 損耗ゼロでも規模分は失う＝−0.5×1×0.5=−0.25
            Assert.AreEqual(0.35f, WarPoliticsRules.SupportAfterDefeat(0.6f, 1f, 0f, P), 1e-5f);
            // 下限0
            Assert.AreEqual(0f, WarPoliticsRules.SupportAfterDefeat(0.1f, 1f, 1f, P), 1e-5f);
        }

        [Test]
        public void GovernmentFalls_BelowCollapseThreshold()
        {
            Assert.IsTrue(WarPoliticsRules.GovernmentFalls(0.14f, P));
            Assert.IsFalse(WarPoliticsRules.GovernmentFalls(0.15f, P)); // 閾値ちょうど＝持ちこたえる
        }

        [Test]
        public void ExpectedSupportSwing_GambleMath()
        {
            // 支持0.2・全規模・全損耗・勝率0.5：勝てば0.5/負ければ0、期待0.25−0.2=+0.05＝博打が引き合う
            Assert.AreEqual(0.05f, WarPoliticsRules.ExpectedSupportSwing(0.2f, 1f, 1f, 0.5f, P), 1e-5f);
            // 勝率0＝確実に失う
            Assert.Less(WarPoliticsRules.ExpectedSupportSwing(0.2f, 1f, 1f, 0f, P), 0f);
        }

        [Test]
        public void AmritsarStory_DesperateExpeditionEndsGovernment()
        {
            // 支持0.3 の政権が全規模出兵→大損耗で敗北→支持0 ＝政権崩壊（アムリッツァの帰結）
            float after = WarPoliticsRules.SupportAfterDefeat(0.3f, 1f, 1f, P);
            Assert.IsTrue(WarPoliticsRules.GovernmentFalls(after, P));
        }
    }
}
