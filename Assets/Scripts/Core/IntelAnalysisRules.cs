using UnityEngine;

namespace Ginei
{
    /// <summary>情報分析の調整係数（INTEL-ANALYSIS）。生情報から敵情判断を導く際の感度。</summary>
    public readonly struct IntelAnalysisParams
    {
        /// <summary>裏付けが信頼度に効く重み（0..1）。実績に裏付けを上乗せする割合。</summary>
        public readonly float corroborationWeight;
        /// <summary>断片統合の逓減カーブ指数（>0、小さいほど少数の断片で像が結ぶ＝逓減が急）。</summary>
        public readonly float synthesisExponent;
        /// <summary>確証バイアスの強さ（0..1）。先入観×(1-規律)にこの割合を掛ける。</summary>
        public readonly float biasWeight;
        /// <summary>確度に対する欺瞞看破の甘さの効き（0..1）。看破が甘いほど推定精度を削る重み。</summary>
        public readonly float deceptionPenaltyWeight;

        public IntelAnalysisParams(float corroborationWeight, float synthesisExponent, float biasWeight, float deceptionPenaltyWeight)
        {
            this.corroborationWeight = Mathf.Clamp01(corroborationWeight);
            this.synthesisExponent = Mathf.Max(0.01f, synthesisExponent);
            this.biasWeight = Mathf.Clamp01(biasWeight);
            this.deceptionPenaltyWeight = Mathf.Clamp01(deceptionPenaltyWeight);
        }

        /// <summary>既定＝裏付け0.5／統合指数0.5／バイアス0.5／欺瞞ペナルティ0.5。</summary>
        public static IntelAnalysisParams Default => new IntelAnalysisParams(0.5f, 0.5f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 情報分析の純ロジック（INTEL-ANALYSIS）。収集した生情報を分析して敵情判断を導く。
    /// 情報源の信頼度評価、断片の統合（全体像の構築）、矛盾報告の裁定、分析官の練度、
    /// 確証バイアス（都合の良い解釈）、分析の確度、欺瞞情報の見破りを扱う。
    /// 情報統合の IntelFusionRules（複数ソースの突き合わせ）とは別＝こちらは分析・評価の質。
    /// 盤面非依存の plain 引数・乱数なし・入力クランプ・実効値パターン（基準値非破壊）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class IntelAnalysisRules
    {
        /// <summary>
        /// 情報源の信頼度 0..1＝情報源の実績(0..1)を基礎に、裏付け(0..1)で底上げ。
        /// trackRecord*(1 - w + w*corroboration)。裏付けが満点なら実績そのもの、無ければ (1-w) 倍に縮む。
        /// </summary>
        public static float SourceReliability(float sourceTrackRecord, float corroboration, IntelAnalysisParams p)
        {
            float track = Mathf.Clamp01(sourceTrackRecord);
            float corr = Mathf.Clamp01(corroboration);
            float w = p.corroborationWeight;
            return Mathf.Clamp01(track * (1f - w + w * corr));
        }

        public static float SourceReliability(float sourceTrackRecord, float corroboration)
            => SourceReliability(sourceTrackRecord, corroboration, IntelAnalysisParams.Default);

        /// <summary>
        /// 断片統合 0..1＝断片の質(0..1)を、断片数で逓減的に増幅して全体像を構築。
        /// quality * (1 - (1/(1+count))^exponent)。断片0なら0、多いほど像が結ぶが逓減。
        /// </summary>
        public static float FragmentSynthesis(int fragmentCount, float fragmentQuality, IntelAnalysisParams p)
        {
            int count = Mathf.Max(0, fragmentCount);
            if (count == 0) return 0f;
            float quality = Mathf.Clamp01(fragmentQuality);
            float coverage = 1f - Mathf.Pow(1f / (1f + count), p.synthesisExponent);
            return Mathf.Clamp01(quality * coverage);
        }

        public static float FragmentSynthesis(int fragmentCount, float fragmentQuality)
            => FragmentSynthesis(fragmentCount, fragmentQuality, IntelAnalysisParams.Default);

        /// <summary>
        /// 矛盾解決 0..1＝矛盾報告を分析官の技量(0..1)で裁く。矛盾が無ければ1、多いほど解決は分析官技量頼み。
        /// skill + (1-skill)/(1+conflicts)。conflicts=0で1、増えるほど skill へ漸近。
        /// </summary>
        public static float ContradictionResolution(int conflictingReports, float analystSkill, IntelAnalysisParams p)
        {
            int conflicts = Mathf.Max(0, conflictingReports);
            float skill = Mathf.Clamp01(analystSkill);
            return Mathf.Clamp01(skill + (1f - skill) / (1f + conflicts));
        }

        public static float ContradictionResolution(int conflictingReports, float analystSkill)
            => ContradictionResolution(conflictingReports, analystSkill, IntelAnalysisParams.Default);

        /// <summary>
        /// 確証バイアス 0..1＝先入観(0..1)×(1-規律)に重みを掛ける（都合の良い解釈の強さ）。
        /// biasWeight * priorBelief * (1 - analystDiscipline)。規律が高いほどバイアスは抑えられる。
        /// </summary>
        public static float ConfirmationBias(float priorBelief, float analystDiscipline, IntelAnalysisParams p)
        {
            float prior = Mathf.Clamp01(priorBelief);
            float discipline = Mathf.Clamp01(analystDiscipline);
            return Mathf.Clamp01(p.biasWeight * prior * (1f - discipline));
        }

        public static float ConfirmationBias(float priorBelief, float analystDiscipline)
            => ConfirmationBias(priorBelief, analystDiscipline, IntelAnalysisParams.Default);

        /// <summary>
        /// 分析の確度 0..1＝情報源の信頼度×断片統合×矛盾解決の幾何平均（どれか欠けると確度が崩れる）。
        /// pow(reliability*synthesis*resolution, 1/3)。
        /// </summary>
        public static float AnalysisConfidence(float sourceReliability, float fragmentSynthesis, float contradictionResolution, IntelAnalysisParams p)
        {
            float r = Mathf.Clamp01(sourceReliability);
            float s = Mathf.Clamp01(fragmentSynthesis);
            float c = Mathf.Clamp01(contradictionResolution);
            return Mathf.Clamp01(Mathf.Pow(r * s * c, 1f / 3f));
        }

        public static float AnalysisConfidence(float sourceReliability, float fragmentSynthesis, float contradictionResolution)
            => AnalysisConfidence(sourceReliability, fragmentSynthesis, contradictionResolution, IntelAnalysisParams.Default);

        /// <summary>
        /// 欺瞞看破 0..1＝分析官技量(0..1)/(技量+欺瞞の巧妙さ)。技量と巧妙さの競り合い。
        /// 双方0なら看破不能の前提＝0。
        /// </summary>
        public static float DeceptionDetection(float analystSkill, float deceptionSophistication, IntelAnalysisParams p)
        {
            float skill = Mathf.Clamp01(analystSkill);
            float soph = Mathf.Clamp01(deceptionSophistication);
            float denom = skill + soph;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(skill / denom);
        }

        public static float DeceptionDetection(float analystSkill, float deceptionSophistication)
            => DeceptionDetection(analystSkill, deceptionSophistication, IntelAnalysisParams.Default);

        /// <summary>
        /// 推定精度 -1..1＝分析の確度から、確証バイアスと欺瞞看破の甘さ(1-detection)の害を差し引く。
        /// confidence - bias - deceptionPenaltyWeight*(1-detection)。負なら誤った敵情判断。
        /// </summary>
        public static float EstimateAccuracy(float analysisConfidence, float confirmationBias, float deceptionDetection, IntelAnalysisParams p)
        {
            float conf = Mathf.Clamp01(analysisConfidence);
            float bias = Mathf.Clamp01(confirmationBias);
            float detect = Mathf.Clamp01(deceptionDetection);
            float deceptionHarm = p.deceptionPenaltyWeight * (1f - detect);
            return Mathf.Clamp(conf - bias - deceptionHarm, -1f, 1f);
        }

        public static float EstimateAccuracy(float analysisConfidence, float confirmationBias, float deceptionDetection)
            => EstimateAccuracy(analysisConfidence, confirmationBias, deceptionDetection, IntelAnalysisParams.Default);

        /// <summary>敵情判断が信頼できるか＝推定精度が閾値以上なら true。閾値は -1..1。</summary>
        public static bool IsEstimateReliable(float estimateAccuracy, float threshold)
        {
            float acc = Mathf.Clamp(estimateAccuracy, -1f, 1f);
            return acc >= Mathf.Clamp(threshold, -1f, 1f);
        }

        /// <summary>既定閾値 0.4＝推定精度がこれ以上なら行動に移せる敵情判断とみなす。</summary>
        public const float DefaultReliabilityThreshold = 0.4f;

        public static bool IsEstimateReliable(float estimateAccuracy)
            => IsEstimateReliable(estimateAccuracy, DefaultReliabilityThreshold);
    }
}
