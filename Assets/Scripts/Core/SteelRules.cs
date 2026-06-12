using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 鉄鋼メーカーのロジック（東証33業種「鉄鋼」・#2024・純ロジック・唯一の窓口）。装置産業＝高炉/電炉で鋼材を作る：高炉の
    /// 投入産出＝鉄鉱石→粗鋼（STL-1）／原料スプレッド＝鋼材価格−原料費（STL-2）／装置産業利益＝高固定費で稼働率が利益を決める
    /// （STL-3）／電炉＝スクラップ循環（STL-4）／稼働率（STL-5）。原料は鉱山（#2018）、鋼材は下流製造（#2016/#2022）・建艦（#884）へ
    /// 接続（read-only/接続のみ）。マクロ近似。test-first。
    /// </summary>
    public static class SteelRules
    {
        // ===== STL-1 高炉（投入産出） =====

        /// <summary>粗鋼産出＝鉄鉱石×転換率（高炉で鉄鉱石を還元して粗鋼に）。</summary>
        public static float CrudeSteelOutput(float ironOre, float conversionRate)
            => Mathf.Max(0f, ironOre) * Mathf.Max(0f, conversionRate);

        // ===== STL-2 原料スプレッド =====

        /// <summary>鋼材スプレッド＝鋼材価格−原料費（鉄鉱石＋石炭）。市況で乱高下するマージン。</summary>
        public static float SteelSpread(float steelPrice, float rawMaterialCost)
            => steelPrice - rawMaterialCost;

        // ===== STL-3 装置産業利益 =====

        /// <summary>製鉄利益＝粗鋼×スプレッド−固定費（高固定費＝稼働を落とすと赤字）。</summary>
        public static float BlastFurnaceProfit(float output, float spread, float fixedCost)
            => Mathf.Max(0f, output) * spread - Mathf.Max(0f, fixedCost);

        // ===== STL-4 電炉（スクラップ循環） =====

        /// <summary>電炉産出＝スクラップ×歩留まり（鉄スクラップを溶かして鋼に＝国内資源循環・高炉より小回り）。</summary>
        public static float ElectricFurnaceOutput(float scrap, float yieldRate)
            => Mathf.Max(0f, scrap) * Mathf.Clamp01(yieldRate);

        // ===== STL-5 稼働率 =====

        /// <summary>設備稼働率＝生産/能力（装置産業は稼働率が収益の鍵）。能力0以下は0。</summary>
        public static float CapacityUtilization(float production, float capacity)
            => capacity <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, production) / capacity);
    }
}
