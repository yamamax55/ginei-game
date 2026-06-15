namespace Ginei
{
    /// <summary>
    /// 小売り（小売業・#2017・純データ）。完成品を仕入れて消費者に売る B2C 企業。メーカー（#2016 作る）・商社（#1027 B2B仲介）
    /// とは別＝経済の出口（最終消費）。店舗数・在庫・値入率（マークアップ）・仕入規模（バイイングパワーの源）を持つ。
    /// 薄利多売・在庫回転・規模で安く仕入れる力が利益を左右する。解決は <see cref="RetailRules"/>。少数集約（タイクン化回避）。
    /// </summary>
    [System.Serializable]
    public class Retailer
    {
        public string name = "小売";
        public Faction faction;

        /// <summary>店舗数（商圏需要を取り込む拠点）。</summary>
        public int storeCount = 1;

        /// <summary>在庫（仕入れて店頭に並べた商品の数量）。</summary>
        public float inventory = 0f;

        /// <summary>値入率（マークアップ＝仕入原価に対する上乗せ率）。</summary>
        public float markupRate = RetailRules.DefaultMarkupRate;

        /// <summary>仕入規模（年間仕入数量＝バイイングパワーの源。大きいほど安く仕入れる）。</summary>
        public float purchaseVolume = 0f;

        public Retailer() { }

        public Retailer(string name, int storeCount = 1, float inventory = 0f,
            float markupRate = RetailRules.DefaultMarkupRate, float purchaseVolume = 0f, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "小売" : name;
            this.storeCount = storeCount;
            this.inventory = inventory;
            this.markupRate = markupRate;
            this.purchaseVolume = purchaseVolume;
            this.faction = faction;
        }
    }
}
