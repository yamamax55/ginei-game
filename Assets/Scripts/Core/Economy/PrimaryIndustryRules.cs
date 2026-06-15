using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 水産・農林業のロジック（東証33業種「水産・農林業」・#2024・純ロジック・唯一の窓口）：一次産業＝天候/資源変動に晒される
    /// 収穫（PRI-1）／養殖＝餌→収量（PRI-2）／乱獲＝持続可能量を超えると資源枯渇（PRI-3）／市況収益（PRI-4）。産品は資源（#92）・
    /// 食料品（#2024）へ接続。マクロ近似。test-first。
    /// </summary>
    public static class PrimaryIndustryRules
    {
        /// <summary>天候調整後の収穫＝基準収量×天候係数（豊作/不作で大きく振れる＝一次産業のリスク）。非負。</summary>
        public static float WeatherAdjustedYield(float baseYield, float weatherFactor)
            => Mathf.Max(0f, Mathf.Max(0f, baseYield) * Mathf.Max(0f, weatherFactor));

        /// <summary>養殖産出＝餌投入×変換効率（養殖は餌を収量に変える＝天候依存を減らす）。</summary>
        public static float AquacultureOutput(float feedInput, float conversionRate)
            => Mathf.Max(0f, feedInput) * Mathf.Max(0f, conversionRate);

        /// <summary>乱獲リスク＝(漁獲量−持続可能量)/持続可能量（プラスは資源枯渇を招く＝将来の収量を食う）。持続可能量0以下は0。</summary>
        public static float OverfishingRisk(float catchVolume, float sustainableYield)
            => sustainableYield <= 0f ? 0f : Mathf.Max(0f, catchVolume - sustainableYield) / sustainableYield;

        /// <summary>一次産品収益＝収量×市況価格。</summary>
        public static float PrimaryRevenue(float volume, float price)
            => Mathf.Max(0f, volume) * Mathf.Max(0f, price);
    }
}
