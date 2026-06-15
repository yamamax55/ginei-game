using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 自動車メーカーのロジック（東証33業種「輸送用機器」・#2024・純ロジック・唯一の窓口）：量産と規模の経済（AUTO-1）／モデル
    /// チェンジ＝モデルが古いと販売減（AUTO-2）／リコール費用（AUTO-3）／系列サプライチェーン＝部品不足で減産（AUTO-4・JIT）／
    /// 利益（AUTO-5）。製造は <see cref="ManufacturerRules"/>(#2016)、鋼材は鉄鋼(#2024)・工作機械(#2023)へ接続。マクロ近似。test-first。
    /// </summary>
    public static class AutoRules
    {
        /// <summary>量産の単価＝基準単価×(1−節約率×(1−基準台数/max(基準, 台数)))（大量生産で1台あたり安くなる＝規模の経済）。基準0以下は基準単価。</summary>
        public static float MassProductionUnitCost(float baseUnitCost, float volume, float referenceVolume, float savingMax)
        {
            if (referenceVolume <= 0f) return Mathf.Max(0f, baseUnitCost);
            float factor = 1f - referenceVolume / Mathf.Max(referenceVolume, Mathf.Max(0f, volume));
            return Mathf.Max(0f, baseUnitCost) * (1f - Mathf.Clamp01(savingMax) * Mathf.Clamp01(factor));
        }

        /// <summary>モデル鮮度の販売＝基準販売×max(下限, 1−経過年×減衰)（モデルが古くなると販売が落ち、フルモデルチェンジで回復）。</summary>
        public static float ModelFreshnessSales(float baseSales, float modelAgeYears, float decayPerYear, float floor)
            => Mathf.Max(0f, baseSales) * Mathf.Max(Mathf.Clamp01(floor), 1f - Mathf.Max(0f, modelAgeYears) * Mathf.Max(0f, decayPerYear));

        /// <summary>リコール費用＝対象台数×1台あたり対応費（品質問題の巨額損失＋ブランド毀損）。</summary>
        public static float RecallCost(float unitsAffected, float costPerUnit)
            => Mathf.Max(0f, unitsAffected) * Mathf.Max(0f, costPerUnit);

        /// <summary>部品制約の生産＝計画生産と部品入手可能数の小さい方（JIT＝部品が止まると減産＝系列のもろさ）。</summary>
        public static float SupplyConstrainedOutput(float plannedOutput, float partsAvailability)
            => Mathf.Min(Mathf.Max(0f, plannedOutput), Mathf.Max(0f, partsAvailability));

        /// <summary>自動車利益＝販売台数×(価格−単価)−固定費（薄利多売＝台数と固定費回収が鍵）。</summary>
        public static float AutoProfit(float unitsSold, float unitPrice, float unitCost, float fixedCost)
            => Mathf.Max(0f, unitsSold) * (unitPrice - unitCost) - Mathf.Max(0f, fixedCost);
    }
}
