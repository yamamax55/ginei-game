using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ゴム製品メーカー（タイヤ）のロジック（東証33業種「ゴム製品」・#2024・純ロジック・唯一の窓口）：タイヤ需要＝新車(OEM)＋
    /// 補修(RUB-1)／天然ゴム市況スプレッド（RUB-2）／利益（RUB-3）／補修需要の下支え＝新車が落ちても補修は安定（RUB-4）。新車は
    /// 自動車（#2024）、原料ゴムは資源（#92）へ接続。マクロ近似。test-first。
    /// </summary>
    public static class RubberRules
    {
        /// <summary>タイヤ需要＝新車装着(OEM)＋補修(交換)。補修は走行に応じ恒常的に発生。</summary>
        public static float TireDemand(float oemDemand, float replacementDemand)
            => Mathf.Max(0f, oemDemand) + Mathf.Max(0f, replacementDemand);

        /// <summary>ゴムスプレッド＝タイヤ価格−原料ゴム費（天然ゴム市況でマージンが動く）。</summary>
        public static float RubberSpread(float tirePrice, float rubberCost)
            => tirePrice - rubberCost;

        /// <summary>ゴム製品利益＝販売数×スプレッド−固定費。</summary>
        public static float RubberProfit(float units, float spread, float fixedCost)
            => Mathf.Max(0f, units) * spread - Mathf.Max(0f, fixedCost);

        /// <summary>補修需要比率＝補修/総需要（高いほど不況に強い＝新車が落ちても補修が下支え）。総0以下は0。</summary>
        public static float ReplacementShare(float replacementDemand, float totalDemand)
            => totalDemand <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, replacementDemand) / totalDemand);
    }
}
