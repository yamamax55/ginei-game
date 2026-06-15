using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 充足率・不足の判定（POPDEM-3・#2042・#93/#94/#179 連携・純ロジック）。
    /// カテゴリ別に 需要×供給（生産#93/在庫/補給#94/市場#179）→充足率・不足を出す。<b>必需の不足は飢餓</b>＝大きな不満#113・人口減#153 圧。
    /// 在庫は delta で消費（暦境界Tick）。集約・後方互換。test-first。
    /// </summary>
    public static class ConsumptionFulfillmentRules
    {
        /// <summary>充足率＝min(1, 供給/需要)。需要0以下は1（要らないものは満たされている）。</summary>
        public static float Fulfillment(float supply, float demand)
            => demand <= 0f ? 1f : Mathf.Clamp01(Mathf.Max(0f, supply) / demand);

        /// <summary>不足＝max(0, 需要−供給)。</summary>
        public static float Shortage(float supply, float demand)
            => Mathf.Max(0f, demand - Mathf.Max(0f, supply));

        /// <summary>飢餓の深さ＝1−必需充足率（必需が満たされないほど飢餓が深い・不満#113/人口減#153 へ）。</summary>
        public static float FamineSeverity(float necessityFulfillment)
            => Mathf.Clamp01(1f - Mathf.Clamp01(necessityFulfillment));

        /// <summary>在庫から消費する量＝min(在庫, 需要)（delta 消費・暦境界Tick）。</summary>
        public static float Consume(float available, float demand)
            => Mathf.Min(Mathf.Max(0f, available), Mathf.Max(0f, demand));
    }
}
