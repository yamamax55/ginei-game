using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// テーマパークのロジック（業種細分化・サービス #2024 のレジャーサブ業種・#2025・純ロジック・唯一の窓口）：入園料収入（PARK-1）／
    /// 園内消費（PARK-2＝飲食・グッズの客単価）／稼働率（PARK-3）／利益（PARK-4）。
    /// 入園料＋園内消費（飲食・物販）の二本柱＝来園者数が両方を押し上げる超高固定費の装置産業。IP（玩具#2025/映画#2025）が集客力になる。マクロ近似。test-first。
    /// </summary>
    public static class ThemeParkRules
    {
        /// <summary>入園料収入＝来園者数×入園料。</summary>
        public static float GateRevenue(int visitors, float ticketPrice)
            => Mathf.Max(0, visitors) * Mathf.Max(0f, ticketPrice);

        /// <summary>園内消費＝来園者数×園内客単価（飲食・グッズ＝入園後の追加消費が利益の柱）。</summary>
        public static float InParkSpend(int visitors, float spendPerGuest)
            => Mathf.Max(0, visitors) * Mathf.Max(0f, spendPerGuest);

        /// <summary>稼働率＝来園者数/年間収容力（混みすぎは満足度を下げ、空きは固定費を回収できない）。収容力0以下は0。</summary>
        public static float CapacityUtilization(int visitors, float annualCapacity)
            => annualCapacity <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0, visitors) / annualCapacity);

        /// <summary>テーマパーク利益＝入園料+園内消費−運営費−固定費（超高固定費＝アトラクション投資の回収）。</summary>
        public static float ThemeParkProfit(float gateRevenue, float inParkRevenue, float operatingCost, float fixedCost)
            => gateRevenue + inParkRevenue - Mathf.Max(0f, operatingCost) - Mathf.Max(0f, fixedCost);
    }
}
