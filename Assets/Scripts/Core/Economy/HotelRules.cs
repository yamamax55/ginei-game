using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ホテルのロジック（業種細分化・サービス #2024 の宿泊サブ業種・#2025・純ロジック・唯一の窓口）：稼働率（HTL-1）／
    /// RevPAR＝稼働率×ADR（販売可能客室1室あたり収益・HTL-2）／客室収益（HTL-3）／高固定費レバレッジの利益（HTL-4）。
    /// 装置産業に近い高固定費＝稼働率が損益を大きく振らす（航空#2024と同型）。観光・出張需要に連動。マクロ近似。test-first。
    /// </summary>
    public static class HotelRules
    {
        /// <summary>稼働率＝販売客室/販売可能客室（埋めるほど効率的）。可能客室0以下は0。</summary>
        public static float OccupancyRate(float soldRooms, float availableRooms)
            => availableRooms <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, soldRooms) / availableRooms);

        /// <summary>RevPAR＝稼働率×ADR（平均客室単価）＝販売可能1室あたり収益（ホテルの基幹指標）。</summary>
        public static float RevPar(float occupancyRate, float adr)
            => Mathf.Clamp01(occupancyRate) * Mathf.Max(0f, adr);

        /// <summary>客室収益＝販売可能客室数×RevPAR。</summary>
        public static float RoomRevenue(int availableRooms, float revPar)
            => Mathf.Max(0, availableRooms) * Mathf.Max(0f, revPar);

        /// <summary>ホテル利益＝収益−運営費（高固定費ゆえ稼働率次第で大きく振れる）。</summary>
        public static float HotelProfit(float revenue, float operatingCost)
            => revenue - Mathf.Max(0f, operatingCost);
    }
}
