using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 環境・リサイクル（廃棄物処理）のロジック（業種細分化・サービス #2024 ／その他製品の資源循環サブ業種・#2025・純ロジック・唯一の窓口）：処理委託料（RCYL-1）／
    /// 回収資源（RCYL-2＝資源#92/#93へ戻る）／再生材の売却収入（RCYL-3）／利益（RCYL-4）。
    /// 廃棄物を引き取る処理委託料（取るほど収入）＋回収した再生材の売却の二重収入＝採掘（#2018）に頼らない都市鉱山（資源#92/#93の循環供給）。マクロ近似。test-first。
    /// </summary>
    public static class RecyclingRules
    {
        /// <summary>処理委託料収入＝廃棄物量×処理単価（廃棄物を引き取るほど収入＝逆有償）。</summary>
        public static float TippingFeeRevenue(float wasteVolume, float feePerVolume)
            => Mathf.Max(0f, wasteVolume) * Mathf.Max(0f, feePerVolume);

        /// <summary>回収資源量＝廃棄物量×回収率（再資源化できた分＝資源#92/#93の循環供給へ戻る）。</summary>
        public static float RecoveredMaterial(float wasteVolume, float recoveryRate)
            => Mathf.Max(0f, wasteVolume) * Mathf.Clamp01(recoveryRate);

        /// <summary>再生材売却収入＝回収資源量×再生材価格（都市鉱山＝採掘#2018に頼らない資源源）。</summary>
        public static float MaterialSalesRevenue(float recoveredMaterial, float materialPrice)
            => Mathf.Max(0f, recoveredMaterial) * Mathf.Max(0f, materialPrice);

        /// <summary>リサイクル利益＝処理委託料+再生材売却−処理コスト−固定費。</summary>
        public static float RecyclingProfit(float tippingRevenue, float materialRevenue, float processingCost, float fixedCost)
            => tippingRevenue + Mathf.Max(0f, materialRevenue) - Mathf.Max(0f, processingCost) - Mathf.Max(0f, fixedCost);
    }
}
