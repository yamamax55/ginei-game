using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 行政物資の充足・不足・消費（STATEDEM-4・#2077・純ロジック）。
    /// 在庫（<see cref="ResourceStockpile"/>）に対し需要を満たし、最も不足する資源で総合充足が決まる（最小律＝`MilitaryReadinessRules`#2049 と同型）。test-first。
    /// </summary>
    public static class StateConsumptionFulfillmentRules
    {
        /// <summary>充足率＝需要0以下は1／clamp01(供給/需要)。</summary>
        public static float Fulfillment(float supply, float demand)
            => demand <= 0f ? 1f : Mathf.Clamp01(Mathf.Max(0f, supply) / demand);

        /// <summary>不足量＝max(0, 需要−供給)。</summary>
        public static float Shortage(float supply, float demand)
            => Mathf.Max(0f, demand - Mathf.Max(0f, supply));

        /// <summary>
        /// 在庫から需要ぶん消費し、満たせなかった不足量を返す（在庫は0未満にならない）。
        /// </summary>
        public static float Consume(ResourceStockpile stock, ResourceType type, float demand)
        {
            if (stock == null || demand <= 0f) return Mathf.Max(0f, demand);
            float have = stock.Get(type);
            float take = Mathf.Min(have, demand);
            stock.Add(type, -take);
            return demand - take;
        }

        /// <summary>総合充足＝物資/弾薬/燃料の充足の最小値（最も不足する資源で決まる）。</summary>
        public static float OverallFulfillment(ResourceStockpile stock, float demandSupplies, float demandAmmo, float demandFuel)
        {
            if (stock == null) return 0f;
            float fS = Fulfillment(stock.Get(ResourceType.物資), demandSupplies);
            float fA = Fulfillment(stock.Get(ResourceType.弾薬), demandAmmo);
            float fF = Fulfillment(stock.Get(ResourceType.燃料), demandFuel);
            return Mathf.Min(fS, Mathf.Min(fA, fF));
        }
    }
}
