namespace Ginei
{
    /// <summary>
    /// 信販会社（#1996 SHIN・純データ）。消費者信用の金融＝割賦債権・カード債権・信用保証残高を抱え、手数料・金利・保証料で
    /// 稼ぐ。銀行（#186/#1976）・リース（#1989）・証券（#1963）とは別の archetype＝個人への少額・多数の与信。自己資本は
    /// 貸倒れのクッション。解決は <see cref="CreditFinanceRules"/>。少数集約（タイクン化回避）。
    /// </summary>
    [System.Serializable]
    public class CreditCompany
    {
        public string name = "信販会社";
        public Faction faction;

        /// <summary>自己資本（貸倒れを吸収するクッション）。</summary>
        public float capital = 100f;

        /// <summary>割賦債権残高（立替えて未回収の分割債権）。</summary>
        public float installmentReceivables = 0f;

        /// <summary>カード債権残高（リボ含む未回収のカード利用残高）。</summary>
        public float cardBalance = 0f;

        /// <summary>信用保証残高（保証している銀行ローンの残高＝at risk）。</summary>
        public float guaranteedLoans = 0f;

        public CreditCompany() { }

        public CreditCompany(string name, float capital, float installmentReceivables = 0f,
            float cardBalance = 0f, float guaranteedLoans = 0f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "信販会社" : name;
            this.capital = capital;
            this.installmentReceivables = installmentReceivables;
            this.cardBalance = cardBalance;
            this.guaranteedLoans = guaranteedLoans;
            this.faction = faction;
        }
    }
}
