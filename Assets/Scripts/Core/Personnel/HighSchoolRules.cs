using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 高校＝中等教育のロジック（#155-157 LIFE-5/6/7 の土台・純ロジック・唯一の窓口）。POP の若年を教育し、上級学校
    /// （士官学校 <see cref="MilitaryAcademyRules"/>／大学・科挙 <see cref="UniversityRules"/>/<see cref="ImperialExamRules"/>）の
    /// <b>候補の母数</b>（<see cref="EducationFactor"/>＝進学率）と<b>素質</b>（<see cref="EffectiveIntakeQuality"/>＝高校の質を上乗せ）を決める。
    /// 「教育が普及・充実するほど良い士官/官吏が増える」＝人材の土台。マクロ背景として効かせる（タイクン化回避）。test-first。
    /// </summary>
    public static class HighSchoolRules
    {
        /// <summary>良い高校卒が候補生の素質に上乗せする最大ぶん（0..1スケール）。</summary>
        public const float MaxTalentBonus = 0.15f;

        /// <summary>進学率→上級教育の候補プール倍率（0..1）。教育が普及するほど多くが受験資格を得る。</summary>
        public static float EducationFactor(float enrollmentRate) => Mathf.Clamp01(enrollmentRate);

        /// <summary>
        /// 1年の高校卒（若年が高校を出て社会/上級教育へ）。若年人口×卒業該当率（既定の若年→生産年齢移行率）×進学率。
        /// コホート未設定なら population×既定若年比で見積る。表示・見積り用（非破壊）。
        /// </summary>
        public static float AnnualGraduates(Province p, float enrollmentRate)
        {
            if (p == null) return 0f;
            float youth = (p.demographics != null)
                ? p.demographics.youth
                : p.population * PopulationDynamicsRules.DefaultYouthShare;
            float aging = DemographicsRules.VitalRates.Default.youthAging; // 若年が学齢を抜ける割合
            return Mathf.Max(0f, youth * aging * Mathf.Clamp01(enrollmentRate));
        }

        /// <summary>教育の質→候補生の素質上乗せ（0..<see cref="MaxTalentBonus"/>）。良い高校ほど準備の整った人材。</summary>
        public static float TalentBonus(float quality) => Mathf.Clamp01(quality) * MaxTalentBonus;

        /// <summary>
        /// 上級学校の実効教育質＝その学校の質＋高校の質の底上げ（0..1）。候補生の素質（卒業生の能力）に効く。
        /// 高校が良いほど、同じ士官学校/大学でも入ってくる人材が良い。
        /// </summary>
        public static float EffectiveIntakeQuality(float schoolQuality, float highSchoolQuality)
            => Mathf.Clamp01(schoolQuality + TalentBonus(highSchoolQuality));
    }
}
