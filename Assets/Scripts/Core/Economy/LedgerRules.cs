using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>勘定科目の類型（複式簿記の5要素・#974）。正常残高側は <see cref="LedgerRules.NormalBalanceSide"/> が決める。</summary>
    public enum AccountType { 資産, 負債, 純資産, 収益, 費用 }

    /// <summary>
    /// 仕訳（複式簿記の1取引・#974）。1取引は必ず2つの勘定に等額で記録される＝借方=貸方。純データ。
    /// 借方勘定・貸方勘定・金額を保持。金額は負にならない（クランプ）。
    /// </summary>
    public class JournalEntry
    {
        public AccountType debitAccount;  // 借方勘定
        public AccountType creditAccount; // 貸方勘定
        public float amount;              // 取引額（借方=貸方の共通額）

        public JournalEntry() { }

        public JournalEntry(AccountType debitAccount, AccountType creditAccount, float amount)
        {
            this.debitAccount = debitAccount;
            this.creditAccount = creditAccount;
            this.amount = Mathf.Max(0f, amount);
        }
    }

    /// <summary>
    /// 複式簿記＝勘定体系＋仕訳エンジンの純ロジック（#974・唯一の窓口）。
    /// 経済活動を借方/貸方の仕訳で記録し、常に <b>Σ借方=Σ貸方</b> が成り立つことを検証する＝会計整合性の中核。
    /// <see cref="FiscalRules"/>（歳入歳出の集計・PB/債務/金利）とは別＝こちらは<b>記帳の整合性エンジン</b>。
    /// 残高（BalanceSheetRules）・損益（IncomeStatementRules）の財務諸表（バックログ）はこの台帳の上に乗る。test-first。
    /// </summary>
    public static class LedgerRules
    {
        /// <summary>台帳の調整値（マジックナンバー禁止＝集約）。</summary>
        public readonly struct LedgerParams
        {
            public readonly float balanceTolerance; // 貸借一致の許容誤差（浮動小数の丸め吸収）

            public LedgerParams(float balanceTolerance)
            {
                this.balanceTolerance = Mathf.Max(0f, balanceTolerance);
            }

            /// <summary>既定＝貸借一致の許容誤差0.001。</summary>
            public static LedgerParams Default => new LedgerParams(0.001f);
        }

        /// <summary>勘定の正常残高側が借方か（資産・費用＝借方／負債・純資産・収益＝貸方）。</summary>
        public static bool NormalBalanceSide(AccountType type)
            => type == AccountType.資産 || type == AccountType.費用;

        /// <summary>個別仕訳の貸借一致（借方=貸方）。金額は非負ゆえ常に true 相当だが、明示的窓口として提供。</summary>
        public static bool IsBalanced(JournalEntry entry)
        {
            // 1仕訳の借方額＝貸方額＝amount（複式簿記では同一取引額を両側に立てる）。負は無効。
            return entry != null && entry.amount >= 0f;
        }

        /// <summary>総借方＝全仕訳の借方額の合計（各仕訳の amount を加算）。</summary>
        public static float TotalDebits(IReadOnlyList<JournalEntry> entries)
        {
            if (entries == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                JournalEntry e = entries[i];
                if (e != null) sum += Mathf.Max(0f, e.amount);
            }
            return sum;
        }

        /// <summary>総貸方＝全仕訳の貸方額の合計（複式簿記では総借方と一致するはず）。</summary>
        public static float TotalCredits(IReadOnlyList<JournalEntry> entries)
        {
            if (entries == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                JournalEntry e = entries[i];
                if (e != null) sum += Mathf.Max(0f, e.amount);
            }
            return sum;
        }

        /// <summary>台帳全体の貸借一致＝<b>Σ借方=Σ貸方</b>（複式簿記の根本原理・#974）。</summary>
        public static bool IsLedgerBalanced(IReadOnlyList<JournalEntry> entries, LedgerParams p)
        {
            return Mathf.Abs(TotalDebits(entries) - TotalCredits(entries)) <= p.balanceTolerance;
        }

        /// <summary>
        /// 特定勘定の残高（その勘定が借方/貸方どちらに現れたかで符号を決める）。
        /// 借方科目（資産・費用）＝借方計上−貸方計上、貸方科目（負債・純資産・収益）＝貸方計上−借方計上。
        /// 正常残高側で計上されれば残高は正になる。
        /// </summary>
        public static float AccountBalance(IReadOnlyList<JournalEntry> entries, AccountType account)
        {
            if (entries == null) return 0f;
            float debitTotal = 0f;  // この勘定が借方に立った額
            float creditTotal = 0f; // この勘定が貸方に立った額
            for (int i = 0; i < entries.Count; i++)
            {
                JournalEntry e = entries[i];
                if (e == null) continue;
                float amt = Mathf.Max(0f, e.amount);
                if (e.debitAccount == account) debitTotal += amt;
                if (e.creditAccount == account) creditTotal += amt;
            }
            // 正常残高側に合わせて符号を取る（正常側計上で残高が正）。
            return NormalBalanceSide(account) ? debitTotal - creditTotal : creditTotal - debitTotal;
        }

        /// <summary>
        /// 試算表の整合＝全勘定の借方残高合計=貸方残高合計（#974）。
        /// 各勘定の残高を正常側で求め、正なら自勘定の正常側へ、負なら反対側へ振り分けて両合計を比べる。
        /// 仕訳が貸借一致していれば必ず一致する＝記帳ミスの検出窓口。
        /// </summary>
        public static bool TrialBalance(IReadOnlyList<JournalEntry> entries, LedgerParams p)
        {
            if (entries == null) return true;
            float debitSide = 0f;
            float creditSide = 0f;
            // 5勘定すべてを走査（AccountType の全要素）。
            for (int t = 0; t <= (int)AccountType.費用; t++)
            {
                AccountType acc = (AccountType)t;
                float bal = AccountBalance(entries, acc);
                bool normalDebit = NormalBalanceSide(acc);
                if (bal >= 0f)
                {
                    // 正常側に残高が立つ。
                    if (normalDebit) debitSide += bal; else creditSide += bal;
                }
                else
                {
                    // 反対側に立つ（残高が負＝正常側と逆）。
                    if (normalDebit) creditSide += -bal; else debitSide += -bal;
                }
            }
            return Mathf.Abs(debitSide - creditSide) <= p.balanceTolerance;
        }
    }
}
