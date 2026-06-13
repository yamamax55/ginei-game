using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 消費財の需要と充足の汎用ロジック（BOM-5・#2098）。消費財（食品/衣類/住宅…）が人口の需要をどれだけ満たすか。
    /// `HousingDemandRules`#2091 の汎用版＝任意の消費財品目に適用。消費財は使われて在庫が減る。不足→生活水準#181/支持#113。test-first。
    /// </summary>
    public static class ConsumerDemandRules
    {
        /// <summary>消費財需要＝人口×1人あたり原単位。</summary>
        public static float Demand(float population, float perCapita)
            => Mathf.Max(0f, population) * Mathf.Max(0f, perCapita);

        /// <summary>充足率＝clamp01(在庫/需要)。需要0以下は1。</summary>
        public static float Fulfillment(float stockAmount, float demand)
            => demand <= 0f ? 1f : Mathf.Clamp01(Mathf.Max(0f, stockAmount) / demand);

        /// <summary>在庫から需要ぶん消費し、満たせなかった不足量を返す（消費財は使われて減る）。</summary>
        public static float Consume(CommodityStock stock, int commodityId, float demand)
        {
            if (stock == null || demand <= 0f) return Mathf.Max(0f, demand);
            float have = stock.Get(commodityId);
            float take = Mathf.Min(have, demand);
            stock.Add(commodityId, -take);
            return demand - take;
        }

        /// <summary>生活水準#181 への寄与倍率＝Lerp(min, 1, 充足)（不足で生活水準が頭打ち）。</summary>
        public static float LivingStandardFactor(float fulfillment, float minFactor)
            => Mathf.Lerp(Mathf.Clamp01(minFactor), 1f, Mathf.Clamp01(fulfillment));

        /// <summary>支持#113 の増減＝−(1−充足)×スケール（消費財不足で不満）。</summary>
        public static float SupportDelta(float fulfillment, float scale)
            => -(1f - Mathf.Clamp01(fulfillment)) * Mathf.Max(0f, scale);
    }
}
