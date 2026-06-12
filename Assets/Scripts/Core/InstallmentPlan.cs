namespace Ginei
{
    /// <summary>
    /// 割賦（分割）契約（#1996 SHIN・純データ）。購入額・分割手数料率・分割回数。信販会社が加盟店へ立替え、消費者から
    /// 手数料付きで分割回収する（解決は <see cref="CreditFinanceRules"/>）。個別の与信明細は持たず代表契約で集計（タイクン化回避）。
    /// </summary>
    [System.Serializable]
    public class InstallmentPlan
    {
        public string name = "割賦契約";
        public Faction borrower;

        /// <summary>購入額（元本）。</summary>
        public float principal;

        /// <summary>分割手数料率（消費者が払う上乗せ＝信販の収益）。</summary>
        public float feeRate = CreditFinanceRules.DefaultInstallmentFeeRate;

        /// <summary>分割回数。</summary>
        public int termPeriods = 1;

        public InstallmentPlan() { }

        public InstallmentPlan(float principal, float feeRate, int termPeriods, Faction borrower = default, string name = null)
        {
            this.principal = principal;
            this.feeRate = feeRate;
            this.termPeriods = termPeriods;
            this.borrower = borrower;
            if (!string.IsNullOrEmpty(name)) this.name = name;
        }
    }
}
