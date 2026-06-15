using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 化学メーカーのロジック（東証33業種「化学」・#2024・純ロジック・唯一の窓口）。装置産業＝高固定費の稼働率ゲーム：
    /// プラント稼働率とオペレーティングレバレッジ（CHM-1）／市況スプレッド＝製品価格−原料価格（CHM-2）／汎用品 vs スペシャリティ
    /// （CHM-3）／スケールメリット＝大型プラントほど単価安（CHM-4）。原料は資源（#92/石油）、製品は市場（#179）・下流製造（#2016）へ
    /// 接続（read-only/接続のみ）。マクロ近似。test-first。
    /// </summary>
    public static class ChemicalRules
    {
        // ===== CHM-1 プラント稼働率 =====

        /// <summary>プラント利益＝能力×稼働率×1単位マージン−固定費（高固定費＝稼働率で利益が大きく振れる）。</summary>
        public static float PlantProfit(float capacity, float utilization, float unitMargin, float fixedCost)
            => Mathf.Max(0f, capacity) * Mathf.Clamp01(utilization) * unitMargin - Mathf.Max(0f, fixedCost);

        /// <summary>損益分岐稼働率＝固定費/(能力×1単位マージン)（これ以上稼働すれば黒字）。分母0以下は1.0超（届かない）。</summary>
        public static float BreakEvenUtilization(float fixedCost, float capacity, float unitMargin)
        {
            float denom = Mathf.Max(0f, capacity) * unitMargin;
            return denom <= 0f ? 999f : Mathf.Max(0f, fixedCost) / denom;
        }

        // ===== CHM-2 市況スプレッド =====

        /// <summary>市況スプレッド＝製品価格−原料費（ナフサ等）。これがマージン＝市況で乱高下。</summary>
        public static float Spread(float productPrice, float feedstockCost)
            => productPrice - feedstockCost;

        // ===== CHM-3 汎用品 vs スペシャリティ =====

        /// <summary>実効マージン＝スペシャリティなら高付加価値マージン、汎用なら薄利の市況マージン（スペシャリティは市況に左右されにくい）。</summary>
        public static float EffectiveMargin(bool isSpecialty, float commodityMargin, float specialtyMargin)
            => isSpecialty ? specialtyMargin : commodityMargin;

        // ===== CHM-4 スケールメリット =====

        /// <summary>規模の単価＝基準単価×(1−節約率×(1−基準能力/max(基準, 能力)))（大型プラントほど単価が下がる）。基準0以下は基準単価。</summary>
        public static float ScaleUnitCost(float baseUnitCost, float capacity, float referenceCapacity, float savingMax)
        {
            if (referenceCapacity <= 0f) return Mathf.Max(0f, baseUnitCost);
            float factor = 1f - referenceCapacity / Mathf.Max(referenceCapacity, Mathf.Max(0f, capacity));
            return Mathf.Max(0f, baseUnitCost) * (1f - Mathf.Clamp01(savingMax) * Mathf.Clamp01(factor));
        }
    }
}
