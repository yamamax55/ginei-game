using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// EC（ネット通販）のロジック（業種細分化・小売 #2017 のオンラインサブ業種・#2025・純ロジック・唯一の窓口）：テイクレート収益＝GMV×手数料率（EC-1）／
    /// 物流費＝注文数×配送原価（EC-2）／ロングテール（少数の死に筋が集積して売上を作る・EC-3）／利益（EC-4）。
    /// 実店舗（小売#2017）と違い在庫を持たないモール型はGMV×テイクレートで稼ぎ、物流費が利益を圧迫。マクロ近似。test-first。
    /// </summary>
    public static class EcommerceRules
    {
        /// <summary>テイクレート収益＝流通総額(GMV)×手数料率（モール型は他社商品の取扱高から手数料を取る）。</summary>
        public static float TakeRateRevenue(float gmv, float takeRate)
            => Mathf.Max(0f, gmv) * Mathf.Clamp01(takeRate);

        /// <summary>物流費＝注文数×1注文あたり配送原価（ECの最大の変動費＝送料無料競争で膨らむ）。</summary>
        public static float LogisticsCost(int orders, float costPerOrder)
            => Mathf.Max(0, orders) * Mathf.Max(0f, costPerOrder);

        /// <summary>ロングテール比率＝死に筋（テール）売上/総売上（無限の棚＝多品種少量の集積が効く）。総売上0以下は0。</summary>
        public static float LongTailShare(float tailSales, float totalSales)
            => totalSales <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, tailSales) / totalSales);

        /// <summary>EC利益＝テイクレート収益−物流費−固定費。</summary>
        public static float EcommerceProfit(float takeRateRevenue, float logisticsCost, float fixedCost)
            => takeRateRevenue - Mathf.Max(0f, logisticsCost) - Mathf.Max(0f, fixedCost);
    }
}
