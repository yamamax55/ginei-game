using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 農業プラント・垂直農法のロジック（業種細分化・水産農林 #2024 の施設栽培サブ業種・#2025・純ロジック・唯一の窓口）：屋内栽培の産出（VFRM-1）／
    /// 照明・空調のエネルギーコスト（VFRM-2＝最大の費目）／惑星環境からの独立（VFRM-3＝屋内ゆえ荒れた惑星でも収量が落ちない）／利益（VFRM-4）。
    /// 露地農業（水産農林#2024の天候変動）と違い、閉鎖施設で天候・季節に依らず多毛作で安定産出＝荒れた惑星（低安定#109/過酷環境）でこそ価値が出る。
    /// 産出は資源#92/#93の食料、消費電力は電力（#2021/#2025）。マクロ近似。test-first。
    /// </summary>
    public static class VerticalFarmRules
    {
        /// <summary>屋内産出＝栽培棚数×1棚あたり収量×年間サイクル数（多段・多毛作＝面積あたり産出が高い）。</summary>
        public static float IndoorYield(int growBeds, float yieldPerBed, int cyclesPerYear)
            => Mathf.Max(0, growBeds) * Mathf.Max(0f, yieldPerBed) * Mathf.Max(0, cyclesPerYear);

        /// <summary>エネルギーコスト＝照明・空調の消費電力×電力単価（屋内栽培の最大の費目＝電力#2025に律速）。</summary>
        public static float EnergyCost(float lightingPower, float energyPrice)
            => Mathf.Max(0f, lightingPower) * Mathf.Max(0f, energyPrice);

        /// <summary>惑星環境を加味した実効収量＝屋内なら過酷さに依らず満額、露地なら過酷さぶん減収（荒れた惑星で屋内栽培の価値が出る）。</summary>
        public static float EffectiveYieldOnPlanet(float baseYield, float planetHarshness, bool isIndoor)
            => isIndoor ? Mathf.Max(0f, baseYield) : Mathf.Max(0f, baseYield) * (1f - Mathf.Clamp01(planetHarshness));

        /// <summary>垂直農法利益＝作物収入−エネルギーコスト−人件費−固定費（高い設備・電力費を多毛作の収量で回収）。</summary>
        public static float VerticalFarmProfit(float cropRevenue, float energyCost, float laborCost, float fixedCost)
            => cropRevenue - Mathf.Max(0f, energyCost) - Mathf.Max(0f, laborCost) - Mathf.Max(0f, fixedCost);
    }
}
