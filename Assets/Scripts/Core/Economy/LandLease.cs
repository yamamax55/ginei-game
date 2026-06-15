namespace Ginei
{
    /// <summary>
    /// 土地の賃貸借契約（#2019 惑星土地の貸出基盤・純データ）。<b>貸主（lessor）が所有権を保ったまま借主（lessee）へ土地の用益を貸す</b>＝
    /// 借主は年地代（<see cref="annualRent"/>）を払い、貸主が受け取る。区画（<see cref="systemId"/>×<see cref="landUse"/>×<see cref="zoned"/>）の
    /// 持分（<see cref="share"/>）を対象に、期間（<see cref="termYears"/>）で契約する。解決は <see cref="LandTransactionRules"/>・台帳は <see cref="LandLeaseRegistry"/>。
    /// </summary>
    public class LandLease
    {
        public int id;
        public int systemId;
        public bool zoned;              // 用途ゾーンのリースか（landUse 有効）
        public LandUseType landUse;     // 対象の地目（zoned のとき）

        // 貸主（所有者）／借主（用益者）＝人物/国家/企業
        public AssetOwnerKind lessorKind;
        public int lessorPersonId; public Faction lessorFaction; public int lessorEnterpriseId;
        public AssetOwnerKind lesseeKind;
        public int lesseePersonId; public Faction lesseeFaction; public int lesseeEnterpriseId;

        public float share;             // 対象の持分（0..1）
        public float annualRent;        // 年地代（貸主が受け取り借主が払う）
        public int termYears;           // 契約期間（年）
        public int startYear;           // 契約開始年（暦・期限判定の基準）

        public LandLease() { }

        public OwnerRef Lessor => new OwnerRef(lessorKind, lessorPersonId, lessorFaction, lessorEnterpriseId);
        public OwnerRef Lessee => new OwnerRef(lesseeKind, lesseePersonId, lesseeFaction, lesseeEnterpriseId);
    }
}
