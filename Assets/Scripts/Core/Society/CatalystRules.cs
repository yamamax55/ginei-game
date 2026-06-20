using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 触媒の非対称性（CEND-3 #2361・static）。他者の変容を可能にする「仲介・触媒」に特化した勢力
    /// （フェザーン #160 類型）は、同盟勢力の発展（成長係数）を加速できる一方、その専門化＝高い触媒特化度
    /// （catalystRatio 0..1）ゆえに自身は超越（CEND-1）できなくなる、という構造的非対称を純ロジックで表す。
    /// 「仲介の最上位は変容しないことで成り立つ」＝触媒特化が <see cref="TranscendenceRules.IsEligible"/> を阻む。
    /// 数値判定（適格スコア・閾値）は <see cref="TranscendenceRules"/> へ委譲し再実装しない。null/Clamp 安全・test-first。
    /// catalystRatio の出所は配線側（FactionRelations/DiplomacyRules の非敵対勢力比率 近似）で、本クラスは純計算のみ。
    /// </summary>
    public static class CatalystRules
    {
        /// <summary>触媒ボーナスの既定強さ（<see cref="CatalystParams.Default"/> と同値・調整の入口）。</summary>
        public const float DefaultCatalystStrength = 0.3f;

        /// <summary>自己進展ペナルティの既定強さ（<see cref="CatalystParams.Default"/> と同値）。</summary>
        public const float DefaultPenaltyStrength = 0.4f;

        /// <summary>超越除外の既定閾値（<see cref="CatalystParams.Default"/> と同値）。</summary>
        public const float DefaultExclusionThreshold = 0.7f;

        /// <summary>
        /// 触媒ボーナス＝同盟勢力の成長係数（targetFactor）を catalystRatio に比例して上乗せした値。
        /// 倍率＝1 + catalystRatio×catalystStrength（catalystRatio 0..1 に Clamp）。
        /// targetFactor が負/NaN なら 0 として扱う。触媒特化が高いほど同盟の発展を加速する。
        /// </summary>
        public static float CatalystBonus(float catalystRatio, float targetFactor, CatalystParams p)
        {
            float ratio = SafeRatio(catalystRatio);
            float baseFactor = (float.IsNaN(targetFactor) || targetFactor < 0f) ? 0f : targetFactor;
            float multiplier = 1f + ratio * p.catalystStrength;
            return baseFactor * multiplier;
        }

        /// <summary>触媒ボーナス（既定パラメータ）。</summary>
        public static float CatalystBonus(float catalystRatio, float targetFactor)
            => CatalystBonus(catalystRatio, targetFactor, CatalystParams.Default);

        /// <summary>
        /// 自己進展ペナルティ係数 0..1＝触媒特化が自勢力の進展を削る倍率（1で無傷・小さいほど抑制）。
        /// ＝1 - catalystRatio×penaltyStrength（catalystRatio 0..1 に Clamp）。Clamp01 で下限0を保証。
        /// </summary>
        public static float SelfAdvancementPenalty(float catalystRatio, CatalystParams p)
        {
            float ratio = SafeRatio(catalystRatio);
            return Mathf.Clamp01(1f - ratio * p.penaltyStrength);
        }

        /// <summary>自己進展ペナルティ係数（既定パラメータ）。</summary>
        public static float SelfAdvancementPenalty(float catalystRatio)
            => SelfAdvancementPenalty(catalystRatio, CatalystParams.Default);

        /// <summary>
        /// 触媒特化度から自身が変容（超越）できるか＝catalystRatio が exclusionThreshold 未満なら true。
        /// 触媒に特化し切る（閾値以上）と、その専門化ゆえに自身は変容できない（フェザーン・ジレンマ）。
        /// </summary>
        public static bool CanTranscend(float catalystRatio, float exclusionThreshold)
        {
            return SafeRatio(catalystRatio) < exclusionThreshold;
        }

        /// <summary>触媒特化度から変容可否（既定閾値）。</summary>
        public static bool CanTranscend(float catalystRatio)
            => CanTranscend(catalystRatio, DefaultExclusionThreshold);

        /// <summary>触媒特化度から変容可否（<see cref="CatalystParams"/> の閾値）。</summary>
        public static bool CanTranscend(float catalystRatio, CatalystParams p)
            => CanTranscend(catalystRatio, p.exclusionThreshold);

        /// <summary>
        /// 触媒除外を加味した超越適格判定＝CEND-1 の <see cref="TranscendenceRules.IsEligible(float, TranscendenceParams)"/>
        /// に「触媒特化が閾値未満」のゲートを AND で重ねる。スコア判定は CEND-1 へ委譲（再実装しない）。
        /// 触媒に特化し切った勢力は、たとえ適格スコアを満たしても変容できない。
        /// </summary>
        public static bool IsEligibleWithCatalyst(float score, float catalystRatio, TranscendenceParams tp, CatalystParams cp)
        {
            return TranscendenceRules.IsEligible(score, tp) && CanTranscend(catalystRatio, cp);
        }

        /// <summary>触媒除外を加味した超越適格判定（既定パラメータ）。</summary>
        public static bool IsEligibleWithCatalyst(float score, float catalystRatio)
            => IsEligibleWithCatalyst(score, catalystRatio, TranscendenceParams.Default, CatalystParams.Default);

        /// <summary>
        /// 勢力状態から触媒除外を加味した超越適格判定＝CEND-1 の <see cref="TranscendenceRules.EligibilityScore(FactionState, TranscendenceParams)"/>
        /// でスコアを出し、触媒ゲートを重ねる。null 安全（s==null→false）。スコア計算は CEND-1 へ委譲。
        /// </summary>
        public static bool IsEligibleWithCatalyst(FactionState s, float catalystRatio, TranscendenceParams tp, CatalystParams cp)
        {
            if (s == null) return false;
            float score = TranscendenceRules.EligibilityScore(s, tp);
            return IsEligibleWithCatalyst(score, catalystRatio, tp, cp);
        }

        /// <summary>勢力状態から触媒除外を加味した超越適格判定（既定パラメータ）。</summary>
        public static bool IsEligibleWithCatalyst(FactionState s, float catalystRatio)
            => IsEligibleWithCatalyst(s, catalystRatio, TranscendenceParams.Default, CatalystParams.Default);

        /// <summary>catalystRatio を 0..1 へ正規化（NaN→0）。</summary>
        private static float SafeRatio(float catalystRatio)
        {
            if (float.IsNaN(catalystRatio)) return 0f;
            return Mathf.Clamp01(catalystRatio);
        }
    }
}
