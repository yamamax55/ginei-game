using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 食品メーカーのロジック（東証33業種「食料品」・#2024・純ロジック・唯一の窓口）：ディフェンシブ需要＝景気に左右されにくい
    /// （FOOD-1・生活必需）／原料コストの価格転嫁とマージン圧迫（FOOD-2）／利益（FOOD-3）。原料は農林水産・市場（#179）、需要は
    /// 家計（#1969）・消費（#1951）へ接続。マクロ近似。test-first。
    /// </summary>
    public static class FoodRules
    {
        /// <summary>ディフェンシブ需要＝基準需要×(1＋景気乖離×感応度)。食料品は感応度が低く、不況でも需要が大きく落ちない（生活必需）。非負。</summary>
        public static float DefensiveDemand(float baseDemand, float cycleDeviation, float sensitivity)
            => Mathf.Max(0f, Mathf.Max(0f, baseDemand) * (1f + cycleDeviation * Mathf.Clamp01(sensitivity)));

        /// <summary>転嫁後の価格＝基準価格＋原料上昇×転嫁率（原料高を一部しか価格に乗せられない）。</summary>
        public static float CostPassThrough(float basePrice, float rawCostIncrease, float passThroughRate)
            => Mathf.Max(0f, basePrice) + Mathf.Max(0f, rawCostIncrease) * Mathf.Clamp01(passThroughRate);

        /// <summary>マージン圧迫＝原料上昇×(1−転嫁率)（転嫁できなかったぶんが利益を削る）。</summary>
        public static float MarginSqueeze(float rawCostIncrease, float passThroughRate)
            => Mathf.Max(0f, rawCostIncrease) * (1f - Mathf.Clamp01(passThroughRate));

        /// <summary>食品利益＝販売数×(価格−原価)（薄利だが安定）。</summary>
        public static float FoodProfit(float units, float price, float cost)
            => Mathf.Max(0f, units) * (price - cost);
    }
}
