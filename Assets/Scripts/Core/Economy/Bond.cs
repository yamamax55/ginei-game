namespace Ginei
{
    /// <summary>
    /// 債券＝借金の証書（#161/#185 債券市場システム基盤・純データ）。株式（出資 <see cref="Listing"/>）に対し<b>債券は借入</b>。
    /// 発行体（国＝国債/企業＝社債）が額面で資本を調達し、表面利率の利息を払い満期に元本を返す。<b>価格は利回りと逆相関</b>
    /// （市場金利が上がると既発債の価格は下がる＝利回りが上がる）、<b>信用リスクが高いほど価格↓利回り↑</b>。解決は <see cref="BondMarketRules"/>。
    /// 価格は額面比（1.0＝額面どおり）。少数集約（タイクン化回避）。
    /// </summary>
    [System.Serializable]
    public class Bond
    {
        public string name = "債券";
        public Faction issuer;

        /// <summary>額面残高（満期に返す元本の総額）。発行で増える。</summary>
        public float faceValue = 1000f;

        /// <summary>表面利率（額面に対する年利＝毎年払う利息の率）。</summary>
        public float couponRate = 0.05f;

        /// <summary>市場価格（額面比・1.0＝額面どおり）。金利/信用で上下し利回りと逆相関。</summary>
        public float price = 1f;

        /// <summary>信用リスク（0..1・デフォルト可能性）。高いほど利回り上乗せ＝価格↓。</summary>
        public float defaultRisk = 0f;

        public Bond() { }

        public Bond(Faction issuer, float faceValue, float couponRate, float price = 1f, float defaultRisk = 0f, string name = "債券")
        {
            this.issuer = issuer;
            this.faceValue = faceValue;
            this.couponRate = couponRate;
            this.price = price;
            this.defaultRisk = defaultRisk;
            this.name = string.IsNullOrEmpty(name) ? "債券" : name;
        }
    }
}
