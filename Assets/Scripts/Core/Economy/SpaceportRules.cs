using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 宇宙港運営会社のロジック（業種細分化・空港会社サブ業種を宇宙設定へ・#2025・純ロジック・唯一の窓口）：星系の軌道港を運営し、
    /// 着陸料の航空系収入（PORT-1）／免税店テナントの物販系収入（PORT-2）／旅客施設使用料（PORT-3）／利益（PORT-4）。
    /// 宇宙鉄道（#2025）・空運（#2024）の発着料（航空系）と、免税店・物販テナント（非航空系）の二本柱＝旅客数が両方を押し上げる装置産業。
    /// 星系（<see cref="StarSystem"/>）のハブ機能として効かせる。マクロ近似。test-first。
    /// </summary>
    public static class SpaceportRules
    {
        /// <summary>着陸料収入＝発着回数×1回あたり着陸料（航空系収入＝宇宙鉄道/空運の発着で稼ぐ）。</summary>
        public static float LandingFeeRevenue(int arrivals, float feePerArrival)
            => Mathf.Max(0, arrivals) * Mathf.Max(0f, feePerArrival);

        /// <summary>免税店テナント収入＝旅客数×1人あたり消費×テナント料率（非航空系＝旅客の買い物から歩合を取る）。</summary>
        public static float ConcessionRevenue(float passengers, float spendPerPassenger, float concessionRate)
            => Mathf.Max(0f, passengers) * Mathf.Max(0f, spendPerPassenger) * Mathf.Clamp01(concessionRate);

        /// <summary>旅客施設使用料＝旅客数×1人あたり施設使用料（PSFC＝旅客から直接徴収）。</summary>
        public static float PassengerServiceCharge(float passengers, float chargePerPassenger)
            => Mathf.Max(0f, passengers) * Mathf.Max(0f, chargePerPassenger);

        /// <summary>宇宙港利益＝航空系収入+非航空系収入−運営費（高固定費＝旅客数が両収入を押し上げる）。</summary>
        public static float SpaceportProfit(float aeroRevenue, float nonAeroRevenue, float operatingCost)
            => aeroRevenue + nonAeroRevenue - Mathf.Max(0f, operatingCost);
    }
}
