using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 報道統制パラメータ（メディア掌握・情報管理）。readonly struct・トップレベル。
    /// ctor で全値をクランプ。Default で標準値。
    /// </summary>
    public readonly struct MediaControlParams
    {
        /// <summary>支配率における国営比率の重み（規制力との配分）。</summary>
        public readonly float ownershipWeight;
        /// <summary>支配率における規制力の重み。</summary>
        public readonly float regulationWeight;
        /// <summary>アジェンダ設定力における論調統一の効き（0..1）。</summary>
        public readonly float disciplineWeight;
        /// <summary>物語支配の最大上乗せ（擁護の伸びしろ）。</summary>
        public readonly float narrativeGain;
        /// <summary>信頼喪失の最大度合い（統制が信用を削る上限0..1）。</summary>
        public readonly float erosionMax;
        /// <summary>地下情報流通の感度（信頼喪失×隙が広がる係数）。</summary>
        public readonly float undergroundSensitivity;
        /// <summary>海外メディア浸透の基準係数。</summary>
        public readonly float foreignScale;
        /// <summary>過剰統制とみなす支配率の閾値（これを超えると反動）。</summary>
        public readonly float overcontrolThreshold;
        /// <summary>過剰統制の反動の最大度合い（0..1）。</summary>
        public readonly float backlashMax;

        public MediaControlParams(
            float ownershipWeight,
            float regulationWeight,
            float disciplineWeight,
            float narrativeGain,
            float erosionMax,
            float undergroundSensitivity,
            float foreignScale,
            float overcontrolThreshold,
            float backlashMax)
        {
            this.ownershipWeight = Mathf.Clamp01(ownershipWeight);
            this.regulationWeight = Mathf.Clamp01(regulationWeight);
            this.disciplineWeight = Mathf.Clamp01(disciplineWeight);
            this.narrativeGain = Mathf.Clamp(narrativeGain, 0f, 2f);
            this.erosionMax = Mathf.Clamp01(erosionMax);
            this.undergroundSensitivity = Mathf.Clamp(undergroundSensitivity, 0f, 4f);
            this.foreignScale = Mathf.Clamp(foreignScale, 0f, 4f);
            this.overcontrolThreshold = Mathf.Clamp01(overcontrolThreshold);
            this.backlashMax = Mathf.Clamp01(backlashMax);
        }

        public static MediaControlParams Default => new MediaControlParams(
            ownershipWeight: 0.5f,
            regulationWeight: 0.5f,
            disciplineWeight: 1f,
            narrativeGain: 0.5f,
            erosionMax: 0.8f,
            undergroundSensitivity: 1f,
            foreignScale: 1f,
            overcontrolThreshold: 0.7f,
            backlashMax: 0.6f);
    }

    /// <summary>
    /// 報道統制ロジック＝政権によるメディアの掌握と情報管理の純数値モデル。
    /// 国営化と規制で報道機関を支配し、論調を統一してアジェンダを設定し政権擁護の物語を支配する。
    /// だが統制は報道の信頼を削り、信頼喪失と統制の隙は地下情報の流通を生む。国境の穴からは海外メディアが
    /// 浸透し、過剰な統制は露骨さゆえの反発（反動）を招く。
    ///
    /// 盤面非依存・plain 引数・決定論・実効値パターン。社会/政治の他ルール（世論・支持率・検閲）とは別系統＝
    /// 本ルールは「報道の支配と物語の制御」の係数モデルに特化する。
    /// </summary>
    public static class MediaControlRules
    {
        /// <summary>
        /// 報道機関の支配率 0..1＝国営比率と規制力の重み付き合成。
        /// 両方高いほど 1 へ。重み和で正規化（どちらか片方では頭打ち）。
        /// </summary>
        public static float MediaDominance(float stateOwnership, float regulatoryControl, MediaControlParams p)
        {
            float own = Mathf.Clamp01(stateOwnership);
            float reg = Mathf.Clamp01(regulatoryControl);
            float wsum = p.ownershipWeight + p.regulationWeight;
            if (wsum <= 0f) return 0f;
            return Mathf.Clamp01((own * p.ownershipWeight + reg * p.regulationWeight) / wsum);
        }

        public static float MediaDominance(float stateOwnership, float regulatoryControl)
            => MediaDominance(stateOwnership, regulatoryControl, MediaControlParams.Default);

        /// <summary>
        /// アジェンダ設定力 0..1＝支配率×論調統一。
        /// 報道機関を握っても論調がバラつけば議題は設定できない（積で効く）。
        /// </summary>
        public static float AgendaSetting(float mediaDominance, float messageDiscipline, MediaControlParams p)
        {
            float dom = Mathf.Clamp01(mediaDominance);
            float disc = Mathf.Clamp01(messageDiscipline);
            // 論調統一は disciplineWeight ぶんだけ効かせ、残りは素の支配率を通す。
            float disciplineFactor = Mathf.Lerp(1f, disc, p.disciplineWeight);
            return Mathf.Clamp01(dom * disciplineFactor);
        }

        public static float AgendaSetting(float mediaDominance, float messageDiscipline)
            => AgendaSetting(mediaDominance, messageDiscipline, MediaControlParams.Default);

        /// <summary>
        /// 政権擁護の物語支配 0..1＝アジェンダ設定に応じて伸びる擁護の強さ。
        /// narrativeGain で伸びしろを与え、上限 1 にクランプ。
        /// </summary>
        public static float NarrativeControl(float agendaSetting, MediaControlParams p)
        {
            float a = Mathf.Clamp01(agendaSetting);
            return Mathf.Clamp01(a * (1f + p.narrativeGain));
        }

        public static float NarrativeControl(float agendaSetting)
            => NarrativeControl(agendaSetting, MediaControlParams.Default);

        /// <summary>
        /// 報道の信頼喪失 0..1＝支配率×受け手の懐疑。統制が露骨なほど、懐疑的な受け手ほど信用を失う。
        /// erosionMax を上限に積で効く。
        /// </summary>
        public static float CredibilityErosion(float mediaDominance, float audienceSkepticism, MediaControlParams p)
        {
            float dom = Mathf.Clamp01(mediaDominance);
            float skep = Mathf.Clamp01(audienceSkepticism);
            return Mathf.Clamp01(dom * skep * p.erosionMax);
        }

        public static float CredibilityErosion(float mediaDominance, float audienceSkepticism)
            => CredibilityErosion(mediaDominance, audienceSkepticism, MediaControlParams.Default);

        /// <summary>
        /// 地下情報の流通 0..1＝信頼喪失×統制の隙。公式報道が信じられないほど、抜け道が多いほど
        /// 非公式情報が流れる。undergroundSensitivity で感度。
        /// </summary>
        public static float UndergroundInformation(float credibilityErosion, float suppressionGaps, MediaControlParams p)
        {
            float ero = Mathf.Clamp01(credibilityErosion);
            float gaps = Mathf.Clamp01(suppressionGaps);
            return Mathf.Clamp01(ero * gaps * p.undergroundSensitivity);
        }

        public static float UndergroundInformation(float credibilityErosion, float suppressionGaps)
            => UndergroundInformation(credibilityErosion, suppressionGaps, MediaControlParams.Default);

        /// <summary>
        /// 海外メディアの浸透 0..1＝国境の穴を、自前の物語支配が薄める。
        /// borderPorosity / (1 + ownNarrativeControl×foreignScale)。物語を強く支配するほど浸透を抑える。
        /// </summary>
        public static float ForeignMediaPenetration(float borderPorosity, float ownNarrativeControl, MediaControlParams p)
        {
            float porosity = Mathf.Clamp01(borderPorosity);
            float own = Mathf.Clamp01(ownNarrativeControl);
            float denom = 1f + own * p.foreignScale;
            return Mathf.Clamp01(porosity / denom);
        }

        public static float ForeignMediaPenetration(float borderPorosity, float ownNarrativeControl)
            => ForeignMediaPenetration(borderPorosity, ownNarrativeControl, MediaControlParams.Default);

        /// <summary>
        /// 過剰統制の反動 0..1＝支配率が閾値を超えた分だけ露骨さへの反発が生じる。
        /// 閾値未満は 0、閾値〜1 を線形に backlashMax へ伸ばす。
        /// </summary>
        public static float BacklashFromOvercontrol(float mediaDominance, MediaControlParams p)
        {
            float dom = Mathf.Clamp01(mediaDominance);
            float th = p.overcontrolThreshold;
            if (dom <= th) return 0f;
            float span = 1f - th;
            if (span <= 0f) return p.backlashMax;
            float over = (dom - th) / span;
            return Mathf.Clamp01(over * p.backlashMax);
        }

        public static float BacklashFromOvercontrol(float mediaDominance)
            => BacklashFromOvercontrol(mediaDominance, MediaControlParams.Default);

        /// <summary>
        /// 物語を支配できているか＝政権擁護の物語支配が地下情報の流通を上回るか。
        /// 差が threshold を超えれば true（擁護優位）。
        /// </summary>
        public static bool IsNarrativeDominant(float narrativeControl, float undergroundInformation, float threshold)
        {
            float nar = Mathf.Clamp01(narrativeControl);
            float und = Mathf.Clamp01(undergroundInformation);
            return (nar - und) > threshold;
        }

        public static bool IsNarrativeDominant(float narrativeControl, float undergroundInformation)
            => IsNarrativeDominant(narrativeControl, undergroundInformation, 0.1f);
    }
}
