using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 超越（人類進化）の解決ロジック（CEND-1 #2355・static）。勢力が安定・正統性・成熟（希望）を
    /// 高め切ると「超越」へ進み、達成すると盤面から退場する＝従来の征服勝利の枠外に出る勝利条件。
    /// 適格スコアは <see cref="FactionState"/> の既存フィールドから合成（新しい state を生やさない）：
    ///   ・安定度 proxy ＝ <see cref="FactionStateRules.Stability"/>（正統性/合意/結束/希望の平均 0..1）
    ///   ・正統性 proxy ＝ regime.legitimacy（天命/正統性 0..1）
    ///   ・成熟/研究 proxy ＝ community.hope（共同体の希望＝集合的成熟度 0..1）
    /// CampaignRules（勝利判定）への配線は親 issue へ委譲＝本クラスは純ロジックのみ。null 安全。test-first。
    /// </summary>
    public static class TranscendenceRules
    {
        /// <summary>適格判定の既定閾値（<see cref="TranscendenceParams.Default"/> と同値・調整の入口）。</summary>
        public const float DefaultThreshold = 0.9f;

        /// <summary>
        /// 超越適格スコア 0..1＝安定・正統性・成熟（希望）の重み付き合成（重み和で正規化し Clamp01）。
        /// いずれかの proxy が高いほどスコアは上がる。null→0。
        /// </summary>
        public static float EligibilityScore(FactionState s, TranscendenceParams p)
        {
            if (s == null) return 0f;

            float stability = Mathf.Clamp01(FactionStateRules.Stability(s));
            float legitimacy = (s.regime != null) ? Mathf.Clamp01(s.regime.legitimacy) : 0f;
            float maturity = (s.community != null) ? Mathf.Clamp01(s.community.hope) : 0f;

            float wSum = p.stabilityWeight + p.legitimacyWeight + p.researchWeight;
            if (wSum <= 0f) return 0f;

            float blended = (stability * p.stabilityWeight
                           + legitimacy * p.legitimacyWeight
                           + maturity * p.researchWeight) / wSum;
            return Mathf.Clamp01(blended);
        }

        /// <summary>適格スコア（既定パラメータ）。</summary>
        public static float EligibilityScore(FactionState s) => EligibilityScore(s, TranscendenceParams.Default);

        /// <summary>スコアが閾値以上なら超越適格。</summary>
        public static bool IsEligible(float score, TranscendenceParams p) => score >= p.threshold;

        /// <summary>スコア（既定パラメータの閾値）で適格判定。</summary>
        public static bool IsEligible(float score) => IsEligible(score, TranscendenceParams.Default);

        /// <summary>超越を開始する＝<see cref="TranscendenceState.isTranscending"/> を true に（冪等・null 安全・達成済みは無視）。</summary>
        public static void BeginTranscendence(TranscendenceState st)
        {
            if (st == null || st.isTranscended) return;
            st.isTranscending = true;
        }

        /// <summary>超越を完了する＝達成済みにし進行中を解く（null 安全）。</summary>
        public static void CompleteTranscendence(TranscendenceState st)
        {
            if (st == null) return;
            st.isTranscended = true;
            st.isTranscending = false;
        }

        /// <summary>従来の征服勝利の対象外か＝超越達成済みなら true（盤面から退場した勢力）。null→false。</summary>
        public static bool IsExcludedFromVictory(TranscendenceState st)
        {
            return st != null && st.isTranscended;
        }
    }
}
