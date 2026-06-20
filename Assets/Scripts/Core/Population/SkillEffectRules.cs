using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 技能の需給接続（SKILL-7・#2034・#93/#1969/#96 連携・純ロジック）。
    /// 形成した技能（SKILL-1〜6）を労働システム #2026 へ橋渡し：生産性#93（熟練で産出↑・POPLAB-5）／賃金#1969（希少スキルは高給・POPLAB-4）／
    /// 徴募#96 の質（高度な航宙士が居る勢力は強い）。実効値パターン（基準非破壊）。test-first。
    /// </summary>
    public static class SkillEffectRules
    {
        /// <summary>生産性寄与係数＝0.7+0.6×技能（熟練で産出#93↑。POPLAB-5 の SkillBonus と整合）。</summary>
        public static float ProductivityContribution(float skill)
            => LaborProductivityRules.SkillBonus(skill);

        /// <summary>賃金プレミアム＝希少性（難易度）×技能（希少スキルを高熟練で持つほど高給#1969）。</summary>
        public static float WagePremium(float skill, float difficulty, float coefficient)
            => 1f + Mathf.Clamp01(skill) * Mathf.Clamp01(difficulty) * Mathf.Max(0f, coefficient);

        /// <summary>軍の質＝軍属技能（保安・整備・運転・航宙）が会戦/兵站の質に効く（#96・高度な航宙士で強い）。</summary>
        public static float MilitaryQuality(float militarySkill, float baseline)
            => Mathf.Max(0f, baseline) * (0.6f + 0.4f * Mathf.Clamp01(militarySkill));

        /// <summary>人手不足×希少スキル＝賃金が跳ねる（需給#POPLAB-4×希少性#SKILL-2の合成）。</summary>
        public static float ScarcityWageFactor(float demandFactor, float skill, float difficulty, float coefficient)
            => Mathf.Max(0f, demandFactor) * WagePremium(skill, difficulty, coefficient);
    }
}
