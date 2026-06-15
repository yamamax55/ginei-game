using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// POPの居住需要と住宅充足の純ロジック（VCHAIN-5・#2091・経済の出口）。
    /// 住宅ストックが人口の居住需要をどれだけ満たすか＝充足率→生活水準#181/支持#113。住宅は経年劣化で減る（建て続けないと不足）。
    /// POP消費#2042 の住宅版。test-first。
    /// </summary>
    public static class HousingDemandRules
    {
        /// <summary>居住需要＝人口×1人あたり必要戸数。</summary>
        public static float HousingDemand(float population, float perCapitaUnits)
            => Mathf.Max(0f, population) * Mathf.Max(0f, perCapitaUnits);

        /// <summary>充足率＝clamp01(住宅ストック/需要)。需要0以下は1（満たされ済み）。</summary>
        public static float Occupancy(float housingStock, float demand)
            => demand <= 0f ? 1f : Mathf.Clamp01(Mathf.Max(0f, housingStock) / demand);

        /// <summary>住宅不足＝max(0, 需要−ストック)。</summary>
        public static float Shortage(float housingStock, float demand)
            => Mathf.Max(0f, demand - Mathf.Max(0f, housingStock));

        /// <summary>住宅余剰＝max(0, ストック−需要)。</summary>
        public static float Surplus(float housingStock, float demand)
            => Mathf.Max(0f, Mathf.Max(0f, housingStock) - demand);

        /// <summary>住宅の経年劣化＝max(0, 住宅×(1−劣化率))。建て続けないと足りなくなる。</summary>
        public static float Depreciate(float housing, float depreciationRate)
            => Mathf.Max(0f, Mathf.Max(0f, housing) * (1f - Mathf.Clamp01(depreciationRate)));

        /// <summary>住宅不足→支持#113 の増減＝−(1−充足)×スケール（不足で低下）。</summary>
        public static float ShortageSupportDelta(float occupancy, float scale)
            => -(1f - Mathf.Clamp01(occupancy)) * Mathf.Max(0f, scale);

        /// <summary>生活水準#181 への住宅寄与倍率＝Lerp(min, 1, 充足)（不足で生活水準が頭打ち）。</summary>
        public static float LivingStandardFactor(float occupancy, float minFactor)
            => Mathf.Lerp(Mathf.Clamp01(minFactor), 1f, Mathf.Clamp01(occupancy));
    }
}
