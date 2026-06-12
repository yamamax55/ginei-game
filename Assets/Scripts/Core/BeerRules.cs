using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ビール会社のロジック（業種細分化・食料品 #2024 の酒類サブ業種・#2025・純ロジック・唯一の窓口）：酒税（BEER-1）／
    /// 酒税の価格転嫁（BEER-2）／市場シェア数量（BEER-3）／酒税抜き利益（BEER-4）。
    /// 酒税は売上でなく国庫#163へ流れる預り金＝価格の大半が税で、わずかな税差が販売数量とシェアを左右する装置産業。マクロ近似。test-first。
    /// </summary>
    public static class BeerRules
    {
        /// <summary>酒税＝販売数量×単位酒税（メーカーが徴収し国庫#163へ納める預り金）。</summary>
        public static float LiquorTax(float volume, float taxPerUnit)
            => Mathf.Max(0f, volume) * Mathf.Max(0f, taxPerUnit);

        /// <summary>税転嫁後の店頭価格＝本体価格+単位酒税×転嫁率（増税分をどれだけ価格へ乗せるか）。</summary>
        public static float PriceAfterTaxPassThrough(float basePrice, float taxPerUnit, float passThroughRate)
            => Mathf.Max(0f, basePrice) + Mathf.Max(0f, taxPerUnit) * Mathf.Max(0f, passThroughRate);

        /// <summary>シェア数量＝総市場×自社シェア（わずかな税区分の差が販売数量を動かす）。</summary>
        public static float MarketShareVolume(float totalMarketVolume, float share)
            => Mathf.Max(0f, totalMarketVolume) * Mathf.Clamp01(share);

        /// <summary>ビール利益＝酒税抜き売上−製造原価−固定費（酒税は素通りゆえ含めない）。</summary>
        public static float BeerProfit(float salesExTax, float productionCost, float fixedCost)
            => salesExTax - Mathf.Max(0f, productionCost) - Mathf.Max(0f, fixedCost);
    }
}
