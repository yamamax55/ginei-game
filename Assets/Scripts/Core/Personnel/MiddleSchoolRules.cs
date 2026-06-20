using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 中学校＝前期中等教育のロジック（#155-157 の土台・高校 <see cref="HighSchoolRules"/> の下層・純ロジック・唯一の窓口）。
    /// 中学校の進学率が<b>高校への進学母数</b>を、質が候補の素質の底上げに効く。上級教育の候補母数は中学校×高校×…の<b>複利</b>で決まる
    /// （裾野の教育が人材の母数を決める）。寄与は高校より小さい（より基礎的）。マクロ背景（タイクン化回避）。test-first。
    /// </summary>
    public static class MiddleSchoolRules
    {
        /// <summary>良い中学校卒が候補生の素質に上乗せする最大ぶん（高校より小さい＝基礎的）。</summary>
        public const float MaxTalentBonus = 0.10f;

        /// <summary>進学率→高校への進学母数倍率（0..1）。普及するほど多くが高校へ進む。</summary>
        public static float EducationFactor(float enrollmentRate) => Mathf.Clamp01(enrollmentRate);

        /// <summary>1年の中学校卒（若年×移行率×進学率）。表示・見積り用（非破壊）。</summary>
        public static float AnnualGraduates(Province p, float enrollmentRate)
        {
            if (p == null) return 0f;
            float youth = (p.demographics != null)
                ? p.demographics.youth
                : p.population * PopulationDynamicsRules.DefaultYouthShare;
            float aging = DemographicsRules.VitalRates.Default.youthAging;
            return Mathf.Max(0f, youth * aging * Mathf.Clamp01(enrollmentRate));
        }

        /// <summary>教育の質→候補生の素質上乗せ（0..<see cref="MaxTalentBonus"/>）。</summary>
        public static float TalentBonus(float quality) => Mathf.Clamp01(quality) * MaxTalentBonus;

        /// <summary>実効教育質＝渡された質（学校＋高校で底上げ済）に中学校の質を更に上乗せ（0..1）。教育チェーンで段階的に積む。</summary>
        public static float EffectiveIntakeQuality(float baseQuality, float middleSchoolQuality)
            => Mathf.Clamp01(baseQuality + TalentBonus(middleSchoolQuality));
    }
}
