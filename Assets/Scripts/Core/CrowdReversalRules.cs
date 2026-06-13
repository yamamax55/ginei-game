using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 群衆易変パラメータ（CRWD-5 #1824・ル・ボン）。全フィールドはコンストラクタでClamp。
    /// </summary>
    public readonly struct CrowdReversalParams
    {
        /// <summary>群衆強度→易変性の基礎係数（強度1で到達する易変性）。</summary>
        public readonly float volatilityScale;
        /// <summary>易変性の非線形指数（&gt;1で強度が高いほど急に移ろいやすい）。</summary>
        public readonly float volatilityExponent;
        /// <summary>反転確率におけるきっかけの効き具合。</summary>
        public readonly float triggerWeight;
        /// <summary>反転確率における易変性の効き具合。</summary>
        public readonly float volatilityWeight;
        /// <summary>反転振幅の基礎倍率。</summary>
        public readonly float amplitudeScale;
        /// <summary>反転を「反転局面」とみなす確率しきい値。</summary>
        public readonly float reversalThreshold;
        /// <summary>反転1回ごとの消耗による振幅減衰係数。</summary>
        public readonly float dampingPerReversal;

        public CrowdReversalParams(
            float volatilityScale,
            float volatilityExponent,
            float triggerWeight,
            float volatilityWeight,
            float amplitudeScale,
            float reversalThreshold,
            float dampingPerReversal)
        {
            this.volatilityScale = Mathf.Clamp01(volatilityScale);
            this.volatilityExponent = Mathf.Clamp(volatilityExponent, 0.1f, 8f);
            this.triggerWeight = Mathf.Clamp01(triggerWeight);
            this.volatilityWeight = Mathf.Clamp01(volatilityWeight);
            this.amplitudeScale = Mathf.Clamp(amplitudeScale, 0f, 4f);
            this.reversalThreshold = Mathf.Clamp01(reversalThreshold);
            this.dampingPerReversal = Mathf.Clamp01(dampingPerReversal);
        }

        public static CrowdReversalParams Default => new CrowdReversalParams(
            volatilityScale: 0.9f,
            volatilityExponent: 1.5f,
            triggerWeight: 0.6f,
            volatilityWeight: 0.4f,
            amplitudeScale: 1.0f,
            reversalThreshold: 0.5f,
            dampingPerReversal: 0.2f);
    }

    /// <summary>
    /// 群衆の易変＝群衆感情の極端さと非連続な一斉反転（CRWD-5 #1824・ル・ボン）。
    /// 群衆の感情は移ろいやすく、歓喜と恐慌の間を一斉に非連続反転する。群衆強度が高いほど
    /// 反転確率と振幅が大きい（昨日の英雄が今日の暴徒の標的）。きっかけ一つで熱狂が憎悪へ、
    /// 勝利の歓喜が敗北の恐慌へ反転する。
    /// 乱数は <c>float roll</c>（0..1）で受ける決定論。盤面非依存のplain引数。実効値パターン。
    /// 分担：<see cref="ReversalRules"/>（老子の逆U字＝極まると反対へ転じる汎用数学）とは別＝
    /// 群衆感情の非連続な一斉反転に特化／同EPIC CRWD の CrowdContagionRules（群衆化の相転移）の
    /// crowdIntensity を入力に取る／ManiaRules（SIR感染）とは別。
    /// </summary>
    public static class CrowdReversalRules
    {
        /// <summary>
        /// 感情易変性：群衆強度が高いほど感情が移ろいやすい（0..1）。指数で非線形に立ち上がる。
        /// </summary>
        public static float EmotionalVolatility(float crowdIntensity, CrowdReversalParams p)
        {
            float c = Mathf.Clamp01(crowdIntensity);
            float shaped = Mathf.Pow(c, p.volatilityExponent);
            return Mathf.Clamp01(shaped * p.volatilityScale);
        }

        public static float EmotionalVolatility(float crowdIntensity)
            => EmotionalVolatility(crowdIntensity, CrowdReversalParams.Default);

        /// <summary>
        /// 反転確率：きっかけの大きさと易変性で決まる（0..1）。両者の加重和。
        /// </summary>
        public static float ReversalProbability(float volatility, float triggerMagnitude, CrowdReversalParams p)
        {
            float v = Mathf.Clamp01(volatility);
            float t = Mathf.Clamp01(triggerMagnitude);
            float prob = t * p.triggerWeight + v * p.volatilityWeight;
            // きっかけと易変性が乗算的に高い時ほど確実に反転する（相乗）。
            float synergy = v * t;
            return Mathf.Clamp01(prob + synergy * (1f - prob));
        }

        public static float ReversalProbability(float volatility, float triggerMagnitude)
            => ReversalProbability(volatility, triggerMagnitude, CrowdReversalParams.Default);

        /// <summary>
        /// 反転振幅：強度が高いほど大きく振れる。現在感情の絶対値が大きいほど反転で大きく動く
        /// （遠くまで振れる）。0..2 程度。
        /// </summary>
        public static float ReversalAmplitude(float crowdIntensity, float currentEmotion, CrowdReversalParams p)
        {
            float c = Mathf.Clamp01(crowdIntensity);
            float e = Mathf.Clamp(currentEmotion, -1f, 1f);
            // 現在の振れ幅(|e|)を含め、反対側まで振り戻すぶんを基礎に強度で増幅。
            float baseSwing = 0.5f + 0.5f * Mathf.Abs(e);
            return Mathf.Max(0f, baseSwing * (0.5f + c) * p.amplitudeScale);
        }

        public static float ReversalAmplitude(float crowdIntensity, float currentEmotion)
            => ReversalAmplitude(crowdIntensity, currentEmotion, CrowdReversalParams.Default);

        /// <summary>
        /// rollで実際に反転するか（決定論）。roll が反転確率を下回れば反転。
        /// </summary>
        public static bool TriggerReversal(float reversalProbability, float roll)
        {
            float prob = Mathf.Clamp01(reversalProbability);
            float r = Mathf.Clamp01(roll);
            return r < prob;
        }

        /// <summary>
        /// 歓喜⇔恐慌の符号反転（+歓喜 / -恐慌）。現在感情を反対符号へ振幅ぶん振る。結果は-1..1。
        /// 感情が0付近でも反転先（符号反転した方向）へ amplitude ぶん振れる。
        /// </summary>
        public static float FlipEmotion(float currentEmotion, float amplitude)
        {
            float e = Mathf.Clamp(currentEmotion, -1f, 1f);
            float amp = Mathf.Max(0f, amplitude);
            // 反転方向＝現在符号の反対（0は恐慌側=負へ振る＝崩落の非対称）。
            float direction = e > 0f ? -1f : (e < 0f ? 1f : -1f);
            float flipped = e + direction * amp;
            return Mathf.Clamp(flipped, -1f, 1f);
        }

        /// <summary>
        /// 崇拝が一転して標的になる度合い（0..1）。昨日の英雄が今日の生贄。
        /// 崇拝が高いほど、そしてきっかけ（失敗）が大きいほど、振り幅は大きい。
        /// </summary>
        public static float HeroToScapegoat(float adulation, float triggerFailure)
        {
            float a = Mathf.Clamp01(adulation);
            float f = Mathf.Clamp01(triggerFailure);
            // 持ち上げた高さ(a)が落差を生む＝崇拝×失敗の積に比例。
            return Mathf.Clamp01(a * f);
        }

        /// <summary>
        /// 勝利の歓喜が敗北の恐慌へ転じる度合い（0..1）。歓喜が高くショックが大きいほど深い恐慌。
        /// </summary>
        public static float EuphoriaToPanic(float euphoria, float shockMagnitude)
        {
            float eu = Mathf.Clamp01(euphoria);
            float s = Mathf.Clamp01(shockMagnitude);
            // 浮かれていた分だけ恐慌が深い＝歓喜とショックの積を基礎に。
            return Mathf.Clamp01(eu * s);
        }

        /// <summary>
        /// 反転を繰り返すと群衆が消耗して振幅が減衰する（0..入力振幅）。
        /// </summary>
        public static float ReversalDamping(float amplitude, int reversals, CrowdReversalParams p)
        {
            float amp = Mathf.Max(0f, amplitude);
            int n = reversals < 0 ? 0 : reversals;
            float factor = Mathf.Clamp01(1f - p.dampingPerReversal * n);
            return amp * factor;
        }

        public static float ReversalDamping(float amplitude, int reversals)
            => ReversalDamping(amplitude, reversals, CrowdReversalParams.Default);

        /// <summary>
        /// 反転局面にあるか（反転確率がしきい値以上）。
        /// </summary>
        public static bool IsReversing(float reversalProbability, float threshold)
        {
            return Mathf.Clamp01(reversalProbability) >= Mathf.Clamp01(threshold);
        }

        public static bool IsReversing(float reversalProbability)
            => IsReversing(reversalProbability, CrowdReversalParams.Default.reversalThreshold);
    }
}
