using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 損益計算書 P/L のスナップショット（#976）。一定期間のフロー＝総収益・総費用・純損益を保持する純データ。
    /// 純損益＝総収益−総費用（赤字＝負を許容）。<see cref="BalanceSheetRules"/>（B/S＝ストックの残高）が一時点の写真なら、
    /// こちらは期間中の流れの集計。<see cref="LedgerRules"/>（#974 台帳）の収益/費用勘定を暦で締めて作る。
    /// </summary>
    public class IncomeStatement
    {
        public float totalRevenue;  // 総収益（収益勘定の集計）
        public float totalExpenses; // 総費用（費用勘定の集計）
        public float netIncome;     // 純損益＝総収益−総費用（黒字＞0／赤字＜0）

        public IncomeStatement() { }

        public IncomeStatement(float totalRevenue, float totalExpenses, float netIncome)
        {
            // 収益・費用は非負クランプ、純損益のみ赤字＝負を許容。
            this.totalRevenue = Mathf.Max(0f, totalRevenue);
            this.totalExpenses = Mathf.Max(0f, totalExpenses);
            this.netIncome = netIncome;
        }
    }

    /// <summary>
    /// 損益計算書 P/L＝収益−費用=損益・暦で締めるフロー計算書の純ロジック（#976・唯一の窓口）。
    /// 一定期間の収益と費用を集計して<b>純損益＝総収益−総費用</b>を出す＝<b>黒字は純資産（利益剰余金）に積もり、赤字は食い潰す</b>。
    /// <see cref="LedgerRules"/>（#974 仕訳エンジン）の収益/費用勘定を期間集計する＝その台帳の上に乗る財務諸表。
    /// <see cref="BalanceSheetRules"/>（B/S＝ストックの残高・同Wave並行）と対をなし、利益剰余金の繰越で両者を繋ぐ。
    /// <see cref="FiscalRules"/>（歳入歳出のマクロ財政）とは別＝こちらは<b>企業会計の様式</b>。test-first。
    /// </summary>
    public static class IncomeStatementRules
    {
        /// <summary>損益計算の調整値（マジックナンバー禁止＝集約）。</summary>
        public readonly struct IncomeStatementParams
        {
            public readonly float profitThreshold; // 黒字判定の閾値（この純損益を超えれば黒字）

            public IncomeStatementParams(float profitThreshold)
            {
                this.profitThreshold = profitThreshold;
            }

            /// <summary>既定＝黒字判定の閾値0（純損益が正なら黒字）。</summary>
            public static IncomeStatementParams Default => new IncomeStatementParams(0f);
        }

        /// <summary>総収益＝収益勘定の正常残高（貸方科目＝貸方計上−借方計上）を期間集計する（#974 台帳から）。</summary>
        public static float TotalRevenue(IReadOnlyList<JournalEntry> entries)
        {
            // 収益は貸方科目＝LedgerRules.AccountBalance が正常側（貸方）で正の残高を返す。
            return Mathf.Max(0f, LedgerRules.AccountBalance(entries, AccountType.収益));
        }

        /// <summary>総費用＝費用勘定の正常残高（借方科目＝借方計上−貸方計上）を期間集計する（#974 台帳から）。</summary>
        public static float TotalExpenses(IReadOnlyList<JournalEntry> entries)
        {
            // 費用は借方科目＝LedgerRules.AccountBalance が正常側（借方）で正の残高を返す。
            return Mathf.Max(0f, LedgerRules.AccountBalance(entries, AccountType.費用));
        }

        /// <summary>純損益＝収益−費用（P/Lの根本式＝黒字＞0／赤字＜0・赤字を許容）。</summary>
        public static float NetIncome(float revenue, float expenses)
        {
            // 収益−費用＝損益。赤字（負）はそのまま返す＝純損益のみ負を許容。
            return Mathf.Max(0f, revenue) - Mathf.Max(0f, expenses);
        }

        /// <summary>台帳から損益計算書を1枚作る＝収益/費用を集計し純損益を確定する（暦で締める窓口）。</summary>
        public static IncomeStatement Build(IReadOnlyList<JournalEntry> entries)
        {
            float rev = TotalRevenue(entries);
            float exp = TotalExpenses(entries);
            return new IncomeStatement(rev, exp, NetIncome(rev, exp));
        }

        /// <summary>黒字判定＝純損益が閾値を超えるか（既定では純損益＞0で黒字）。</summary>
        public static bool IsProfit(IncomeStatement statement, IncomeStatementParams p)
        {
            return statement != null && statement.netIncome > p.profitThreshold;
        }

        /// <summary>利益率＝純損益 / 総収益（収益に対する純益の割合・収益0なら0）。赤字なら負。</summary>
        public static float ProfitMargin(float revenue, float netIncome)
        {
            float r = Mathf.Max(0f, revenue);
            if (r <= 0f) return 0f;
            return netIncome / r;
        }

        /// <summary>営業利益率＝（収益−営業費用）/ 収益（本業の稼ぐ力・収益0なら0）。</summary>
        public static float OperatingMargin(float revenue, float operatingExpenses)
        {
            float r = Mathf.Max(0f, revenue);
            if (r <= 0f) return 0f;
            float operatingIncome = r - Mathf.Max(0f, operatingExpenses);
            return operatingIncome / r;
        }

        /// <summary>
        /// 利益剰余金の繰越＝当期の純損益から配当を引いて純資産へ積み上げる（P/LとB/Sを繋ぐ・<see cref="BalanceSheetRules"/> へ接続）。
        /// 黒字（純益−配当＞0）は利益剰余金＝純資産に積もり、赤字は食い潰す（剰余金は負＝累積赤字も許容）。
        /// </summary>
        public static float RetainedEarningsTick(float retainedEarnings, float netIncome, float dividends)
        {
            // 純益から配当（非負クランプ）を払った残りを繰越利益剰余金へ加算。赤字や過大配当で剰余金は負になりうる。
            return retainedEarnings + netIncome - Mathf.Max(0f, dividends);
        }
    }
}
