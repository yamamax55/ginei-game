namespace Ginei
{
    /// <summary>
    /// 商社＝総合商社（FRM-5 #1027・純データ）。生産（企業 #1022）でも金融（銀行 #186/証券 #1963）でもなく、
    /// <b>調達と販売を仲介し・自己勘定で裁定し・与信と在庫・為替リスクを負い・川上に事業投資する</b>archetype。
    /// 自己資本・在庫（買い付けた財＝価格/為替リスク）・与信（取引先への信用供与）・資源権益/事業投資への出資を持つ。
    /// 計算は <see cref="TradingHouseRules"/> が唯一の窓口。フェザーン #160＝両陣営に売る商社国家の原型。少数集約（タイクン化回避）。
    /// </summary>
    [System.Serializable]
    public class TradingHouse
    {
        public string name = "商社";
        public Faction faction;

        /// <summary>自己資本（在庫評価損・焦げ付きを吸収するクッション）。</summary>
        public float capital = 100f;

        /// <summary>在庫（自己勘定で買い付けた財の評価額。相場・為替が動くと評価損益）。</summary>
        public float inventory = 0f;

        /// <summary>口銭率（仲介手数料率＝取り次いだ取引額に対する割合）。</summary>
        public float commissionRate = TradingHouseRules.DefaultCommissionRate;

        /// <summary>与信残高（取引先への信用供与＝トレードファイナンス）。</summary>
        public float tradeCredit = 0f;

        /// <summary>資源権益への投資額（川上の資源開発に出資し供給を確保＝#178）。</summary>
        public float resourceStakes = 0f;

        /// <summary>事業投資の出資額（川上企業 #1022 へ出資しサプライチェーンを束ねる）。</summary>
        public float businessStakes = 0f;

        public TradingHouse() { }

        public TradingHouse(string name, float capital, float inventory = 0f,
            float commissionRate = TradingHouseRules.DefaultCommissionRate, float tradeCredit = 0f,
            float resourceStakes = 0f, float businessStakes = 0f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "商社" : name;
            this.capital = capital;
            this.inventory = inventory;
            this.commissionRate = commissionRate;
            this.tradeCredit = tradeCredit;
            this.resourceStakes = resourceStakes;
            this.businessStakes = businessStakes;
            this.faction = faction;
        }
    }
}
