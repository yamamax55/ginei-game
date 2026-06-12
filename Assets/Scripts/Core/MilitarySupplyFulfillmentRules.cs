using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 補給充足と欠乏（MILSUP-3・#2049・#94 連携・純ロジック）。
    /// カテゴリ別に 需要×供給（在庫`ResourceStockpile`/補給線#94）→充足率・欠乏。<b>補給切れは枯渇</b>＝部隊が損耗（滅びの時計#94）。
    /// 在庫は delta で消費（暦境界Tick）。集約・後方互換。test-first。
    /// </summary>
    public static class MilitarySupplyFulfillmentRules
    {
        /// <summary>充足率＝min(1, 供給/需要)。需要0以下は1。</summary>
        public static float Fulfillment(float supply, float demand)
            => demand <= 0f ? 1f : Mathf.Clamp01(Mathf.Max(0f, supply) / demand);

        /// <summary>欠乏＝max(0, 需要−供給)。</summary>
        public static float Shortage(float supply, float demand)
            => Mathf.Max(0f, demand - Mathf.Max(0f, supply));

        /// <summary>在庫から消費する量＝min(在庫, 需要)（delta 消費）。</summary>
        public static float Consume(float available, float demand)
            => Mathf.Min(Mathf.Max(0f, available), Mathf.Max(0f, demand));

        /// <summary>補給切れによる損耗＝兵力×損耗率×(1−補給充足)（滅びの時計#94＝干上がった部隊がすり減る）。</summary>
        public static float AttritionFromShortage(float strength, float supplyReadiness, float attritionRate)
            => Mathf.Max(0f, strength) * Mathf.Max(0f, attritionRate) * (1f - Mathf.Clamp01(supplyReadiness));
    }
}
