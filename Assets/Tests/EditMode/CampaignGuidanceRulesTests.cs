using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>キャンペーンの「次の一手」ガイダンス（B＝目的可視化）：優先順位を固定する。</summary>
    public class CampaignGuidanceRulesTests
    {
        [Test]
        public void Engagement_HasTopPriority()
        {
            // 交戦中の回廊があれば、遊休艦隊や敵領の有無に関わらず「前線へ潜行」。
            Assert.AreEqual(CampaignHint.前線へ潜行,
                CampaignGuidanceRules.NextAction(hasEngagement: true, idleFleetCount: 5, rivalSystemsRemain: true));
            Assert.AreEqual(CampaignHint.前線へ潜行,
                CampaignGuidanceRules.NextAction(hasEngagement: true, idleFleetCount: 0, rivalSystemsRemain: false));
        }

        [Test]
        public void IdleFleets_WhenNoEngagement_SuggestsMission()
        {
            Assert.AreEqual(CampaignHint.任務を発令,
                CampaignGuidanceRules.NextAction(hasEngagement: false, idleFleetCount: 1, rivalSystemsRemain: true));
        }

        [Test]
        public void NoEngagementNoIdle_ButRivalsRemain_SuggestsAdvance()
        {
            Assert.AreEqual(CampaignHint.領土を広げよ,
                CampaignGuidanceRules.NextAction(hasEngagement: false, idleFleetCount: 0, rivalSystemsRemain: true));
        }

        [Test]
        public void Nothing_Actionable_FallsBackToWait()
        {
            Assert.AreEqual(CampaignHint.好機を待て,
                CampaignGuidanceRules.NextAction(hasEngagement: false, idleFleetCount: 0, rivalSystemsRemain: false));
        }
    }
}
