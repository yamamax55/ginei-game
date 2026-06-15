using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 人材派遣会社のロジック（業種細分化・サービス #2024 の人材仲介サブ業種・#2025・純ロジック・唯一の窓口）：スプレッド＝請求単価−支払賃金（STF-1）／
    /// マージン率（STF-2）／派遣収益＝稼働者数×時間×請求単価（STF-3）／利益（STF-4）。
    /// 労働市場（#1957）と企業（#1022）の間で人を回し差額（マージン）で稼ぐ＝稼働者数が収益の源、雇用の調整弁。マクロ近似。test-first。
    /// </summary>
    public static class StaffingRules
    {
        /// <summary>スプレッド＝請求単価−支払賃金（派遣会社の1時間当たり取り分）。</summary>
        public static float StaffingSpread(float billRate, float payRate)
            => billRate - payRate;

        /// <summary>マージン率＝スプレッド/請求単価（中抜き率＝高いほど派遣会社が取る）。請求単価0以下は0。</summary>
        public static float StaffingMarginRate(float billRate, float payRate)
            => billRate <= 0f ? 0f : (billRate - payRate) / billRate;

        /// <summary>派遣収益＝稼働者数×1人当たり時間×請求単価（稼働者を増やし遊ばせないのが鍵）。</summary>
        public static float StaffingRevenue(int deployedWorkers, float hoursPerWorker, float billRate)
            => Mathf.Max(0, deployedWorkers) * Mathf.Max(0f, hoursPerWorker) * Mathf.Max(0f, billRate);

        /// <summary>派遣利益＝稼働者数×時間×スプレッド−固定費（マッチング・管理コスト）。</summary>
        public static float StaffingProfit(int deployedWorkers, float hoursPerWorker, float spread, float fixedCost)
            => Mathf.Max(0, deployedWorkers) * Mathf.Max(0f, hoursPerWorker) * spread - Mathf.Max(0f, fixedCost);
    }
}
