namespace Ginei
{
    /// <summary>
    /// 金融機関（#1939 リーマンショック・純データ）。銀行 <see cref="Bank"/>(預金/貸出 #186)より<b>危機の主体</b>に焦点：
    /// 自己資本（損失のクッション）・総資産・証券化商品エクスポージャ（サブプライム露呈で毀損 <see cref="SubprimeRules.RevealLoss"/>）・
    /// インターバンク相互接続度（カウンターパーティ＝伝染の経路）・too-big-to-fail。解決は <see cref="FinancialCrisisRules"/>。
    /// 少数集約（タイクン化回避＝全行列のエクスポージャ行列は持たない）。
    /// </summary>
    [System.Serializable]
    public class FinancialInstitution
    {
        public string name = "金融機関";
        public Faction faction;

        /// <summary>自己資本（損失を吸収するクッション。マイナス＝債務超過＝破綻）。</summary>
        public float capital = 100f;

        /// <summary>総資産。レバレッジ＝資産/自己資本。</summary>
        public float assets = 1000f;

        /// <summary>証券化商品(MBS/CDO)への投資額。サブプライム露呈で評価損になる。</summary>
        public float mbsExposure = 0f;

        /// <summary>インターバンク相互接続度（0..1）。高いほど他行の破綻が伝染しやすい（カウンターパーティリスク）。</summary>
        public float interbankLinkage = 0.3f;

        /// <summary>大きすぎて潰せない（システム上重要＝救済対象）。</summary>
        public bool tooBigToFail = false;

        public FinancialInstitution() { }

        public FinancialInstitution(string name, float capital, float assets, float mbsExposure = 0f,
            float interbankLinkage = 0.3f, bool tooBigToFail = false, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "金融機関" : name;
            this.capital = capital;
            this.assets = assets;
            this.mbsExposure = mbsExposure;
            this.interbankLinkage = interbankLinkage;
            this.tooBigToFail = tooBigToFail;
            this.faction = faction;
        }
    }
}
