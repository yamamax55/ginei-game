using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// サブプライム証券化と格付けインフレ（<see cref="SubprimeRules"/>）を固定する：プライム/サブプライムの割合と加重リスク、
    /// 真の格付け、SOX法前は AAA 表示（ジャンク混入）・制定後は是正、隠れた損失の露呈。
    /// </summary>
    public class SubprimeTests
    {
        private static MortgageBundle Bundle(float primePrincipal, float subprimePrincipal)
            => new MortgageBundle("MBS", new List<Loan>
            {
                Loan.Of(LoanType.プライム, primePrincipal),
                Loan.Of(LoanType.サブプライム, subprimePrincipal),
            });

        [Test]
        public void ShareAndPoolRisk_Weighted()
        {
            var b = Bundle(40f, 160f); // 総200・サブプライム80%
            Assert.AreEqual(200f, SubprimeRules.TotalPrincipal(b), 1e-3f);
            Assert.AreEqual(0.8f, SubprimeRules.SubprimeShare(b), 1e-3f);
            // 加重リスク＝(40*0.02 + 160*0.50)/200 = (0.8+80)/200 = 0.404
            Assert.AreEqual(0.404f, SubprimeRules.PoolDefaultRisk(b), 1e-3f);
        }

        [Test]
        public void PrimeBundle_IsGenuinelyHighGrade()
        {
            var prime = new MortgageBundle("優良", new List<Loan> { Loan.Of(LoanType.プライム, 200f) });
            Assert.IsTrue(CreditRatingRules.IsInvestmentGrade(SubprimeRules.TrueRating(prime))); // 本当に高格付け
            Assert.IsFalse(SubprimeRules.IsRatingInflated(prime, soxEnacted: false));            // インフレなし
            Assert.AreEqual(0f, SubprimeRules.RevealLoss(prime, false), 1e-3f);                  // 隠れた損失なし
        }

        [Test]
        public void SubprimeHeavy_RatedAAA_BeforeSOX_ButTrulyJunk()
        {
            var b = Bundle(40f, 160f); // サブプライム80%＝真はジャンク
            // 真の格付けは投資適格でない（ジャンク）
            Assert.IsFalse(CreditRatingRules.IsInvestmentGrade(SubprimeRules.TrueRating(b)));
            // SOX法制定前：ジャンクが混ざっているのに AAA と表示される
            Assert.AreEqual(CreditRating.AAA, SubprimeRules.StatedRating(b, soxEnacted: false));
            Assert.IsTrue(SubprimeRules.IsRatingInflated(b, false));   // 格付けインフレ
            Assert.Greater(SubprimeRules.HiddenRisk(b, false), 0f);    // AAAに隠れたリスク
            Assert.Greater(SubprimeRules.RevealLoss(b, false), 0f);    // 露呈すれば損失
        }

        [Test]
        public void AfterSOX_RatingCorrectedToTrue_NoHiddenLoss()
        {
            var b = Bundle(40f, 160f);
            // SOX法制定後：表示格付けが真の格付け（ジャンク）に是正される＝AAAの粉飾が剥がれる
            Assert.AreEqual(SubprimeRules.TrueRating(b), SubprimeRules.StatedRating(b, soxEnacted: true));
            Assert.IsFalse(SubprimeRules.IsRatingInflated(b, true)); // もう紛れ込めない
            Assert.AreEqual(0f, SubprimeRules.HiddenRisk(b, true), 1e-4f);
            Assert.AreEqual(0f, SubprimeRules.RevealLoss(b, true), 1e-4f);
        }

        [Test]
        public void NullSafe()
        {
            Assert.AreEqual(0f, SubprimeRules.TotalPrincipal(null), 1e-4f);
            Assert.AreEqual(0f, SubprimeRules.SubprimeShare(null), 1e-4f);
            Assert.AreEqual(0f, SubprimeRules.PoolDefaultRisk(null), 1e-4f);
        }
    }
}
