using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 外食チェーンのロジック（業種細分化・サービス #2024 の飲食サブ業種・#2025・純ロジック・唯一の窓口）：FLコスト＝食材費+人件費（REST-1）／
    /// FL比率＝売上に対する割合（REST-2）／店舗売上＝席数×回転×客単価（REST-3）／利益（REST-4）。
    /// 食材は食料品#2024、労働は労働市場#1957から。立地と回転率が採算を決める労働集約・低マージン業態。マクロ近似。test-first。
    /// </summary>
    public static class RestaurantRules
    {
        /// <summary>FLコスト＝食材費(Food)+人件費(Labor)（外食の二大変動費）。</summary>
        public static float FlCost(float foodCost, float laborCost)
            => Mathf.Max(0f, foodCost) + Mathf.Max(0f, laborCost);

        /// <summary>FL比率＝FLコスト/売上（60%を超えると赤字圏＝外食の生命線）。売上0以下は0。</summary>
        public static float FlRatio(float foodCost, float laborCost, float sales)
            => sales <= 0f ? 0f : (Mathf.Max(0f, foodCost) + Mathf.Max(0f, laborCost)) / sales;

        /// <summary>店舗売上＝席数×回転数×客単価（席を何回転させるかが鍵）。</summary>
        public static float StoreSales(int seats, float turnover, float avgSpend)
            => Mathf.Max(0, seats) * Mathf.Max(0f, turnover) * Mathf.Max(0f, avgSpend);

        /// <summary>外食利益＝売上−FLコスト−その他固定費（家賃・光熱）。</summary>
        public static float RestaurantProfit(float sales, float flCost, float fixedCost)
            => sales - Mathf.Max(0f, flCost) - Mathf.Max(0f, fixedCost);
    }
}
