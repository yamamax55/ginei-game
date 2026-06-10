using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 勲章・栄典を固定する：価値は半減期カーブで逓減（乱発のインフレ）、士気/忠誠は価値比例、
    /// 形骸化判定、一括授与の限界効用逓減。境界を担保。
    /// </summary>
    public class HonorsRulesTests
    {
        private static readonly HonorsParams P = HonorsParams.Default;
        // 士気0.2/忠誠0.15/半減数20/形骸化閾値0.2

        [Test]
        public void AwardValue_HalfLifeCurve()
        {
            Assert.AreEqual(1f, HonorsRules.AwardValue(0, P), 1e-5f);     // 最初の勲章＝満額
            Assert.AreEqual(0.5f, HonorsRules.AwardValue(20, P), 1e-5f);  // 半減数で半額
            Assert.AreEqual(1f / 3f, HonorsRules.AwardValue(40, P), 1e-5f); // 1/(1+2)
        }

        [Test]
        public void Bonuses_ScaleWithValue()
        {
            Assert.AreEqual(0.2f, HonorsRules.MoraleBonus(0, P), 1e-5f);
            Assert.AreEqual(0.1f, HonorsRules.MoraleBonus(20, P), 1e-5f);
            Assert.AreEqual(0.15f, HonorsRules.LoyaltyBonus(0, P), 1e-5f);
            Assert.AreEqual(0.075f, HonorsRules.LoyaltyBonus(20, P), 1e-5f);
        }

        [Test]
        public void IsDebased_WhenValueCollapses()
        {
            // 価値0.2を割る授与数：1/(1+n/20)<0.2 → n>80
            Assert.IsFalse(HonorsRules.IsDebased(80, P));  // ちょうど0.2＝まだ
            Assert.IsTrue(HonorsRules.IsDebased(81, P));
        }

        [Test]
        public void BatchMoraleTotal_DiminishingReturns()
        {
            // 最初の10個 vs 既に100個配ったあとの10個＝同じ10個でも効果が違う
            float fresh = HonorsRules.BatchMoraleTotal(0, 10, P);
            float late = HonorsRules.BatchMoraleTotal(100, 10, P);
            Assert.Greater(fresh, late);
            // 一括20個は単発×20より小さい（配るたびに価値が逓減）
            float batch = HonorsRules.BatchMoraleTotal(0, 20, P);
            Assert.Less(batch, HonorsRules.MoraleBonus(0, P) * 20f);
            // 空バッチ＝0
            Assert.AreEqual(0f, HonorsRules.BatchMoraleTotal(0, 0, P), 1e-5f);
        }
    }
}
