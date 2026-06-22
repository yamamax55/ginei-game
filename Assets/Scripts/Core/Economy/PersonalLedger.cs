using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 複式簿記の勘定科目（プレイヤーの私有財産の動きを記帳する具体科目）。会計区分（5要素）は既存の
    /// <see cref="AccountType"/> へ <see cref="BookkeepingRules.ClassOf"/> で対応づける（区分enumは重複定義しない）。
    /// </summary>
    public enum LedgerAccount
    {
        現金,     // 資産：私有財産の現金（Person.wealth）
        私兵,     // 資産：購入した私兵艦艇（Person.privateSoldiers の評価額）
        借入金,   // 負債：私財担保の借金（Person.personalDebt）
        純資産,   // 純資産：期首資本（開始残高の相手勘定）
        俸給,     // 収益：給与など私財の流入
        慰労費,   // 費用：艦隊慰労の支出
        商人口銭, // 費用：艦艇購入の商人手数料
    }

    /// <summary>仕訳の1脚（借方 or 貸方の1行＝具体科目×金額）。</summary>
    public readonly struct JournalLeg
    {
        public readonly LedgerAccount account;
        public readonly bool isDebit; // true=借方／false=貸方
        public readonly long amount;  // 非負

        public JournalLeg(LedgerAccount account, bool isDebit, long amount)
        {
            this.account = account;
            this.isDebit = isDebit;
            this.amount = amount < 0 ? 0 : amount;
        }
    }

    /// <summary>
    /// 仕訳（1取引＝複数脚。借方合計＝貸方合計で釣り合う）。月（暦の月インデックス）と摘要を持つ。
    /// 既存の単脚 <see cref="JournalEntry"/>（5区分・月なし・整合エンジン）に対し、具体科目で多脚・月次の記帳を担う。
    /// </summary>
    public class LedgerEntry
    {
        public int month;          // 暦の月インデックス（記帳した月）
        public string memo = "";   // 摘要
        public readonly List<JournalLeg> legs = new List<JournalLeg>();

        public LedgerEntry() { }
        public LedgerEntry(int month, string memo) { this.month = month; this.memo = memo; }

        public LedgerEntry Debit(LedgerAccount a, long amount) { if (amount > 0) legs.Add(new JournalLeg(a, true, amount)); return this; }
        public LedgerEntry Credit(LedgerAccount a, long amount) { if (amount > 0) legs.Add(new JournalLeg(a, false, amount)); return this; }
    }

    /// <summary>個人の仕訳帳（プレイヤーの私有財産の動きを時系列で蓄える）。集計は <see cref="BookkeepingRules"/>。</summary>
    public class PersonalLedger
    {
        public readonly List<LedgerEntry> entries = new List<LedgerEntry>();

        public void Post(LedgerEntry e) { if (e != null && e.legs.Count > 0) entries.Add(e); }
        public int Count => entries.Count;
        public void Clear() => entries.Clear();
    }
}
