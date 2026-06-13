using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 職業訓練校の輩出ロジック（SKILL-4・#2034・純ロジック・唯一の窓口）。
    /// 各 institution が対応する JSOC小分類（<see cref="JsocMinorClassification"/>・現存＋本作固有）へ技能を供給する＝定員×質×修了率で輩出、
    /// 高難度スキル（SKILL-2）ほど同じ質でも到達熟練度が低い。既存の教育チェーンに倣う（集約・タイクン回避）。test-first。
    /// </summary>
    public static class VocationalTrainingRules
    {
        /// <summary>種類ごとの既定の供給先 JSOC小分類（網羅的整備＝宇宙設定固有を含む）。</summary>
        public static string DefaultTargetMinor(TrainingInstitutionType type)
        {
            switch (type)
            {
                case TrainingInstitutionType.公共職業訓練校:       return "531"; // 製品製造・加工
                case TrainingInstitutionType.職業能力開発校:       return "551"; // 機械整備・修理
                case TrainingInstitutionType.企業内訓練:           return "531"; // 製品製造・加工
                case TrainingInstitutionType.徒弟見習い:           return "541"; // 機械組立
                case TrainingInstitutionType.軍技能訓練:           return "431"; // 宇宙艦隊将兵
                case TrainingInstitutionType.航宙士養成所:         return "622"; // 宇宙船操縦士
                case TrainingInstitutionType.テラフォーミング訓練所: return "092"; // テラフォーミング技師
                default:                                          return "692"; // 軌道作業訓練＝小惑星・宇宙採掘員
            }
        }

        /// <summary>受入数＝min(定員, 候補プール)（候補が支えられる範囲）。</summary>
        public static float Intake(int capacity, float candidatePool)
            => Mathf.Min(Mathf.Max(0, capacity), Mathf.Max(0f, candidatePool));

        /// <summary>修了者数＝受入数×修了率。</summary>
        public static float TrainedSupply(float intake, float completionRate)
            => Mathf.Max(0f, intake) * Mathf.Clamp01(completionRate);

        /// <summary>到達熟練度＝質×(1−0.5×難易度)（高難度スキルは同じ質でも到達熟練度が低い＝狭き門）。</summary>
        public static float SkillYield(float quality, float difficulty)
            => Mathf.Clamp01(Mathf.Clamp01(quality) * (1f - 0.5f * Mathf.Clamp01(difficulty)));

        /// <summary>その訓練校が供給する技能の到達熟練度（供給先小分類の難易度で律速）。</summary>
        public static float SkillYieldFor(VocationalTrainingSchool school)
        {
            if (school == null) return 0f;
            float diff = SkillDifficultyRules.DifficultyOf(school.targetMinorCode);
            return SkillYield(school.quality, diff);
        }
    }
}
