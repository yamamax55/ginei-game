using UnityEngine;

namespace Ginei
{
    /// <summary>情報統合の調整係数（INTEL-FUSION）。複数の偵察ソースを突き合わせる際の感度。</summary>
    public readonly struct IntelFusionParams
    {
        /// <summary>ソース一致時に確信へ加算する最大ボーナス（0..1）。</summary>
        public readonly float corroborationWeight;
        /// <summary>ソース矛盾時に確信から差し引く最大ペナルティ（0..1）。</summary>
        public readonly float contradictionWeight;
        /// <summary>情報の陳腐化速度（大きいほど古い情報の価値が速く落ちる）。timeliness=1/(1+k*age)。</summary>
        public readonly float agingRate;
        /// <summary>欺瞞リスクの重み（敵欺瞞努力がこの割合まで確信を削る）。</summary>
        public readonly float deceptionWeight;

        public IntelFusionParams(float corroborationWeight, float contradictionWeight, float agingRate, float deceptionWeight)
        {
            this.corroborationWeight = Mathf.Clamp01(corroborationWeight);
            this.contradictionWeight = Mathf.Clamp01(contradictionWeight);
            this.agingRate = Mathf.Max(0f, agingRate);
            this.deceptionWeight = Mathf.Clamp01(deceptionWeight);
        }

        /// <summary>既定＝一致+0.3／矛盾-0.4／陳腐化0.1／欺瞞0.5。</summary>
        public static IntelFusionParams Default => new IntelFusionParams(0.3f, 0.4f, 0.1f, 0.5f);
    }

    /// <summary>
    /// 情報統合の純ロジック（INTEL-FUSION）。偵察・通信傍受・哨戒・捕虜尋問など複数の情報源を
    /// 突き合わせて敵の全体像を描く。独立した複数ソースが一致すれば単一ソースより確信が増し、
    /// 矛盾・欺瞞・陳腐化はその確信を削る。個別の情報源（ReconRules／SignalIntelligenceRules／
    /// FogOfWarRules）や偏り補正（AvailabilityBiasRules）とは別＝複数ソースの「統合」に特化。
    /// 盤面非依存の plain 引数・乱数なし・入力クランプ・実効値パターン（基準値非破壊）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class IntelFusionRules
    {
        /// <summary>
        /// 複数ソースを統合した確信度 0..1。各ソースの確信を「失敗確率の積」で合成（独立ソースほど精度↑）。
        /// 1 - Π(1 - c_i)。配列 null/空は 0。1ソースならその値そのもの。
        /// </summary>
        public static float FusedConfidence(float[] sourceConfidences, IntelFusionParams p)
        {
            if (sourceConfidences == null || sourceConfidences.Length == 0) return 0f;
            float missProduct = 1f;
            for (int i = 0; i < sourceConfidences.Length; i++)
            {
                float c = Mathf.Clamp01(sourceConfidences[i]);
                missProduct *= (1f - c);
            }
            return Mathf.Clamp01(1f - missProduct);
        }

        public static float FusedConfidence(float[] sourceConfidences)
            => FusedConfidence(sourceConfidences, IntelFusionParams.Default);

        /// <summary>
        /// 裏付けボーナス 0..corroborationWeight。複数ソース（2以上）が一致するほど確信が増す。
        /// ソース数が多いほど・一致度 sourceAgreement(0..1) が高いほど大きい。1ソース以下は 0。
        /// </summary>
        public static float CorroborationBonus(int sourceCount, float sourceAgreement, IntelFusionParams p)
        {
            if (sourceCount < 2) return 0f;
            float agree = Mathf.Clamp01(sourceAgreement);
            // 2ソースで 0.5、ソースが増えるほど 1 に漸近（(n-1)/n）。
            float countFactor = 1f - 1f / sourceCount;
            return p.corroborationWeight * countFactor * agree;
        }

        public static float CorroborationBonus(int sourceCount, float sourceAgreement)
            => CorroborationBonus(sourceCount, sourceAgreement, IntelFusionParams.Default);

        /// <summary>矛盾ペナルティ 0..contradictionWeight。矛盾度 sourceDisagreement(0..1) に比例して確信を下げる。</summary>
        public static float ContradictionPenalty(float sourceDisagreement, IntelFusionParams p)
        {
            return p.contradictionWeight * Mathf.Clamp01(sourceDisagreement);
        }

        public static float ContradictionPenalty(float sourceDisagreement)
            => ContradictionPenalty(sourceDisagreement, IntelFusionParams.Default);

        /// <summary>
        /// 欺瞞フィルタ＝統合確信から、敵の欺瞞努力 enemyDeceptionEffort(0..1) が混ぜるリスクを差し引く。
        /// fused*(1 - deceptionWeight*effort)。0..1。
        /// </summary>
        public static float DeceptionFilter(float fusedConfidence, float enemyDeceptionEffort, IntelFusionParams p)
        {
            float f = Mathf.Clamp01(fusedConfidence);
            float effort = Mathf.Clamp01(enemyDeceptionEffort);
            return Mathf.Clamp01(f * (1f - p.deceptionWeight * effort));
        }

        public static float DeceptionFilter(float fusedConfidence, float enemyDeceptionEffort)
            => DeceptionFilter(fusedConfidence, enemyDeceptionEffort, IntelFusionParams.Default);

        /// <summary>
        /// 鮮度係数 0..1＝古い情報ほど価値が下がる。時間減衰 1/(1+agingRate*age)。
        /// intelAge(時間)が 0 で 1、増えるほど 0 へ。負の age は 0 にクランプ。
        /// </summary>
        public static float TimelinessFactor(float intelAge, IntelFusionParams p)
        {
            float age = Mathf.Max(0f, intelAge);
            return Mathf.Clamp01(1f / (1f + p.agingRate * age));
        }

        public static float TimelinessFactor(float intelAge)
            => TimelinessFactor(intelAge, IntelFusionParams.Default);

        /// <summary>敵の全体像のうち判明している割合 0..1＝判明アスペクト数/総アスペクト数。総数0以下は0。</summary>
        public static float PictureCompleteness(int coveredAspects, int totalAspects)
        {
            if (totalAspects <= 0) return 0f;
            int covered = Mathf.Clamp(coveredAspects, 0, totalAspects);
            return (float)covered / totalAspects;
        }

        /// <summary>行動に移せる確度の情報 0..1＝統合確信×全体像の判明割合（どちらか欠けると行動に移せない）。</summary>
        public static float ActionableIntel(float fusedConfidence, float pictureCompleteness)
        {
            return Mathf.Clamp01(fusedConfidence) * Mathf.Clamp01(pictureCompleteness);
        }

        /// <summary>敵情報が信頼できるか＝統合確信が閾値以上なら true。</summary>
        public static bool IsPictureReliable(float fusedConfidence, float threshold)
        {
            return Mathf.Clamp01(fusedConfidence) >= Mathf.Clamp01(threshold);
        }
    }
}
