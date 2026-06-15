namespace Ginei
{
    /// <summary>
    /// 株式市場の上場銘柄（#185 株式市場システム基盤・純データ）。<b>操業企業</b>（<see cref="Enterprise"/>＝利潤の源）と
    /// <b>株価の顔</b>（<see cref="Company"/>）を<b>発行済み株式数</b>で結ぶ＝実体経済↔株価の連結。企業の利潤が1株あたり収益(EPS)・配当になり、
    /// 増資（公募）で調達した資本は企業の生産基盤へ投下される。解決は <see cref="StockMarketSystemRules"/>。
    /// </summary>
    [System.Serializable]
    public class Listing
    {
        public string name = "上場企業";

        /// <summary>操業企業（収益の源）。</summary>
        [System.NonSerialized] public Enterprise enterprise;

        /// <summary>株価の顔（評価・収束は <see cref="StockMarketRules"/>）。</summary>
        public Company stock;

        /// <summary>発行済み株式数（時価総額＝株価×これ。増資で増え希薄化する）。</summary>
        public float shares = 100f;

        public Listing() { }

        public Listing(Enterprise enterprise, Company stock, float shares = 100f, string name = "上場企業")
        {
            this.enterprise = enterprise;
            this.stock = stock;
            this.shares = shares;
            this.name = string.IsNullOrEmpty(name) ? "上場企業" : name;
        }
    }
}
