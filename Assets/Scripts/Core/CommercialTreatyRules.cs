using UnityEngine;

namespace Ginei
{
    /// <summary>通商条約（交易協定）の調整係数。関税引き下げ→貿易拡大→比較優位の利得・非対称・依存・互恵を結ぶ。</summary>
    public readonly struct CommercialTreatyParams
    {
        /// <summary>関税引き下げ×市場規模 → 貿易拡大の利得倍率。</summary>
        public readonly float expansionGain;
        /// <summary>専門化×貿易拡大 → 比較優位の利得倍率。</summary>
        public readonly float advantageGain;
        /// <summary>交渉力差 → 条約の非対称（不平等度）の倍率。</summary>
        public readonly float asymmetryGain;
        /// <summary>貿易拡大/経済規模 → 貿易依存度の倍率。</summary>
        public readonly float dependencyGain;
        /// <summary>依存×非対称 → 政治的影響力の倍率。</summary>
        public readonly float influenceGain;
        /// <summary>互恵的とみなす互恵性の既定閾値（0..1）。</summary>
        public readonly float reciprocityThreshold;

        public CommercialTreatyParams(
            float expansionGain, float advantageGain, float asymmetryGain,
            float dependencyGain, float influenceGain, float reciprocityThreshold)
        {
            this.expansionGain = Mathf.Max(0f, expansionGain);
            this.advantageGain = Mathf.Max(0f, advantageGain);
            this.asymmetryGain = Mathf.Max(0f, asymmetryGain);
            this.dependencyGain = Mathf.Max(0f, dependencyGain);
            this.influenceGain = Mathf.Max(0f, influenceGain);
            this.reciprocityThreshold = Mathf.Clamp01(reciprocityThreshold);
        }

        /// <summary>既定＝拡大1.0・優位1.0・非対称1.0・依存1.0・影響1.0・互恵閾値0.6。</summary>
        public static CommercialTreatyParams Default =>
            new CommercialTreatyParams(1f, 1f, 1f, 1f, 1f, 0.6f);
    }

    /// <summary>
    /// 通商条約（交易協定）の純ロジック。関税の相互引き下げが貿易を拡大し、専門化（比較優位）で総利得を生む。
    /// 交渉力の差は条約を非対称（不平等条約）にして利得の取り分を歪め、弱い側に偏った貿易依存は強い側の政治的
    /// 影響力に転化する。両者の取り分のバランス＝互恵性が高く総利得が正なら条約は相互利益的。
    /// 乱数なし・決定論。実効値パターン（基準値非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CommercialTreatyRules
    {
        /// <summary>
        /// 貿易の拡大＝関税引き下げ tariffReduction(0..1) × 市場規模 marketSize(≥0) × expansionGain。
        /// 関税ゼロ引き下げや市場ゼロなら拡大なし。
        /// </summary>
        public static float TradeExpansion(float tariffReduction, float marketSize, CommercialTreatyParams p)
        {
            float tr = Mathf.Clamp01(tariffReduction);
            float ms = Mathf.Max(0f, marketSize);
            return tr * ms * p.expansionGain;
        }

        public static float TradeExpansion(float tariffReduction, float marketSize)
            => TradeExpansion(tariffReduction, marketSize, CommercialTreatyParams.Default);

        /// <summary>
        /// 比較優位の利得＝専門化 specialization(0..1) × 貿易拡大 tradeExpansion(≥0) × advantageGain。
        /// 各国が得意分野へ特化するほど交易から得られる総利得が大きい。
        /// </summary>
        public static float ComparativeAdvantageGain(float specialization, float tradeExpansion, CommercialTreatyParams p)
        {
            float sp = Mathf.Clamp01(specialization);
            float te = Mathf.Max(0f, tradeExpansion);
            return sp * te * p.advantageGain;
        }

        public static float ComparativeAdvantageGain(float specialization, float tradeExpansion)
            => ComparativeAdvantageGain(specialization, tradeExpansion, CommercialTreatyParams.Default);

        /// <summary>
        /// 条約の非対称（不平等度・符号付き -1..1）＝交渉力の差 (A−B) × asymmetryGain。
        /// 正＝A が有利（A に偏った不平等条約）、負＝B が有利、0＝対等。
        /// </summary>
        public static float TermsAsymmetry(float bargainingPowerA, float bargainingPowerB, CommercialTreatyParams p)
        {
            float a = Mathf.Clamp01(bargainingPowerA);
            float b = Mathf.Clamp01(bargainingPowerB);
            return Mathf.Clamp((a - b) * p.asymmetryGain, -1f, 1f);
        }

        public static float TermsAsymmetry(float bargainingPowerA, float bargainingPowerB)
            => TermsAsymmetry(bargainingPowerA, bargainingPowerB, CommercialTreatyParams.Default);

        /// <summary>
        /// 弱い側の取り分＝比較優位の総利得 comparativeAdvantageGain(≥0) × (1 − |非対称|)。
        /// 条約が非対称なほど弱い側の取り分は痩せる（対等なら利得満額）。
        /// </summary>
        public static float GainsDistribution(float comparativeAdvantageGain, float termsAsymmetry, CommercialTreatyParams p)
        {
            float gain = Mathf.Max(0f, comparativeAdvantageGain);
            float fair = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Clamp(termsAsymmetry, -1f, 1f)));
            return gain * fair;
        }

        public static float GainsDistribution(float comparativeAdvantageGain, float termsAsymmetry)
            => GainsDistribution(comparativeAdvantageGain, termsAsymmetry, CommercialTreatyParams.Default);

        /// <summary>
        /// 貿易依存度（0..1）＝貿易拡大 tradeExpansion(≥0) / 経済規模 economicSize × dependencyGain。
        /// 自国経済に占める交易の比重。economicSize=0 は依存度0として安全に扱う（拡大の行き場なし）。
        /// </summary>
        public static float TradeDependency(float tradeExpansion, float economicSize, CommercialTreatyParams p)
        {
            float te = Mathf.Max(0f, tradeExpansion);
            float es = Mathf.Max(0f, economicSize);
            if (es <= 0f) return 0f;
            return Mathf.Clamp01(te / es * p.dependencyGain);
        }

        public static float TradeDependency(float tradeExpansion, float economicSize)
            => TradeDependency(tradeExpansion, economicSize, CommercialTreatyParams.Default);

        /// <summary>
        /// 強い側の政治的影響力（0..1）＝貿易依存 tradeDependency(0..1) × |非対称| × influenceGain。
        /// 弱い側が交易に依存し、かつ条約が非対称なほど、強い側は依存を梃子に政治的に影響を及ぼせる。
        /// </summary>
        public static float PoliticalInfluence(float tradeDependency, float asymmetry, CommercialTreatyParams p)
        {
            float dep = Mathf.Clamp01(tradeDependency);
            float asym = Mathf.Abs(Mathf.Clamp(asymmetry, -1f, 1f));
            return Mathf.Clamp01(dep * asym * p.influenceGain);
        }

        public static float PoliticalInfluence(float tradeDependency, float asymmetry)
            => PoliticalInfluence(tradeDependency, asymmetry, CommercialTreatyParams.Default);

        /// <summary>
        /// 互恵性（0..1）＝両者の取り分のバランス。差が小さいほど高い＝1 − |A−B|/(A+B)。
        /// 双方ゼロなら互恵性なし（0）。p は将来拡張用（現状は素のバランス）。
        /// </summary>
        public static float Reciprocity(float gainsDistributionA, float gainsDistributionB, CommercialTreatyParams p)
        {
            float a = Mathf.Max(0f, gainsDistributionA);
            float b = Mathf.Max(0f, gainsDistributionB);
            float sum = a + b;
            if (sum <= 0f) return 0f;
            return Mathf.Clamp01(1f - Mathf.Abs(a - b) / sum);
        }

        public static float Reciprocity(float gainsDistributionA, float gainsDistributionB)
            => Reciprocity(gainsDistributionA, gainsDistributionB, CommercialTreatyParams.Default);

        /// <summary>
        /// 条約が相互利益的か＝総利得が正（comparativeAdvantageGain > 0）かつ互恵性が閾値以上。
        /// 片務的（互恵性が低い不平等条約）や利得ゼロ（貿易が拡がらない）は不成立。
        /// </summary>
        public static bool IsTreatyMutuallyBeneficial(float comparativeAdvantageGain, float reciprocity, float threshold)
        {
            float gain = Mathf.Max(0f, comparativeAdvantageGain);
            float rec = Mathf.Clamp01(reciprocity);
            float th = Mathf.Clamp01(threshold);
            return gain > 0f && rec >= th;
        }

        public static bool IsTreatyMutuallyBeneficial(float comparativeAdvantageGain, float reciprocity)
            => IsTreatyMutuallyBeneficial(comparativeAdvantageGain, reciprocity, CommercialTreatyParams.Default.reciprocityThreshold);
    }
}
