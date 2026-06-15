using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 資産担保融資（#186/#2070・<see cref="CollateralLoanRules"/>／<see cref="CollateralLoanRegistry"/>）を固定する：
    /// 借入上限（LTV）・LTV比・担保割れ判定・利息・差し押さえ回収・組成/返済。
    /// </summary>
    public class CollateralLoanRulesTests
    {
        [SetUp]
        public void Clear() => CollateralLoanRegistry.Clear();

        private static CollateralLoanRules.CollateralParams P => CollateralLoanRules.CollateralParams.Default;

        [Test]
        public void MaxBorrow_IsCollateralTimesLtv()
        {
            Assert.AreEqual(700f, CollateralLoanRules.MaxBorrow(1000f, 0.7f), 1e-3f);
            Assert.AreEqual(0f, CollateralLoanRules.MaxBorrow(-100f, 0.7f), 1e-3f);
        }

        [Test]
        public void LoanToValue_AndUnderwater()
        {
            Assert.AreEqual(0.5f, CollateralLoanRules.LoanToValue(500f, 1000f), 1e-4f);
            // 担保が下がって LTV比 0.95 > 0.90 → 担保割れ
            Assert.IsTrue(CollateralLoanRules.IsUnderwater(950f, 1000f, P));
            Assert.IsFalse(CollateralLoanRules.IsUnderwater(700f, 1000f, P));
            // 担保0は超大＝担保割れ
            Assert.IsTrue(CollateralLoanRules.IsUnderwater(100f, 0f, P));
        }

        [Test]
        public void AnnualInterest_AndForeclosureRecovery()
        {
            Assert.AreEqual(35f, CollateralLoanRules.AnnualInterest(700f, 0.05f), 1e-3f);
            Assert.AreEqual(800f, CollateralLoanRules.ForeclosureRecovery(1000f, P), 1e-3f); // 1000×(1−0.2)
        }

        [Test]
        public void Borrow_RegistersClampedToMaxLtv()
        {
            var loan = CollateralLoanRules.Borrow(OwnerRef.Person(1), OwnerRef.State(Faction.帝国),
                requestedAmount: 900f, collateralValue: 1000f, interestRate: 0.05f, pledgedSystemId: 5, P);
            Assert.IsNotNull(loan);
            Assert.AreEqual(700f, loan.principal, 1e-3f); // 要求900だが上限700
            Assert.AreEqual("P:1", loan.Borrower.Key);
            Assert.AreEqual("F:帝国", loan.Lender.Key);
            Assert.AreEqual(5, loan.pledgedSystemId);
            Assert.AreEqual(1, CollateralLoanRegistry.All.Count);
            Assert.AreEqual(1, CollateralLoanRegistry.ByBorrower(OwnerRef.Person(1)).Count);
            Assert.AreEqual(1, CollateralLoanRegistry.ByLender(OwnerRef.State(Faction.帝国)).Count);
        }

        [Test]
        public void Borrow_ZeroCollateral_Fails()
        {
            Assert.IsNull(CollateralLoanRules.Borrow(OwnerRef.Person(1), OwnerRef.State(Faction.帝国), 100f, 0f, 0.05f, -1, P));
            Assert.AreEqual(0, CollateralLoanRegistry.All.Count);
        }

        [Test]
        public void Repay_ReducesPrincipal_NotBelowZero()
        {
            var loan = CollateralLoanRules.Borrow(OwnerRef.Person(1), OwnerRef.State(Faction.帝国), 700f, 1000f, 0.05f, 5, P);
            Assert.AreEqual(200f, CollateralLoanRules.Repay(loan, 200f), 1e-3f);
            Assert.AreEqual(500f, loan.principal, 1e-3f);
            Assert.AreEqual(500f, CollateralLoanRules.Repay(loan, 999f), 1e-3f); // 残高超えない
            Assert.AreEqual(0f, loan.principal, 1e-3f);
        }
    }
}
