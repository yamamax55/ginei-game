using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 金属製品メーカーのロジック（東証33業種「金属製品」・#2024・純ロジック・唯一の窓口）：鋼材を加工して中間財/最終財を作る：
    /// 加工の投入産出＝鋼材→製品（MPR-1）／加工マージン＝製品価格−鋼材費−加工費（MPR-2）／利益（MPR-3）。原料は鉄鋼（#2024）・
    /// 非鉄（#2024）、製品は下流製造（#2016/#2022）・建設（#2024）へ接続。マクロ近似。test-first。
    /// </summary>
    public static class MetalProductRules
    {
        /// <summary>加工産出＝鋼材投入×歩留まり（切断/成形/溶接で製品に）。</summary>
        public static float ProcessedOutput(float steelInput, float yieldRate)
            => Mathf.Max(0f, steelInput) * Mathf.Clamp01(yieldRate);

        /// <summary>加工マージン（製品あたり）＝製品価格−鋼材費−加工費（素材を仕入れ付加価値を載せる）。</summary>
        public static float ProcessingMargin(float productPrice, float steelCost, float processingCost)
            => productPrice - steelCost - processingCost;

        /// <summary>金属製品利益＝産出×加工マージン−固定費。</summary>
        public static float MetalProductProfit(float output, float processingMargin, float fixedCost)
            => Mathf.Max(0f, output) * processingMargin - Mathf.Max(0f, fixedCost);
    }
}
