using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 精密機器メーカーのロジック（東証33業種「精密機器」・#2024・純ロジック・唯一の窓口）：高付加価値ニッチ＝少量高採算（PRC-1）／
    /// R&D集約（PRC-2）／技術障壁による価格支配力（PRC-3）。汎用製造は <see cref="ManufacturerRules"/>(#2016)、装置は工作機械(#2023)へ
    /// 接続。マクロ近似。test-first。
    /// </summary>
    public static class PrecisionRules
    {
        /// <summary>ニッチ利益＝販売数×(価格−原価)（少量でも高単価・高マージンで稼ぐ）。</summary>
        public static float NicheProfit(float volume, float unitPrice, float unitCost)
            => Mathf.Max(0f, volume) * (unitPrice - unitCost);

        /// <summary>粗利率＝(価格−原価)/価格（精密機器は高い＝付加価値の高さ）。価格0以下は0。</summary>
        public static float GrossMarginRate(float price, float cost)
            => price <= 0f ? 0f : (price - cost) / price;

        /// <summary>R&D集約度＝研究開発費/売上（高いほど技術で稼ぐ業態）。売上0以下は0。</summary>
        public static float RdIntensity(float rdSpend, float revenue)
            => revenue <= 0f ? 0f : Mathf.Max(0f, rdSpend) / revenue;

        /// <summary>技術リードの価格プレミアム＝基準価格×(1＋(自社技術−競合技術)×リードあたりプレミアム)（技術障壁が価格支配力に）。</summary>
        public static float TechLeadPremium(float ownTech, float rivalTech, float basePrice, float premiumPerLead)
            => Mathf.Max(0f, basePrice) * (1f + (ownTech - rivalTech) * Mathf.Max(0f, premiumPerLead));
    }
}
