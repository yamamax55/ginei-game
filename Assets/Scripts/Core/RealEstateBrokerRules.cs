using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 不動産仲介のロジック（業種細分化・不動産 #2019 の仲介サブ業種・#2025・純ロジック・唯一の窓口）：仲介手数料（BROK-1）／
    /// 両手仲介（BROK-2＝売主・買主双方から取る）／成約率（BROK-3）／利益（BROK-4）。
    /// 物件を保有せず（不動産#2019は保有）取引を仲介して手数料で稼ぐ＝在庫リスクなしのマッチング（配車#2025/EC#2025と同型）。両手仲介が利益を倍にする。マクロ近似。test-first。
    /// </summary>
    public static class RealEstateBrokerRules
    {
        /// <summary>仲介手数料＝取引額×手数料率（片手＝一方からのみ）。</summary>
        public static float BrokerageCommission(float transactionValue, float commissionRate)
            => Mathf.Max(0f, transactionValue) * Mathf.Clamp01(commissionRate);

        /// <summary>両手仲介手数料＝取引額×手数料率×2（売主・買主双方から取る＝利益が倍）。</summary>
        public static float BothSidesCommission(float transactionValue, float commissionRate)
            => Mathf.Max(0f, transactionValue) * Mathf.Clamp01(commissionRate) * 2f;

        /// <summary>成約率＝成約件数/媒介件数（預かった物件をどれだけ成約に導けるか）。媒介0以下は0。</summary>
        public static float ListingConversion(int closedDeals, int listings)
            => listings <= 0 ? 0f : Mathf.Clamp01((float)Mathf.Max(0, closedDeals) / listings);

        /// <summary>仲介利益＝手数料収入−営業担当の歩合−固定費（歩合制の人件費）。</summary>
        public static float BrokerProfit(float commissionRevenue, float agentSplit, float fixedCost)
            => commissionRevenue - Mathf.Max(0f, agentSplit) - Mathf.Max(0f, fixedCost);
    }
}
