using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 放送・メディアのロジック（業種細分化・情報通信 #2024 の放送サブ業種・#2025・純ロジック・唯一の窓口）：視聴率（BC-1）／
    /// 視聴率に連動する広告枠収入（BC-2）／有料放送の購読収入（BC-3）／利益（BC-4）。
    /// 視聴率が広告単価を決める広告モデルと、加入者課金の有料放送モデルの二本柱＝コンテンツ制作費が主コスト（情報通信#2024の業態特化）。
    /// 政治の世論（支持#113）・情報戦（#819）の土台にもなりうる。マクロ近似。test-first。
    /// </summary>
    public static class BroadcastRules
    {
        /// <summary>視聴率＝視聴世帯/総世帯（広告単価の源＝高いほど広告枠が高く売れる）。総世帯0以下は0。</summary>
        public static float AudienceRating(float viewers, float totalHouseholds)
            => totalHouseholds <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, viewers) / totalHouseholds);

        /// <summary>広告枠収入＝視聴ポイント（GRP）×ポイント単価（視聴率×投下量で広告価値が決まる）。</summary>
        public static float AdRevenue(float ratingPoints, float pricePerPoint)
            => Mathf.Max(0f, ratingPoints) * Mathf.Max(0f, pricePerPoint);

        /// <summary>有料放送収入＝加入者数×月額（広告に依らない安定収入）。</summary>
        public static float SubscriptionRevenue(int subscribers, float monthlyFee)
            => Mathf.Max(0, subscribers) * Mathf.Max(0f, monthlyFee);

        /// <summary>放送利益＝広告収入+購読収入−コンテンツ制作費−固定費（制作費が主コスト）。</summary>
        public static float BroadcastProfit(float adRevenue, float subscriptionRevenue, float contentCost, float fixedCost)
            => adRevenue + subscriptionRevenue - Mathf.Max(0f, contentCost) - Mathf.Max(0f, fixedCost);
    }
}
