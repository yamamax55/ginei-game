using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>複式簿記：勘定区分・借方正常・貸借一致・勘定残高・月次増減・月次借貸合計の一致。</summary>
    public class BookkeepingRulesTests
    {
        [Test]
        public void ClassOf_And_DebitIncreases()
        {
            Assert.AreEqual(AccountType.資産, BookkeepingRules.ClassOf(LedgerAccount.現金));
            Assert.AreEqual(AccountType.資産, BookkeepingRules.ClassOf(LedgerAccount.私兵));
            Assert.AreEqual(AccountType.負債, BookkeepingRules.ClassOf(LedgerAccount.借入金));
            Assert.AreEqual(AccountType.純資産, BookkeepingRules.ClassOf(LedgerAccount.純資産));
            Assert.AreEqual(AccountType.収益, BookkeepingRules.ClassOf(LedgerAccount.俸給));
            Assert.AreEqual(AccountType.費用, BookkeepingRules.ClassOf(LedgerAccount.慰労費));

            Assert.IsTrue(BookkeepingRules.DebitIncreasesBalance(LedgerAccount.現金));   // 資産
            Assert.IsTrue(BookkeepingRules.DebitIncreasesBalance(LedgerAccount.慰労費)); // 費用
            Assert.IsFalse(BookkeepingRules.DebitIncreasesBalance(LedgerAccount.借入金)); // 負債
            Assert.IsFalse(BookkeepingRules.DebitIncreasesBalance(LedgerAccount.俸給));   // 収益
        }

        [Test]
        public void Entry_IsBalanced_WhenDebitsEqualCredits()
        {
            var e = new LedgerEntry(1, "テスト").Debit(LedgerAccount.私兵, 5000).Debit(LedgerAccount.商人口銭, 150)
                .Credit(LedgerAccount.現金, 3000).Credit(LedgerAccount.借入金, 2150);
            Assert.AreEqual(5150, BookkeepingRules.EntryDebitTotal(e));
            Assert.AreEqual(5150, BookkeepingRules.EntryCreditTotal(e));
            Assert.IsTrue(BookkeepingRules.IsBalanced(e));
        }

        [Test]
        public void Balances_AfterOpeningAndPurchase_SatisfyAccountingIdentity()
        {
            var l = new PersonalLedger();
            BookkeepingRules.PostOpeningCash(l, 1, 10000);
            BookkeepingRules.PostShipPurchase(l, 1, 5000, 150, 3000, 2150, "私兵購入");

            Assert.AreEqual(7000, BookkeepingRules.Balance(l, LedgerAccount.現金, 1));   // 10000−3000
            Assert.AreEqual(5000, BookkeepingRules.Balance(l, LedgerAccount.私兵, 1));
            Assert.AreEqual(2150, BookkeepingRules.Balance(l, LedgerAccount.借入金, 1));
            Assert.AreEqual(10000, BookkeepingRules.Balance(l, LedgerAccount.純資産, 1));
            Assert.AreEqual(150, BookkeepingRules.Balance(l, LedgerAccount.商人口銭, 1)); // 費用

            // 会計恒等式：資産 = 負債 + 純資産 + (収益−費用)。12000 = 2150 + 10000 + (0−150)。
            long assets = BookkeepingRules.Balance(l, LedgerAccount.現金, 1) + BookkeepingRules.Balance(l, LedgerAccount.私兵, 1);
            long liab = BookkeepingRules.Balance(l, LedgerAccount.借入金, 1);
            long equity = BookkeepingRules.Balance(l, LedgerAccount.純資産, 1);
            long expense = BookkeepingRules.Balance(l, LedgerAccount.商人口銭, 1) + BookkeepingRules.Balance(l, LedgerAccount.慰労費, 1);
            long income = BookkeepingRules.Balance(l, LedgerAccount.俸給, 1);
            Assert.AreEqual(assets, liab + equity + (income - expense));
        }

        [Test]
        public void MonthlyTotals_DebitsEqualCredits_PerMonth()
        {
            var l = new PersonalLedger();
            BookkeepingRules.PostOpeningCash(l, 1, 10000);
            BookkeepingRules.PostShipPurchase(l, 1, 5000, 150, 3000, 2150, "購入");
            BookkeepingRules.PostRelief(l, 2, 2000, "慰労");

            Assert.AreEqual(BookkeepingRules.MonthlyDebitTotal(l, 1), BookkeepingRules.MonthlyCreditTotal(l, 1));
            Assert.AreEqual(BookkeepingRules.MonthlyDebitTotal(l, 2), BookkeepingRules.MonthlyCreditTotal(l, 2));
            // 月2は慰労 2000 のみ＝借方2000・貸方2000。
            Assert.AreEqual(2000, BookkeepingRules.MonthlyDebitTotal(l, 2));
            // 月2の現金増減は −2000（慰労で出金）。
            Assert.AreEqual(-2000, BookkeepingRules.MonthlyMovement(l, LedgerAccount.現金, 2));
            // 慰労費は月2に +2000。
            Assert.AreEqual(2000, BookkeepingRules.MonthlyMovement(l, LedgerAccount.慰労費, 2));
        }

        [Test]
        public void NullLedger_IsSafe()
        {
            Assert.AreEqual(0, BookkeepingRules.Balance(null, LedgerAccount.現金, 9));
            Assert.AreEqual(0, BookkeepingRules.MonthlyDebitTotal(null, 1));
            Assert.AreEqual(0, BookkeepingRules.MonthlyMovement(null, LedgerAccount.現金, 1));
        }
    }
}
