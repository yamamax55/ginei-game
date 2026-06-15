namespace Ginei
{
    /// <summary>
    /// 兵器メーカー（防衛 contractor・#2020・純データ）。兵器を作って軍（国家）に納める企業。汎用メーカー（#2016 製造）でも
    /// 軍産複合体の政治ロビー（#204/#1389）でもない operate＝兵器の開発・調達・輸出・戦力供給。基準兵器性能・兵器R&D水準・
    /// 生産能力・輸出規制を持つ。顧客は軍（防衛調達）・特需と平時遊休・両陣営商法（#160）。解決は <see cref="WeaponsRules"/>。
    /// </summary>
    [System.Serializable]
    public class WeaponsManufacturer
    {
        public string name = "兵器メーカー";
        public Faction faction;

        /// <summary>基準兵器性能（火力/装甲などの基準値）。</summary>
        public float baseWeaponSpec = 100f;

        /// <summary>兵器R&D水準（蓄積。兵器性能を上げる）。</summary>
        public float rdLevel = 0f;

        /// <summary>生産能力（年間に作れる兵器数）。</summary>
        public float productionCapacity = 0f;

        /// <summary>輸出規制（敵対勢力への輸出を禁じるか）。</summary>
        public bool exportRestricted = false;

        public WeaponsManufacturer() { }

        public WeaponsManufacturer(string name, float baseWeaponSpec = 100f, float rdLevel = 0f,
            float productionCapacity = 0f, bool exportRestricted = false, Faction faction = default)
        {
            this.name = string.IsNullOrEmpty(name) ? "兵器メーカー" : name;
            this.baseWeaponSpec = baseWeaponSpec;
            this.rdLevel = rdLevel;
            this.productionCapacity = productionCapacity;
            this.exportRestricted = exportRestricted;
            this.faction = faction;
        }
    }
}
