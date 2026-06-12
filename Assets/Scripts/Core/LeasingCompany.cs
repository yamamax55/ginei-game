namespace Ginei
{
    /// <summary>
    /// リース会社（#1989 LEAS・純データ）。資産を取得して貸し出し、リース料で稼ぐ。自己資本・リース資産簿価（貸し出し中の資産）・
    /// リース債権残高（未回収のリース料）を持つ。資産購入の資金は銀行 #1976/債券 #161 で調達。解決は <see cref="LeasingRules"/>。
    /// </summary>
    [System.Serializable]
    public class LeasingCompany
    {
        public string name = "リース会社";
        public Faction faction;

        /// <summary>自己資本（残価損・貸倒れを吸収するクッション）。</summary>
        public float capital = 100f;

        /// <summary>リース資産簿価（貸し出し中の資産の帳簿価額）。</summary>
        public float leasedAssetsValue = 0f;

        /// <summary>リース債権残高（これから受け取るリース料の未回収分）。</summary>
        public float outstandingReceivables = 0f;

        public LeasingCompany() { }

        public LeasingCompany(string name, float capital, float leasedAssetsValue = 0f,
            float outstandingReceivables = 0f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "リース会社" : name;
            this.capital = capital;
            this.leasedAssetsValue = leasedAssetsValue;
            this.outstandingReceivables = outstandingReceivables;
            this.faction = faction;
        }
    }
}
