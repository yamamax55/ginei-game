namespace Ginei
{
    /// <summary>
    /// 複式簿記の集計ロジック（純・決定論）。具体科目の仕訳帳（<see cref="PersonalLedger"/>）から勘定残高・月次増減・
    /// 月次の借方/貸方合計（貸借一致の検証）を算出する唯一の窓口＋共通取引の記帳ヘルパ（購入/慰労/期首）。
    /// 会計の正常残高側は既存 <see cref="LedgerRules.NormalBalanceSide"/> へ委譲（重複しない）。具体科目→5区分は <see cref="ClassOf"/>。
    /// プレイヤーの私有財産の動きを <see cref="PersonalBookkeepingOverlay"/> が月次で観測するのに使う。test-first。
    /// </summary>
    public static class BookkeepingRules
    {
        /// <summary>具体科目→会計区分（既存 <see cref="AccountType"/> の5要素へ対応）。</summary>
        public static AccountType ClassOf(LedgerAccount a)
        {
            switch (a)
            {
                case LedgerAccount.現金:
                case LedgerAccount.私兵: return AccountType.資産;
                case LedgerAccount.借入金: return AccountType.負債;
                case LedgerAccount.純資産: return AccountType.純資産;
                case LedgerAccount.俸給: return AccountType.収益;
                default: return AccountType.費用; // 慰労費・商人口銭
            }
        }

        /// <summary>借方記入で残高が増える科目か（資産・費用＝借方正常）。判定は <see cref="LedgerRules.NormalBalanceSide"/> へ委譲。</summary>
        public static bool DebitIncreasesBalance(LedgerAccount a) => LedgerRules.NormalBalanceSide(ClassOf(a));

        /// <summary>仕訳の借方合計。</summary>
        public static long EntryDebitTotal(LedgerEntry e)
        {
            if (e == null) return 0;
            long s = 0;
            for (int i = 0; i < e.legs.Count; i++) if (e.legs[i].isDebit) s += e.legs[i].amount;
            return s;
        }

        /// <summary>仕訳の貸方合計。</summary>
        public static long EntryCreditTotal(LedgerEntry e)
        {
            if (e == null) return 0;
            long s = 0;
            for (int i = 0; i < e.legs.Count; i++) if (!e.legs[i].isDebit) s += e.legs[i].amount;
            return s;
        }

        /// <summary>仕訳が貸借一致（複式簿記の整合）か。</summary>
        public static bool IsBalanced(LedgerEntry e) => EntryDebitTotal(e) == EntryCreditTotal(e);

        /// <summary>科目の残高（uptoMonth 以前を累計・正常側を正とする符号付き）。</summary>
        public static long Balance(PersonalLedger ledger, LedgerAccount account, int uptoMonthInclusive)
        {
            if (ledger == null) return 0;
            bool debitPos = DebitIncreasesBalance(account);
            long bal = 0;
            for (int i = 0; i < ledger.entries.Count; i++)
            {
                LedgerEntry e = ledger.entries[i];
                if (e == null || e.month > uptoMonthInclusive) continue;
                for (int j = 0; j < e.legs.Count; j++)
                {
                    JournalLeg leg = e.legs[j];
                    if (leg.account != account) continue;
                    bal += (leg.isDebit == debitPos) ? leg.amount : -leg.amount;
                }
            }
            return bal;
        }

        /// <summary>その月の科目の増減（正常側を正）。</summary>
        public static long MonthlyMovement(PersonalLedger ledger, LedgerAccount account, int month)
        {
            if (ledger == null) return 0;
            bool debitPos = DebitIncreasesBalance(account);
            long delta = 0;
            for (int i = 0; i < ledger.entries.Count; i++)
            {
                LedgerEntry e = ledger.entries[i];
                if (e == null || e.month != month) continue;
                for (int j = 0; j < e.legs.Count; j++)
                {
                    JournalLeg leg = e.legs[j];
                    if (leg.account != account) continue;
                    delta += (leg.isDebit == debitPos) ? leg.amount : -leg.amount;
                }
            }
            return delta;
        }

        /// <summary>その月の借方合計（全科目）。貸方合計と一致するはず（複式の検算）。</summary>
        public static long MonthlyDebitTotal(PersonalLedger ledger, int month)
        {
            if (ledger == null) return 0;
            long s = 0;
            for (int i = 0; i < ledger.entries.Count; i++)
            {
                LedgerEntry e = ledger.entries[i];
                if (e != null && e.month == month) s += EntryDebitTotal(e);
            }
            return s;
        }

        /// <summary>その月の貸方合計（全科目）。</summary>
        public static long MonthlyCreditTotal(PersonalLedger ledger, int month)
        {
            if (ledger == null) return 0;
            long s = 0;
            for (int i = 0; i < ledger.entries.Count; i++)
            {
                LedgerEntry e = ledger.entries[i];
                if (e != null && e.month == month) s += EntryCreditTotal(e);
            }
            return s;
        }

        // ── 記帳ヘルパ（共通取引を貸借一致の仕訳にして Post する） ──

        /// <summary>期首残高（手元の現金を 純資産 を相手に建てる）。仕訳帳が空のときだけ呼ぶ運用。</summary>
        public static void PostOpeningCash(PersonalLedger ledger, int month, long cash)
        {
            if (ledger == null || cash <= 0) return;
            ledger.Post(new LedgerEntry(month, "期首残高").Debit(LedgerAccount.現金, cash).Credit(LedgerAccount.純資産, cash));
        }

        /// <summary>私兵艦艇の購入＝私兵(資産)＋口銭(費用) を 現金/借入金 で賄う（貸借一致）。</summary>
        public static void PostShipPurchase(PersonalLedger ledger, int month, long assetValue, long commission, long cashPaid, long borrowed, string memo)
        {
            if (ledger == null) return;
            var e = new LedgerEntry(month, string.IsNullOrEmpty(memo) ? "私兵艦艇の購入" : memo);
            e.Debit(LedgerAccount.私兵, assetValue);
            e.Debit(LedgerAccount.商人口銭, commission);
            e.Credit(LedgerAccount.現金, cashPaid);
            e.Credit(LedgerAccount.借入金, borrowed);
            ledger.Post(e);
        }

        /// <summary>艦隊慰労＝慰労費(費用) を 現金 で支払う。</summary>
        public static void PostRelief(PersonalLedger ledger, int month, long cost, string memo)
        {
            if (ledger == null || cost <= 0) return;
            ledger.Post(new LedgerEntry(month, string.IsNullOrEmpty(memo) ? "艦隊慰労" : memo)
                .Debit(LedgerAccount.慰労費, cost).Credit(LedgerAccount.現金, cost));
        }
    }
}
