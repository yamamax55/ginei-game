using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 貸借対照表B/Sのスナップショット（#975・純データ）。ある時点の財政状態を「資産＝負債＋純資産」で表す。
    /// 総資産・総負債・純資産の3値を保持（残高は <see cref="BalanceSheetRules"/> が台帳から集計する）。
    /// </summary>
    public class BalanceSheet
    {
        public float totalAssets;      // 総資産
        public float totalLiabilities; // 総負債
        public float equity;           // 純資産（資産−負債）

        public BalanceSheet() { }

        public BalanceSheet(float totalAssets, float totalLiabilities, float equity)
        {
            this.totalAssets = totalAssets;
            this.totalLiabilities = totalLiabilities;
            this.equity = equity;
        }
    }

    /// <summary>
    /// 貸借対照表＝資産=負債+純資産の静的スナップショットの純ロジック（#975・唯一の窓口）。
    /// <see cref="LedgerRules"/>（#974 台帳＝仕訳エンジン Σ借方=Σ貸方）の <see cref="AccountType"/> と
    /// <see cref="LedgerRules.AccountBalance"/> を再利用して資産/負債/純資産の残高をまとめ、
    /// 複式簿記の必然「資産=負債+純資産」を検証する＝ある時点の財政状態の集計表。
    /// <c>IncomeStatementRules</c>（P/L＝損益＝収益−費用・同Wave並行）が期間のフローを表すのに対し、こちらは<b>時点のストック</b>。
    /// <see cref="FiscalRules"/>（歳入歳出のPB/債務/金利）とは別＝企業会計の様式（<c>DebtToEquityRatio</c> が
    /// <see cref="FiscalRules.DebtRatio"/> と概念接続）。test-first。
    /// </summary>
    public static class BalanceSheetRules
    {
        /// <summary>B/Sの調整値（マジックナンバー禁止＝集約）。</summary>
        public readonly struct BalanceSheetParams
        {
            public readonly float balanceTolerance; // 恒等式一致の許容誤差（浮動小数の丸め吸収）

            public BalanceSheetParams(float balanceTolerance)
            {
                this.balanceTolerance = Mathf.Max(0f, balanceTolerance);
            }

            /// <summary>既定＝恒等式一致の許容誤差0.001。</summary>
            public static BalanceSheetParams Default => new BalanceSheetParams(0.001f);
        }

        /// <summary>総資産＝資産勘定の残高合計（<see cref="LedgerRules.AccountBalance"/> を資産科目へ適用）。</summary>
        public static float TotalAssets(IReadOnlyList<JournalEntry> entries)
            => LedgerRules.AccountBalance(entries, AccountType.資産);

        /// <summary>総負債＝負債勘定の残高合計。</summary>
        public static float TotalLiabilities(IReadOnlyList<JournalEntry> entries)
            => LedgerRules.AccountBalance(entries, AccountType.負債);

        /// <summary>純資産＝資産−負債（複式簿記の恒等式の移項。台帳の純資産勘定残高とも一致するはず）。</summary>
        public static float Equity(IReadOnlyList<JournalEntry> entries)
            => TotalAssets(entries) - TotalLiabilities(entries);

        /// <summary>台帳から貸借対照表スナップショットを組み立てる（資産・負債・純資産＝資産−負債）。</summary>
        public static BalanceSheet Build(IReadOnlyList<JournalEntry> entries)
        {
            float assets = TotalAssets(entries);
            float liabilities = TotalLiabilities(entries);
            return new BalanceSheet(assets, liabilities, assets - liabilities);
        }

        /// <summary>恒等式の検証＝<b>資産=負債+純資産</b>（複式簿記の必然・#975）。許容誤差内で一致するか。</summary>
        public static bool IsBalanced(BalanceSheet bs, BalanceSheetParams p)
        {
            if (bs == null) return false;
            return Mathf.Abs(bs.totalAssets - (bs.totalLiabilities + bs.equity)) <= p.balanceTolerance;
        }

        /// <summary>
        /// 負債比率＝総負債/純資産（財務健全性。高いほどレバレッジ過大＝<see cref="FiscalRules.DebtRatio"/> と概念接続）。
        /// 純資産が0以下（債務超過）なら計測不能ゆえ正の無限大を返す。
        /// </summary>
        public static float DebtToEquityRatio(BalanceSheet bs)
        {
            if (bs == null) return 0f;
            if (bs.equity <= 0f) return float.PositiveInfinity; // 債務超過＝健全性は測れない
            return Mathf.Max(0f, bs.totalLiabilities) / bs.equity;
        }

        /// <summary>支払能力＝純資産が正か（true＝健全／false＝債務超過＝資産より負債が多い）。</summary>
        public static bool Solvency(BalanceSheet bs)
            => bs != null && bs.equity > 0f;

        /// <summary>運転資本＝流動資産−流動負債（短期の資金繰り。正＝余裕／負＝ショート懸念）。</summary>
        public static float WorkingCapital(float currentAssets, float currentLiabilities)
            => Mathf.Max(0f, currentAssets) - Mathf.Max(0f, currentLiabilities);
    }
}
