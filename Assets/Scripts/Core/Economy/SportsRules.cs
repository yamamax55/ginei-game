using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// スポーツ興行のロジック（業種細分化・サービス #2024 の興行サブ業種・#2025・純ロジック・唯一の窓口）：入場料収入（SPRT-1）／
    /// 放映権収入（SPRT-2）／グッズ・物販収入（SPRT-3）／利益（SPRT-4）。
    /// 入場料・放映権（放送#2025連動）・グッズの三本柱＝人気（成績）が全収入を押し上げる。選手人件費が主コスト＝戦力と財政のトレードオフ（艦隊編成の縮図）。マクロ近似。test-first。
    /// </summary>
    public static class SportsRules
    {
        /// <summary>入場料収入＝観客数×平均チケット単価（スタジアムを埋めるほど稼ぐ）。</summary>
        public static float GateRevenue(int attendance, float avgTicketPrice)
            => Mathf.Max(0, attendance) * Mathf.Max(0f, avgTicketPrice);

        /// <summary>放映権収入＝試合数×1試合あたり放映権料（放送#2025が買う＝人気が単価を押し上げる）。</summary>
        public static float BroadcastRightsRevenue(int games, float rightsPerGame)
            => Mathf.Max(0, games) * Mathf.Max(0f, rightsPerGame);

        /// <summary>グッズ・物販収入＝販売数×単価（ファンのロイヤリティ収入）。</summary>
        public static float MerchandiseRevenue(int unitsSold, float pricePerUnit)
            => Mathf.Max(0, unitsSold) * Mathf.Max(0f, pricePerUnit);

        /// <summary>スポーツ興行利益＝入場料+放映権+グッズ−運営費（選手人件費が主コスト＝強さは金で買う）。</summary>
        public static float SportsProfit(float gateRevenue, float broadcastRevenue, float merchRevenue, float operatingCost)
            => gateRevenue + broadcastRevenue + merchRevenue - Mathf.Max(0f, operatingCost);
    }
}
