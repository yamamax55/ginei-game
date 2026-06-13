namespace Ginei
{
    /// <summary>信託の種別（#2003 TRST）。金銭・年金・投資・不動産。種別ごとに運用と分配の扱いが変わる。</summary>
    public enum TrustType { 金銭信託, 年金信託, 投資信託, 不動産信託 }

    /// <summary>
    /// 信託（#2003 TRST・純データ）。委託者が資産（元本）を受託者（信託銀行）へ託し、受益者のために運用させる。種別・元本・
    /// 元本保証の有無を持つ。<b>分別管理</b>で受託者の固有財産と分離される（倒産隔離）。解決は <see cref="TrustBankRules"/>。少数集約。
    /// </summary>
    [System.Serializable]
    public class Trust
    {
        public string name = "信託";
        public Faction beneficiary;

        /// <summary>信託の種別。</summary>
        public TrustType trustType = TrustType.金銭信託;

        /// <summary>信託元本（委託者が託した資産）。</summary>
        public float principal;

        /// <summary>元本保証付きか（合同運用指定金銭信託＝信託銀行が一定利回りを約束）。false＝実績配当。</summary>
        public bool isPrincipalGuaranteed = false;

        public Trust() { }

        public Trust(float principal, TrustType trustType = TrustType.金銭信託, bool isPrincipalGuaranteed = false,
            Faction beneficiary = default, string name = null)
        {
            this.principal = principal;
            this.trustType = trustType;
            this.isPrincipalGuaranteed = isPrincipalGuaranteed;
            this.beneficiary = beneficiary;
            if (!string.IsNullOrEmpty(name)) this.name = name;
        }
    }
}
