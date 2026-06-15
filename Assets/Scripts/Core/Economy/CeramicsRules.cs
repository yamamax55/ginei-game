using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ガラス・土石製品メーカー（セメント等）のロジック（東証33業種「ガラス・土石製品」・#2024・純ロジック・唯一の窓口）：
    /// 窯業の投入産出＝石灰石→セメント（GLS-1）／装置産業（窯）の利益＝高固定費（GLS-2）／重量物ゆえ輸送費が地域独占を生む（GLS-3）／
    /// 建設（#2024）連動需要（GLS-4）。資源（#92）・建設（#2024）へ接続。マクロ近似。test-first。
    /// </summary>
    public static class CeramicsRules
    {
        /// <summary>セメント産出＝石灰石×変換率（窯で焼成）。</summary>
        public static float CementOutput(float limestone, float conversionRate)
            => Mathf.Max(0f, limestone) * Mathf.Clamp01(conversionRate);

        /// <summary>窯業利益＝産出×1単位マージン−固定費（装置産業＝稼働率が利益を決める）。</summary>
        public static float KilnProfit(float output, float unitMargin, float fixedCost)
            => Mathf.Max(0f, output) * unitMargin - Mathf.Max(0f, fixedCost);

        /// <summary>地域独占の商圏距離＝製品価値/距離あたり輸送費（重量物は輸送費が高く遠方へ運べず＝地産地消で地域独占）。輸送費0以下は超広域。</summary>
        public static float LocalMonopolyRange(float productValue, float transportCostPerDistance)
            => transportCostPerDistance <= 0f ? 999999f : Mathf.Max(0f, productValue) / transportCostPerDistance;

        /// <summary>建設連動需要＝建設活動×需要係数（セメント/ガラスは建設投資に直結）。</summary>
        public static float ConstructionLinkedDemand(float constructionActivity, float demandFactor)
            => Mathf.Max(0f, constructionActivity) * Mathf.Max(0f, demandFactor);
    }
}
