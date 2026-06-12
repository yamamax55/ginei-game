using UnityEngine;

namespace Ginei
{
    /// <summary>企業の生産投入（FIRMPROD-1・#2084）。原材料／労働／エネルギー／資本財。</summary>
    public enum ProductionInput { 原材料, 労働, エネルギー, 資本財 }

    /// <summary>
    /// 企業の投入係数（FIRMPROD-1・#2084・lookup・唯一の窓口）。
    /// 単位産出あたりに必要な投入＝技術係数（レオンチェフ型＝固定係数）。`EnterpriseRules.Output`#1022 の計画産出に掛けて投入需要を得る。
    /// 業種別の強度は呼び側が intensity で渡す。`MilitarySupplyRules`#2049/`AdministrationConsumptionRules`#2077 と同型。test-first。
    /// </summary>
    public static class EnterpriseInputRules
    {
        // 単位産出あたりの投入係数 [ProductionInput 原材料/労働/エネルギー/資本財]。唯一の出所。
        private static readonly float[] coefficients = { 0.5f, 0.2f, 0.3f, 0.1f };

        /// <summary>単位産出あたりの投入係数。</summary>
        public static float InputCoefficient(ProductionInput input)
            => coefficients[(int)input];

        /// <summary>投入需要＝計画産出×係数×強度（業種別 intensity は呼び側が渡す・既定1）。</summary>
        public static float InputDemand(float plannedOutput, ProductionInput input, float intensity = 1f)
            => Mathf.Max(0f, plannedOutput) * InputCoefficient(input) * Mathf.Max(0f, intensity);
    }
}
