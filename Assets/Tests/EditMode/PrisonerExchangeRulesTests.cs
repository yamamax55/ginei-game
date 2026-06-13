using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 捕虜交換を固定する：価値は階級tier比例、総価値集計、釣り合い比、切迫が許容幅を広げる
    /// （人材が枯れた側は不利な交換も呑む）、相互受諾で成立、タダ取り拒否。境界を担保。
    /// </summary>
    public class PrisonerExchangeRulesTests
    {
        private static readonly ExchangeParams P = ExchangeParams.Default;
        // tier段差+50%/基準1/許容±20%

        [Test]
        public void PrisonerValue_ByRankTier()
        {
            Assert.AreEqual(1f, PrisonerExchangeRules.PrisonerValue(0, P), 1e-5f);    // 兵卒
            Assert.AreEqual(3.5f, PrisonerExchangeRules.PrisonerValue(5, P), 1e-5f);  // 准将＝1+5×0.5
            Assert.AreEqual(6f, PrisonerExchangeRules.PrisonerValue(10, P), 1e-5f);   // 元帥
        }

        [Test]
        public void TotalValue_SumsGroup()
        {
            // 兵卒2名＋准将1名＝1+1+3.5
            Assert.AreEqual(5.5f, PrisonerExchangeRules.TotalValue(new[] { 0, 0, 5 }, P), 1e-4f);
            Assert.AreEqual(0f, PrisonerExchangeRules.TotalValue(null, P), 1e-5f);
            Assert.AreEqual(0f, PrisonerExchangeRules.TotalValue(new int[0], P), 1e-5f);
        }

        [Test]
        public void OfferRatio_EdgeCases()
        {
            Assert.AreEqual(1f, PrisonerExchangeRules.OfferRatio(10f, 10f), 1e-5f);
            Assert.AreEqual(2f, PrisonerExchangeRules.OfferRatio(20f, 10f), 1e-5f);
            Assert.IsTrue(float.IsPositiveInfinity(PrisonerExchangeRules.OfferRatio(10f, 0f))); // タダ取り要求
            Assert.AreEqual(1f, PrisonerExchangeRules.OfferRatio(0f, 0f), 1e-5f); // 双方なし＝等価扱い
        }

        [Test]
        public void Acceptable_FairnessWithTolerance()
        {
            // 切迫なし：許容1.2まで
            Assert.IsTrue(PrisonerExchangeRules.Acceptable(12f, 10f, 0f, P));   // 比1.2＝許容内
            Assert.IsFalse(PrisonerExchangeRules.Acceptable(13f, 10f, 0f, P));  // 比1.3＝拒否
            // 等価は常に可
            Assert.IsTrue(PrisonerExchangeRules.Acceptable(10f, 10f, 0f, P));
        }

        [Test]
        public void Acceptable_DesperationWidensTolerance()
        {
            // 切迫1.0：許容1.2+1.0=2.2 まで＝倍出しでも呑む
            Assert.IsTrue(PrisonerExchangeRules.Acceptable(20f, 10f, 1f, P));
            Assert.IsFalse(PrisonerExchangeRules.Acceptable(23f, 10f, 1f, P));
            // タダ取りには切迫していても応じない
            Assert.IsFalse(PrisonerExchangeRules.Acceptable(10f, 0f, 1f, P));
        }

        [Test]
        public void DealStruck_RequiresMutualAcceptance()
        {
            // 等価交換＝成立
            Assert.IsTrue(PrisonerExchangeRules.DealStruck(10f, 10f, 0f, 0f, P));
            // Aが多く出す比1.5：A拒否（切迫なし）＝不成立
            Assert.IsFalse(PrisonerExchangeRules.DealStruck(15f, 10f, 0f, 0f, P));
            // Aが切迫していれば同じ提示でも成立（Bは受け取り超過なので常に可）
            Assert.IsTrue(PrisonerExchangeRules.DealStruck(15f, 10f, 1f, 0f, P));
        }
    }
}
