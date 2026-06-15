using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 幼稚園＝就学前教育のロジック（#155-157 の土台の最下根・純ロジック・唯一の窓口）。就園率が教育チェーンの根を更に下へ、
    /// 質が素質を最も基礎的に底上げする（寄与は全段で最小 <see cref="MaxTalentBonus"/>）。小学校 <see cref="ElementarySchoolRules"/> の下層・同型。
    /// 保育（労働支援/出生）を担う保育園 <see cref="NurseryRules"/> とは効き先が別（こちらは教育チェーン）。マクロ背景（タイクン化回避）。test-first。
    /// </summary>
    public static class KindergartenRules
    {
        /// <summary>素質上乗せの最大ぶん（全段で最小＝最も基礎的）。</summary>
        public const float MaxTalentBonus = 0.03f;

        /// <summary>就園率→上の段（小学校）への進学母数倍率（0..1）。</summary>
        public static float EducationFactor(float enrollmentRate) => Mathf.Clamp01(enrollmentRate);

        /// <summary>1年の修了児（学齢児童×移行率×就園率）。表示・見積り用（非破壊）。</summary>
        public static float AnnualGraduates(Province p, float enrollmentRate)
        {
            if (p == null) return 0f;
            float youth = (p.demographics != null)
                ? p.demographics.youth
                : p.population * PopulationDynamicsRules.DefaultYouthShare;
            float aging = DemographicsRules.VitalRates.Default.youthAging;
            return Mathf.Max(0f, youth * aging * Mathf.Clamp01(enrollmentRate));
        }

        /// <summary>教育の質→素質上乗せ（0..<see cref="MaxTalentBonus"/>）。</summary>
        public static float TalentBonus(float quality) => Mathf.Clamp01(quality) * MaxTalentBonus;

        /// <summary>実効教育質＝渡された質に幼稚園の質を更に上乗せ（0..1）。教育チェーンの最下根として段階的に積む。</summary>
        public static float EffectiveIntakeQuality(float baseQuality, float kindergartenQuality)
            => Mathf.Clamp01(baseQuality + TalentBonus(kindergartenQuality));
    }
}
