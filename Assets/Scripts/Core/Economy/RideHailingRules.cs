using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 配車・ライドシェアのロジック（業種細分化・サービス #2024 ／陸運の配車プラットフォームサブ業種・#2025・純ロジック・唯一の窓口）：総取扱額（RIDE-1）／
    /// プラットフォーム手数料（RIDE-2＝テイクレート）／需給連動のサージ価格（RIDE-3）／利益（RIDE-4）。
    /// 車を持たずドライバーと乗客を仲介するプラットフォーム＝総取扱額×テイクレートで稼ぐ（EC#2025/人材派遣#2025と同型のマッチング）。需給で価格が動くサージが特徴。マクロ近似。test-first。
    /// </summary>
    public static class RideHailingRules
    {
        /// <summary>総取扱額＝乗車回数×平均運賃（プラットフォームを通る取引総額）。</summary>
        public static float GrossBookings(int rides, float avgFare)
            => Mathf.Max(0, rides) * Mathf.Max(0f, avgFare);

        /// <summary>プラットフォーム手数料＝総取扱額×テイクレート（残りはドライバーの取り分）。</summary>
        public static float PlatformRevenue(float grossBookings, float takeRate)
            => Mathf.Max(0f, grossBookings) * Mathf.Clamp01(takeRate);

        /// <summary>サージ価格＝基本運賃×(1+max(0,需給比−1)×感応度)（需要が供給を上回ると値上がり＝供給を呼ぶ）。</summary>
        public static float SurgePrice(float baseFare, float demandSupplyRatio, float surgeSensitivity)
            => Mathf.Max(0f, baseFare) * (1f + Mathf.Max(0f, demandSupplyRatio - 1f) * Mathf.Max(0f, surgeSensitivity));

        /// <summary>配車利益＝プラットフォーム手数料−ドライバー獲得インセンティブ−固定費（ドライバー確保の補助金が嵩む）。</summary>
        public static float RideHailingProfit(float platformRevenue, float driverIncentive, float fixedCost)
            => platformRevenue - Mathf.Max(0f, driverIncentive) - Mathf.Max(0f, fixedCost);
    }
}
