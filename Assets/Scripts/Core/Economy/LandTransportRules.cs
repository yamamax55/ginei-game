using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 陸運会社（鉄道/トラック）のロジック（東証33業種「陸運業」・#2024・純ロジック・唯一の窓口）：輸送量×距離＝トンキロ（LND-1）／
    /// 運賃収益（LND-2）／採算＝運賃−燃料−固定費（LND-3）／積載率＝空車を減らすのが鍵（LND-4）。燃料は原油、貨物は各業種の物流へ
    /// 接続。マクロ近似。test-first。
    /// </summary>
    public static class LandTransportRules
    {
        /// <summary>トンキロ＝輸送量×距離（陸運の仕事量の単位）。</summary>
        public static float TonKilometers(float volume, float distance)
            => Mathf.Max(0f, volume) * Mathf.Max(0f, distance);

        /// <summary>運賃収益＝トンキロ×トンキロ単価。</summary>
        public static float TransportRevenue(float tonKilometers, float ratePerTonKm)
            => Mathf.Max(0f, tonKilometers) * Mathf.Max(0f, ratePerTonKm);

        /// <summary>陸運利益＝運賃収益−燃料費−固定費（積載率が低いと固定費を回収できず赤字）。</summary>
        public static float TransportProfit(float revenue, float fuelCost, float fixedCost)
            => revenue - Mathf.Max(0f, fuelCost) - Mathf.Max(0f, fixedCost);

        /// <summary>積載率＝積載/総積載能力（空車・空きスペースを減らすほど効率的）。総能力0以下は0。</summary>
        public static float LoadFactor(float loadedCapacity, float totalCapacity)
            => totalCapacity <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, loadedCapacity) / totalCapacity);
    }
}
