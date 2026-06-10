using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 貸借対照表B/S（#975）のEditModeテスト。複式簿記台帳（<see cref="LedgerRules"/> の <see cref="AccountType"/> 再利用）から
    /// 資産/負債/純資産を集計し、恒等式「資産=負債+純資産」と債務超過判定を担保する。
    /// </summary>
    public class BalanceSheetRulesTests
    {
        // 標準的な台帳：資本金1000で資産取得＋借入500で資産取得。
        // → 資産1500・負債500・純資産1000（資産=負債+純資産=1500）。
        private static List<JournalEntry> SampleLedger()
        {
            return new List<JournalEntry>
            {
                new JournalEntry(AccountType.資産, AccountType.純資産, 1000f), // 資本金で資産
                new JournalEntry(AccountType.資産, AccountType.負債, 500f),    // 借入で資産
            };
        }

        /// <summary>総資産＝資産勘定の残高合計（借方計上の合計）。</summary>
        [Test]
        public void TotalAssets_資産勘定の残高合計()
        {
            Assert.AreEqual(1500f, BalanceSheetRules.TotalAssets(SampleLedger()), 1e-4f);
        }

        /// <summary>総負債＝負債勘定の残高合計（貸方計上の合計）。</summary>
        [Test]
        public void TotalLiabilities_負債勘定の残高合計()
        {
            Assert.AreEqual(500f, BalanceSheetRules.TotalLiabilities(SampleLedger()), 1e-4f);
        }

        /// <summary>純資産＝資産−負債（恒等式の移項）。</summary>
        [Test]
        public void Equity_資産マイナス負債()
        {
            Assert.AreEqual(1000f, BalanceSheetRules.Equity(SampleLedger()), 1e-4f);
        }

        /// <summary>Build＋IsBalanced＝資産=負債+純資産の恒等式が成立する。</summary>
        [Test]
        public void IsBalanced_恒等式が成立する()
        {
            BalanceSheet bs = BalanceSheetRules.Build(SampleLedger());
            Assert.AreEqual(1500f, bs.totalAssets, 1e-4f);
            Assert.AreEqual(500f, bs.totalLiabilities, 1e-4f);
            Assert.AreEqual(1000f, bs.equity, 1e-4f);
            Assert.IsTrue(BalanceSheetRules.IsBalanced(bs, BalanceSheetRules.BalanceSheetParams.Default));
        }

        /// <summary>恒等式を崩した手組みB/Sは検証に失敗する（資産≠負債+純資産）。</summary>
        [Test]
        public void IsBalanced_不整合は失敗する()
        {
            BalanceSheet bad = new BalanceSheet(1500f, 500f, 900f); // 500+900=1400≠1500
            Assert.IsFalse(BalanceSheetRules.IsBalanced(bad, BalanceSheetRules.BalanceSheetParams.Default));
        }

        /// <summary>負債比率＝負債/純資産（500/1000=0.5）／純資産0以下は無限大。</summary>
        [Test]
        public void DebtToEquityRatio_負債比率()
        {
            BalanceSheet bs = BalanceSheetRules.Build(SampleLedger());
            Assert.AreEqual(0.5f, BalanceSheetRules.DebtToEquityRatio(bs), 1e-4f);

            BalanceSheet insolvent = new BalanceSheet(400f, 500f, -100f);
            Assert.IsTrue(float.IsPositiveInfinity(BalanceSheetRules.DebtToEquityRatio(insolvent)));
        }

        /// <summary>支払能力＝純資産が正なら健全／負債が資産を上回る債務超過は false。</summary>
        [Test]
        public void Solvency_債務超過判定()
        {
            BalanceSheet healthy = BalanceSheetRules.Build(SampleLedger());
            Assert.IsTrue(BalanceSheetRules.Solvency(healthy));

            // 負債800を抱えた資産600 → 純資産−200＝債務超過。
            var ledger = new List<JournalEntry>
            {
                new JournalEntry(AccountType.資産, AccountType.負債, 600f),
                new JournalEntry(AccountType.費用, AccountType.負債, 200f), // 損失で負債増・純資産食う
            };
            // 資産600・負債800・純資産−200。
            BalanceSheet insolvent = BalanceSheetRules.Build(ledger);
            Assert.AreEqual(600f, insolvent.totalAssets, 1e-4f);
            Assert.AreEqual(800f, insolvent.totalLiabilities, 1e-4f);
            Assert.AreEqual(-200f, insolvent.equity, 1e-4f);
            Assert.IsFalse(BalanceSheetRules.Solvency(insolvent));
        }

        /// <summary>運転資本＝流動資産−流動負債（短期の資金繰り）。</summary>
        [Test]
        public void WorkingCapital_流動資産マイナス流動負債()
        {
            Assert.AreEqual(300f, BalanceSheetRules.WorkingCapital(800f, 500f), 1e-4f);
            Assert.AreEqual(-200f, BalanceSheetRules.WorkingCapital(300f, 500f), 1e-4f);
        }
    }
}
