namespace Ginei
{
    /// <summary>
    /// 中央銀行／FRB（#1945 CBNK・純データ）。金融政策の主体＝勢力ごとに1つ。政策金利（市場の基準金利 #161/#163 の出所）・
    /// インフレ目標・マネーサプライ・法定準備率（信用創造 #186）・独立性（政治圧力への耐性 CB-5）を持つ。
    /// 解決は <see cref="MonetaryPolicyRules"/>（唯一の窓口）。少数集約＝勢力レベルのマクロ近似（タイクン化回避）。
    /// </summary>
    [System.Serializable]
    public class CentralBank
    {
        public string name = "中央銀行";
        public Faction faction;

        /// <summary>政策金利（市場の基準金利＝<see cref="FiscalRules.InterestRate"/> の baseInterestRate / 国債利回り #161 の出所）。</summary>
        public float policyRate = 0.02f;

        /// <summary>インフレ目標（テイラー則の基準＝既定2%）。</summary>
        public float inflationTarget = 0.02f;

        /// <summary>マネーサプライ（流通する貨幣量。公開市場操作で増減＝CB-3）。</summary>
        public float moneySupply = 1000f;

        /// <summary>法定準備率（銀行が積む準備の割合＝信用創造 #186 を絞る）。</summary>
        public float reserveRequirement = 0.1f;

        /// <summary>独立性（0..1）。高いほどテイラー則どおり、低いほど政府の政治圧力に従う（CB-5）。</summary>
        public float independence = 0.7f;

        public CentralBank() { }

        public CentralBank(string name, float policyRate = 0.02f, float inflationTarget = 0.02f,
            float moneySupply = 1000f, float reserveRequirement = 0.1f, float independence = 0.7f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "中央銀行" : name;
            this.policyRate = policyRate;
            this.inflationTarget = inflationTarget;
            this.moneySupply = moneySupply;
            this.reserveRequirement = reserveRequirement;
            this.independence = independence;
            this.faction = faction;
        }
    }
}
