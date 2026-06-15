using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// LegalGeneralityRules の調整値（マジックナンバー集約・基準非破壊）。既定は <see cref="Default"/>。
    /// 恣意性が合意を蝕む速さ・抵抗権の閾値・予測可能性ボーナスの強さ・特権の罰などを束ねる。
    /// </summary>
    public readonly struct LegalGeneralityParams
    {
        /// <summary>恣意性1あたり・単位時間あたりに被治者の合意を蝕む率（/時間）。</summary>
        public readonly float consentErosionRate;
        /// <summary>抵抗権が正当化される恣意性の既定しきい値（これを超えると不服従が正当化される）。</summary>
        public readonly float resistanceThreshold;
        /// <summary>法の支配指数→予測可能性ボーナスの最大係数（経済・統治効率の上振れ幅）。</summary>
        public readonly float predictabilityBonusMax;
        /// <summary>特権（法の前の不平等）1あたりに平等適用を損なう割合（0..1）。</summary>
        public readonly float privilegeWeight;
        /// <summary>恣意性に占める裁量の重み（残りが特定者を狙う個別命令の重み）。</summary>
        public readonly float discretionWeight;
        /// <summary>「法による支配(rule BY law)」に堕したと判定する恣意性の既定しきい値。</summary>
        public readonly float ruleByLawThreshold;

        public LegalGeneralityParams(
            float consentErosionRate, float resistanceThreshold, float predictabilityBonusMax,
            float privilegeWeight, float discretionWeight, float ruleByLawThreshold)
        {
            this.consentErosionRate = consentErosionRate;
            this.resistanceThreshold = resistanceThreshold;
            this.predictabilityBonusMax = predictabilityBonusMax;
            this.privilegeWeight = privilegeWeight;
            this.discretionWeight = discretionWeight;
            this.ruleByLawThreshold = ruleByLawThreshold;
        }

        /// <summary>
        /// 既定（合意侵食率0.2／抵抗権閾値0.6／予測可能性ボーナス上限0.4／特権重み0.5／
        /// 裁量重み0.4＝残り0.6が個別命令／rule BY law 閾値0.7）。
        /// 法は一般的・抽象的・事前のルールであるべきで、恣意的命令ほど合意を速く蝕む。
        /// </summary>
        public static LegalGeneralityParams Default => new LegalGeneralityParams(
            consentErosionRate: 0.2f, resistanceThreshold: 0.6f, predictabilityBonusMax: 0.4f,
            privilegeWeight: 0.5f, discretionWeight: 0.4f, ruleByLawThreshold: 0.7f);
    }

    /// <summary>
    /// 法の一般性と恣意的命令の純ロジック（HAYK-4 #1549・test-first）。ハイエク『隷属への道』の
    /// 「法の支配（rule OF law）＝法は一般的・抽象的・万人に等しく適用される事前のルールでなければならず、
    /// 特定の個人・集団を狙う恣意的な個別命令は法でなく権力の道具」をモデル化する唯一の窓口。
    /// 法の一般性指数（<see cref="RuleOfLawIndex"/>）が高いほど予測可能性と正統性が保たれ、
    /// 恣意的命令が増えると合意が時間で撤回され（<see cref="ConsentErosion"/>）抵抗権が発動する
    /// （<see cref="ResistanceRight"/>）。法でなく「法による支配(rule BY law)」への堕落も判定する。
    /// 乱数なし（決定論）・全入力クランプ・調整値は <see cref="LegalGeneralityParams"/> に集約。
    /// <para>
    /// 分担：<see cref="MagnaCartaRules"/> は王権の契約的制約（権力の量）、
    /// <see cref="ConstitutionRules"/> は憲法の範囲（権力の枠）を扱うのに対し、
    /// ここは「法の質＝一般性 vs 恣意的個別命令」を扱う。合意の撤回は <c>ConsentRules</c> への入力、
    /// 自生的秩序（同 EPIC HAYK の <c>SpontaneousOrderRules</c>）とも別系統。
    /// </para>
    /// </summary>
    public static class LegalGeneralityRules
    {
        /// <summary>
        /// 法の支配指数（rule OF law）＝一般性×予測可能性×平等適用（いずれも 0..1）。
        /// 三者の積＝「万人に等しい事前の一般ルール」のどれか一つでも欠ければ法の支配は痩せる。0..1。
        /// </summary>
        public static float RuleOfLawIndex(float generality, float predictability, float equalApplication)
        {
            float g = Mathf.Clamp01(generality);
            float p = Mathf.Clamp01(predictability);
            float e = Mathf.Clamp01(equalApplication);
            return g * p * e;
        }

        /// <summary>
        /// 恣意性の度合い＝特定者を狙う個別命令と裁量の多さの加重和（0..1）。
        /// 命令の重みは <c>1 - discretionWeight</c>、裁量の重みは <c>discretionWeight</c>。
        /// </summary>
        public static float ArbitrarinessLevel(float targetedDecrees, float discretion, LegalGeneralityParams prm)
        {
            float d = Mathf.Clamp01(targetedDecrees);
            float disc = Mathf.Clamp01(discretion);
            float w = Mathf.Clamp01(prm.discretionWeight);
            return Mathf.Clamp01((1f - w) * d + w * disc);
        }

        /// <summary>恣意性（既定パラメータ）。</summary>
        public static float ArbitrarinessLevel(float targetedDecrees, float discretion)
            => ArbitrarinessLevel(targetedDecrees, discretion, LegalGeneralityParams.Default);

        /// <summary>
        /// 法的安定性＝法の支配が高いほど予測可能で取引・投資が安心できる（0..1）。
        /// ここでは法の支配指数をそのまま法的安定性とみなす（一般ルールが先にあるから将来を読める）。
        /// </summary>
        public static float LegalCertainty(float ruleOfLawIndex) => Mathf.Clamp01(ruleOfLawIndex);

        /// <summary>
        /// 恣意的命令による合意の侵食量（時間追従）。恣意性が高いほど被治者の合意が速く撤回される
        /// ＝<c>ConsentRules</c> の協力/合意に引く入力。dt 比例・非負を返す（撤回量）。
        /// </summary>
        public static float ConsentErosion(float arbitrarinessLevel, float deltaTime, LegalGeneralityParams prm)
        {
            float a = Mathf.Clamp01(arbitrarinessLevel);
            if (deltaTime <= 0f) return 0f;
            return Mathf.Max(0f, a * prm.consentErosionRate * deltaTime);
        }

        /// <summary>合意の侵食（既定パラメータ）。</summary>
        public static float ConsentErosion(float arbitrarinessLevel, float deltaTime)
            => ConsentErosion(arbitrarinessLevel, deltaTime, LegalGeneralityParams.Default);

        /// <summary>
        /// 抵抗権が正当化されるか＝恣意性がしきい値を超えたとき true
        /// （法でない恣意的命令への不服従が正当化される）。
        /// </summary>
        public static bool ResistanceRight(float arbitrarinessLevel, float threshold)
            => Mathf.Clamp01(arbitrarinessLevel) > Mathf.Clamp01(threshold);

        /// <summary>抵抗権発動可否（既定しきい値）。</summary>
        public static bool ResistanceRight(float arbitrarinessLevel)
            => ResistanceRight(arbitrarinessLevel, LegalGeneralityParams.Default.resistanceThreshold);

        /// <summary>
        /// 予測可能性ボーナス＝法の予測可能性が経済・統治の効率を上げる係数（1..1+predictabilityBonusMax）。
        /// 法の支配が高いほど将来を読めて取引・投資・統治が円滑になる。
        /// </summary>
        public static float PredictabilityBonus(float ruleOfLawIndex, LegalGeneralityParams prm)
            => 1f + Mathf.Clamp01(ruleOfLawIndex) * prm.predictabilityBonusMax;

        /// <summary>予測可能性ボーナス（既定パラメータ）。</summary>
        public static float PredictabilityBonus(float ruleOfLawIndex)
            => PredictabilityBonus(ruleOfLawIndex, LegalGeneralityParams.Default);

        /// <summary>
        /// 法の前の平等＝平等適用から特権（法の前の不平等）ぶんを差し引いた実効平等（0..1）。
        /// 特権が大きいほど「万人に等しい」が崩れ法の支配を損なう。基準（equalApplication）は非破壊。
        /// </summary>
        public static float EqualityBeforeLaw(float equalApplication, float privilege, LegalGeneralityParams prm)
        {
            float e = Mathf.Clamp01(equalApplication);
            float pr = Mathf.Clamp01(privilege);
            return Mathf.Clamp01(e - pr * prm.privilegeWeight);
        }

        /// <summary>法の前の平等（既定パラメータ）。</summary>
        public static float EqualityBeforeLaw(float equalApplication, float privilege)
            => EqualityBeforeLaw(equalApplication, privilege, LegalGeneralityParams.Default);

        /// <summary>
        /// 「法による支配(rule BY law)」に堕したか＝法を権力の道具に使う状態の判定。
        /// 一般性が低く（事前の一般ルールでない）かつ恣意性がしきい値を超えたとき true
        /// ＝特定者を狙う個別命令が法の体裁をまとっただけ。
        /// </summary>
        public static bool IsRuleByLaw(float generality, float arbitrariness, float threshold)
        {
            float g = Mathf.Clamp01(generality);
            float a = Mathf.Clamp01(arbitrariness);
            float t = Mathf.Clamp01(threshold);
            return a > t && g < (1f - t);
        }

        /// <summary>rule BY law 判定（既定しきい値）。</summary>
        public static bool IsRuleByLaw(float generality, float arbitrariness)
            => IsRuleByLaw(generality, arbitrariness, LegalGeneralityParams.Default.ruleByLawThreshold);
    }
}
