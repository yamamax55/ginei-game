using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 暗号資産・分散金融のロジック（業種細分化・その他金融の投機サブ業種・#2025・純ロジック・唯一の窓口）：採掘報酬（CRYP-1）／
    /// 保有資産の時価（CRYP-2）／ボラティリティのドローダウン（CRYP-3＝高騰と暴落）／取引所の手数料収入（CRYP-4）。
    /// 裏付けの薄い投機資産＝価格変動が激しく一攫千金と暴落が表裏。取引所は売買手数料で堅実に稼ぐ（フェザーン#160的な無国籍金融）。マクロ近似。test-first。
    /// </summary>
    public static class CryptoAssetRules
    {
        /// <summary>採掘報酬＝自分のハッシュシェア×ブロック報酬（計算力を提供した割合で新規発行を得る）。</summary>
        public static float MiningReward(float hashShare, float blockReward)
            => Mathf.Clamp01(hashShare) * Mathf.Max(0f, blockReward);

        /// <summary>保有資産の時価＝保有量×価格（価格は乱高下する）。</summary>
        public static float MarketValue(float holdings, float price)
            => Mathf.Max(0f, holdings) * Mathf.Max(0f, price);

        /// <summary>ドローダウン＝(ピーク時価−現在時価)/ピーク時価（高値からの下落率＝暴落の深さ）。ピーク0以下は0。</summary>
        public static float VolatilityDrawdown(float peakValue, float currentValue)
            => peakValue <= 0f ? 0f : Mathf.Clamp01((peakValue - Mathf.Max(0f, currentValue)) / peakValue);

        /// <summary>取引所手数料収入＝取引高×手数料率（投機の熱狂で売買が増えるほど取引所が儲かる）。</summary>
        public static float TradingFeeRevenue(float tradeVolume, float feeRate)
            => Mathf.Max(0f, tradeVolume) * Mathf.Clamp01(feeRate);
    }
}
