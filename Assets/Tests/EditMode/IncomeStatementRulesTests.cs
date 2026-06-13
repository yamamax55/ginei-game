using System.Collections.Generic;
using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>IncomeStatementRules（損益計算書 P/L＝収益−費用=損益・#976）の EditMode テスト。LedgerRules の収益/費用勘定を再利用。</summary>
    public class IncomeStatementRulesTests
    {
        private static IncomeStatementRules.IncomeStatementParams P => IncomeStatementRules.IncomeStatementParams.Default;

        // 収益300・費用200の取引台帳（LedgerRules.AccountType を再利用）。
        // 売上(現金 借 / 収益 貸)200・売上100、費用(費用 借 / 現金 貸)200。
        private static List<JournalEntry> SampleLedger() => new List<JournalEntry>
        {
            new JournalEntry(AccountType.資産, AccountType.収益, 200f),
            new JournalEntry(AccountType.資産, AccountType.収益, 100f),
            new JournalEntry(AccountType.費用, AccountType.資産, 200f),
        };

        /// <summary>総収益＝収益勘定の集計（300）、総費用＝費用勘定の集計（200）。</summary>
        [Test]
        public void TotalRevenueExpenses_台帳から集計()
        {
            var entries = SampleLedger();
            Assert.AreEqual(300f, IncomeStatementRules.TotalRevenue(entries), 1e-4f);
            Assert.AreEqual(200f, IncomeStatementRules.TotalExpenses(entries), 1e-4f);
        }

        /// <summary>純損益＝収益−費用＝損益（300−200=100の黒字）。</summary>
        [Test]
        public void NetIncome_収益マイナス費用()
        {
            Assert.AreEqual(100f, IncomeStatementRules.NetIncome(300f, 200f), 1e-4f);
        }

        /// <summary>費用が収益を上回れば赤字＝純損益は負を許容（150−400=−250）。</summary>
        [Test]
        public void NetIncome_赤字は負を許容()
        {
            Assert.AreEqual(-250f, IncomeStatementRules.NetIncome(150f, 400f), 1e-4f);
        }

        /// <summary>Build＝台帳から損益計算書を1枚作り黒字判定（純益100＞0で黒字）。</summary>
        [Test]
        public void Build_黒字判定()
        {
            var s = IncomeStatementRules.Build(SampleLedger());
            Assert.AreEqual(300f, s.totalRevenue, 1e-4f);
            Assert.AreEqual(200f, s.totalExpenses, 1e-4f);
            Assert.AreEqual(100f, s.netIncome, 1e-4f);
            Assert.IsTrue(IncomeStatementRules.IsProfit(s, P));
        }

        /// <summary>赤字なら黒字判定は false（純益−50）。</summary>
        [Test]
        public void IsProfit_赤字はfalse()
        {
            var deficit = new IncomeStatement(100f, 150f, -50f);
            Assert.IsFalse(IncomeStatementRules.IsProfit(deficit, P));
        }

        /// <summary>利益率＝純益/収益（100/400=0.25）、営業利益率＝(収益−営業費用)/収益（(400−300)/400=0.25）。</summary>
        [Test]
        public void Margins_利益率と営業利益率()
        {
            Assert.AreEqual(0.25f, IncomeStatementRules.ProfitMargin(400f, 100f), 1e-4f);
            Assert.AreEqual(0.25f, IncomeStatementRules.OperatingMargin(400f, 300f), 1e-4f);
            // 収益0なら0（ゼロ除算回避）。
            Assert.AreEqual(0f, IncomeStatementRules.ProfitMargin(0f, 50f), 1e-4f);
        }

        /// <summary>利益剰余金の繰越＝純益−配当が純資産へ積もる（黒字で積み・赤字で食い潰す＝B/Sへ接続）。</summary>
        [Test]
        public void RetainedEarningsTick_黒字は積み赤字は食い潰す()
        {
            // 繰越1000＋当期純益100−配当40＝1060を純資産へ繰越。
            Assert.AreEqual(1060f, IncomeStatementRules.RetainedEarningsTick(1000f, 100f, 40f), 1e-4f);
            // 赤字（純益−300）は剰余金を食い潰す＝1000−300=700。
            Assert.AreEqual(700f, IncomeStatementRules.RetainedEarningsTick(1000f, -300f, 0f), 1e-4f);
        }
    }
}
