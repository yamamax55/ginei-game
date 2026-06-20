namespace Ginei
{
    /// <summary>
    /// GDP（国内総生産）の支出面の内訳＋物価水準＋潜在GDP（#1951 GDP・純データ）。支出面 GDP＝民間消費 C＋投資 I＋
    /// 政府支出 G＋純輸出(X−M)。物価水準で実質化（GDP-2）、潜在GDPとの差で需給ギャップ（GDP-3）。勢力ごとに1つの
    /// マクロ集約（タイクン化回避＝個別取引の会計は持たない）。解決は <see cref="GdpRules"/>（唯一の窓口）。
    /// </summary>
    [System.Serializable]
    public class GdpAccounts
    {
        public string name = "国民経済計算";
        public Faction faction;

        /// <summary>民間消費（C）。</summary>
        public float consumption = 0f;

        /// <summary>投資（I＝設備投資・在庫・住宅）。</summary>
        public float investment = 0f;

        /// <summary>政府支出（G＝財政 #163 の歳出）。</summary>
        public float government = 0f;

        /// <summary>輸出（X＝交易 #94）。</summary>
        public float exports = 0f;

        /// <summary>輸入（M＝控除項目）。</summary>
        public float imports = 0f;

        /// <summary>物価水準（GDPデフレータの素＝基準1.0。実質化に使う・GDP-2）。</summary>
        public float priceLevel = 1f;

        /// <summary>潜在GDP（完全雇用の産出＝需給ギャップの基準・GDP-3）。0以下は未設定。</summary>
        public float potentialOutput = 0f;

        public GdpAccounts() { }

        public GdpAccounts(float consumption, float investment, float government, float exports, float imports,
            float priceLevel = 1f, float potentialOutput = 0f, Faction faction = default)
        {
            this.consumption = consumption;
            this.investment = investment;
            this.government = government;
            this.exports = exports;
            this.imports = imports;
            this.priceLevel = priceLevel;
            this.potentialOutput = potentialOutput;
            this.faction = faction;
        }
    }
}
