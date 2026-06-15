using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 情報環境の純データ（検閲水準と情報自由度＝ミル『自由論』の言論の自由）。informationFreedom は情報自由度(0..1)、
    /// censorshipLevel は検閲水準(0..1)、hiddenError は検閲下で表に出ず溜まった隠れた誤り(0..1)。
    /// 解決は <see cref="CensorshipRules"/> が窓口。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    [System.Serializable]
    public struct InformationEnvironment
    {
        public float informationFreedom;  // 情報自由度 0..1
        public float censorshipLevel;     // 検閲水準 0..1
        public float hiddenError;         // 隠れた誤り 0..1（検閲下で蓄積）

        public InformationEnvironment(float informationFreedom, float censorshipLevel, float hiddenError = 0f)
        {
            this.informationFreedom = Mathf.Clamp01(informationFreedom);
            this.censorshipLevel = Mathf.Clamp01(censorshipLevel);
            this.hiddenError = Mathf.Clamp01(hiddenError);
        }
    }

    /// <summary>検閲の調整係数（短期安定と長期腐敗の非対称）。</summary>
    public readonly struct CensorshipParams
    {
        /// <summary>検閲が異論を抑え込む効率（0..1。1で検閲水準ぶんの異論を完全に静める）。</summary>
        public readonly float suppressionEfficiency;
        /// <summary>長期腐敗の蓄積速度（検閲水準に比例・per dt。批判なき権力は腐る）。</summary>
        public readonly float corruptionRate;
        /// <summary>非対称トレードオフの重み（長期腐敗を短期安定より重く見る係数≥1。割に合わなさ）。</summary>
        public readonly float asymmetryWeight;
        /// <summary>支配的意見が死んだ教条に堕する速さ（検閲水準に比例・per dt。反論なき真理は形骸化）。</summary>
        public readonly float dogmaRate;
        /// <summary>隠れた誤りの蓄積速度（検閲水準に比例・per dt。検閲下では誤りが表に出ない）。</summary>
        public readonly float hiddenErrorRate;
        /// <summary>情報自由がもたらす長期健全性の便益率（自由度に比例。誤りの早期発見と真理の精錬）。</summary>
        public readonly float freedomBenefitRate;
        /// <summary>検閲の罠とみなす長期腐敗の閾値（これを超え短期安定が高ければ罠）。</summary>
        public readonly float trapThreshold;

        public CensorshipParams(float suppressionEfficiency, float corruptionRate, float asymmetryWeight,
                                float dogmaRate, float hiddenErrorRate, float freedomBenefitRate, float trapThreshold)
        {
            this.suppressionEfficiency = Mathf.Clamp01(suppressionEfficiency);
            this.corruptionRate = Mathf.Max(0f, corruptionRate);
            this.asymmetryWeight = Mathf.Max(1f, asymmetryWeight);
            this.dogmaRate = Mathf.Max(0f, dogmaRate);
            this.hiddenErrorRate = Mathf.Max(0f, hiddenErrorRate);
            this.freedomBenefitRate = Mathf.Max(0f, freedomBenefitRate);
            this.trapThreshold = Mathf.Clamp01(trapThreshold);
        }

        /// <summary>既定＝抑圧効率0.8・腐敗蓄積0.1・非対称重み2.0・教条化0.08・隠れ誤り0.1・自由便益0.05・罠閾値0.5。</summary>
        public static CensorshipParams Default => new CensorshipParams(0.8f, 0.1f, 2f, 0.08f, 0.1f, 0.05f, 0.5f);
    }

    /// <summary>
    /// 検閲水準と情報自由度の純ロジック（MILL-1 #1474・ミル『自由論』）。検閲は短期的に異論を抑え体制を安定させるが、
    /// 長期的には誤りを正せず腐敗を招き、反論のない支配的意見すら「死んだ教条」になって活力を失う＝
    /// 短期安定（今すぐ）と長期腐敗（じわじわ）の非対称なトレードオフ。封殺された意見はそれが真理だった可能性を
    /// 奪い、誤りであっても真理との衝突で真理を生き生きと保つ機会を奪う。
    /// <see cref="FreePressRules"/>（報道が腐敗を発見する監視側）・<see cref="PropagandaRules"/>（世論操作の発信側）
    /// とは別系統＝こちらは検閲の短期安定と長期腐敗の非対称（<see cref="InformationEnvironment"/> が中核データ）。
    /// 隠れた誤りの蓄積は InstitutionalCorrectionRules（誤り蓄積＝隠蔽）と整合し、情報自由の便益は
    /// LibertyCultureRules（同 EPIC MILL）と分担する。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CensorshipRules
    {
        /// <summary>
        /// 短期安定（0..1）＝検閲が即座に異論を抑え込む静穏。異論 dissent(0..1) のうち
        /// 検閲水準 × 抑圧効率ぶんが今すぐ静まる＝検閲は短期的に体制を安定させる（即効）。
        /// 異論ゼロなら安定への寄与もゼロ（抑えるべき声が無い）。
        /// </summary>
        public static float ShortTermStability(float censorshipLevel, float dissent, CensorshipParams p)
        {
            float c = Mathf.Clamp01(censorshipLevel);
            float d = Mathf.Clamp01(dissent);
            return Mathf.Clamp01(c * p.suppressionEfficiency * d);
        }

        public static float ShortTermStability(float censorshipLevel, float dissent)
            => ShortTermStability(censorshipLevel, dissent, CensorshipParams.Default);

        /// <summary>
        /// 長期腐敗の増分（≥0）＝検閲水準 × 腐敗蓄積率 × dt。批判が封じられた権力は誤りを正されず腐る
        /// ＝検閲は長期的に腐敗を招く（じわじわ）。検閲ゼロなら腐敗の増分もゼロ。
        /// </summary>
        public static float LongTermCorruption(float censorshipLevel, float dt, CensorshipParams p)
        {
            return Mathf.Clamp01(censorshipLevel) * p.corruptionRate * Mathf.Max(0f, dt);
        }

        public static float LongTermCorruption(float censorshipLevel, float dt)
            => LongTermCorruption(censorshipLevel, dt, CensorshipParams.Default);

        /// <summary>
        /// 非対称トレードオフの正味評価＝短期安定 − 長期腐敗 × 非対称重み。
        /// 安定は今すぐ得られるが腐敗はじわじわ重く効く（重み≥1で割引なく重く見る）＝割に合わない。
        /// 正なら当座は得、負なら長期的には損（負ほど検閲の代償が大きい）。
        /// </summary>
        public static float AsymmetricTradeoff(float shortTermStability, float longTermCorruption, CensorshipParams p)
        {
            float s = Mathf.Clamp01(shortTermStability);
            float corr = Mathf.Max(0f, longTermCorruption);
            return s - corr * p.asymmetryWeight;
        }

        public static float AsymmetricTradeoff(float shortTermStability, float longTermCorruption)
            => AsymmetricTradeoff(shortTermStability, longTermCorruption, CensorshipParams.Default);

        /// <summary>
        /// 封殺された真理の喪失（0..1）＝検閲水準 × 封じた意見に含まれた真理の割合 suppressedValidIdeas(0..1)。
        /// ミル＝封じた意見は正しかったかもしれず、それを封殺することは真理かもしれない可能性を奪う。
        /// 検閲ゼロ、あるいは封じた意見に真理が無ければ喪失もゼロ。
        /// </summary>
        public static float SuppressedTruthLoss(float censorshipLevel, float suppressedValidIdeas)
        {
            return Mathf.Clamp01(censorshipLevel) * Mathf.Clamp01(suppressedValidIdeas);
        }

        /// <summary>
        /// 死んだ教条への堕落の増分（≥0）＝検閲水準 × 教条化率 × dt。反論を許さない支配的意見は
        /// 真理であっても衝突で磨かれず、活力を失って「死んだ教条」になる（形骸化）。
        /// 検閲ゼロ（反論が生きている）なら教条化の増分もゼロ。
        /// </summary>
        public static float DeadDogma(float censorshipLevel, float dt, CensorshipParams p)
        {
            return Mathf.Clamp01(censorshipLevel) * p.dogmaRate * Mathf.Max(0f, dt);
        }

        public static float DeadDogma(float censorshipLevel, float dt)
            => DeadDogma(censorshipLevel, dt, CensorshipParams.Default);

        /// <summary>
        /// 隠れた誤りの1tick更新（0..1）。新規の誤り newErrors(per dt・0..1) のうち検閲水準ぶんは表に出ず蓄積し、
        /// 検閲が無い分（情報自由＝1−検閲水準）は露見して溜まらない＝検閲下では誤りが隠れて積もる。
        /// InstitutionalCorrectionRules の隠蔽と整合（自由なら都度補正・統制下なら累積）。
        /// </summary>
        public static float HiddenErrorAccumulation(float hidden, float censorshipLevel, float newErrors, float dt, CensorshipParams p)
        {
            float stock = Mathf.Clamp01(hidden);
            float c = Mathf.Clamp01(censorshipLevel);
            float gained = Mathf.Clamp01(newErrors) * c * p.hiddenErrorRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(stock + gained);
        }

        public static float HiddenErrorAccumulation(float hidden, float censorshipLevel, float newErrors, float dt)
            => HiddenErrorAccumulation(hidden, censorshipLevel, newErrors, dt, CensorshipParams.Default);

        /// <summary>
        /// 情報自由の便益（≥0）＝情報自由度 × 便益率。情報自由は誤りの早期発見と真理の精錬をもたらし、
        /// 体制の長期健全性に効く（検閲の長期腐敗の対極＝自由な批判が組織を健やかに保つ）。
        /// </summary>
        public static float InformationFreedomBenefit(float informationFreedom, CensorshipParams p)
        {
            return Mathf.Clamp01(informationFreedom) * p.freedomBenefitRate;
        }

        public static float InformationFreedomBenefit(float informationFreedom)
            => InformationFreedomBenefit(informationFreedom, CensorshipParams.Default);

        /// <summary>
        /// 検閲の罠か＝短期安定に釣られて長期腐敗に陥った状態。
        /// 長期腐敗 longTermCorruption が閾値 threshold を超え、かつ短期安定 shortTermStability が
        /// その閾値以上（短期の静穏が腐敗を覆い隠している）なら罠＝目先の安定が長期の腐敗を招いている。
        /// </summary>
        public static bool IsCensorshipTrap(float longTermCorruption, float shortTermStability, float threshold)
        {
            float corr = Mathf.Max(0f, longTermCorruption);
            float s = Mathf.Clamp01(shortTermStability);
            float t = Mathf.Clamp01(threshold);
            return corr > t && s >= t;
        }

        public static bool IsCensorshipTrap(float longTermCorruption, float shortTermStability)
            => IsCensorshipTrap(longTermCorruption, shortTermStability, CensorshipParams.Default.trapThreshold);
    }
}
