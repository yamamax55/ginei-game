using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>キャンペーン難易度：勝敗しきい値と開始戦力傾きが難易度で正しく動くことを固定する。</summary>
    public class CampaignDifficultyRulesTests
    {
        [Test]
        public void Normal_UsesDefaultThresholds()
        {
            var p = CampaignDifficultyRules.VictoryParams(CampaignDifficulty.普通);
            Assert.AreEqual(0.7f, p.dominationFraction, 1e-4f);
            Assert.AreEqual(0.7f, p.rivalDominationFraction, 1e-4f);
        }

        [Test]
        public void Easy_PlayerWinsEasier_RivalNeedsMore()
        {
            var p = CampaignDifficultyRules.VictoryParams(CampaignDifficulty.易しい);
            Assert.Less(p.dominationFraction, 0.7f);        // 自軍は少ない支配で勝てる
            Assert.Greater(p.rivalDominationFraction, 0.7f); // 敵は多く握らないと勝てない
        }

        [Test]
        public void Hard_PlayerWinsHarder_RivalNeedsLess()
        {
            var p = CampaignDifficultyRules.VictoryParams(CampaignDifficulty.難しい);
            Assert.Greater(p.dominationFraction, 0.7f);
            Assert.Less(p.rivalDominationFraction, 0.7f);
        }

        [Test]
        public void StrengthFactors_TiltByDifficulty()
        {
            // 易しい：自軍 > 普通(1.0) > 難しい／敵はその逆。
            Assert.Greater(CampaignDifficultyRules.PlayerStrengthFactor(CampaignDifficulty.易しい), 1f);
            Assert.AreEqual(1f, CampaignDifficultyRules.PlayerStrengthFactor(CampaignDifficulty.普通), 1e-4f);
            Assert.Less(CampaignDifficultyRules.PlayerStrengthFactor(CampaignDifficulty.難しい), 1f);

            Assert.Less(CampaignDifficultyRules.EnemyStrengthFactor(CampaignDifficulty.易しい), 1f);
            Assert.AreEqual(1f, CampaignDifficultyRules.EnemyStrengthFactor(CampaignDifficulty.普通), 1e-4f);
            Assert.Greater(CampaignDifficultyRules.EnemyStrengthFactor(CampaignDifficulty.難しい), 1f);
        }
    }
}
