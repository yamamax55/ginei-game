namespace Ginei
{
    /// <summary>
    /// 資産担保融資の契約（#186/#2070 資産担保・純データ）。借り手が<b>資産（土地等）を担保に</b>貸し手から借り入れる＝
    /// 借入残高（<see cref="principal"/>）・金利・担保評価（<see cref="collateralValue"/>＝呼び側が時価で更新）。担保価値が借入を割ると
    /// <b>差し押さえ（foreclosure）</b>。所有者は人物/国家/企業（<see cref="OwnerRef"/>）。解決は <see cref="CollateralLoanRules"/>・台帳は
    /// <see cref="CollateralLoanRegistry"/>。担保は土地権利証（<see cref="pledgedSystemId"/> の借り手の deed）を想定（差し押さえで貸し手へ移る）。
    /// </summary>
    public class CollateralLoan
    {
        public int id;

        // 借り手／貸し手（人物/国家/企業）
        public AssetOwnerKind borrowerKind;
        public int borrowerPersonId; public Faction borrowerFaction; public int borrowerEnterpriseId;
        public AssetOwnerKind lenderKind;
        public int lenderPersonId; public Faction lenderFaction; public int lenderEnterpriseId;

        public float principal;        // 借入残高
        public float interestRate;     // 金利（年）
        public float collateralValue;  // 担保の時価（呼び側が更新）
        public int pledgedSystemId;    // 担保に入れた土地の星系id（差し押さえ対象・−1=土地以外/未指定）

        public CollateralLoan() { pledgedSystemId = -1; }

        public OwnerRef Borrower => new OwnerRef(borrowerKind, borrowerPersonId, borrowerFaction, borrowerEnterpriseId);
        public OwnerRef Lender => new OwnerRef(lenderKind, lenderPersonId, lenderFaction, lenderEnterpriseId);
    }
}
