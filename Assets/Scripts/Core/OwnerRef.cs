namespace Ginei
{
    /// <summary>
    /// 所有者の参照（人物/国家/企業の三態を1つにまとめた軽量値・#2063/#2070/#1022）。権利証・リース等の取引APIで
    /// 「誰が」を渡す共通窓口＝<see cref="AssetOwnerKind"/>＋各id。既存3クラス（NamedAsset/FinancialHolding/PropertyDeed）の
    /// owner 表現と同義（こちらは取引・照合用の値型）。test-first。
    /// </summary>
    public readonly struct OwnerRef
    {
        public readonly AssetOwnerKind kind;
        public readonly int personId;
        public readonly Faction faction;
        public readonly int enterpriseId;

        public OwnerRef(AssetOwnerKind kind, int personId = 0, Faction faction = default, int enterpriseId = 0)
        {
            this.kind = kind;
            this.personId = personId;
            this.faction = faction;
            this.enterpriseId = enterpriseId;
        }

        public static OwnerRef Person(int id) => new OwnerRef(AssetOwnerKind.人物, personId: id);
        public static OwnerRef State(Faction f) => new OwnerRef(AssetOwnerKind.国家, faction: f);
        public static OwnerRef Enterprise(int id) => new OwnerRef(AssetOwnerKind.企業, enterpriseId: id);

        /// <summary>所有者キー（"P:id"／"F:faction"／"E:id"）。</summary>
        public string Key =>
            kind == AssetOwnerKind.人物 ? $"P:{personId}" :
            kind == AssetOwnerKind.企業 ? $"E:{enterpriseId}" : $"F:{faction}";

        /// <summary>権利証がこの所有者のものか。</summary>
        public bool Owns(PropertyDeed d)
        {
            if (d == null || d.ownerKind != kind) return false;
            switch (kind)
            {
                case AssetOwnerKind.人物: return d.ownerPersonId == personId;
                case AssetOwnerKind.企業: return d.ownerEnterpriseId == enterpriseId;
                default: return d.ownerFaction == faction;
            }
        }

        /// <summary>権利証へ所有者を書き込む。</summary>
        public void ApplyTo(PropertyDeed d)
        {
            if (d == null) return;
            d.ownerKind = kind;
            d.ownerPersonId = personId;
            d.ownerFaction = faction;
            d.ownerEnterpriseId = enterpriseId;
        }
    }
}
