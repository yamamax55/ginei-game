using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 惑星（<see cref="Province"/>）の行政物資需要（STATEDEM-2・#2077・純ロジック）。
    /// 人口規模の行政・インフラ・公共サービスが物資を消費＝人口×原単位（<see cref="AdministrationConsumptionRules"/>）。
    /// 人口の多い惑星ほど行政コストが嵩む。集約（個体粒度へ降りない）。test-first。
    /// </summary>
    public static class PlanetMaterialDemandRules
    {
        /// <summary>惑星の行政物資需要＝人口×1人あたり原単位。</summary>
        public static float Demand(Province planet, ResourceType type)
            => planet == null ? 0f : Mathf.Max(0f, planet.population) * AdministrationConsumptionRules.PerCapitaRate(type);

        /// <summary>全資源の需要合計（規模感）。</summary>
        public static float TotalDemand(Province planet)
            => Demand(planet, ResourceType.物資) + Demand(planet, ResourceType.弾薬) + Demand(planet, ResourceType.燃料);
    }
}
