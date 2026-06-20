using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 宇宙鉄道（宇宙旅客運輸）会社のロジック（業種細分化・陸運/運輸 #2024 の旅客サブ業種を宇宙設定へ・#2025・純ロジック・唯一の窓口）：
    /// 回廊（<see cref="Corridor"/>）を線路、星系（<see cref="StarSystem"/>）を停車駅に見立て、星系間に旅客列車を走らせる宇宙版「鉄道会社」。
    /// 旅客運輸収入（RAIL-1）＋駅ナカ・沿線（星系）開発の非運輸収入（RAIL-2＝不動産#2019連動）／乗車率（RAIL-3）／多角化した利益（RAIL-4）。
    /// 現実の鉄道同様、運輸そのものは薄利でも沿線開発で稼ぐ二本柱。銀河グラフ上の旅客流動として効かせる。マクロ近似。test-first。
    /// </summary>
    public static class SpaceRailwayRules
    {
        /// <summary>旅客運輸収入＝乗客数×運賃（星系間の旅客輸送＝回廊を走る列車の本業）。</summary>
        public static float PassengerRevenue(float passengers, float farePerPassenger)
            => Mathf.Max(0f, passengers) * Mathf.Max(0f, farePerPassenger);

        /// <summary>非運輸収入＝駅ナカ商業+沿線（星系）不動産開発（運輸の薄利を埋める第二の柱＝不動産#2019連動）。</summary>
        public static float NonTransportRevenue(float stationRetail, float lineRealEstateIncome)
            => Mathf.Max(0f, stationRetail) + Mathf.Max(0f, lineRealEstateIncome);

        /// <summary>乗車率＝乗客数/輸送力（座席を埋めるほど効率的＝固定費の高い装置産業）。輸送力0以下は0。</summary>
        public static float LoadFactor(float passengers, float capacity)
            => capacity <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, passengers) / capacity);

        /// <summary>宇宙鉄道利益＝運輸収入+非運輸収入−運営費（運輸は薄利・沿線開発が利益の源＝多角化で景気耐性）。</summary>
        public static float RailwayProfit(float transportRevenue, float nonTransportRevenue, float operatingCost)
            => transportRevenue + nonTransportRevenue - Mathf.Max(0f, operatingCost);
    }
}
