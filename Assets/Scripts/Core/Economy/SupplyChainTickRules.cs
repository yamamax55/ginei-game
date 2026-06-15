namespace Ginei
{
    /// <summary>代表生産チェーンの係数（VCHAIN-6・#2091）。森林再生・伐採・製材・建設・住宅の調整値。</summary>
    public struct SupplyChainParams
    {
        public float forestCapacity;     // 森林の上限
        public float regenRate;          // 森林の再生率
        public float harvestRate;        // 持続可能な伐採率（これを超えると過伐採）
        public float harvestPace;        // 伐採能力（年あたり）
        public float woodPerMaterial;    // 建材1あたり木材投入
        public float sawmillPace;        // 製材能力（年あたり建材）
        public float sawmillYield;       // 製材の歩留まり
        public float materialPerHouse;   // 住宅1戸あたり建材
        public float buildPace;          // 建設能力（年あたり戸数）
        public float perCapitaHousing;   // 1人あたり必要戸数
        public float housingDepreciation;// 住宅の劣化率

        /// <summary>既定の係数（デモ用）。</summary>
        public static SupplyChainParams Default => new SupplyChainParams
        {
            forestCapacity = 1000f, regenRate = 0.3f, harvestRate = 0.1f, harvestPace = 80f,
            woodPerMaterial = 2f, sawmillPace = 100f, sawmillYield = 0.9f,
            materialPerHouse = 5f, buildPace = 20f, perCapitaHousing = 0.3f, housingDepreciation = 0.02f,
        };
    }

    /// <summary>代表生産チェーンの1パスの結果（VCHAIN-6・#2091）。各段の産出＋住宅充足＋過伐採。</summary>
    public struct SupplyChainResult
    {
        public float woodHarvested;     // 伐採した木材
        public float materialsProduced; // 製材した建材
        public float housesBuilt;       // 建設した住宅
        public float housingStock;      // 住宅ストック（劣化後）
        public float housingDemand;     // 居住需要
        public float occupancy;         // 住宅充足率
        public bool overharvest;        // 過伐採（森林が枯れる）
    }

    /// <summary>
    /// 代表生産チェーンの暦境界オーケストレータ（VCHAIN-6・#2091 配線・純ロジック）。
    /// 年次1パスで 森林再生→伐採（木材）→製材（建材）→建設（住宅）→住宅劣化→居住需要充足 を流す薄い窓口。
    /// 各段は `ForestryRules`/`SawmillRules`/`ConstructionChainRules`/`HousingDemandRules` へ委譲。test-first。
    /// </summary>
    public static class SupplyChainTickRules
    {
        /// <summary>1年ぶんチェーンを流し、<see cref="ChainStock"/> を破壊的に更新して結果を返す。</summary>
        public static SupplyChainResult TickYear(ChainStock cs, float population, SupplyChainParams p)
        {
            var r = new SupplyChainResult();
            if (cs == null) return r;

            // ① 森林の再生
            cs.forest = ForestryRules.Regrow(cs.forest, p.forestCapacity, p.regenRate);

            // ② 伐採（森林→木材）。過伐採は森林を枯らす。
            float harvested = ForestryRules.Harvest(cs.forest, p.harvestPace);
            r.overharvest = ForestryRules.IsOverharvest(harvested, cs.forest, p.harvestRate);
            cs.forest = UnityEngine.Mathf.Max(0f, cs.forest - harvested);
            cs.Add(SupplyChainGood.木材, harvested);
            r.woodHarvested = harvested;

            // ③ 製材（木材→建材）
            float gross = SawmillRules.GrossMaterials(p.sawmillPace, cs.Get(SupplyChainGood.木材), p.woodPerMaterial);
            float materials = SawmillRules.MaterialOutput(gross, p.sawmillYield);
            cs.Add(SupplyChainGood.木材, -SawmillRules.WoodConsumed(gross, p.woodPerMaterial));
            cs.Add(SupplyChainGood.建材, materials);
            r.materialsProduced = materials;

            // ④ 建設（建材→住宅）
            float houses = ConstructionChainRules.HousesBuilt(p.buildPace, cs.Get(SupplyChainGood.建材), p.materialPerHouse);
            cs.Add(SupplyChainGood.建材, -ConstructionChainRules.MaterialsConsumed(houses, p.materialPerHouse));
            cs.Add(SupplyChainGood.住宅, houses);
            r.housesBuilt = houses;

            // ⑤ 住宅の劣化
            cs.housing = HousingDemandRules.Depreciate(cs.housing, p.housingDepreciation);

            // ⑥ 居住需要の充足
            r.housingDemand = HousingDemandRules.HousingDemand(population, p.perCapitaHousing);
            r.housingStock = cs.Get(SupplyChainGood.住宅);
            r.occupancy = HousingDemandRules.Occupancy(r.housingStock, r.housingDemand);
            return r;
        }
    }
}
