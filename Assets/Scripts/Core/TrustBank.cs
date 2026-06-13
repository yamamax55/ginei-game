namespace Ginei
{
    /// <summary>
    /// 信託銀行（#2003 TRST・純データ）。受託者として他人の資産を運用し信託報酬を得る＋銀行業務を併営する。固有財産
    /// （自己資本）と<b>受託資産（AUM＝信託財産・分別管理で別勘定）</b>を分けて持つ。信託財産は倒産隔離されるので、自己資本が
    /// 飛んでも受益者の資産は守られる。解決は <see cref="TrustBankRules"/>。少数集約（タイクン化回避）。
    /// </summary>
    [System.Serializable]
    public class TrustBank
    {
        public string name = "信託銀行";
        public Faction faction;

        /// <summary>固有財産＝自己資本（信託銀行自身の資本。受託資産とは分別管理）。</summary>
        public float capital = 100f;

        /// <summary>受託資産残高（AUM＝預かって運用している信託財産の総額・分別管理）。</summary>
        public float assetsUnderManagement = 0f;

        /// <summary>運用報酬率（AUM に対する信託報酬の割合）。</summary>
        public float trustFeeRate = TrustBankRules.DefaultTrustFeeRate;

        public TrustBank() { }

        public TrustBank(string name, float capital, float assetsUnderManagement = 0f,
            float trustFeeRate = TrustBankRules.DefaultTrustFeeRate, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "信託銀行" : name;
            this.capital = capital;
            this.assetsUnderManagement = assetsUnderManagement;
            this.trustFeeRate = trustFeeRate;
            this.faction = faction;
        }
    }
}
