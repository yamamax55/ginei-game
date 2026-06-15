using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 建設会社のロジック（東証33業種「建設業」・#2024・純ロジック・唯一の窓口）：受注産業＝工事進行基準で収益計上（BLD-1）／
    /// 完成工事と原価（BLD-2）／採算＝契約額−実原価・原価超過で赤字工事（BLD-3）／受注残（BLD-4）。資材は鉄鋼/化学(#2024)・建機
    /// (#2022)、発注は不動産(#2019)・インフラ(#2021)・公共（財政 #163）から。マクロ近似（個別工区 micro は持たない）。test-first。
    /// </summary>
    public static class ConstructionRules
    {
        /// <summary>工事進捗率＝発生原価/見積総原価（工事進行基準＝原価の進み具合で進捗を測る）。総原価0以下は0。</summary>
        public static float PercentageOfCompletion(float costIncurred, float estimatedTotalCost)
            => estimatedTotalCost <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, costIncurred) / estimatedTotalCost);

        /// <summary>計上売上＝契約額×進捗率（完成を待たず進捗に応じて売上を立てる）。</summary>
        public static float RecognizedRevenue(float contractValue, float completionRatio)
            => Mathf.Max(0f, contractValue) * Mathf.Clamp01(completionRatio);

        /// <summary>工事利益＝契約額−実原価（請負ゆえ原価超過は丸ごとメーカー負担＝赤字工事）。</summary>
        public static float ProjectProfit(float contractValue, float actualCost)
            => contractValue - actualCost;

        /// <summary>原価超過＝実原価−見積原価（工期延長・資材高騰で膨らむ＝採算悪化の元凶）。</summary>
        public static float CostOverrun(float estimatedCost, float actualCost)
            => actualCost - estimatedCost;

        /// <summary>受注後の受注残＝受注残＋新規受注−完成工事高（受注産業のパイプライン）。非負。</summary>
        public static float BacklogAfterOrders(float backlog, float newOrders, float completedWork)
            => Mathf.Max(0f, Mathf.Max(0f, backlog) + Mathf.Max(0f, newOrders) - Mathf.Max(0f, completedWork));
    }
}
