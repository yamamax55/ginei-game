namespace Ginei
{
    /// <summary>
    /// 不動産の権利証＝惑星所有権の持分（NFIN-4・#2070・純データ・後方互換）。
    /// 惑星（<see cref="StarSystem"/>/<see cref="Planet"/>）の所有権を<b>持分（share 0..1）</b>として持ち、地代収益を生む。
    /// 所有者は人物/国家（<see cref="AssetOwnerKind"/>#2063 再利用）。相続#152 のたびに分割され<b>細分化</b>（<see cref="PropertyFragmentationRules"/>）。
    /// 評価/収益は <see cref="PropertyValuationRules"/>。test-first。
    /// </summary>
    public class PropertyDeed
    {
        public int id;
        public int systemId;        // 対象の星系/惑星

        // --- 所有者（人物 or 国家） ---
        public AssetOwnerKind ownerKind = AssetOwnerKind.人物;
        public int ownerPersonId;
        public Faction ownerFaction;

        public float share;         // 持分（0..1）
        public float baseValue;     // 惑星全体の評価額（share=1 のときの価値）
        public float rentRate;      // 地代率（年）

        public PropertyDeed() { }

        public PropertyDeed(int id, int systemId, float share, float baseValue)
        {
            this.id = id;
            this.systemId = systemId;
            this.share = share;
            this.baseValue = baseValue;
        }

        public bool IsPersonOwned => ownerKind == AssetOwnerKind.人物;
        public bool IsFactionOwned => ownerKind == AssetOwnerKind.国家;
        public string OwnerKey => IsPersonOwned ? $"P:{ownerPersonId}" : $"F:{ownerFaction}";
    }
}
