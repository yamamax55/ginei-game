using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 報道・通信社のロジック（業種細分化・情報通信 #2024 の報道サブ業種・#2025・純ロジック・唯一の窓口）：購読収入（NEWS-1）／
    /// 通信社の配信料（NEWS-2＝他メディアへニュースを売る）／世論への影響度（NEWS-3＝情報戦#819/支持#113の土台）／利益（NEWS-4）。
    /// 購読料＋他メディアへの配信料で稼ぎ、発行部数×信頼度が世論（支持#113）への影響力になる＝情報戦（#819）・開示エンジン（#495）の供給源。取材費が主コスト。マクロ近似。test-first。
    /// </summary>
    public static class NewsAgencyRules
    {
        /// <summary>購読収入＝購読者数×月額（読者から直接得る収入）。</summary>
        public static float SubscriptionRevenue(int subscribers, float monthlyFee)
            => Mathf.Max(0, subscribers) * Mathf.Max(0f, monthlyFee);

        /// <summary>配信料収入＝契約メディア数×配信ライセンス料（通信社＝他の新聞・放送#2025へニュースを卸す）。</summary>
        public static float WireServiceRevenue(int mediaClients, float licenseFee)
            => Mathf.Max(0, mediaClients) * Mathf.Max(0f, licenseFee);

        /// <summary>世論影響度＝発行部数×信頼度（情報戦#819/支持#113を動かす力＝信頼度が低いと届かない）。</summary>
        public static float InfluenceReach(float circulation, float credibility)
            => Mathf.Max(0f, circulation) * Mathf.Clamp01(credibility);

        /// <summary>報道・通信社利益＝購読収入+配信料−取材費−固定費（取材ネットワークが主コスト）。</summary>
        public static float NewsAgencyProfit(float subscriptionRevenue, float wireRevenue, float reportingCost, float fixedCost)
            => subscriptionRevenue + Mathf.Max(0f, wireRevenue) - Mathf.Max(0f, reportingCost) - Mathf.Max(0f, fixedCost);
    }
}
