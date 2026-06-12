using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// パルプ・紙メーカーのロジック（東証33業種「パルプ・紙」・#2024・純ロジック・唯一の窓口）：木材→パルプの投入産出（PPR-1）／
    /// 古紙リサイクル（PPR-2）／装置産業の利益＝高固定費（PPR-3）／デジタル化で紙需要が構造的に減少（PPR-4）。森林資源（#92）・
    /// 市場（#179）へ接続。マクロ近似。test-first。
    /// </summary>
    public static class PaperRules
    {
        /// <summary>パルプ産出＝木材×変換率（木材を砕いてパルプに）。</summary>
        public static float PulpOutput(float wood, float conversionRate)
            => Mathf.Max(0f, wood) * Mathf.Clamp01(conversionRate);

        /// <summary>再生紙産出＝古紙×再生率（古紙を循環利用＝資源節約）。</summary>
        public static float RecycledOutput(float wastePaper, float recycleRate)
            => Mathf.Max(0f, wastePaper) * Mathf.Clamp01(recycleRate);

        /// <summary>製紙利益＝産出×1単位マージン−固定費（装置産業＝稼働率が利益を決める）。</summary>
        public static float PaperMillProfit(float output, float unitMargin, float fixedCost)
            => Mathf.Max(0f, output) * unitMargin - Mathf.Max(0f, fixedCost);

        /// <summary>デジタル化後の需要＝基準需要×(1−電子化代替率)（紙→デジタルで新聞/印刷用紙の需要が構造的に減る）。非負。</summary>
        public static float DigitalDeclineDemand(float baseDemand, float digitalSubstitution)
            => Mathf.Max(0f, baseDemand) * (1f - Mathf.Clamp01(digitalSubstitution));
    }
}
