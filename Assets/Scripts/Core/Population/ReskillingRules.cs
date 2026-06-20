using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// リスキリング＝成人POPの職業転換（SKILL-5・#2034・純ロジック）。
    /// 既存の職を持つ POP が新しいスキルへ転換する＝<b>POPLAB-2 転職フロー #2028 の移動コスト/速度を技能・難易度で決める</b>。
    /// 近い職（同じ大分類）への転換は速く安い／高度スキル（SKILL-2 高難度・前提教育要）への転換は遅い・前提を満たさないと不可／年齢で効率が落ちる。test-first。
    /// </summary>
    public static class ReskillingRules
    {
        /// <summary>転換コスト＝難易度ベース×職業距離係数（同じ大分類=近い=安い／別大分類=遠い=高い）。</summary>
        public static float TransitionCost(OccupationCategory from, OccupationCategory to, float targetDifficulty, float baseCost)
        {
            float distance = (from == to) ? 0.5f : 1.5f; // 近い職は半額・遠い職は1.5倍
            return Mathf.Max(0f, baseCost) * (0.5f + Mathf.Clamp01(targetDifficulty)) * distance;
        }

        /// <summary>年齢効率＝若いほどリスキリングしやすい（基準年齢以下は1.0・以降は逓減）。</summary>
        public static float AgeEfficiency(int age, int peakAge, int retireAge)
        {
            if (age <= peakAge) return 1f;
            if (age >= retireAge) return 0f;
            return Mathf.Clamp01(1f - (float)(age - peakAge) / Mathf.Max(1, retireAge - peakAge));
        }

        /// <summary>前提教育を満たさない高度スキルへは転換不可（ゲート）。</summary>
        public static bool CanReskill(EducationLevel attained, float targetDifficulty)
            => SkillEducationRules.MeetsPrerequisite(attained, SkillDifficultyRules.Prerequisite(targetDifficulty));

        /// <summary>
        /// 転換速度（1tickで進む割合 0..1）＝基準速度×(1−難易度)×年齢効率。前提未達は0（不可）。
        /// 高度スキルへの転換は遅い・若いほど速い。
        /// </summary>
        public static float TransitionSpeed(float baseSpeed, float targetDifficulty, int age, int peakAge, int retireAge, EducationLevel attained)
        {
            if (!CanReskill(attained, targetDifficulty)) return 0f;
            return Mathf.Clamp01(Mathf.Max(0f, baseSpeed) * (1f - Mathf.Clamp01(targetDifficulty)) * AgeEfficiency(age, peakAge, retireAge));
        }
    }
}
