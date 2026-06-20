namespace Ginei
{
    /// <summary>
    /// 証券会社（投資銀行・ブローカー・#1963 SEC・純データ）。発行体（資本を募る企業 #185/政府 #161）と投資家をつなぐ金融仲介。
    /// 銀行 <see cref="Bank"/>(預金/貸出 #186)と違い<b>仲介手数料・引受・自己売買</b>で稼ぐ：委託手数料の母数＝預かり資産、
    /// 引受・自己売買で抱える在庫（相場が崩れると評価損＝危機 #1939 の急所）、自己資本（在庫リスクのクッション）。
    /// 解決は <see cref="SecuritiesFirmRules"/>（唯一の窓口）。少数集約（タイクン化回避＝注文板は持たない）。
    /// </summary>
    [System.Serializable]
    public class SecuritiesFirm
    {
        public string name = "証券会社";
        public Faction faction;

        /// <summary>自己資本（在庫評価損を吸収するクッション。マイナス＝債務超過＝破綻）。</summary>
        public float capital = 100f;

        /// <summary>保有在庫（自己売買・引受の売れ残りで抱える証券の評価額。相場変動で評価損益が出る）。</summary>
        public float inventory = 0f;

        /// <summary>預かり資産（顧客の証券残高＝委託手数料 SEC-1 の母数）。</summary>
        public float clientAssets = 0f;

        /// <summary>委託売買手数料率（取引額に対する割合）。</summary>
        public float commissionRate = SecuritiesFirmRules.DefaultCommissionRate;

        /// <summary>引受手数料率（発行額に対する割合＝SEC-2）。</summary>
        public float underwritingFeeRate = SecuritiesFirmRules.DefaultUnderwritingFeeRate;

        /// <summary>ビッド・アスク・スプレッド（マーケットメイクの値差＝SEC-3）。</summary>
        public float bidAskSpread = SecuritiesFirmRules.DefaultBidAskSpread;

        public SecuritiesFirm() { }

        public SecuritiesFirm(string name, float capital, float inventory = 0f, float clientAssets = 0f,
            float commissionRate = SecuritiesFirmRules.DefaultCommissionRate,
            float underwritingFeeRate = SecuritiesFirmRules.DefaultUnderwritingFeeRate,
            float bidAskSpread = SecuritiesFirmRules.DefaultBidAskSpread, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "証券会社" : name;
            this.capital = capital;
            this.inventory = inventory;
            this.clientAssets = clientAssets;
            this.commissionRate = commissionRate;
            this.underwritingFeeRate = underwritingFeeRate;
            this.bidAskSpread = bidAskSpread;
            this.faction = faction;
        }
    }
}
