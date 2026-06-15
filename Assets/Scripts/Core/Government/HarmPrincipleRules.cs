using UnityEngine;

namespace Ginei
{
    /// <summary>危害原理（ミル『自由論』）の調整係数。</summary>
    public readonly struct HarmPrincipleParams
    {
        /// <summary>他者への危害が規制の正当性へ寄与する強さ（危害＝規制を正当化する唯一の根拠）。</summary>
        public readonly float harmLegitimacyWeight;
        /// <summary>パターナリズム（本人のための干渉）が正当性を欠く強さ＝おせっかいのコスト。</summary>
        public readonly float paternalismPenaltyRate;
        /// <summary>道徳・善を理由にした規制が危害原理を超える行き過ぎ係数。</summary>
        public readonly float moralisticOverreachRate;
        /// <summary>正当性なき抑圧が招く反発の非線形度（過剰抑圧コストの指数）。</summary>
        public readonly float overSuppressionExponent;

        public HarmPrincipleParams(float harmLegitimacyWeight, float paternalismPenaltyRate, float moralisticOverreachRate, float overSuppressionExponent)
        {
            this.harmLegitimacyWeight = Mathf.Clamp01(harmLegitimacyWeight);
            this.paternalismPenaltyRate = Mathf.Clamp01(paternalismPenaltyRate);
            this.moralisticOverreachRate = Mathf.Clamp01(moralisticOverreachRate);
            this.overSuppressionExponent = Mathf.Max(1f, overSuppressionExponent);
        }

        /// <summary>既定＝危害寄与1.0・パターナリズム罰0.8・道徳的行き過ぎ0.9・過剰抑圧の非線形度2（二乗で加速）。</summary>
        public static HarmPrincipleParams Default => new HarmPrincipleParams(1f, 0.8f, 0.9f, 2f);
    }

    /// <summary>
    /// 危害原理（harm principle）の純ロジック（MILL-3 #1480・ミル『自由論』）。権力が個人の自由に干渉して正当化されうる
    /// 唯一の目的は他者への危害を防ぐことのみ＝本人自身のため（パターナリズム）や道徳・善のためという理由では
    /// 正当化されない。他者への危害だけが規制を正当化し、それを超えた過剰抑圧は正当性を失い加速度的にコストを生む
    /// （正当性なき抑圧は反発を非線形に招く）。<see cref="MartialLawRules"/>（戒厳令＝治安維持の時限措置）/
    /// <see cref="JusticeRules"/>（5つの正義観の是認）/`LegalGeneralityRules`（法の一般性）/`LibertyCultureRules`（自由の文化・同EPIC MILL）
    /// とは別＝「他者への危害のみ規制しうる」ミルの原理（過剰抑圧の正当性閾値）。すべて 0..1 の plain 引数で完結・
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class HarmPrincipleRules
    {
        /// <summary>
        /// 規制の正当性 0..1。他者への危害が大きいほど規制が正当化される（危害がなければ規制の正当性は低い）。
        /// 危害×寄与を規制の広さ（範囲）で締める＝広い規制ほど大きな危害でしか正当化されない。
        /// </summary>
        public static float RegulationLegitimacy(float harmToOthers, float regulationScope, HarmPrincipleParams p)
        {
            float harm = Mathf.Clamp01(harmToOthers);
            float scope = Mathf.Clamp01(regulationScope);
            // 危害が正当化する量に対し、規制の広さが要求を引き上げる（scope を超える正当性は出ない）
            float justified = harm * p.harmLegitimacyWeight;
            return Mathf.Clamp01(justified * (1f - scope) + justified * scope * harm);
        }

        public static float RegulationLegitimacy(float harmToOthers, float regulationScope)
            => RegulationLegitimacy(harmToOthers, regulationScope, HarmPrincipleParams.Default);

        /// <summary>
        /// パターナリズムのペナルティ 0..1。本人のための干渉（自己加害の規制）は危害原理上の正当性を欠く＝
        /// 自己関与的な規制が大きいほどコストが上がる（おせっかいのコスト）。
        /// </summary>
        public static float PaternalismPenalty(float selfRegardingRegulation, HarmPrincipleParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(selfRegardingRegulation) * p.paternalismPenaltyRate);
        }

        public static float PaternalismPenalty(float selfRegardingRegulation)
            => PaternalismPenalty(selfRegardingRegulation, HarmPrincipleParams.Default);

        /// <summary>
        /// 道徳的行き過ぎ 0..1。道徳・善を理由にした規制は他者への危害を伴わない限り危害原理を超える行き過ぎ
        /// （徳の強制は自由への不当な干渉）。
        /// </summary>
        public static float MoralisticOverreach(float moralityBasedRegulation, HarmPrincipleParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(moralityBasedRegulation) * p.moralisticOverreachRate);
        }

        public static float MoralisticOverreach(float moralityBasedRegulation)
            => MoralisticOverreach(moralityBasedRegulation, HarmPrincipleParams.Default);

        /// <summary>
        /// 過剰抑圧のコスト。危害が正当化する範囲を超えた抑圧（regulationScope − harmToOthers の正の超過分）が
        /// 加速度的にコストを生む＝正当性なき抑圧は反発を非線形（指数 overSuppressionExponent）に招く。
        /// 危害が範囲を上回る（規制が危害に届いていない）なら過剰でないのでコスト0。
        /// </summary>
        public static float OverSuppressionCost(float regulationScope, float harmToOthers, HarmPrincipleParams p)
        {
            float scope = Mathf.Clamp01(regulationScope);
            float harm = Mathf.Clamp01(harmToOthers);
            float excess = Mathf.Max(0f, scope - harm); // 危害を超えた抑圧ぶん
            return Mathf.Pow(excess, p.overSuppressionExponent);
        }

        public static float OverSuppressionCost(float regulationScope, float harmToOthers)
            => OverSuppressionCost(regulationScope, harmToOthers, HarmPrincipleParams.Default);

        /// <summary>
        /// 規制が正当化される危害の閾値判定＝他者への危害が threshold（既定0.3）以上のときのみ規制は正当。
        /// これ未満の危害しかない規制は不当（false）。
        /// </summary>
        public static bool LegitimacyThreshold(float harmToOthers, float threshold = 0.3f)
        {
            return Mathf.Clamp01(harmToOthers) >= Mathf.Clamp01(threshold);
        }

        /// <summary>
        /// 自由の領域 0..1。本人にのみ関わる（自己関与的な）領域は自由が守られるべき＝自己決定の聖域。
        /// 自己関与度が高いほど干渉から守られるべき自由が大きい（selfRegarding と同値の聖域指標）。
        /// </summary>
        public static float LibertyZone(float selfRegarding)
        {
            return Mathf.Clamp01(selfRegarding);
        }

        /// <summary>
        /// 危害の勾配 0..1。直接的危害と間接的危害を重み付けて合成＝直接危害ほど規制が正当化される
        /// （間接危害 directWeight 既定0.4 で割り引く）。
        /// </summary>
        public static float HarmGradient(float directHarm, float indirectHarm, float indirectWeight = 0.4f)
        {
            float d = Mathf.Clamp01(directHarm);
            float ind = Mathf.Clamp01(indirectHarm);
            float w = Mathf.Clamp01(indirectWeight);
            return Mathf.Clamp01(d + ind * w);
        }

        /// <summary>
        /// 危害原理を超えて過剰に介入する国家か＝道徳的行き過ぎとパターナリズムのペナルティの和が
        /// threshold（既定0.6）を超える。徳と本人のためを口実に自由を侵す国家の判定。
        /// </summary>
        public static bool IsOverreachingState(float moralisticOverreach, float paternalismPenalty, float threshold = 0.6f)
        {
            float over = Mathf.Clamp01(moralisticOverreach) + Mathf.Clamp01(paternalismPenalty);
            return over > Mathf.Max(0f, threshold);
        }
    }
}
