using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 労働生産性・適所度の産出接続（POPLAB-5・#2026・#110/#93 連携・純ロジック）。
    /// 適所度（<see cref="OccupationRules.AlignmentFactor"/> #110＝類型に合った職の割合）×技能（#2034）×就業率→産出効率#93。
    /// 産出（<see cref="ResourceProductionRules"/>）へ乗算（実効値パターン・基準非破壊）。適材適所（正名#866）が効く。test-first。
    /// </summary>
    public static class LaborProductivityRules
    {
        /// <summary>適所度ボーナス＝0.8+0.4×適所度（0.8..1.2）。類型に合った職ほど効率↑。</summary>
        public static float AlignmentBonus(float alignment)
            => 0.8f + 0.4f * Mathf.Clamp01(alignment);

        /// <summary>技能ボーナス＝0.7+0.6×技能（0.7..1.3）。熟練ほど効率↑（#2034 技能ストック）。</summary>
        public static float SkillBonus(float skill)
            => 0.7f + 0.6f * Mathf.Clamp01(skill);

        /// <summary>労働生産性係数＝適所度ボーナス×技能ボーナス×就業率（1.0が標準）。</summary>
        public static float ProductivityFactor(float alignment, float skill, float employmentRate)
            => AlignmentBonus(alignment) * SkillBonus(skill) * Mathf.Clamp01(employmentRate);

        /// <summary>実効産出＝基準産出#93×労働生産性係数（基準は非破壊）。</summary>
        public static float EffectiveOutput(float baseOutput, float productivityFactor)
            => Mathf.Max(0f, baseOutput) * Mathf.Max(0f, productivityFactor);
    }
}
