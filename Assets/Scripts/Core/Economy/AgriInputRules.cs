using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 種苗・農業資材のロジック（業種細分化・水産農林 #2024 ／化学の農業資材サブ業種・#2025・純ロジック・唯一の窓口）：種苗・資材の売上（AGRI-1）／
    /// 顧客（農家）への収量向上価値（AGRI-2＝高く売れる根拠）／形質ロイヤリティ（AGRI-3＝毎作課金）／利益（AGRI-4）。
    /// 露地農業（水産農林#2024）・垂直農法（#2025）へ種苗・肥料を供給する川上＝収量を上げる価値で高く売り、優良形質のロイヤリティで毎作課金する。マクロ近似。test-first。
    /// </summary>
    public static class AgriInputRules
    {
        /// <summary>種苗・資材売上＝販売数×単価。</summary>
        public static float SeedSalesRevenue(int units, float pricePerUnit)
            => Mathf.Max(0, units) * Mathf.Max(0f, pricePerUnit);

        /// <summary>収量向上価値＝基準収量×向上率×作物価格（農家に生む価値＝種苗を高く売れる根拠）。</summary>
        public static float YieldUpliftValue(float baseYield, float upliftRate, float cropPrice)
            => Mathf.Max(0f, baseYield) * Mathf.Max(0f, upliftRate) * Mathf.Max(0f, cropPrice);

        /// <summary>形質ロイヤリティ収入＝作付面積×面積あたり形質使用料（優良形質を毎作課金＝自家採種を許さない継続収入）。</summary>
        public static float RoyaltyTraitRevenue(float acres, float traitFeePerAcre)
            => Mathf.Max(0f, acres) * Mathf.Max(0f, traitFeePerAcre);

        /// <summary>農業資材利益＝種苗売上+形質ロイヤリティ−研究開発費−固定費（品種改良のR&Dが先行投資）。</summary>
        public static float AgriInputProfit(float seedRevenue, float traitRevenue, float rdCost, float fixedCost)
            => seedRevenue + Mathf.Max(0f, traitRevenue) - Mathf.Max(0f, rdCost) - Mathf.Max(0f, fixedCost);
    }
}
