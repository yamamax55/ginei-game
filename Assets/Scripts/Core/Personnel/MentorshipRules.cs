using UnityEngine;

namespace Ginei
{
    /// <summary>師弟関係の調整係数。</summary>
    public readonly struct MentorshipParams
    {
        /// <summary>忍耐0の師でも残る教える力の下限割合（強い武人が良い師とは限らない）。</summary>
        public readonly float patienceFloor;
        /// <summary>伝授が弟子の経験獲得に上乗せする最大倍率分（quality=1・talent=1 のとき +この値）。</summary>
        public readonly float maxLearningBonus;
        /// <summary>才能0の弟子でも受け取れる伝授の下限割合。</summary>
        public readonly float talentFloor;
        /// <summary>師から学べる上限＝師の技量に掛かる比率（師を超えるには独り立ちが要る）。</summary>
        public readonly float ceilingRatio;
        /// <summary>独り立ちの目安年数（これ以上の長居は型に嵌まる）。</summary>
        public readonly float independenceYears;
        /// <summary>長居1年あたりの独創性の摩耗速度。</summary>
        public readonly float overstayErosionRate;
        /// <summary>独創性の摩耗の上限（0..1）。</summary>
        public readonly float maxOverstayPenalty;
        /// <summary>師の死の遺産が正負を分ける弟子の準備度の分水嶺（これ未満は崩れ・以上は飛躍）。</summary>
        public readonly float readinessPivot;
        /// <summary>師の死の遺産のスケール（絆1×準備度の偏差に掛ける）。</summary>
        public readonly float legacyScale;

        public MentorshipParams(float patienceFloor, float maxLearningBonus, float talentFloor,
                                float ceilingRatio, float independenceYears, float overstayErosionRate,
                                float maxOverstayPenalty, float readinessPivot, float legacyScale)
        {
            this.patienceFloor = Mathf.Clamp01(patienceFloor);
            this.maxLearningBonus = Mathf.Max(0f, maxLearningBonus);
            this.talentFloor = Mathf.Clamp01(talentFloor);
            this.ceilingRatio = Mathf.Clamp01(ceilingRatio);
            this.independenceYears = Mathf.Max(0f, independenceYears);
            this.overstayErosionRate = Mathf.Max(0f, overstayErosionRate);
            this.maxOverstayPenalty = Mathf.Clamp01(maxOverstayPenalty);
            this.readinessPivot = Mathf.Clamp01(readinessPivot);
            this.legacyScale = Mathf.Max(0f, legacyScale);
        }

        /// <summary>既定＝忍耐下限0.3・伝授最大+1.0・才能下限0.5・上限比0.9・独り立ち10年・摩耗0.05/年・摩耗上限0.5・分水嶺0.5・遺産スケール1。</summary>
        public static MentorshipParams Default =>
            new MentorshipParams(0.3f, 1f, 0.5f, 0.9f, 10f, 0.05f, 0.5f, 0.5f, 1f);
    }

    /// <summary>
    /// 師弟（メルカッツ型）の純ロジック。老練の師が後進の成長を加速し、師の死で独り立ちが試される。
    /// 「師は階段であり天井」＝伝授は弟子の経験獲得を速める（階段＝<see cref="LearningMultiplier"/> を
    /// <see cref="GrowthRules.GainExperience"/> の amount に掛ける想定）が、師の下で届く技量には
    /// 上限がある（天井＝<see cref="SkillCeiling"/>。超えるには独り立ちが要る）。
    /// 成長曲線そのもの（<see cref="GrowthRules"/>＝経験→実効ボーナス）・人材の出自と学閥
    /// （<see cref="CareerPipelineRules"/>＝制度のパイプライン）とは別系統で、ここは
    /// 「個対個の伝授」の係数のみを扱う。倍率は基準値に掛けて使う（実効値パターン・基準非破壊）。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MentorshipRules
    {
        /// <summary>
        /// 師の教える力（0..1）。技量（0..100）×忍耐の混合＝強い武人が良い師とは限らない。
        /// 忍耐0でも技量比×patienceFloor は残る（背中で教える）が、忍耐が揃って初めて満点になる。
        /// </summary>
        public static float TeachingQuality(float mentorSkill, float mentorPatience, MentorshipParams p)
        {
            float skill = Mathf.Clamp(mentorSkill, 0f, 100f) / 100f;
            float patience = Mathf.Clamp01(mentorPatience);
            return skill * Mathf.Lerp(p.patienceFloor, 1f, patience);
        }

        public static float TeachingQuality(float mentorSkill, float mentorPatience)
            => TeachingQuality(mentorSkill, mentorPatience, MentorshipParams.Default);

        /// <summary>
        /// 弟子の成長倍率（1..1+maxLearningBonus）。<see cref="GrowthRules.GainExperience"/> の
        /// 経験獲得量に掛ける想定。教えの質×弟子の才能（talentFloor を下限に受容）で決まり、
        /// 師がいなければ（quality=0）等倍＝従来動作。
        /// </summary>
        public static float LearningMultiplier(float quality, float apprenticeTalent, MentorshipParams p)
        {
            float q = Mathf.Clamp01(quality);
            float receptivity = Mathf.Lerp(p.talentFloor, 1f, Mathf.Clamp01(apprenticeTalent));
            return 1f + p.maxLearningBonus * q * receptivity;
        }

        public static float LearningMultiplier(float quality, float apprenticeTalent)
            => LearningMultiplier(quality, apprenticeTalent, MentorshipParams.Default);

        /// <summary>
        /// 師から学べる技量の上限（0..100）＝師の技量×ceilingRatio。師の型をなぞる限り師には届かない
        /// ＝師を超えるには独り立ちが要る（師は階段であり天井）。
        /// </summary>
        public static float SkillCeiling(float mentorSkill, MentorshipParams p)
        {
            return Mathf.Clamp(mentorSkill, 0f, 100f) * p.ceilingRatio;
        }

        public static float SkillCeiling(float mentorSkill)
            => SkillCeiling(mentorSkill, MentorshipParams.Default);

        /// <summary>
        /// 独り立ちの時期か。師の下で学べる天井（<see cref="SkillCeiling"/>）に達した＝もう学ぶものがない、
        /// または在籍が independenceYears に達した＝長居は型に嵌まる、のどちらかで真。
        /// </summary>
        public static bool IndependenceTest(float apprenticeSkill, float mentorSkill, float yearsUnderMentor, MentorshipParams p)
        {
            float skill = Mathf.Clamp(apprenticeSkill, 0f, 100f);
            if (skill >= SkillCeiling(mentorSkill, p)) return true;
            return Mathf.Max(0f, yearsUnderMentor) >= p.independenceYears;
        }

        public static bool IndependenceTest(float apprenticeSkill, float mentorSkill, float yearsUnderMentor)
            => IndependenceTest(apprenticeSkill, mentorSkill, yearsUnderMentor, MentorshipParams.Default);

        /// <summary>
        /// 師の下に居すぎたことによる独創性の摩耗（0..maxOverstayPenalty）。independenceYears までは0、
        /// 超過1年ごとに overstayErosionRate ずつ増える＝守破離の「守」に留まり続ける代償。
        /// 実効能力や成長係数から減じて使う（基準非破壊）。
        /// </summary>
        public static float OverstayPenalty(float yearsUnderMentor, MentorshipParams p)
        {
            float over = Mathf.Max(0f, yearsUnderMentor) - p.independenceYears;
            if (over <= 0f) return 0f;
            return Mathf.Min(p.maxOverstayPenalty, over * p.overstayErosionRate);
        }

        public static float OverstayPenalty(float yearsUnderMentor)
            => OverstayPenalty(yearsUnderMentor, MentorshipParams.Default);

        /// <summary>
        /// 師の死の遺産（−legacyScale×pivot..+legacyScale×(1−pivot)）。準備のできた弟子は師の死で
        /// 飛躍し（正＝遺志を継ぐ）、未熟な弟子は支えを失って崩れる（負）＝二面性。
        /// 絆（bond）が深いほど振れ幅は大きく、絆0なら何も残らない（喪失も飛躍もない）。
        /// 士気・成長係数等への加算修正子として使う（基準非破壊）。
        /// </summary>
        public static float LegacyOnMentorDeath(float bond, float apprenticeReadiness, MentorshipParams p)
        {
            float b = Mathf.Clamp01(bond);
            float readiness = Mathf.Clamp01(apprenticeReadiness);
            return b * (readiness - p.readinessPivot) * p.legacyScale;
        }

        public static float LegacyOnMentorDeath(float bond, float apprenticeReadiness)
            => LegacyOnMentorDeath(bond, apprenticeReadiness, MentorshipParams.Default);
    }
}
