using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍の栄誉credential（史実：旧日本陸軍）。なし／星＝陸軍大学校卒（天保銭組＝エリート参謀の徽章）／
    /// 恩賜の軍刀＝大学校卒の成績上位（恩賜の軍刀を下賜された首席グループ）。<b>「星あり・軍刀あり」がもっとも優遇</b>。
    /// </summary>
    public enum MilitaryHonor { なし, 星, 恩賜の軍刀 }

    /// <summary>
    /// 昇進ドクトリン（選択レバー）。学閥主義＝credential（恩賜組）が人事を支配（史実の旧軍）／
    /// 実力主義＝merit が支配（米軍との対比＝下位credentialの俊英が追い越せる）。
    /// </summary>
    public enum PromotionDoctrine { 学閥主義, 実力主義 }

    /// <summary>
    /// 恩賜の軍刀組の純ロジック（史実ベース・MILEDU/#155 の優遇システム・test-first・唯一の窓口）。
    /// <b>大学校卒（星）を優遇し、その成績上位 <see cref="SwordQuota"/> 名を恩賜の軍刀組として最優遇</b>する学閥credential を、
    /// <b>学閥主義↔実力主義（米軍対比）の選択</b>で効かせ方を切り替える。credential 軸＝本モジュール／席次↔実力の混合軸＝
    /// <see cref="SeniorityRules"/>（重複実装しない・doctrine は <see cref="CredentialWeight"/> で credential 軸の rigidity を与える）。
    /// 大学校卒の輩出は <see cref="MilitaryAcademyRules"/>（Pass大学校・<see cref="MilitaryAcademyRules.WarCollegeTierBonus"/>＝任官時の星の優遇）。
    /// 基準値非破壊（実効値パターン）・状態は変えない（read-only）。
    /// </summary>
    public static class MilitarySwordHonorRules
    {
        /// <summary>恩賜の軍刀組の定員＝大学校卒の成績上位この人数（史実の陸大恩賜≒6名に倣う・既定5）。</summary>
        public const int SwordQuota = 5;

        // credential スコア（恩賜＞星＞なし＝「星あり・軍刀あり」がもっとも優遇）。
        public const float SwordScore = 1.0f;   // 恩賜の軍刀
        public const float StarScore = 0.6f;    // 星（大学校卒）
        public const float LineScore = 0.3f;    // なし（隊付＝一般将校）

        // doctrine→credential 軸の重み（学閥主義は credential 支配・実力主義は merit 支配）。
        public const float CredentialWeight学閥 = 0.85f;
        public const float CredentialWeight実力 = 0.25f;

        /// <summary>その学歴が陸軍大学校卒（星＝エリート参謀の徽章）か。</summary>
        public static bool IsWarCollegeGraduate(MilitaryDegree degree) => degree == MilitaryDegree.大学校卒;

        /// <summary>
        /// 栄誉credential を判定する。大学校卒かつ大学校内の席次が上位 <see cref="SwordQuota"/>（warCollegeRank 1..5）＝恩賜の軍刀、
        /// 大学校卒だが上位外＝星、それ未満＝なし。warCollegeRank は大学校卒コホート内の席次（1=首席）。
        /// </summary>
        public static MilitaryHonor HonorOf(MilitaryDegree degree, int warCollegeRank)
        {
            if (!IsWarCollegeGraduate(degree)) return MilitaryHonor.なし;
            return (warCollegeRank >= 1 && warCollegeRank <= SwordQuota) ? MilitaryHonor.恩賜の軍刀 : MilitaryHonor.星;
        }

        /// <summary>恩賜の軍刀組か（大学校卒の成績上位 <see cref="SwordQuota"/>）。</summary>
        public static bool IsSwordGroup(MilitaryDegree degree, int warCollegeRank)
            => HonorOf(degree, warCollegeRank) == MilitaryHonor.恩賜の軍刀;

        /// <summary>credential スコア（恩賜＞星＞なし）。</summary>
        public static float CredentialScore(MilitaryHonor honor)
        {
            switch (honor)
            {
                case MilitaryHonor.恩賜の軍刀: return SwordScore;
                case MilitaryHonor.星:        return StarScore;
                default:                       return LineScore;
            }
        }

        /// <summary>doctrine→credential 軸の重み 0..1（学閥主義は高＝credential 支配／実力主義は低＝merit 支配）。</summary>
        public static float CredentialWeight(PromotionDoctrine doctrine)
            => doctrine == PromotionDoctrine.学閥主義 ? CredentialWeight学閥 : CredentialWeight実力;

        /// <summary>
        /// 昇進の優遇スコア 0..1＝credential（恩賜＞星＞なし）と実務 merit を doctrine の重みで混ぜる。
        /// <b>学閥主義</b>では低 merit の恩賜組が高 merit の隊付を上回る（史実：軍刀組の人事独占）。
        /// <b>実力主義</b>では merit が支配し、恩賜でなくとも俊英が追い越せる（米軍との対比）。大きいほど優遇。
        /// </summary>
        public static float PromotionFavor(MilitaryHonor honor, float merit, PromotionDoctrine doctrine)
        {
            float w = Mathf.Clamp01(CredentialWeight(doctrine));
            float m = Mathf.Clamp01(merit);
            return Mathf.Lerp(m, CredentialScore(honor), w); // w高→credential、w低→merit
        }

        /// <summary>学歴・席次・merit から直接 優遇スコアを出す一括版。</summary>
        public static float PromotionFavor(MilitaryDegree degree, int warCollegeRank, float merit, PromotionDoctrine doctrine)
            => PromotionFavor(HonorOf(degree, warCollegeRank), merit, doctrine);
    }
}
