using UnityEngine;

namespace Ginei
{
    /// <summary>利用可能性ヒューリスティックの調整係数。</summary>
    public readonly struct AvailabilityBiasParams
    {
        /// <summary>想起の鮮度が減衰する速さ（1/(1+decayRate×t) の係数。大きいほど速く忘れる）。</summary>
        public readonly float recencyDecayRate;
        /// <summary>主観確率の歪みの最大幅（想起容易性で真の確率をどれだけ動かせるか・0..1）。</summary>
        public readonly float distortionScale;
        /// <summary>直近の大敗後に敵を過大評価する強さ（severity=1 で加わる過大評価の最大）。</summary>
        public readonly float shockOverestimateScale;
        /// <summary>平穏が続くと脅威を過小評価する速さ（油断の蓄積速度）。</summary>
        public readonly float complacencyRate;
        /// <summary>基準率データに触れると想起バイアスが薄まる速さ（exposure=1 での減衰の強さ・0..1）。</summary>
        public readonly float baseRateDamping;

        public AvailabilityBiasParams(
            float recencyDecayRate, float distortionScale, float shockOverestimateScale,
            float complacencyRate, float baseRateDamping)
        {
            this.recencyDecayRate = Mathf.Max(0f, recencyDecayRate);
            this.distortionScale = Mathf.Clamp01(distortionScale);
            this.shockOverestimateScale = Mathf.Max(0f, shockOverestimateScale);
            this.complacencyRate = Mathf.Max(0f, complacencyRate);
            this.baseRateDamping = Mathf.Clamp01(baseRateDamping);
        }

        /// <summary>既定＝鮮度減衰0.5・歪み幅0.6・ショック過大評価0.5・油断速度0.05・基準率減衰0.7。</summary>
        public static AvailabilityBiasParams Default
            => new AvailabilityBiasParams(0.5f, 0.6f, 0.5f, 0.05f, 0.7f);
    }

    /// <summary>
    /// 利用可能性ヒューリスティックの純ロジック（カーネマン KAHN-5・近接記憶バイアス）。
    /// 最近の・鮮烈な・想起しやすい出来事ほどその発生確率を過大評価する＝直近の大敗後は敵を過大評価し、
    /// 平穏が続くと脅威を過小評価する（油断）。基準率データに触れると想起バイアスは薄まる。
    /// <see cref="GenerationalMemoryRules"/>（戦争記憶の世代的半減期＝開戦閾値の世代スケール変化）とは別＝
    /// こちらは個人の確率判断を歪める想起容易性のミクロ効果。<see cref="ReconRules"/>（情報の不確実性＝真値の推定誤差）
    /// とも別＝<see cref="AvailabilityBiasRules"/> は真の確率を歪めた主観確率を返す。同 EPIC の
    /// OverconfidenceBiasRules（過信＝自分の判断の確信度バイアス）とは別系統の系統的バイアス。
    /// 盤面非依存の plain 引数・乱数なし・決定論・基準確率非破壊（実効値パターン）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AvailabilityBiasRules
    {
        /// <summary>
        /// 想起の鮮度（0..1）＝出来事からの経過時間で減衰（最近ほど重い）。
        /// 1/(1+decayRate×t) の代数式（log/exp を使わない）。t=0 で 1。
        /// </summary>
        public static float RecencyWeight(float timeSinceEvent, AvailabilityBiasParams p)
        {
            float t = Mathf.Max(0f, timeSinceEvent);
            return 1f / (1f + p.recencyDecayRate * t);
        }

        public static float RecencyWeight(float timeSinceEvent)
            => RecencyWeight(timeSinceEvent, AvailabilityBiasParams.Default);

        /// <summary>
        /// 鮮烈さによる想起の底上げ（1..2）＝鮮烈な大事件（eventSeverity 0..1）ほど記憶に焼き付き想起しやすい。
        /// severity=0 で 1.0（凡庸な出来事は割り増しなし）、severity=1 で 2.0（最大の倍率）。
        /// </summary>
        public static float VividnessBoost(float eventSeverity)
        {
            return 1f + Mathf.Clamp01(eventSeverity);
        }

        /// <summary>
        /// 想起容易性スコア（0..）＝鮮度×鮮烈さ×頻度。頻度 frequency は触れた回数の正規化値（0..1 想定だが下限のみクランプ）。
        /// </summary>
        public static float AvailabilityScore(float recencyWeight, float vividness, float frequency)
        {
            float recency = Mathf.Clamp01(recencyWeight);
            float vivid = Mathf.Max(0f, vividness);
            float freq = Mathf.Max(0f, frequency);
            return recency * vivid * freq;
        }

        /// <summary>
        /// 想起容易性で真の確率を歪めた主観確率（0..1）。availabilityScore が大きいほど真の確率を distortionScale 幅で
        /// 1 へ引き上げる（過大評価）。score=0 なら真の確率のまま。基準（trueProbability）は非破壊＝歪めた値を返す。
        /// </summary>
        public static float PerceivedProbability(float trueProbability, float availabilityScore, AvailabilityBiasParams p)
        {
            float tp = Mathf.Clamp01(trueProbability);
            // score を 0..1 の引き上げ係数へ（飽和）：score/(1+score)
            float score = Mathf.Max(0f, availabilityScore);
            float pull = score / (1f + score);
            float lift = p.distortionScale * pull;
            return Mathf.Clamp01(Mathf.Lerp(tp, 1f, lift));
        }

        public static float PerceivedProbability(float trueProbability, float availabilityScore)
            => PerceivedProbability(trueProbability, availabilityScore, AvailabilityBiasParams.Default);

        /// <summary>
        /// 主観と現実のズレ（-1..1）＝主観確率−真の確率。正なら過大評価、負なら過小評価。
        /// </summary>
        public static float ProbabilityDistortion(float perceivedProbability, float trueProbability)
        {
            return Mathf.Clamp01(perceivedProbability) - Mathf.Clamp01(trueProbability);
        }

        /// <summary>
        /// 直近の大敗後の敵過大評価（0..shockOverestimateScale）＝直近の敗北の苛烈さ（recentDefeatSeverity 0..1）に比例。
        /// 鮮烈な大敗ほど想起されやすく、敵の脅威を過大評価する。
        /// </summary>
        public static float ThreatOverestimateAfterShock(float recentDefeatSeverity, AvailabilityBiasParams p)
        {
            return Mathf.Clamp01(recentDefeatSeverity) * p.shockOverestimateScale;
        }

        public static float ThreatOverestimateAfterShock(float recentDefeatSeverity)
            => ThreatOverestimateAfterShock(recentDefeatSeverity, AvailabilityBiasParams.Default);

        /// <summary>
        /// 平穏が続くことによる油断（0..1）＝脅威を過小評価する度合い。平穏期間 peacefulDuration が長いほど
        /// 脅威の事例が想起されず油断が蓄積する。complacencyRate×duration / (1+complacencyRate×duration) で 1 へ飽和。
        /// </summary>
        public static float ComplacencyAfterCalm(float peacefulDuration, AvailabilityBiasParams p)
        {
            float d = Mathf.Max(0f, peacefulDuration);
            float x = p.complacencyRate * d;
            return Mathf.Clamp01(x / (1f + x));
        }

        public static float ComplacencyAfterCalm(float peacefulDuration)
            => ComplacencyAfterCalm(peacefulDuration, AvailabilityBiasParams.Default);

        /// <summary>
        /// 基準率データに触れると想起バイアスが薄まる＝補正後の想起容易性スコア。
        /// baseRateExposure（0..1＝統計・基準率にどれだけ触れたか）が大きいほどスコアを縮める
        /// （脱バイアス＝記憶の鮮烈さより数字で考える）。
        /// </summary>
        public static float BiasDecayWithData(float availabilityScore, float baseRateExposure, AvailabilityBiasParams p)
        {
            float score = Mathf.Max(0f, availabilityScore);
            float exposure = Mathf.Clamp01(baseRateExposure);
            return score * (1f - p.baseRateDamping * exposure);
        }

        public static float BiasDecayWithData(float availabilityScore, float baseRateExposure)
            => BiasDecayWithData(availabilityScore, baseRateExposure, AvailabilityBiasParams.Default);

        /// <summary>
        /// 歪みが閾値を超えたら想起バイアス駆動の判定（true）。ズレの絶対値が threshold 超で
        /// 「確率判断が記憶の想起容易性に乗っ取られている」とみなす。
        /// </summary>
        public static bool IsAvailabilityDriven(float probabilityDistortion, float threshold)
        {
            return Mathf.Abs(probabilityDistortion) > Mathf.Max(0f, threshold);
        }
    }
}
