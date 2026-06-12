using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 企業の投入需要（FIRMPROD-2・#2084・純ロジック）。
    /// 計画産出（<see cref="EnterpriseRules.Output"/>#1022）から投入需要を導く（<see cref="EnterpriseInputRules"/>）。
    /// 労働需要は既存 `EnterpriseRules.LaborDemand`#1022 に委譲＝本系統は原材料/エネルギー/資本財の物的投入を主に扱う。集約。test-first。
    /// </summary>
    public static class EnterpriseDemandRules
    {
        /// <summary>企業の投入需要＝計画産出×投入係数。</summary>
        public static float Demand(Enterprise e, ProductionInput input)
            => EnterpriseInputRules.InputDemand(EnterpriseRules.Output(e), input);

        /// <summary>計画産出（float）からの投入需要（Enterprise を持たない見積り用）。</summary>
        public static float DemandForOutput(float plannedOutput, ProductionInput input)
            => EnterpriseInputRules.InputDemand(plannedOutput, input);

        /// <summary>勢力/業種の集約投入需要＝企業群の合計。</summary>
        public static float AggregateDemand(IReadOnlyList<Enterprise> enterprises, ProductionInput input)
        {
            if (enterprises == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < enterprises.Count; i++) sum += Demand(enterprises[i], input);
            return sum;
        }
    }
}
