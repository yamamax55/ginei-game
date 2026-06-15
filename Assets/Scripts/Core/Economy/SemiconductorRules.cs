using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 半導体メーカーのロジック（業種細分化・電気機器 #2024 のデバイスサブ業種・#2025・純ロジック・唯一の窓口）：歩留まり（SEMI-1）／
    /// チップ産出＝ウェハ投入×1枚あたりダイ数×歩留まり（SEMI-2）／微細化の巨額設備投資＝世代ごとに指数的増大（SEMI-3）／減価償却込みの利益（SEMI-4）。
    /// 情報通信のシリコンサイクル（既存 ElectronicsRules）と差別化＝こちらは微細化投資の巨額固定費と歩留まりが核。マクロ近似。test-first。
    /// </summary>
    public static class SemiconductorRules
    {
        /// <summary>歩留まり＝良品ダイ/総ダイ（微細化が進むほど歩留まり向上が死活的）。総ダイ0以下は0。</summary>
        public static float WaferYield(float goodDies, float totalDies)
            => totalDies <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, goodDies) / totalDies);

        /// <summary>チップ産出＝ウェハ投入枚数×1枚あたりダイ数×歩留まり。</summary>
        public static float ChipOutput(float waferStarts, float diesPerWafer, float yield)
            => Mathf.Max(0f, waferStarts) * Mathf.Max(0f, diesPerWafer) * Mathf.Clamp01(yield);

        /// <summary>微細化の設備投資＝基準投資×(1+世代コスト増)^進んだ世代数（ムーアの法則の裏で工場コストが指数的に膨らむ）。</summary>
        public static float CapexPerNode(float baseCapex, int generationsAdvanced, float costGrowthPerGen)
            => Mathf.Max(0f, baseCapex) * Mathf.Pow(1f + Mathf.Max(0f, costGrowthPerGen), Mathf.Max(0, generationsAdvanced));

        /// <summary>半導体利益＝チップ売上−設備の減価償却−固定費（巨額固定費ゆえ稼働率が高くないと回収できない）。</summary>
        public static float SemiconductorProfit(float chipsSold, float pricePerChip, float capexDepreciation, float fixedCost)
            => Mathf.Max(0f, chipsSold) * Mathf.Max(0f, pricePerChip) - Mathf.Max(0f, capexDepreciation) - Mathf.Max(0f, fixedCost);
    }
}
