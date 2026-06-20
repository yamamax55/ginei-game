using UnityEngine;

namespace Ginei
{
    /// <summary>継承の質→次代の安定（HDRN-4 #1806）の調整係数。</summary>
    public readonly struct SuccessionQualityParams
    {
        /// <summary>後継の資質スコアが初期正統性に与える重み。</summary>
        public readonly float heirMeritWeight;
        /// <summary>退位の計画性（legacyGift）が初期正統性に与える重み。</summary>
        public readonly float legacyGiftWeight;
        /// <summary>宮廷合意が初期正統性に与える重み。</summary>
        public readonly float courtConsensusWeight;
        /// <summary>継承危機リスクが初期正統性を削る強さ。</summary>
        public readonly float crisisRiskPenalty;
        /// <summary>初期安定度ボーナスの最大幅（高品質継承のときに与えられる安定押し上げ幅）。</summary>
        public readonly float maxStabilityBonus;
        /// <summary>初期安定度ペナルティの最大幅（低品質継承のときに与えられる安定押し下げ幅）。</summary>
        public readonly float maxStabilityPenalty;
        /// <summary>「安定継承」と判断する正統性スコアの閾値（以上で安定・未満で動乱のリスク）。</summary>
        public readonly float stableThreshold;

        public SuccessionQualityParams(float heirMeritWeight, float legacyGiftWeight,
                                       float courtConsensusWeight, float crisisRiskPenalty,
                                       float maxStabilityBonus, float maxStabilityPenalty,
                                       float stableThreshold)
        {
            this.heirMeritWeight = Mathf.Clamp01(heirMeritWeight);
            this.legacyGiftWeight = Mathf.Clamp01(legacyGiftWeight);
            this.courtConsensusWeight = Mathf.Clamp01(courtConsensusWeight);
            this.crisisRiskPenalty = Mathf.Clamp01(crisisRiskPenalty);
            this.maxStabilityBonus = Mathf.Clamp01(maxStabilityBonus);
            this.maxStabilityPenalty = Mathf.Clamp01(maxStabilityPenalty);
            this.stableThreshold = Mathf.Clamp01(stableThreshold);
        }

        /// <summary>
        /// 既定＝後継資質重み0.4・退位の計画性重み0.3・宮廷合意重み0.3・危機ペナルティ0.5・
        /// 安定ボーナス上限0.2・安定ペナルティ上限0.3・安定閾値0.55。
        /// </summary>
        public static SuccessionQualityParams Default
            => new SuccessionQualityParams(0.4f, 0.3f, 0.3f, 0.5f, 0.2f, 0.3f, 0.55f);
    }

    /// <summary>
    /// 継承の質が次代の安定を決める純ロジック（HDRN-4 #1806）。後継の資質・退位の計画性・宮廷の合意・
    /// 継承危機リスクを合成し、新統治者が最初の治世をどれだけの正統性で始められるかを算出する。
    /// 高品質の継承は次代に正統性ボーナスと安定度の押し上げを贈り、低品質の継承は正統性不足と
    /// 安定ペナルティで次代を苦境に立たせる＝「準備された継承」か「ぶっつけ本番の継承」かで国の命運が変わる。
    /// 分担：<see cref="SuccessionRules"/>（後継候補の選定・組織継承の技術）とは別＝継承の質の評価と
    /// 次代への影響／<see cref="SuccessionWarRules"/>（継承戦争の勝敗）とは別＝戦争前の継承品質／
    /// <see cref="HeirDesignationRules"/>（立太子の意思決定）とは別＝継承が完了した後の品質評価。
    /// 盤面非依存のplain引数・乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SuccessionQualityRules
    {
        /// <summary>
        /// 継承品質スコア（0..1）＝後継資質・退位の計画性・宮廷合意の加重和から危機リスクを引いたもの。
        /// 3要素の品質が高くても継承危機リスクが大きければ削れる。
        /// </summary>
        /// <param name="heirMerit">後継の資質スコア（0..1）。</param>
        /// <param name="legacyGift">退位の計画性・遺産の贈り物（0..1、AbdicationRules.LegacyGiftから）。</param>
        /// <param name="courtConsensus">宮廷の合意度（0..1）。</param>
        /// <param name="crisisRisk">継承危機リスク（0..1、HeirDesignationRules.SuccessionCrisisRiskから）。</param>
        /// <param name="p">調整係数。</param>
        public static float SuccessionQuality(float heirMerit, float legacyGift,
                                              float courtConsensus, float crisisRisk,
                                              SuccessionQualityParams p)
        {
            float m = Mathf.Clamp01(heirMerit);
            float g = Mathf.Clamp01(legacyGift);
            float c = Mathf.Clamp01(courtConsensus);
            float r = Mathf.Clamp01(crisisRisk);
            float wsum = p.heirMeritWeight + p.legacyGiftWeight + p.courtConsensusWeight;
            float rawScore = wsum > 0f
                ? (m * p.heirMeritWeight + g * p.legacyGiftWeight + c * p.courtConsensusWeight) / wsum
                : 0f;
            return Mathf.Clamp01(rawScore - r * p.crisisRiskPenalty);
        }

        public static float SuccessionQuality(float heirMerit, float legacyGift,
                                              float courtConsensus, float crisisRisk)
            => SuccessionQuality(heirMerit, legacyGift, courtConsensus, crisisRisk,
                                 SuccessionQualityParams.Default);

        /// <summary>
        /// 次代の初期正統性（0..1）＝継承品質スコアをそのまま初期正統性として扱う。
        /// 先代の正統性（baseLegitimacy）を上限として、品質スコアで比例スケールする。
        /// 先代が高正統性で品質の高い継承なら満額・品質が低ければ先代の正統性を持ち越せない。
        /// </summary>
        public static float InitialLegitimacy(float baseLegitimacy, float successionQuality)
        {
            return Mathf.Clamp01(Mathf.Clamp01(baseLegitimacy) * Mathf.Clamp01(successionQuality));
        }

        /// <summary>
        /// 安定度への影響（-maxPenalty..+maxBonus）＝継承品質が閾値以上でボーナス・未満でペナルティ。
        /// 継承品質 q が stableThreshold 以上: +maxStabilityBonus × (q − threshold) / (1 − threshold)。
        /// 未満: −maxStabilityPenalty × (threshold − q) / threshold。
        /// </summary>
        public static float StabilityEffect(float successionQuality, SuccessionQualityParams p)
        {
            float q = Mathf.Clamp01(successionQuality);
            float th = Mathf.Clamp01(p.stableThreshold);
            if (q >= th)
            {
                float span = 1f - th;
                if (span <= 0f) return p.maxStabilityBonus;
                return Mathf.Clamp(p.maxStabilityBonus * (q - th) / span, 0f, p.maxStabilityBonus);
            }
            else
            {
                if (th <= 0f) return 0f;
                return Mathf.Clamp(-p.maxStabilityPenalty * (th - q) / th,
                                   -p.maxStabilityPenalty, 0f);
            }
        }

        public static float StabilityEffect(float successionQuality)
            => StabilityEffect(successionQuality, SuccessionQualityParams.Default);

        /// <summary>
        /// 安定継承か（bool）＝継承品質が閾値以上なら動乱のリスクがなく安定した権力移譲と判定する。
        /// </summary>
        public static bool IsStableSuccession(float successionQuality, SuccessionQualityParams p)
            => Mathf.Clamp01(successionQuality) >= p.stableThreshold;

        public static bool IsStableSuccession(float successionQuality)
            => IsStableSuccession(successionQuality, SuccessionQualityParams.Default);

        /// <summary>
        /// 継承の総合ボーナス係数（0..2）＝次代の実効能力に掛ける乗数。1.0が等倍。
        /// 継承品質が高いほど1を超えて次代が「のびのびと」統治を始められる（前任からの薫陶・遺産）。
        /// 継承品質が低いほど1を下回り、継承混乱の重荷が次代の実効統治力を削る。
        /// bonus = 1 + StabilityEffect / maxStabilityBonus × 0.2 （±20%の乗数）。
        /// </summary>
        public static float SuccessionBonusFactor(float successionQuality, SuccessionQualityParams p)
        {
            float effect = StabilityEffect(successionQuality, p);
            float maxEffect = Mathf.Max(p.maxStabilityBonus, p.maxStabilityPenalty);
            if (maxEffect <= 0f) return 1f;
            return Mathf.Clamp(1f + effect / maxEffect * 0.2f, 0.8f, 1.2f);
        }

        public static float SuccessionBonusFactor(float successionQuality)
            => SuccessionBonusFactor(successionQuality, SuccessionQualityParams.Default);
    }
}
