using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍要求物資の暦境界オーケストレータ（MILSUP-6・#2049 配線・純ロジック）。
    /// 戦略艦隊（<see cref="StrategicFleet"/>）の補給レディネスを1期ぶん更新＝補給線#94 が通れば回復、断たれると枯渇して<b>損耗</b>（滅びの時計#94）。
    /// <b>GalaxyView の日次Tick から呼ぶ薄い窓口</b>＝判定は各 MILSUP ルールへ委譲（<see cref="PopConsumptionTickRules"/> と同型）。集約・後方互換。test-first。
    /// </summary>
    public static class MilitarySupplyTickRules
    {
        public const float DefaultRecoverStep = 0.34f; // 補給時の回復（約3日で満補給）
        public const float DefaultDepleteStep = 0.20f; // 補給切れ時の枯渇（約5日で空）
        public const float DefaultAttritionRate = 0.05f; // 干上がった部隊の損耗率

        /// <summary>
        /// 1期ぶんの補給更新。supplied なら回復、補給切れなら枯渇＋損耗（兵力減）。損耗した兵力（int）を返す（プール計上用）。
        /// </summary>
        public static int TickFleet(StrategicFleet f, bool supplied, float recoverStep, float depleteStep, float attritionRate)
        {
            if (f == null) return 0;
            if (supplied)
            {
                f.supply = Mathf.Clamp01(f.supply + Mathf.Max(0f, recoverStep));
                return 0;
            }
            f.supply = Mathf.Clamp01(f.supply - Mathf.Max(0f, depleteStep));
            int lost = Mathf.RoundToInt(MilitarySupplyFulfillmentRules.AttritionFromShortage(f.strength, f.supply, attritionRate));
            f.strength = Mathf.Max(0, f.strength - lost);
            return lost;
        }

        /// <summary>既定パラメータでの1期Tick。</summary>
        public static int TickFleet(StrategicFleet f, bool supplied)
            => TickFleet(f, supplied, DefaultRecoverStep, DefaultDepleteStep, DefaultAttritionRate);
    }
}
