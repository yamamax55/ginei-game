using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 林業の純ロジック（VCHAIN-2・#2091）。森林（再生資源）を再生・伐採して木材を得る。
    /// 持続可能量を超える伐採は森林を枯らす（再生が追いつかない＝`PrimaryIndustryRules.OverfishingRisk`#2024 と同じ思想）。test-first。
    /// </summary>
    public static class ForestryRules
    {
        /// <summary>森林の再生（ロジスティック）＝forest + regenRate×forest×(1−forest/capacity)。上限 capacity。</summary>
        public static float Regrow(float forest, float capacity, float regenRate)
        {
            float f = Mathf.Max(0f, forest);
            float cap = Mathf.Max(0f, capacity);
            if (cap <= 0f) return 0f;
            float growth = Mathf.Max(0f, regenRate) * f * (1f - f / cap);
            return Mathf.Min(cap, f + growth);
        }

        /// <summary>持続可能な伐採量＝森林×伐採率（これを超えると枯れる）。</summary>
        public static float SustainableYield(float forest, float harvestRate)
            => Mathf.Max(0f, forest) * Mathf.Max(0f, harvestRate);

        /// <summary>実伐採量＝min(需要, 森林)＝得られる木材（森林もこのぶん減る）。</summary>
        public static float Harvest(float forest, float woodDemand)
            => Mathf.Min(Mathf.Max(0f, woodDemand), Mathf.Max(0f, forest));

        /// <summary>過伐採か＝伐採量が持続可能量を超えた（森林が枯れていく）。</summary>
        public static bool IsOverharvest(float harvested, float forest, float harvestRate)
            => harvested > SustainableYield(forest, harvestRate);
    }
}
