using UnityEngine;

namespace Ginei
{
    /// <summary>必要な前提教育水準（SKILL-2・#2034・教育チェーン#155-157 に対応）。</summary>
    public enum EducationLevel { 初等, 中等, 高等, 専門高等 }

    /// <summary>
    /// スキル習得難易度＝希少性モデル（SKILL-2・#2034・純ロジック・lookup）。
    /// <b>貴重なスキルほど習得難易度が高い</b>：高度なスキルは前提教育・長期間を要し供給が細い→希少性→賃金#1969・生産性#93 のプレミアム。
    /// 難易度は大分類（<see cref="OccupationCategory"/>）の基準＋注目すべき小分類（<see cref="JsocMinorClassification"/>）の上書き。係数で背景的に（タイクン回避）。test-first。
    /// </summary>
    public static class SkillDifficultyRules
    {
        // 大分類別の基準難易度（0..1）。運搬清掃<生産/農林/建設<事務/販売/サービス<保安<管理<専門技術。
        public static float DifficultyOf(OccupationCategory c)
        {
            switch (c)
            {
                case OccupationCategory.運搬清掃包装: return 0.10f;
                case OccupationCategory.農林漁業:     return 0.30f;
                case OccupationCategory.生産工程:     return 0.30f;
                case OccupationCategory.建設採掘:     return 0.30f;
                case OccupationCategory.事務:         return 0.40f;
                case OccupationCategory.販売:         return 0.40f;
                case OccupationCategory.サービス:     return 0.40f;
                case OccupationCategory.輸送機械運転: return 0.45f;
                case OccupationCategory.保安:         return 0.50f;
                case OccupationCategory.管理:         return 0.70f;
                case OccupationCategory.専門技術:     return 0.80f;
                default:                              return 0.00f; // 無職
            }
        }

        /// <summary>小分類別の難易度＝大分類基準＋高度職の上書き（航宙士/ワープ航法/医師/研究者/テラフォ等は特に高い）。</summary>
        public static float DifficultyOf(string minorCode)
        {
            switch (minorCode)
            {
                case "623": return 0.95f; // ワープ航法士
                case "622": return 0.90f; // 宇宙船操縦士
                case "121": return 0.90f; // 医師
                case "051": return 0.85f; // 研究者
                case "092": return 0.85f; // テラフォーミング技師
                case "112": return 0.80f; // 艦艇設計技術者
                case "072": return 0.80f; // 宇宙航行システム開発技術者
                case "602": return 0.55f; // 宇宙列車運転士
                case "692": return 0.45f; // 小惑星・宇宙採掘員
            }
            return DifficultyOf(JsocMinorClassification.MajorOf(minorCode));
        }

        /// <summary>必要な前提教育水準（難易度が高いほど高度な教育が要る）。</summary>
        public static EducationLevel Prerequisite(float difficulty)
        {
            float d = Mathf.Clamp01(difficulty);
            if (d >= 0.8f) return EducationLevel.専門高等;
            if (d >= 0.55f) return EducationLevel.高等;
            if (d >= 0.3f) return EducationLevel.中等;
            return EducationLevel.初等;
        }

        /// <summary>習得期間（月）＝難易度に比例（最短1ヶ月〜最長 maxMonths）。貴重なスキルほど長い。</summary>
        public static float AcquisitionMonths(float difficulty, float maxMonths)
            => Mathf.Lerp(1f, Mathf.Max(1f, maxMonths), Mathf.Clamp01(difficulty));

        /// <summary>希少性プレミアム＝1+難易度×係数（賃金#1969・生産性#93 へ・高難度＝供給が細く高給）。</summary>
        public static float RarityPremium(float difficulty, float coefficient)
            => 1f + Mathf.Clamp01(difficulty) * Mathf.Max(0f, coefficient);
    }
}
