using System.Collections.Generic;
using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>LedgerRules（複式簿記＝Σ借方=Σ貸方の整合性エンジン・#974）の EditMode テスト。</summary>
    public class LedgerRulesTests
    {
        private static LedgerRules.LedgerParams P => LedgerRules.LedgerParams.Default;

        /// <summary>正常残高側＝資産・費用は借方、負債・純資産・収益は貸方。</summary>
        [Test]
        public void NormalBalanceSide_資産費用は借方_他は貸方()
        {
            Assert.IsTrue(LedgerRules.NormalBalanceSide(AccountType.資産));
            Assert.IsTrue(LedgerRules.NormalBalanceSide(AccountType.費用));
            Assert.IsFalse(LedgerRules.NormalBalanceSide(AccountType.負債));
            Assert.IsFalse(LedgerRules.NormalBalanceSide(AccountType.純資産));
            Assert.IsFalse(LedgerRules.NormalBalanceSide(AccountType.収益));
        }

        /// <summary>個別仕訳は金額が非負なら貸借一致（負は無効）。</summary>
        [Test]
        public void IsBalanced_非負仕訳は一致_nullは不一致()
        {
            var ok = new JournalEntry(AccountType.資産, AccountType.収益, 100f);
            Assert.IsTrue(LedgerRules.IsBalanced(ok));
            Assert.IsFalse(LedgerRules.IsBalanced(null));
            // 負の金額はクランプされ0になり一致扱い。
            var clamped = new JournalEntry(AccountType.資産, AccountType.収益, -50f);
            Assert.AreEqual(0f, clamped.amount, 1e-4f);
            Assert.IsTrue(LedgerRules.IsBalanced(clamped));
        }

        /// <summary>総借方=総貸方（複式簿記＝Σ借方=Σ貸方）。</summary>
        [Test]
        public void TotalDebitsCredits_常に一致()
        {
            var entries = new List<JournalEntry>
            {
                new JournalEntry(AccountType.資産, AccountType.収益, 300f), // 現金売上
                new JournalEntry(AccountType.費用, AccountType.資産, 120f), // 現金で経費
                new JournalEntry(AccountType.資産, AccountType.負債, 500f), // 借入で現金増
            };
            Assert.AreEqual(920f, LedgerRules.TotalDebits(entries), 1e-3f);
            Assert.AreEqual(920f, LedgerRules.TotalCredits(entries), 1e-3f);
            Assert.IsTrue(LedgerRules.IsLedgerBalanced(entries, P));
        }

        /// <summary>空・null の台帳は貸借一致（0=0）。</summary>
        [Test]
        public void IsLedgerBalanced_空とnullは一致()
        {
            Assert.IsTrue(LedgerRules.IsLedgerBalanced(new List<JournalEntry>(), P));
            Assert.IsTrue(LedgerRules.IsLedgerBalanced(null, P));
        }

        /// <summary>勘定残高は正常側で計上されると正（資産=借方計上で増・収益=貸方計上で増）。</summary>
        [Test]
        public void AccountBalance_正常側計上で正の残高()
        {
            var entries = new List<JournalEntry>
            {
                new JournalEntry(AccountType.資産, AccountType.収益, 300f), // 資産+300 / 収益+300
                new JournalEntry(AccountType.費用, AccountType.資産, 120f), // 費用+120 / 資産-120
            };
            // 資産＝借方300−貸方120＝180。
            Assert.AreEqual(180f, LedgerRules.AccountBalance(entries, AccountType.資産), 1e-3f);
            // 収益＝貸方300（正常側=貸方）＝300。
            Assert.AreEqual(300f, LedgerRules.AccountBalance(entries, AccountType.収益), 1e-3f);
            // 費用＝借方120（正常側=借方）＝120。
            Assert.AreEqual(120f, LedgerRules.AccountBalance(entries, AccountType.費用), 1e-3f);
        }

        /// <summary>反正常側に計上された勘定残高は負になる（負債を借方で返済＝負残高として現れる）。</summary>
        [Test]
        public void AccountBalance_反正常側計上は負()
        {
            var entries = new List<JournalEntry>
            {
                new JournalEntry(AccountType.負債, AccountType.資産, 200f), // 負債を借方で減らす（返済）/ 資産-200
            };
            // 負債＝貸方0−借方200＝-200（正常側=貸方なので借方計上は負）。
            Assert.AreEqual(-200f, LedgerRules.AccountBalance(entries, AccountType.負債), 1e-3f);
        }

        /// <summary>試算表は貸借一致した仕訳群で整合する（全勘定の借方残高合計=貸方残高合計）。</summary>
        [Test]
        public void TrialBalance_整合した台帳で一致()
        {
            var entries = new List<JournalEntry>
            {
                new JournalEntry(AccountType.資産, AccountType.純資産, 1000f), // 出資で資産増
                new JournalEntry(AccountType.資産, AccountType.収益, 300f),   // 売上
                new JournalEntry(AccountType.費用, AccountType.資産, 200f),   // 経費
            };
            // 借方側：資産(1000+300-200=1100)+費用(200)=1300。
            // 貸方側：純資産1000+収益300=1300。
            Assert.IsTrue(LedgerRules.TrialBalance(entries, P));
            Assert.IsTrue(LedgerRules.IsLedgerBalanced(entries, P));
        }

        /// <summary>許容誤差内の丸めは一致扱い（既定 tolerance=0.001）。</summary>
        [Test]
        public void IsLedgerBalanced_許容誤差内は一致()
        {
            // 微小な amount でも Σ借方=Σ貸方（同一 amount を両側に立てるため常に一致）。
            var entries = new List<JournalEntry>
            {
                new JournalEntry(AccountType.資産, AccountType.収益, 0.0005f),
            };
            Assert.IsTrue(LedgerRules.IsLedgerBalanced(entries, P));
            Assert.IsTrue(LedgerRules.TrialBalance(entries, P));
        }
    }
}
