namespace Ginei
{
    /// <summary>金融商品の種類（NFIN-1・#2070）。株式（配当）／債券（クーポン）／投資信託（分配金）。</summary>
    public enum FinancialInstrument { 株式, 債券, 投資信託 }

    /// <summary>
    /// 金融資産の保有持分（NFIN-1・#2070・純データ・後方互換）。
    /// 所有者（人物/国家＝<see cref="AssetOwnerKind"/>#2063 再利用）が原資産（株#185/債券#161/投資信託#2003）の
    /// <b>口数（units）を保有</b>し、配当・クーポン・分配金（<see cref="incomePerUnit"/>）を年次で受け取る。
    /// 原資産価格が暴落#185 で0に張り付くと時価が0＝<b>紙くず化</b>（<see cref="FinancialAssetRules.IsWorthless"/>）。
    /// 評価/収益は <see cref="FinancialAssetRules"/>。test-first。
    /// </summary>
    public class FinancialHolding
    {
        public int id;
        public FinancialInstrument instrument;
        public int underlyingId;        // 原資産のid（Listing#185/Bond#161/Trust#2003）
        public string underlyingName;   // 銘柄名（表示用）

        // --- 所有者（人物 or 国家・NamedAsset と同じ表現） ---
        public AssetOwnerKind ownerKind = AssetOwnerKind.人物;
        public int ownerPersonId;
        public Faction ownerFaction;

        public float units;             // 口数（保有量）
        public float unitPrice;         // 時価（1口あたり）
        public float incomePerUnit;     // 配当/クーポン/分配（1口・年あたり）
        public float bookCost;          // 取得原価（含み損益の基準）

        public FinancialHolding() { }

        public FinancialHolding(int id, FinancialInstrument instrument, string underlyingName)
        {
            this.id = id;
            this.instrument = instrument;
            this.underlyingName = underlyingName;
        }

        /// <summary>人物所有か。</summary>
        public bool IsPersonOwned => ownerKind == AssetOwnerKind.人物;

        /// <summary>国家所有か。</summary>
        public bool IsFactionOwned => ownerKind == AssetOwnerKind.国家;

        /// <summary>所有者キー（集計用）。</summary>
        public string OwnerKey => IsPersonOwned ? $"P:{ownerPersonId}" : $"F:{ownerFaction}";
    }
}
