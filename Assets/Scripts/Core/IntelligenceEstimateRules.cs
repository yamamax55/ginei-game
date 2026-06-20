using UnityEngine;

namespace Ginei
{
    /// <summary>彼我戦力評価（インテリジェンス・エスティメイト）の調整係数。マジックナンバー禁止＝ここに集約。全フィールド ctor で Clamp。</summary>
    public readonly struct IntelligenceEstimateParams
    {
        /// <summary>索敵カバーが低いほど観測戦力から実戦力を割り増す最大率（カバー0で observed/(1-coverScale) まで膨らむ）。</summary>
        public readonly float coverScale;
        /// <summary>不確実性の幅の最大率（カバー0かつ情報が完全に古いと推定の±maxBand倍まで広がる）。</summary>
        public readonly float maxBand;
        /// <summary>情報の古さが不確実性へ効く重み（0..1・残りは索敵カバー不足ぶん）。</summary>
        public readonly float ageWeight;
        /// <summary>過大評価バイアスの最大値（脅威認識×慎重さが満点で overBiasMax）。</summary>
        public readonly float overBiasMax;
        /// <summary>過小評価バイアスの最大値（慢心×敵秘匿が満点で underBiasMax）。</summary>
        public readonly float underBiasMax;
        /// <summary>意図推測における敵態勢の重み（0..1）。</summary>
        public readonly float postureWeight;
        /// <summary>意図推測における行動パターンの重み（0..1・残りは態勢ぶん。分析力は全体を底上げ）。</summary>
        public readonly float patternWeight;

        public IntelligenceEstimateParams(float coverScale, float maxBand, float ageWeight,
                                          float overBiasMax, float underBiasMax,
                                          float postureWeight, float patternWeight)
        {
            this.coverScale = Mathf.Clamp(coverScale, 0f, 0.95f);
            this.maxBand = Mathf.Max(0f, maxBand);
            this.ageWeight = Mathf.Clamp01(ageWeight);
            this.overBiasMax = Mathf.Clamp01(overBiasMax);
            this.underBiasMax = Mathf.Clamp01(underBiasMax);
            this.postureWeight = Mathf.Clamp01(postureWeight);
            this.patternWeight = Mathf.Clamp01(patternWeight);
        }

        /// <summary>既定＝カバー割増0.5/不確実性幅0.6/古さ重み0.5/過大上限0.4/過小上限0.4/態勢重み0.5/パターン重み0.5。</summary>
        public static IntelligenceEstimateParams Default =>
            new IntelligenceEstimateParams(0.5f, 0.6f, 0.5f, 0.4f, 0.4f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 彼我戦力評価の純ロジック。観測戦力と索敵カバーから敵の実戦力を推定し、その不確実性の幅（最良・最悪ケース）、
    /// 過大評価/過小評価のバイアス、敵の意図の推測、見積もりの正味バイアスを決定論的に算出する。
    /// 入力は全て clamp、符号付き出力は -1..1 に clamp、配列は null/空安全。実効値パターン（観測値は非破壊）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class IntelligenceEstimateRules
    {
        /// <summary>
        /// 敵の実戦力の点推定。観測戦力 observedForces を索敵カバー intelCoverage(0..1) で割り増す＝
        /// カバーが低いほど見落とした戦力ぶん大きく見積もる（観測は氷山の一角）。observed の合計が出所。
        /// </summary>
        public static float StrengthEstimate(float[] observedForces, float intelCoverage, IntelligenceEstimateParams p)
        {
            float observed = 0f;
            if (observedForces != null)
            {
                for (int i = 0; i < observedForces.Length; i++)
                    observed += Mathf.Max(0f, observedForces[i]);
            }
            float cover = Mathf.Clamp01(intelCoverage);
            // カバー不足ぶん（1-cover）を coverScale で割増率に写す。cover=1 で割増0、cover=0 で coverScale。
            float gapFactor = 1f - p.coverScale * (1f - cover);
            gapFactor = Mathf.Max(0.05f, gapFactor);
            return observed / gapFactor;
        }

        public static float StrengthEstimate(float[] observedForces, float intelCoverage)
            => StrengthEstimate(observedForces, intelCoverage, IntelligenceEstimateParams.Default);

        /// <summary>
        /// 不確実性の幅（推定戦力に対する割合 0..maxBand）。索敵カバー不足 (1-coverage) と情報の古さ intelAge(0..1) を
        /// ageWeight で混ぜる＝カバーが薄く情報が古いほど幅が広がる。
        /// </summary>
        public static float UncertaintyBand(float intelCoverage, float intelAge, IntelligenceEstimateParams p)
        {
            float gap = 1f - Mathf.Clamp01(intelCoverage);
            float age = Mathf.Clamp01(intelAge);
            float blend = p.ageWeight * age + (1f - p.ageWeight) * gap;
            return p.maxBand * Mathf.Clamp01(blend);
        }

        public static float UncertaintyBand(float intelCoverage, float intelAge)
            => UncertaintyBand(intelCoverage, intelAge, IntelligenceEstimateParams.Default);

        /// <summary>過大評価バイアス 0..overBiasMax＝脅威認識 threatPerception(0..1) ×慎重さ cautionLevel(0..1)（敵を恐れ慎重なほど膨らます）。</summary>
        public static float OverestimateBias(float threatPerception, float cautionLevel, IntelligenceEstimateParams p)
        {
            float t = Mathf.Clamp01(threatPerception);
            float c = Mathf.Clamp01(cautionLevel);
            return p.overBiasMax * t * c;
        }

        public static float OverestimateBias(float threatPerception, float cautionLevel)
            => OverestimateBias(threatPerception, cautionLevel, IntelligenceEstimateParams.Default);

        /// <summary>過小評価バイアス 0..underBiasMax＝慢心 overconfidence(0..1) ×敵の秘匿 enemyDeception(0..1)（侮り隠されるほど見くびる）。</summary>
        public static float UnderestimateBias(float overconfidence, float enemyDeception, IntelligenceEstimateParams p)
        {
            float o = Mathf.Clamp01(overconfidence);
            float d = Mathf.Clamp01(enemyDeception);
            return p.underBiasMax * o * d;
        }

        public static float UnderestimateBias(float overconfidence, float enemyDeception)
            => UnderestimateBias(overconfidence, enemyDeception, IntelligenceEstimateParams.Default);

        /// <summary>最悪ケース見積もり＝推定戦力 ×(1+不確実性幅)（敵がこれだけ居るかもしれない上端）。負にはならない。</summary>
        public static float WorstCaseEstimate(float strengthEstimate, float uncertaintyBand, IntelligenceEstimateParams p)
        {
            float s = Mathf.Max(0f, strengthEstimate);
            float band = Mathf.Clamp01(uncertaintyBand);
            return s * (1f + band);
        }

        public static float WorstCaseEstimate(float strengthEstimate, float uncertaintyBand)
            => WorstCaseEstimate(strengthEstimate, uncertaintyBand, IntelligenceEstimateParams.Default);

        /// <summary>最良ケース見積もり＝推定戦力 ×(1-不確実性幅)（敵が思ったより少ない下端）。0 未満にはならない。</summary>
        public static float BestCaseEstimate(float strengthEstimate, float uncertaintyBand, IntelligenceEstimateParams p)
        {
            float s = Mathf.Max(0f, strengthEstimate);
            float band = Mathf.Clamp01(uncertaintyBand);
            return s * (1f - band);
        }

        public static float BestCaseEstimate(float strengthEstimate, float uncertaintyBand)
            => BestCaseEstimate(strengthEstimate, uncertaintyBand, IntelligenceEstimateParams.Default);

        /// <summary>
        /// 敵の意図の推測 0..1（高いほど攻勢的な意図と読む）。敵の態勢 enemyPosture(0..1・前進的か) と
        /// 行動パターン historicalPattern(0..1・過去の攻勢傾向) を重みで混ぜ、分析力 analystSkill(0..1) で
        /// 中庸(0.5)から真値方向へ確信を強める（分析力0で 0.5＝判断保留、分析力1で生の混合値）。
        /// </summary>
        public static float IntentInference(float enemyPosture, float historicalPattern, float analystSkill, IntelligenceEstimateParams p)
        {
            float posture = Mathf.Clamp01(enemyPosture);
            float pattern = Mathf.Clamp01(historicalPattern);
            float skill = Mathf.Clamp01(analystSkill);
            float wp = p.postureWeight;
            float wq = p.patternWeight;
            float denom = wp + wq;
            float raw;
            if (denom <= 0f) raw = 0.5f;
            else raw = (wp * posture + wq * pattern) / denom;
            // 分析力が低いほど判断保留(0.5)へ引き戻す。
            return Mathf.Clamp01(Mathf.Lerp(0.5f, raw, skill));
        }

        public static float IntentInference(float enemyPosture, float historicalPattern, float analystSkill)
            => IntentInference(enemyPosture, historicalPattern, analystSkill, IntelligenceEstimateParams.Default);

        /// <summary>
        /// 見積もりの正味バイアス -1..1。過大評価 overestimateBias から過小評価 underestimateBias を引く＝
        /// 正で悲観(敵を過大視)・負で楽観(過小視)。両バイアスは 0..1 にクランプして差を取る。
        /// </summary>
        public static float EstimateBias(float overestimateBias, float underestimateBias, IntelligenceEstimateParams p)
        {
            float over = Mathf.Clamp01(overestimateBias);
            float under = Mathf.Clamp01(underestimateBias);
            return Mathf.Clamp(over - under, -1f, 1f);
        }

        public static float EstimateBias(float overestimateBias, float underestimateBias)
            => EstimateBias(overestimateBias, underestimateBias, IntelligenceEstimateParams.Default);
    }
}
