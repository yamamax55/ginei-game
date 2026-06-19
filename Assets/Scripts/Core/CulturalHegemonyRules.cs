using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 文化的覇権パラメータ（グラムシ的ヘゲモニー）。readonly struct・トップレベル。
    /// ctor で全値をクランプ。Default で標準値。
    /// </summary>
    public readonly struct CulturalHegemonyParams
    {
        /// <summary>価値観の浸透における文化制度の重み。</summary>
        public readonly float institutionWeight;
        /// <summary>価値観の浸透におけるメディア支配の重み。</summary>
        public readonly float mediaWeight;
        /// <summary>常識化の最大上乗せ（反復で前提化が進む伸びしろ）。</summary>
        public readonly float commonSenseGain;
        /// <summary>同意調達の最大上乗せ（自然さで納得が深まる伸びしろ）。</summary>
        public readonly float consentGain;
        /// <summary>対抗文化の感度（対抗叙事×異論が広がる係数）。</summary>
        public readonly float counterSensitivity;
        /// <summary>覇権安定における対抗文化の差し引きの効き。</summary>
        public readonly float counterWeight;
        /// <summary>文化的正統性における有機的知識人の効き。</summary>
        public readonly float intellectualWeight;
        /// <summary>ソフトパワーの基準係数（正統性×魅力の伸び）。</summary>
        public readonly float softPowerScale;

        public CulturalHegemonyParams(
            float institutionWeight,
            float mediaWeight,
            float commonSenseGain,
            float consentGain,
            float counterSensitivity,
            float counterWeight,
            float intellectualWeight,
            float softPowerScale)
        {
            this.institutionWeight = Mathf.Clamp01(institutionWeight);
            this.mediaWeight = Mathf.Clamp01(mediaWeight);
            this.commonSenseGain = Mathf.Clamp(commonSenseGain, 0f, 2f);
            this.consentGain = Mathf.Clamp(consentGain, 0f, 2f);
            this.counterSensitivity = Mathf.Clamp(counterSensitivity, 0f, 4f);
            this.counterWeight = Mathf.Clamp01(counterWeight);
            this.intellectualWeight = Mathf.Clamp01(intellectualWeight);
            this.softPowerScale = Mathf.Clamp(softPowerScale, 0f, 2f);
        }

        public static CulturalHegemonyParams Default => new CulturalHegemonyParams(
            institutionWeight: 0.5f,
            mediaWeight: 0.5f,
            commonSenseGain: 0.5f,
            consentGain: 0.5f,
            counterSensitivity: 1f,
            counterWeight: 1f,
            intellectualWeight: 1f,
            softPowerScale: 1f);
    }

    /// <summary>
    /// 文化的覇権ロジック＝グラムシ的ヘゲモニーの純数値モデル。
    /// 支配階級は文化制度（学校・宗教・メディア）を通じて自らの価値観を社会へ浸透させ、反復によって
    /// それを「疑われない常識（コモンセンス）」へと固める。常識が自然なものと感じられるほど、支配は
    /// 強制でなく被支配者自身の同意（manufactured consent＝同意の調達）として内面化される。
    /// だが対抗叙事と知識人の異論は対抗文化（カウンターヘゲモニー）を生み、覇権の安定を脅かす。
    /// 浸透と有機的知識人は文化的正統性を、正統性と国際的魅力はソフトパワーを生む。
    ///
    /// 盤面非依存・plain 引数・決定論・実効値パターン。社会/政治の他ルール（報道統制・世論・正統性）とは
    /// 別系統＝本ルールは「価値観の支配と同意の調達」の係数モデルに特化する。
    /// </summary>
    public static class CulturalHegemonyRules
    {
        /// <summary>
        /// 価値観の浸透 0..1＝文化制度とメディア支配の重み付き合成。
        /// 両方高いほど 1 へ。重み和で正規化（どちらか片方では頭打ち）。
        /// </summary>
        public static float ValuePenetration(float culturalInstitutions, float mediaControl, CulturalHegemonyParams p)
        {
            float inst = Mathf.Clamp01(culturalInstitutions);
            float media = Mathf.Clamp01(mediaControl);
            float wsum = p.institutionWeight + p.mediaWeight;
            if (wsum <= 0f) return 0f;
            return Mathf.Clamp01((inst * p.institutionWeight + media * p.mediaWeight) / wsum);
        }

        public static float ValuePenetration(float culturalInstitutions, float mediaControl)
            => ValuePenetration(culturalInstitutions, mediaControl, CulturalHegemonyParams.Default);

        /// <summary>
        /// 常識の形成 0..1＝浸透×反復で「疑われない前提」へ。
        /// 反復が高いほど浸透が常識へ固まる。commonSenseGain で伸びしろを与え上限 1。
        /// </summary>
        public static float CommonSenseFormation(float valuePenetration, float repetition, CulturalHegemonyParams p)
        {
            float pen = Mathf.Clamp01(valuePenetration);
            float rep = Mathf.Clamp01(repetition);
            return Mathf.Clamp01(pen * (1f + rep * p.commonSenseGain));
        }

        public static float CommonSenseFormation(float valuePenetration, float repetition)
            => CommonSenseFormation(valuePenetration, repetition, CulturalHegemonyParams.Default);

        /// <summary>
        /// 同意の調達 0..1＝常識×自然さで強制でなく納得させる。
        /// 支配が自然なものと感じられるほど内面化された同意となる。consentGain で伸びしろ、上限 1。
        /// </summary>
        public static float ManufacturedConsent(float commonSenseFormation, float perceivedNaturalness, CulturalHegemonyParams p)
        {
            float cs = Mathf.Clamp01(commonSenseFormation);
            float nat = Mathf.Clamp01(perceivedNaturalness);
            return Mathf.Clamp01(cs * (1f + nat * p.consentGain));
        }

        public static float ManufacturedConsent(float commonSenseFormation, float perceivedNaturalness)
            => ManufacturedConsent(commonSenseFormation, perceivedNaturalness, CulturalHegemonyParams.Default);

        /// <summary>
        /// 対抗文化 0..1＝対抗叙事×知識人の異論。代替の物語と異を唱える知識人がそろうほど対抗ヘゲモニーが育つ。
        /// counterSensitivity で感度（積で効く＝どちらか欠ければ崩れる）。
        /// </summary>
        public static float CounterHegemony(float alternativeNarratives, float intellectualDissent, CulturalHegemonyParams p)
        {
            float alt = Mathf.Clamp01(alternativeNarratives);
            float dis = Mathf.Clamp01(intellectualDissent);
            return Mathf.Clamp01(alt * dis * p.counterSensitivity);
        }

        public static float CounterHegemony(float alternativeNarratives, float intellectualDissent)
            => CounterHegemony(alternativeNarratives, intellectualDissent, CulturalHegemonyParams.Default);

        /// <summary>
        /// 覇権の安定 -1..1＝同意の調達から対抗文化を差し引いた符号付き優劣。
        /// 正＝覇権優位（同意が対抗を上回る）／負＝覇権が揺らぐ。counterWeight で対抗の効きを調整。
        /// </summary>
        public static float HegemonicStability(float manufacturedConsent, float counterHegemony, CulturalHegemonyParams p)
        {
            float consent = Mathf.Clamp01(manufacturedConsent);
            float counter = Mathf.Clamp01(counterHegemony);
            return Mathf.Clamp(consent - counter * p.counterWeight, -1f, 1f);
        }

        public static float HegemonicStability(float manufacturedConsent, float counterHegemony)
            => HegemonicStability(manufacturedConsent, counterHegemony, CulturalHegemonyParams.Default);

        /// <summary>
        /// 文化的正統性 0..1＝浸透×有機的知識人。価値観を語り正当化する知識人がいるほど正統性が立つ。
        /// intellectualWeight で知識人の効きを与え、Lerp で素の浸透との配分（積で効く）。
        /// </summary>
        public static float CulturalLegitimacy(float valuePenetration, float organicIntellectuals, CulturalHegemonyParams p)
        {
            float pen = Mathf.Clamp01(valuePenetration);
            float org = Mathf.Clamp01(organicIntellectuals);
            // 有機的知識人は intellectualWeight ぶんだけ効かせ、残りは素の浸透を通す。
            float intellectualFactor = Mathf.Lerp(1f, org, p.intellectualWeight);
            return Mathf.Clamp01(pen * intellectualFactor);
        }

        public static float CulturalLegitimacy(float valuePenetration, float organicIntellectuals)
            => CulturalLegitimacy(valuePenetration, organicIntellectuals, CulturalHegemonyParams.Default);

        /// <summary>
        /// ソフトパワー 0..1＝文化的正統性×国際的魅力。自国の正統性が他国にも魅力的に映るほど影響力になる。
        /// softPowerScale で伸びしろ、上限 1。
        /// </summary>
        public static float SoftPower(float culturalLegitimacy, float globalAppeal, CulturalHegemonyParams p)
        {
            float leg = Mathf.Clamp01(culturalLegitimacy);
            float appeal = Mathf.Clamp01(globalAppeal);
            return Mathf.Clamp01(leg * appeal * (1f + p.softPowerScale));
        }

        public static float SoftPower(float culturalLegitimacy, float globalAppeal)
            => SoftPower(culturalLegitimacy, globalAppeal, CulturalHegemonyParams.Default);

        /// <summary>
        /// 覇権が維持されるか＝覇権の安定が閾値を上回るか。
        /// hegemonicStability（-1..1）が threshold を超えれば true。
        /// </summary>
        public static bool IsHegemonyMaintained(float hegemonicStability, float threshold)
        {
            float s = Mathf.Clamp(hegemonicStability, -1f, 1f);
            return s > threshold;
        }

        public static bool IsHegemonyMaintained(float hegemonicStability)
            => IsHegemonyMaintained(hegemonicStability, 0.2f);
    }
}
