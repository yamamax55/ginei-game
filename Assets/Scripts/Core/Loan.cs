namespace Ginei
{
    /// <summary>ローンの種別。プライム＝優良借り手（低リスク）／サブプライム＝信用力の低い借り手（高リスク）。</summary>
    public enum LoanType { プライム, サブプライム }

    /// <summary>
    /// 住宅ローン（#185 サブプライム・純データ）。プライム（優良）/サブプライム（信用力低）の借入。証券化（<see cref="MortgageBundle"/>）で
    /// 束ねられ、格付け会社（<see cref="CreditRatingRules"/>）が格付けする。サブプライムは個別には高リスク＝本来ジャンク。解決は <see cref="SubprimeRules"/>。
    /// </summary>
    [System.Serializable]
    public class Loan
    {
        public LoanType type = LoanType.プライム;

        /// <summary>元本。</summary>
        public float principal = 100f;

        /// <summary>借り手のデフォルトリスク（0..1）。サブプライムは高い。</summary>
        public float defaultRisk = SubprimeRules.PrimeDefaultRisk;

        public Loan() { }

        public Loan(LoanType type, float principal, float defaultRisk)
        {
            this.type = type;
            this.principal = principal;
            this.defaultRisk = defaultRisk;
        }

        /// <summary>種別の既定リスクでローンを作る（プライム/サブプライムの代表値）。</summary>
        public static Loan Of(LoanType type, float principal)
            => new Loan(type, principal, SubprimeRules.DefaultRiskFor(type));
    }
}
