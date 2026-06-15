using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 賃金の暦境界オーケストレータ（POPLAB-4 配線・#2026/#1969・純ロジック）。
    /// 惑星（<see cref="Province"/>）の労働賃金指数を1年ぶん調整＝<b>労働逼迫（就業率＝人手不足）×技能（#2034）</b>で目標へ収束（粘着＝緩やか）。
    /// 人手不足・高技能の惑星ほど賃金が高くなり、生活水準#181/支持#113 の係数に効く（実効値パターン・基準非破壊）。集約・暦境界Tick・後方互換。test-first。
    /// </summary>
    public static class LaborWageTickRules
    {
        public const float DefaultAdjustRate = 0.3f; // 年次の賃金調整速度（粘着＝緩やか）

        /// <summary>目標賃金指数＝(0.7+0.3×就業率)×(0.8+0.4×技能)。逼迫（高就業）と高技能で上がる。</summary>
        public static float TargetWageIndex(float employmentRate, float skill)
            => (0.7f + 0.3f * Mathf.Clamp01(employmentRate)) * (0.8f + 0.4f * Mathf.Clamp01(skill));

        /// <summary>1年ぶんの賃金調整＝就業率（<see cref="OccupationRules.EmploymentRate"/>）×技能（<see cref="PopLaborTickRules.OverallSkill"/>）の目標へ粘着収束。</summary>
        public static void TickYear(Province p, float adjustRate)
        {
            if (p == null) return;
            float emp = OccupationRules.EmploymentRate(p);
            float skill = PopLaborTickRules.OverallSkill(p);
            float target = TargetWageIndex(emp, skill);
            p.wageIndex += (target - p.wageIndex) * Mathf.Clamp01(adjustRate);
        }
    }
}
