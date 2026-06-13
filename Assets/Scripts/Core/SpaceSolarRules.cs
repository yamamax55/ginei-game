using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 宇宙太陽光発電のロジック（業種細分化・電力 #2021/#2025 の宇宙発電サブ業種・#2025・純ロジック・唯一の窓口）：軌道での発電量（SSOL-1）／
    /// 地上への送電（SSOL-2＝ビーミング効率の伝送ロス）／売電収入（SSOL-3）／利益（SSOL-4）。
    /// 大気・天候・夜に左右されず軌道で常時発電し地上へビーム送電＝電力小売（#2025）/公益（#2021）へベースロード電力を供給。打ち上げ費の償却が重い。マクロ近似。test-first。
    /// </summary>
    public static class SpaceSolarRules
    {
        /// <summary>軌道発電量＝パネル面積×太陽光強度×変換効率（軌道は大気減衰・夜が無く常時発電）。</summary>
        public static float OrbitalGeneration(float panelArea, float solarFlux, float efficiency)
            => Mathf.Max(0f, panelArea) * Mathf.Max(0f, solarFlux) * Mathf.Clamp01(efficiency);

        /// <summary>地上送電量＝発電量×ビーミング効率（マイクロ波/レーザー送電の伝送ロスを差し引く）。</summary>
        public static float BeamingDelivery(float generatedPower, float beamingEfficiency)
            => Mathf.Max(0f, generatedPower) * Mathf.Clamp01(beamingEfficiency);

        /// <summary>売電収入＝送電量×売電単価（電力小売#2025/公益#2021へ売る）。</summary>
        public static float PowerSalesRevenue(float deliveredPower, float pricePerUnit)
            => Mathf.Max(0f, deliveredPower) * Mathf.Max(0f, pricePerUnit);

        /// <summary>宇宙太陽光利益＝売電収入−打ち上げ償却−保守費−固定費（打ち上げ費の償却が最大の費目）。</summary>
        public static float SpaceSolarProfit(float powerRevenue, float launchAmortization, float maintenanceCost, float fixedCost)
            => powerRevenue - Mathf.Max(0f, launchAmortization) - Mathf.Max(0f, maintenanceCost) - Mathf.Max(0f, fixedCost);
    }
}
