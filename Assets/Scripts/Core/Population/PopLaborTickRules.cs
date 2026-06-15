using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// POP労働・技能の暦境界オーケストレータ（#2026/#2034 配線・純ロジック）。
    /// 惑星（<see cref="Province"/>）の労働技能を1年ぶん形成する＝教育（#155-157）でベースライン上限を決め、OJT（SKILL-6）で各職業の熟練度を上限へ収束。
    /// <b>GalaxyView の年次Tick（<c>RunAnnualLifecycleTick</c>）から呼ぶ薄い窓口</b>＝重い判定は各 SKILL ルールへ委譲。集約（惑星×職業6種）・暦境界Tick・後方互換。test-first。
    /// </summary>
    public static class PopLaborTickRules
    {
        public const float DefaultLearnRate = 0.2f; // OJT の年次学習率（経験曲線）

        /// <summary>
        /// 1年ぶんの労働技能形成。教育の普及率×質でベースライン、職業別の難易度（SKILL-2）＋到達教育（前提）で上限を決め、OJT で収束。
        /// <see cref="Province.skills"/> は null なら既定0から起こす（後方互換）。
        /// </summary>
        public static void TickYear(Province p, float enrollmentRate, float quality, EducationLevel attained, float learnRate)
        {
            if (p == null) return;
            if (p.skills == null) p.skills = new SkillStock();
            float baseline = SkillEducationRules.BaselineSkill(enrollmentRate, quality);
            for (int i = 0; i < SkillStock.Count; i++)
            {
                var o = (Occupation)i;
                if (o == Occupation.無職) continue;
                var major = OccupationClassificationRules.MajorGroupOf(o);
                float difficulty = SkillDifficultyRules.DifficultyOf(major);
                float ceiling = SkillEducationRules.SkillCeiling(baseline, attained, difficulty);
                p.skills.levels[i] = OnTheJobTrainingRules.Advance(p.skills.levels[i], ceiling, learnRate, 0f);
            }
        }

        /// <summary>惑星の総合労働技能（就労シェア重み付き平均・観測/集計用）。workforce 未設定は類型既定で見積り。</summary>
        public static float OverallSkill(Province p)
        {
            if (p == null || p.skills == null) return 0f;
            Workforce w = p.workforce ?? OccupationRules.Default(p.systemType);
            return p.skills.WeightedAverage(w);
        }
    }
}
