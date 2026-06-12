using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 宇宙採掘（小惑星・ガス採掘）のロジック（業種細分化・鉱山 #2018 の宇宙版サブ業種・#2025・純ロジック・唯一の窓口）：採掘量（AMIN-1）／
    /// 鉱石品位×金属価格の収入（AMIN-2）／探鉱の成否（AMIN-3＝当てれば大鉱床）／利益（AMIN-4＝打ち上げ費が重い）。
    /// 惑星鉱山（#2018）と違い微小重力下の小惑星・ガス採掘＝探鉱の当たり外れが大きく、打ち上げ・回収コストが採算を縛る。産出は希少資源#178/資源#92へ。マクロ近似。test-first。
    /// </summary>
    public static class AsteroidMiningRules
    {
        /// <summary>採掘量＝採掘機数×1機あたり産出×稼働日数（微小重力下の連続採掘）。</summary>
        public static float ExtractedVolume(int miningRigs, float yieldPerRig, int operatingDays)
            => Mathf.Max(0, miningRigs) * Mathf.Max(0f, yieldPerRig) * Mathf.Max(0, operatingDays);

        /// <summary>鉱石価値＝採掘量×品位×金属価格（高品位の小惑星ほど価値が高い）。</summary>
        public static float OreGradeValue(float extractedVolume, float oreGrade, float metalPrice)
            => Mathf.Max(0f, extractedVolume) * Mathf.Clamp01(oreGrade) * Mathf.Max(0f, metalPrice);

        /// <summary>探鉱の成否＝探査精度が閾値以上で鉱床発見（リスク投資＝外れると探査費が丸損）。</summary>
        public static bool ProspectingSuccess(float surveyQuality, float threshold)
            => surveyQuality >= threshold;

        /// <summary>宇宙採掘利益＝鉱石収入−打ち上げ費−運用費−固定費（打ち上げ・回収コストが重い）。</summary>
        public static float AsteroidMiningProfit(float revenue, float launchCost, float operatingCost, float fixedCost)
            => revenue - Mathf.Max(0f, launchCost) - Mathf.Max(0f, operatingCost) - Mathf.Max(0f, fixedCost);
    }
}
