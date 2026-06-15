using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 学校経路＝教育による技能ベースライン（SKILL-3・#2034・教育チェーン#155-157 連携・純ロジック）。
    /// 既存の教育チェーン（小〜大学・<see cref="EducationLevel"/>）の<b>普及率×質</b>を POP 大衆の技能ベースラインへ写像。
    /// 高度スキル（SKILL-2 高難度）は前提教育を満たす惑星でしか十分に育たない＝教育格差が技能格差に。ネームド供給（既存）は不変。test-first。
    /// </summary>
    public static class SkillEducationRules
    {
        /// <summary>技能ベースライン＝普及率×質（0..1・大衆の基礎技能）。</summary>
        public static float BaselineSkill(float enrollmentRate, float quality)
            => Mathf.Clamp01(Mathf.Clamp01(enrollmentRate) * Mathf.Clamp01(quality));

        /// <summary>到達教育水準が前提教育を満たすか（高度スキルの取得可否ゲート）。</summary>
        public static bool MeetsPrerequisite(EducationLevel attained, EducationLevel required)
            => (int)attained >= (int)required;

        /// <summary>
        /// スキルの実効習得上限＝前提を満たせばベースライン、満たさなければ難易度ぶん頭打ち
        /// （前提教育の無い惑星で高難度スキルは育たない）。
        /// </summary>
        public static float SkillCeiling(float baselineSkill, EducationLevel attained, float difficulty)
        {
            var required = SkillDifficultyRules.Prerequisite(difficulty);
            if (MeetsPrerequisite(attained, required)) return Mathf.Clamp01(baselineSkill);
            // 前提未達＝難易度が高いほど大きく頭打ち
            return Mathf.Clamp01(baselineSkill * (1f - Mathf.Clamp01(difficulty)));
        }
    }
}
