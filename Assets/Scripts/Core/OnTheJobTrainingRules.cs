using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// OJT・現場習熟（SKILL-6・#2034・純ロジック）。
    /// 就労を続けると技能が上がる（叩き上げ）＝経験曲線で目標上限へ収束（逓減）。ただし<b>上限は教育/難易度で律速</b>
    /// （前提教育の無い高度スキルは OJT だけでは頭打ち・<see cref="SkillEducationRules.SkillCeiling"/>）。企業内訓練（SKILL-4）で効率↑。
    /// 既存の差分収束規律に従う。test-first。
    /// </summary>
    public static class OnTheJobTrainingRules
    {
        /// <summary>1tickの習熟＝(上限−現在)×学習率×(1+企業内訓練ボーナス)（経験曲線・逓減）。負なら0。</summary>
        public static float OjtGain(float currentSkill, float skillCeiling, float learnRate, float corporateTrainingBonus)
        {
            float gap = Mathf.Clamp01(skillCeiling) - Mathf.Clamp01(currentSkill);
            if (gap <= 0f) return 0f;
            return gap * Mathf.Clamp01(learnRate) * (1f + Mathf.Max(0f, corporateTrainingBonus));
        }

        /// <summary>1tick後の技能＝現在＋習熟（上限でクランプ）。</summary>
        public static float Advance(float currentSkill, float skillCeiling, float learnRate, float corporateTrainingBonus)
            => Mathf.Min(Mathf.Clamp01(skillCeiling),
                Mathf.Clamp01(currentSkill) + OjtGain(currentSkill, skillCeiling, learnRate, corporateTrainingBonus));

        /// <summary>習熟上限＝教育/難易度で律速（前提を満たさない高度スキルは頭打ち＝SKILL-3 へ委譲）。</summary>
        public static float SkillCeiling(float educationBaseline, EducationLevel attained, float difficulty)
            => SkillEducationRules.SkillCeiling(educationBaseline, attained, difficulty);
    }
}
