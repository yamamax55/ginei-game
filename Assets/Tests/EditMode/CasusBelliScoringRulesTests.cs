using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>CasusBelliScoringRules（開戦理由の文脈スコアリング DIPLO #2119／#192 拡張）の純ロジック検証。</summary>
    public class CasusBelliScoringRulesTests
    {
        [Test]
        public void Liberation_HigherWithUnstableTarget()
        {
            float stable = CasusBelliScoringRules.Score(CasusBelli.解放, 0f, 0f, 0f, 1f, 0f);
            float unstable = CasusBelliScoringRules.Score(CasusBelli.解放, 0f, 0f, 1f, 1f, 0f);
            Assert.Greater(unstable, stable);
        }

        [Test]
        public void Conquest_HigherWithExpansionism()
        {
            float low = CasusBelliScoringRules.Score(CasusBelli.征服, 0f, 0f, 0f, 1f, 0f);
            float high = CasusBelliScoringRules.Score(CasusBelli.征服, 0f, 1f, 0f, 1f, 0f);
            Assert.Greater(high, low);
        }

        [Test]
        public void Punish_HigherWithRecentBetrayal()
        {
            float none = CasusBelliScoringRules.Score(CasusBelli.懲罰, 0f, 0f, 0f, 1f, 0f);
            float betrayed = CasusBelliScoringRules.Score(CasusBelli.懲罰, 0f, 0f, 0f, 1f, 1f);
            Assert.Greater(betrayed, none);
        }

        [Test]
        public void Vassalize_HigherWithPowerAdvantage()
        {
            float even = CasusBelliScoringRules.Score(CasusBelli.従属, 0f, 0f, 0f, 1f, 0f);
            float dominant = CasusBelliScoringRules.Score(CasusBelli.従属, 0f, 0f, 0f, 3f, 0f);
            Assert.Greater(dominant, even);
        }

        [Test]
        public void Score_ClampedToUnit()
        {
            float s = CasusBelliScoringRules.Score(CasusBelli.解放, -100f, 1f, 1f, 5f, 1f);
            Assert.LessOrEqual(s, 1f);
            Assert.GreaterOrEqual(s, 0f);
        }

        [Test]
        public void Best_PicksLiberation_WhenTargetVeryUnstable()
        {
            CasusBelli best = CasusBelliScoringRules.Best(0f, 0f, 1f, 1f, 0f);
            Assert.AreEqual(CasusBelli.解放, best);
        }

        [Test]
        public void Best_ScoreIsMaximalAmongAllCausus()
        {
            // Best が返す大義のスコアは全大義の中で最大であること（選択の整合性）。
            float opinion = 20f, exp = 0.5f, instab = 0.3f, power = 1.5f, betrayal = 0.4f;
            CasusBelli best = CasusBelliScoringRules.Best(opinion, exp, instab, power, betrayal);
            float bestScore = CasusBelliScoringRules.Score(best, opinion, exp, instab, power, betrayal);
            foreach (CasusBelli cb in System.Enum.GetValues(typeof(CasusBelli)))
            {
                float s = CasusBelliScoringRules.Score(cb, opinion, exp, instab, power, betrayal);
                Assert.LessOrEqual(s, bestScore + 1e-6f);
            }
        }
    }
}
