using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 小学校＝初等教育のロジック（#155-157 の土台の根・中学校 <see cref="MiddleSchoolRules"/> の下層・純ロジック・唯一の窓口）。
    /// ほぼ全員が受ける基礎教育＝識字・基礎学力の裾野。就学率が<b>中学校→高校→上級学校</b>の進学率複利の根を成し、質が素質を底上げする
    /// （最も基礎的＝寄与最小 <see cref="MaxTalentBonus"/>）。「初等教育が普及・充実するほど国全体の人材の母数と質が太る」。マクロ背景（タイクン化回避）。test-first。
    /// </summary>
    public static class ElementarySchoolRules
    {
        /// <summary>良い初等教育が候補生の素質に上乗せする最大ぶん（最小＝最も基礎的）。</summary>
        public const float MaxTalentBonus = 0.05f;

        /// <summary>就学率→上の段（中学校）への進学母数倍率（0..1）。</summary>
        public static float EducationFactor(float enrollmentRate) => Mathf.Clamp01(enrollmentRate);

        /// <summary>1年の小学校卒（学齢児童×移行率×就学率）。表示・見積り用（非破壊）。</summary>
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

        /// <summary>実効教育質＝渡された質に初等教育の質を更に上乗せ（0..1）。教育チェーンの根として段階的に積む。</summary>
        public static float EffectiveIntakeQuality(float baseQuality, float elementaryQuality)
            => Mathf.Clamp01(baseQuality + TalentBonus(elementaryQuality));
    }
}
