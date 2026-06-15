using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// データセンターのロジック（業種細分化・情報通信 #2024 ×電力 #2025 ×不動産のサブ業種・#2025・純ロジック・唯一の窓口）：コロケーション収入（DC-1）／
    /// 電力使用効率PUE（DC-2＝全施設電力/IT電力）／電力運用コスト（DC-3＝最大の費目）／利益（DC-4）。
    /// ラック貸し（不動産的）＋膨大な電力消費（電力#2025に律速）＝PUEが低いほど（冷却が効率的なほど）採算が良い。SaaS#2025/AI計算の土台インフラ。マクロ近似。test-first。
    /// </summary>
    public static class DataCenterRules
    {
        /// <summary>コロケーション収入＝ラック数×月額ラック料（サーバ設置スペースを貸す＝不動産的収益）。</summary>
        public static float ColocationRevenue(int racks, float monthlyRackFee)
            => Mathf.Max(0, racks) * Mathf.Max(0f, monthlyRackFee);

        /// <summary>PUE（電力使用効率）＝全施設電力/IT機器電力（1.0に近いほど冷却ロスが小さく効率的）。IT電力0以下は0。</summary>
        public static float PowerUsageEffectiveness(float totalFacilityPower, float itPower)
            => itPower <= 0f ? 0f : Mathf.Max(0f, totalFacilityPower) / itPower;

        /// <summary>電力運用コスト＝IT電力×PUE×電力単価（IT機器＋冷却の総電力＝最大の費目で電力#2025に律速）。</summary>
        public static float PowerOperatingCost(float itPower, float pue, float energyPrice)
            => Mathf.Max(0f, itPower) * Mathf.Max(0f, pue) * Mathf.Max(0f, energyPrice);

        /// <summary>データセンター利益＝コロケーション収入−電力コスト−固定費。</summary>
        public static float DataCenterProfit(float revenue, float powerCost, float fixedCost)
            => revenue - Mathf.Max(0f, powerCost) - Mathf.Max(0f, fixedCost);
    }
}
