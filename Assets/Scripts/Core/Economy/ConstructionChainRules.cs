using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 建設の純ロジック（VCHAIN-4・#2091）。建材を投入して住宅を建てる＝投入産出（建材→住宅ストック）。
    /// 利潤は既存 `ConstructionRules`#2024 に委譲（請負の原価超過＝赤字工事）＝二重実装しない。test-first。
    /// </summary>
    public static class ConstructionChainRules
    {
        /// <summary>建材から建てられる戸数＝建材/1戸あたり建材（係数0以下は0）。</summary>
        public static float BuildableHouses(float materialsAvailable, float materialPerHouse)
            => materialPerHouse <= 0f ? 0f : Mathf.Max(0f, materialsAvailable) / materialPerHouse;

        /// <summary>実建設戸数＝min(建設能力, 建材で建てられる戸数)。建材が足りなければ建たない。</summary>
        public static float HousesBuilt(float buildPace, float materialsAvailable, float materialPerHouse)
            => Mathf.Min(Mathf.Max(0f, buildPace), BuildableHouses(materialsAvailable, materialPerHouse));

        /// <summary>消費した建材＝建設戸数×1戸あたり建材。</summary>
        public static float MaterialsConsumed(float housesBuilt, float materialPerHouse)
            => Mathf.Max(0f, housesBuilt) * Mathf.Max(0f, materialPerHouse);

        /// <summary>住宅の請負額＝戸数×戸あたり価格。</summary>
        public static float HousingValue(float housesBuilt, float pricePerHouse)
            => Mathf.Max(0f, housesBuilt) * Mathf.Max(0f, pricePerHouse);

        /// <summary>建設利潤＝請負額−実費（`ConstructionRules.ProjectProfit`#2024 へ委譲）。</summary>
        public static float HousingProfit(float housesBuilt, float pricePerHouse, float unitCost)
            => ConstructionRules.ProjectProfit(HousingValue(housesBuilt, pricePerHouse), Mathf.Max(0f, housesBuilt) * Mathf.Max(0f, unitCost));
    }
}
