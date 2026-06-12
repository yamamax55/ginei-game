using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 玩具・エンタメ（IP/版権）のロジック（業種細分化・その他製品 #2024 のIPサブ業種・#2025・純ロジック・唯一の窓口）：版権ライセンス料（TOY-1）／
    /// ヒット商品の売上（TOY-2）／キャラクターの人気減衰（TOY-3）／利益（TOY-4）。
    /// 自社IPを他社へ貸す版権ロイヤリティ（不労所得的）＋自社商品の売上＝ヒット依存（ゲーム#2025と同型）だがキャラクターは長寿命IP化すれば長く稼ぐ。マクロ近似。test-first。
    /// </summary>
    public static class ToyEntertainmentRules
    {
        /// <summary>版権ライセンス料＝ライセンシーの商品売上×ロイヤリティ率（自社IPを貸して得る不労所得的収入）。</summary>
        public static float LicenseRoyalty(float merchandiseSales, float royaltyRate)
            => Mathf.Max(0f, merchandiseSales) * Mathf.Clamp01(royaltyRate);

        /// <summary>ヒット商品売上＝販売数×単価（当たれば大きいが博打性）。</summary>
        public static float HitToyRevenue(int units, float pricePerUnit)
            => Mathf.Max(0, units) * Mathf.Max(0f, pricePerUnit);

        /// <summary>キャラクター人気減衰後の売上＝ピーク売上×(1−減衰率)^経過年（ブームは去る＝長寿命IP化で減衰を緩める）。</summary>
        public static float CharacterLifecycleSales(float peakSales, int yearsSincePeak, float decayRate)
            => Mathf.Max(0f, peakSales) * Mathf.Pow(1f - Mathf.Clamp01(decayRate), Mathf.Max(0, yearsSincePeak));

        /// <summary>玩具・エンタメ利益＝商品売上+版権料−製造原価−固定費（版権料は原価がかからず利益率が高い）。</summary>
        public static float ToyProfit(float productSales, float licenseRoyalty, float productionCost, float fixedCost)
            => productSales + Mathf.Max(0f, licenseRoyalty) - Mathf.Max(0f, productionCost) - Mathf.Max(0f, fixedCost);
    }
}
