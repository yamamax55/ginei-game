using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 航空会社のロジック（東証33業種「空運業」・#2024・純ロジック・唯一の窓口）：座席稼働率（ロードファクター）が収益を決める
    /// （AIR-1）／損益分岐ロードファクター（AIR-2）／便採算＝運賃収入−燃料−固定費（AIR-3）。高固定費＝1席でも多く埋めるのが勝負
    /// （イールドマネジメント）。原油（燃料）・家計（#1969）へ接続。マクロ近似（個便 micro は持たない）。test-first。
    /// </summary>
    public static class AirlineRules
    {
        /// <summary>旅客収入＝座席数×ロードファクター×運賃（埋まった席ぶんだけ稼ぐ）。</summary>
        public static float PassengerRevenue(float seats, float loadFactor, float fare)
            => Mathf.Max(0f, seats) * Mathf.Clamp01(loadFactor) * Mathf.Max(0f, fare);

        /// <summary>損益分岐ロードファクター＝固定費/(座席数×運賃)（この稼働率を超えれば黒字＝高固定費ゆえ高水準）。分母0以下は1.0超。</summary>
        public static float BreakEvenLoadFactor(float fixedCost, float seats, float fare)
        {
            float denom = Mathf.Max(0f, seats) * fare;
            return denom <= 0f ? 999f : Mathf.Max(0f, fixedCost) / denom;
        }

        /// <summary>便利益＝旅客収入−燃料費−固定費（埋まらない便は赤字を垂れ流す）。</summary>
        public static float FlightProfit(float passengerRevenue, float fuelCost, float fixedCost)
            => passengerRevenue - Mathf.Max(0f, fuelCost) - Mathf.Max(0f, fixedCost);
    }
}
