using UnityEngine;

namespace Ginei
{
    /// <summary>節用（倹約）ドクトリンの調整係数。</summary>
    public readonly struct FrugalityDoctrineParams
    {
        /// <summary>倹約×無駄削減が財政効率に返す最大向上（実効値はこのぶん 1.0 を超える）。</summary>
        public readonly float efficiencyScale;
        /// <summary>節約分の再投資が産出に返す最大倍率上乗せ。</summary>
        public readonly float outputScale;
        /// <summary>倹約が貴族合意を削る最大幅（誇示的浪費を奪われる不満）。</summary>
        public readonly float nobleConsentScale;
        /// <summary>倹約が民を益す最大幅（無駄を省いた分が民へ）。</summary>
        public readonly float commonerBenefitScale;
        /// <summary>過度な倹約の疲れの蓄積係数（厳しく長いほど窮屈）。</summary>
        public readonly float fatigueScale;
        /// <summary>過度な倹約（窮屈すぎ）の既定閾値。</summary>
        public readonly float austerityThreshold;

        public FrugalityDoctrineParams(float efficiencyScale, float outputScale, float nobleConsentScale, float commonerBenefitScale, float fatigueScale, float austerityThreshold)
        {
            this.efficiencyScale = Mathf.Max(0f, efficiencyScale);
            this.outputScale = Mathf.Max(0f, outputScale);
            this.nobleConsentScale = Mathf.Max(0f, nobleConsentScale);
            this.commonerBenefitScale = Mathf.Max(0f, commonerBenefitScale);
            this.fatigueScale = Mathf.Max(0f, fatigueScale);
            this.austerityThreshold = Mathf.Clamp01(austerityThreshold);
        }

        /// <summary>既定＝財政効率0.3・産出0.2・貴族合意0.4・民益0.3・疲れ0.5・過度閾値0.8。</summary>
        public static FrugalityDoctrineParams Default => new FrugalityDoctrineParams(0.3f, 0.2f, 0.4f, 0.3f, 0.5f, 0.8f);
    }

    /// <summary>
    /// 節用（せつよう）の財政効率の純ロジック（MOZI-5 #1567・墨子「節用・節葬」参考）。
    /// 倹約を旨とし無駄な浪費（豪奢な葬礼・音楽・贅沢）を省くドクトリンが財政効率を上げ産出を増やすが、
    /// 浪費で地位を示す貴族・特権層の合意を下げる＝倹約の財政効率と貴族不満のトレードオフ。
    /// 財政の実体は <see cref="FiscalRules"/>、欠乏下の配給は <see cref="RationingRules"/> が扱い、
    /// 誇示的浪費そのものの正統性効果は <see cref="OstentationRules"/>（本ルールはその対極）。
    /// ここは倹約ドクトリンの財政効率と貴族不満のトレードオフのみ。乱数なし・決定論。基準非破壊。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FrugalityDoctrineRules
    {
        /// <summary>
        /// 財政効率の向上倍率＝倹約度(0..1)×無駄削減(0..1)を効率スケールに通す（実効値≥1.0）。
        /// 浪費を省くほど国庫が富む＝同じ歳入でより多くを賄える。倹約も削減も無ければ満額1.0のまま。
        /// </summary>
        public static float FiscalEfficiency(float frugality, float wasteCut, FrugalityDoctrineParams p)
        {
            float f = Mathf.Clamp01(frugality);
            float w = Mathf.Clamp01(wasteCut);
            return 1f + f * w * p.efficiencyScale; // 浪費を省いた分だけ効率が伸びる
        }

        public static float FiscalEfficiency(float frugality, float wasteCut)
            => FiscalEfficiency(frugality, wasteCut, FrugalityDoctrineParams.Default);

        /// <summary>
        /// 産出の向上倍率＝財政効率(≥1.0)の余剰ぶん×再投資率(0..1)を産出スケールに通す（実効値≥1.0）。
        /// 節約で浮いた分を生産へ再投資するほど産出が増える。再投資0なら浮いても産出は伸びない＝1.0。
        /// </summary>
        public static float OutputGain(float fiscalEfficiency, float reinvestment, FrugalityDoctrineParams p)
        {
            float surplus = Mathf.Max(0f, fiscalEfficiency - 1f); // 効率の余剰（節約分）
            float r = Mathf.Clamp01(reinvestment);
            return 1f + surplus * r * p.outputScale;
        }

        public static float OutputGain(float fiscalEfficiency, float reinvestment)
            => OutputGain(fiscalEfficiency, reinvestment, FrugalityDoctrineParams.Default);

        /// <summary>
        /// 貴族合意の低下幅（0..nobleConsentScale）＝倹約(0..1)×貴族の力(0..1)。
        /// 倹約が強いほど、また誇示で地位を示す特権層が強いほど、浪費を奪われた貴族の合意が下がる。
        /// 貴族の力が0（特権層が居ない）なら奪うものが無く不満も0。
        /// </summary>
        public static float NobleConsent(float frugality, float aristocraticPower, FrugalityDoctrineParams p)
        {
            float f = Mathf.Clamp01(frugality);
            float a = Mathf.Clamp01(aristocraticPower);
            return f * a * p.nobleConsentScale; // 誇示的浪費を奪われた貴族の不満
        }

        public static float NobleConsent(float frugality, float aristocraticPower)
            => NobleConsent(frugality, aristocraticPower, FrugalityDoctrineParams.Default);

        /// <summary>
        /// 民の益（0..commonerBenefitScale）＝倹約(0..1)に比例。無駄を省いた分が民へ回る。
        /// </summary>
        public static float CommonerBenefit(float frugality, FrugalityDoctrineParams p)
        {
            return Mathf.Clamp01(frugality) * p.commonerBenefitScale;
        }

        public static float CommonerBenefit(float frugality)
            => CommonerBenefit(frugality, FrugalityDoctrineParams.Default);

        /// <summary>
        /// 倹約疲れ（0..fatigueScale）＝倹約(0..1)×継続期間(0..1)。
        /// 倹約が長く厳しいほど民も窮屈さに疲れる＝過度な節約のコスト。期間0なら疲れ0。
        /// </summary>
        public static float AusterityFatigue(float frugality, float duration, FrugalityDoctrineParams p)
        {
            float f = Mathf.Clamp01(frugality);
            float d = Mathf.Clamp01(duration);
            return f * d * p.fatigueScale;
        }

        public static float AusterityFatigue(float frugality, float duration)
            => AusterityFatigue(frugality, duration, FrugalityDoctrineParams.Default);

        /// <summary>
        /// 統治への総合効果＝民の益 − 貴族の不満 − 倹約疲れ（トレードオフが一式に出る）。
        /// 倹約は民を益すが、誇示的浪費を奪われた貴族の合意を下げ、長引けば民も疲れる。
        /// </summary>
        public static float NetGovernanceEffect(float commonerBenefit, float nobleConsent, float austerityFatigue)
        {
            return commonerBenefit - Mathf.Max(0f, nobleConsent) - Mathf.Max(0f, austerityFatigue);
        }

        /// <summary>
        /// 奢侈禁止令の貫徹度（0..1）＝倹約(0..1)を貴族の抵抗(0..1)が削る。
        /// 倹約の意志が強くても、特権層の抵抗が強いと禁止令は貫けない＝(1-抵抗)を掛ける。
        /// </summary>
        public static float SumptuaryEnforcement(float frugality, float eliteResistance)
        {
            float f = Mathf.Clamp01(frugality);
            float r = Mathf.Clamp01(eliteResistance);
            return f * (1f - r); // 抵抗が強いほど貫けない
        }

        /// <summary>
        /// 過度な倹約か＝倹約度が threshold を超える（窮屈すぎ＝民が疲れ反発する水準）。
        /// </summary>
        public static bool IsExcessiveAusterity(float frugality, float threshold)
        {
            return Mathf.Clamp01(frugality) > Mathf.Clamp01(threshold);
        }

        public static bool IsExcessiveAusterity(float frugality)
            => IsExcessiveAusterity(frugality, FrugalityDoctrineParams.Default.austerityThreshold);
    }
}
