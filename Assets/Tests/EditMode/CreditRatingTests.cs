using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 格付け会社（<see cref="CreditRatingRules"/>）を固定する：信用力/デフォルトリスク→格付け、格付け→スプレッド/デフォルトリスク
    /// （悪い格ほど高い）、投資適格の線引き、格上げ/格下げ、債券（#185）との連結。
    /// </summary>
    public class CreditRatingTests
    {
        [Test]
        public void Rate_ByCreditworthiness()
        {
            Assert.AreEqual(CreditRating.AAA, CreditRatingRules.Rate(1f));   // 最良
            Assert.AreEqual(CreditRating.BBB, CreditRatingRules.Rate(0.65f)); // 投資適格の下限域
            Assert.AreEqual(CreditRating.D, CreditRatingRules.Rate(0f));      // 最悪
            // デフォルトリスクが低いほど高格付け
            Assert.AreEqual(CreditRating.AAA, CreditRatingRules.RatingFromDefaultRisk(0f));
            Assert.AreEqual(CreditRating.D, CreditRatingRules.RatingFromDefaultRisk(1f));
        }

        [Test]
        public void Spread_AndDefaultRisk_WorsenWithRating()
        {
            Assert.AreEqual(0f, CreditRatingRules.Spread(CreditRating.AAA), 1e-4f);
            Assert.Greater(CreditRatingRules.Spread(CreditRating.D), CreditRatingRules.Spread(CreditRating.AAA));
            // 単調：格が下がるほどスプレッド・デフォルトリスクが上がる
            for (int r = (int)CreditRating.AAA; r < (int)CreditRating.D; r++)
            {
                Assert.LessOrEqual(CreditRatingRules.Spread((CreditRating)r), CreditRatingRules.Spread((CreditRating)(r + 1)));
                Assert.LessOrEqual(CreditRatingRules.DefaultRiskOf((CreditRating)r), CreditRatingRules.DefaultRiskOf((CreditRating)(r + 1)));
            }
        }

        [Test]
        public void InvestmentGrade_Boundary()
        {
            Assert.IsTrue(CreditRatingRules.IsInvestmentGrade(CreditRating.AAA));
            Assert.IsTrue(CreditRatingRules.IsInvestmentGrade(CreditRating.BBB)); // 投資適格の下限
            Assert.IsFalse(CreditRatingRules.IsInvestmentGrade(CreditRating.BB)); // ジャンク
            Assert.IsFalse(CreditRatingRules.IsInvestmentGrade(CreditRating.D));
        }

        [Test]
        public void Upgrade_Downgrade_Clamped()
        {
            Assert.AreEqual(CreditRating.AA, CreditRatingRules.Downgrade(CreditRating.AAA));  // 1ノッチ悪化
            Assert.AreEqual(CreditRating.D, CreditRatingRules.Downgrade(CreditRating.CCC, 5)); // Dで頭打ち
            Assert.AreEqual(CreditRating.AAA, CreditRatingRules.Upgrade(CreditRating.AA));     // 1ノッチ改善
            Assert.AreEqual(CreditRating.AAA, CreditRatingRules.Upgrade(CreditRating.A, 5));   // AAAで頭打ち
        }

        [Test]
        public void FeedsBondYield_WorseRatingHigherYield()
        {
            // 格付け→デフォルトリスク→債券の必要利回り：格が悪いほど利回りが上がる（借入コスト↑）
            var P = BondMarketRules.BondParams.Default;
            float yAAA = BondMarketRules.RequiredYield(0.05f, CreditRatingRules.DefaultRiskOf(CreditRating.AAA), P);
            float yB = BondMarketRules.RequiredYield(0.05f, CreditRatingRules.DefaultRiskOf(CreditRating.B), P);
            Assert.Greater(yB, yAAA);
        }
    }
}
