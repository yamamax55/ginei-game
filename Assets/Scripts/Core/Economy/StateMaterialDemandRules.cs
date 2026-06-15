using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 国家（<see cref="Faction"/>）の行政物資需要（STATEDEM-3・#2077・純ロジック）。
    /// 所有惑星の集約（<see cref="PlanetMaterialDemandRules"/>）＋中央政府の overhead（版図規模＝星系数に比例）。
    /// 版図が広いほど中央統治コストが嵩む（`LogisticsRules`#844 と整合）。集約。test-first。
    /// </summary>
    public static class StateMaterialDemandRules
    {
        // 星系1つあたりの中央政府 overhead 原単位 [ResourceType]。版図規模に比例（弾薬は行政が消費しない）。
        private static readonly float[] centralRates = { 5.0f, 0.0f, 2.0f }; // 物資/弾薬/燃料

        /// <summary>所有惑星の行政需要の合計。</summary>
        public static float AggregateDemand(IReadOnlyList<Province> provinces, ResourceType type)
        {
            if (provinces == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < provinces.Count; i++) sum += PlanetMaterialDemandRules.Demand(provinces[i], type);
            return sum;
        }

        /// <summary>中央政府の overhead＝星系数×原単位（版図規模）。</summary>
        public static float CentralOverhead(int systemCount, ResourceType type)
            => Mathf.Max(0, systemCount) * centralRates[(int)type];

        /// <summary>国家の総需要＝惑星集約＋中央 overhead。</summary>
        public static float TotalStateDemand(IReadOnlyList<Province> provinces, int systemCount, ResourceType type)
            => AggregateDemand(provinces, type) + CentralOverhead(systemCount, type);
    }
}
