using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 電力小売（新電力）のロジック（業種細分化・公益事業 #2021 の自由化後サブ業種・#2025・純ロジック・唯一の窓口）：卸電力の調達コスト（PWR-1）／
    /// 燃料費調整（燃調費＝燃料価格を料金へ転嫁・PWR-2）／小売マージン（PWR-3）／利益（PWR-4）。
    /// 発送電を持たず卸電力市場から調達して売る薄利モデル＝市場価格スパイクで逆ザヤ（公益#2021の自然独占とは別の競争業態）。マクロ近似。test-first。
    /// </summary>
    public static class PowerRetailRules
    {
        /// <summary>卸調達コスト＝調達量×卸電力市場価格（自前の発電を持たず市場で買う＝市場価格に晒される）。</summary>
        public static float WholesaleProcurementCost(float volume, float marketPrice)
            => Mathf.Max(0f, volume) * Mathf.Max(0f, marketPrice);

        /// <summary>燃調費調整後の料金＝基準料金+燃料指数の変動分×転嫁率（燃料高を料金へ転嫁＝燃料費調整制度）。</summary>
        public static float FuelAdjustedPrice(float baseTariff, float fuelIndexDelta, float adjustmentRate)
            => Mathf.Max(0f, baseTariff) + fuelIndexDelta * Mathf.Max(0f, adjustmentRate);

        /// <summary>小売マージン＝小売価格−調達原価（薄利＝市場価格が跳ねると逆ザヤ）。</summary>
        public static float RetailMargin(float retailPrice, float procurementCost)
            => retailPrice - procurementCost;

        /// <summary>電力小売利益＝売上−調達コスト−固定費（市場価格スパイクで赤字化＝薄資本の新電力は破綻しやすい）。</summary>
        public static float PowerRetailProfit(float sales, float procurementCost, float fixedCost)
            => sales - Mathf.Max(0f, procurementCost) - Mathf.Max(0f, fixedCost);
    }
}
