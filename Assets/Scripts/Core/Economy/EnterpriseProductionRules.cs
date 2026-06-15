using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 投入制約のもとでの実生産と投入消費（FIRMPROD-4・#2084・純ロジック）。
    /// 実産出＝min(計画, 制約)＝投入不足で工場が遊休（減産）。投入は実産出に比例して消費される。test-first。
    /// </summary>
    public static class EnterpriseProductionRules
    {
        /// <summary>実産出＝投入制約つき産出（<see cref="ProductionConstraintRules.ConstrainedOutput"/>）。</summary>
        public static float RealizedOutput(float plannedOutput, float availMaterials, float availEnergy, float availCapital)
            => ProductionConstraintRules.ConstrainedOutput(plannedOutput, availMaterials, availEnergy, availCapital);

        /// <summary>稼働率＝実産出/計画産出（投入不足で1未満＝遊休）。計画0以下は0。</summary>
        public static float CapacityUtilization(float realizedOutput, float plannedOutput)
            => plannedOutput <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, realizedOutput) / plannedOutput);

        /// <summary>実産出に応じた投入消費＝<see cref="EnterpriseInputRules.InputDemand"/>(実産出, 投入)。減産時は投入も減る。</summary>
        public static float InputConsumed(float realizedOutput, ProductionInput input)
            => EnterpriseInputRules.InputDemand(realizedOutput, input);
    }
}
