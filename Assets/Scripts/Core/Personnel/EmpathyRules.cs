using UnityEngine;

namespace Ginei
{
    /// <summary>共感評判の調整係数（道徳感情論 TMS-1 #1578）。</summary>
    public readonly struct EmpathyParams
    {
        /// <summary>観察者の近さ→共感の強さの重み（近いほど他者の感情を強く感じる）。</summary>
        public readonly float proximityWeight;
        /// <summary>共感反応→是認/否認の感度（中立0.5からの振れ幅）。</summary>
        public readonly float approvalSensitivity;
        /// <summary>是認→支持修正子の振れ幅（是認0.5を中立として ±supportSwing）。</summary>
        public readonly float supportSwing;
        /// <summary>是認→忠誠修正子の振れ幅（個人的紐帯で増幅）。</summary>
        public readonly float loyaltySwing;
        /// <summary>是認→opinion 修正子の振れ幅（伝播で増幅）。</summary>
        public readonly float opinionSwing;
        /// <summary>不正・危害が呼ぶ憤りの強さ（resentment スケール）。</summary>
        public readonly float resentmentScale;

        public EmpathyParams(float proximityWeight, float approvalSensitivity, float supportSwing,
                             float loyaltySwing, float opinionSwing, float resentmentScale)
        {
            this.proximityWeight = Mathf.Clamp01(proximityWeight);
            this.approvalSensitivity = Mathf.Clamp01(approvalSensitivity);
            this.supportSwing = Mathf.Max(0f, supportSwing);
            this.loyaltySwing = Mathf.Max(0f, loyaltySwing);
            this.opinionSwing = Mathf.Max(0f, opinionSwing);
            this.resentmentScale = Mathf.Max(0f, resentmentScale);
        }

        /// <summary>既定＝近さ重み0.5/是認感度1.0/支持±0.3/忠誠±0.4/opinion±0.5/憤り1.0。</summary>
        public static EmpathyParams Default => new EmpathyParams(0.5f, 1f, 0.3f, 0.4f, 0.5f, 1f);
    }

    /// <summary>
    /// 共感評判の純ロジック（アダム・スミス『道徳感情論』TMS-1 #1578）。人は他者の境遇に身を置いて行動を
    /// 是認/否認し（共感 sympathy）、その道徳感情が為政者・人物の支持・忠誠・opinion を動かす＝残虐は否認を、
    /// 慈悲は是認を呼ぶ。観察者は近いほど他者の感情を強く共有し、不正・危害には憤り（resentment）で応える。
    /// 行動の適宜性（propriety＝状況の中庸への一致）も評価し、意図と結果のずれは道徳的運として評価を割り引く/増す。
    /// <see cref="ReputationRules"/>（勝敗による武名の増減）/<see cref="JusticeRules"/>（5つの正義観による政策の是認）
    /// /ImpartialObserverRules（同 EPIC TMS・公平な観察者の視点での裁定）とは別＝共感（他者の感情への同調）に
    /// 基づく道徳評価エンジン。全入力クランプ・乱数なし決定論・基準値非破壊（修正子を返す）。test-first。
    /// </summary>
    public static class EmpathyRules
    {
        /// <summary>
        /// 共感反応 -1..1。行動の善悪（actionValence＝正＝慈悲/負＝残虐）に、観察者の近さで増した感受性を掛ける。
        /// 近い観察者ほど他者の境遇を強く感じ、共感反応の絶対値が大きくなる（proximityWeight で基礎感受性と按分）。
        /// </summary>
        public static float SympatheticResponse(float actionValence, float observerProximity, EmpathyParams p)
        {
            float v = Mathf.Clamp(actionValence, -1f, 1f);
            float prox = Mathf.Clamp01(observerProximity);
            // 近さで増す感受性＝基礎(1-w) + 近さ×w（遠くても0にはならない）
            float sensitivity = (1f - p.proximityWeight) + p.proximityWeight * prox;
            return Mathf.Clamp(v * sensitivity, -1f, 1f);
        }

        public static float SympatheticResponse(float actionValence, float observerProximity)
            => SympatheticResponse(actionValence, observerProximity, EmpathyParams.Default);

        /// <summary>
        /// 道徳的是認 0..1（0.5中立）。共感反応（-1..1）を中立0.5を境に approvalSensitivity で写す＝
        /// 慈悲（正の共感）は是認（&gt;0.5）、残虐（負の共感）は否認（&lt;0.5）を呼ぶ。
        /// </summary>
        public static float MoralApproval(float sympatheticResponse, EmpathyParams p)
        {
            float s = Mathf.Clamp(sympatheticResponse, -1f, 1f);
            return Mathf.Clamp01(0.5f + 0.5f * s * p.approvalSensitivity);
        }

        public static float MoralApproval(float sympatheticResponse)
            => MoralApproval(sympatheticResponse, EmpathyParams.Default);

        /// <summary>
        /// 適宜性 propriety 0..1（スミス）。行動の度合い（action）が状況の適切さ（situationalNorm＝中庸）に
        /// どれだけ合うか＝隔たりが小さいほど高い。過剰も過小も propriety を下げる（中庸が最も是認される）。
        /// </summary>
        public static float Propriety(float action, float situationalNorm)
        {
            float a = Mathf.Clamp01(action);
            float n = Mathf.Clamp01(situationalNorm);
            return Mathf.Clamp01(1f - Mathf.Abs(a - n));
        }

        /// <summary>
        /// 是認→支持修正子（実効値・基準非破壊）。是認0.5を中立として ±supportSwing へ写す＝
        /// 是認1.0で +supportSwing、否認0.0で −supportSwing。道徳感情が支持を上下させる。
        /// </summary>
        public static float SupportModifier(float moralApproval, EmpathyParams p)
        {
            float m = Mathf.Clamp01(moralApproval);
            return (m - 0.5f) * 2f * p.supportSwing;
        }

        public static float SupportModifier(float moralApproval)
            => SupportModifier(moralApproval, EmpathyParams.Default);

        /// <summary>
        /// 是認→忠誠修正子（実効値・基準非破壊）。是認（中立0.5基準）に個人的紐帯（personalBond 0..1）を
        /// 掛けて ±loyaltySwing へ写す＝紐帯が深い相手の善行ほど忠誠を強く動かす。
        /// </summary>
        public static float LoyaltyModifier(float moralApproval, float personalBond, EmpathyParams p)
        {
            float m = Mathf.Clamp01(moralApproval);
            float bond = Mathf.Clamp01(personalBond);
            return (m - 0.5f) * 2f * p.loyaltySwing * bond;
        }

        public static float LoyaltyModifier(float moralApproval, float personalBond)
            => LoyaltyModifier(moralApproval, personalBond, EmpathyParams.Default);

        /// <summary>
        /// 是認→opinion 修正子（<see cref="DiplomacyRules"/> 等への入力）。是認（中立0.5基準）に伝播範囲
        /// （mediaReach 0..1）を掛けて ±opinionSwing へ写す＝道徳評価が広く伝わるほど opinion を動かす。
        /// </summary>
        public static float OpinionShift(float moralApproval, float mediaReach, EmpathyParams p)
        {
            float m = Mathf.Clamp01(moralApproval);
            float reach = Mathf.Clamp01(mediaReach);
            return (m - 0.5f) * 2f * p.opinionSwing * reach;
        }

        public static float OpinionShift(float moralApproval, float mediaReach)
            => OpinionShift(moralApproval, mediaReach, EmpathyParams.Default);

        /// <summary>
        /// 不正・危害が呼ぶ憤り 0..resentmentScale（スミスの resentment）。危害の深刻さ（harmSeverity 0..1）に
        /// 観察者の近さを掛ける＝身近な不正ほど強い憤りを生む（被害者に共感して加害者を否認する）。
        /// </summary>
        public static float ResentmentFromInjustice(float harmSeverity, float observerProximity, EmpathyParams p)
        {
            float harm = Mathf.Clamp01(harmSeverity);
            float prox = Mathf.Clamp01(observerProximity);
            // 近さで増す（基礎(1-w) + 近さ×w）＝遠くても完全には消えない
            float reach = (1f - p.proximityWeight) + p.proximityWeight * prox;
            return Mathf.Max(0f, harm * reach * p.resentmentScale);
        }

        public static float ResentmentFromInjustice(float harmSeverity, float observerProximity)
            => ResentmentFromInjustice(harmSeverity, observerProximity, EmpathyParams.Default);

        /// <summary>
        /// 道徳的運 -1..1（結果の道徳的運）。意図（intent -1..1）と結果（outcome -1..1）のずれが評価を割り引く/増す。
        /// 善意でも悪い結果なら評価は割引かれ（&lt;intent）、悪意でも良い結果なら割増される＝人は結果でも裁く。
        /// 意図と結果の平均＝意図を結果の方向へ寄せた評価（intent と outcome の中庸）。
        /// </summary>
        public static float MoralLuck(float intent, float outcome)
        {
            float i = Mathf.Clamp(intent, -1f, 1f);
            float o = Mathf.Clamp(outcome, -1f, 1f);
            return Mathf.Clamp((i + o) * 0.5f, -1f, 1f);
        }
    }
}
