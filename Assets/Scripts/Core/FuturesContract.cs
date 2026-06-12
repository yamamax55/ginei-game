namespace Ginei
{
    /// <summary>
    /// 先物契約（#1933 FUTR・純データ）。<b>将来の受け渡しを今の価格で約束する契約</b>。株式（出資 <see cref="Listing"/>）・
    /// 債券（借入 <see cref="Bond"/>）に対し<b>先物＝価格の予約</b>。ロング(買い)/ショート(売り)で値動きの損益が決まり、現物の反対側を
    /// 持てば<b>ヘッジ</b>（価格リスク固定）、小資金で大ポジションを持てば<b>投機</b>（レバレッジ）。解決は <see cref="FuturesMarketRules"/>。
    /// 少数集約（タイクン化回避＝個別の板/限月ツリーは持たない）。
    /// </summary>
    [System.Serializable]
    public class FuturesContract
    {
        public string underlying = "物資"; // 原資産（物資/弾薬/燃料/株価指数 等）
        public Faction holder;

        /// <summary>約定価格（建てたときの先物価格）。評価損益の基準。</summary>
        public float contractPrice;

        /// <summary>数量（建玉・0以上）。</summary>
        public float quantity = 1f;

        /// <summary>ロング(買い)＝値上がりで利益／ショート(売り)＝値下がりで利益。</summary>
        public bool isLong = true;

        /// <summary>預託した証拠金。</summary>
        public float margin;

        /// <summary>残存期間（年・先物価格の保有コスト計算用）。</summary>
        public float timeToMaturity = 1f;

        public FuturesContract() { }

        public FuturesContract(string underlying, float contractPrice, float quantity, bool isLong,
            float margin = 0f, float timeToMaturity = 1f, Faction holder = default)
        {
            this.underlying = string.IsNullOrEmpty(underlying) ? "物資" : underlying;
            this.contractPrice = contractPrice;
            this.quantity = quantity;
            this.isLong = isLong;
            this.margin = margin;
            this.timeToMaturity = timeToMaturity;
            this.holder = holder;
        }
    }
}
