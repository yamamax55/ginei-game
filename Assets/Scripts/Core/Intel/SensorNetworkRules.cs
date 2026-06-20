using UnityEngine;

namespace Ginei
{
    /// <summary>センサー網（索敵網）の調整係数。探知距離・カバー率・冗長性・誤探知・データ統合・被探知の重み。</summary>
    public readonly struct SensorNetworkParams
    {
        /// <summary>探知距離の基礎倍率（センサ出力×信号強度^指数 に掛ける）。</summary>
        public readonly float rangeScale;
        /// <summary>目標信号強度に対する探知距離の逓減指数（0.5＝平方根的にしか伸びない）。</summary>
        public readonly float signatureExponent;
        /// <summary>カバー率の基礎倍率（数×個別範囲×重複効率 に掛ける）。</summary>
        public readonly float coverageScale;
        /// <summary>センサ数に対するカバー率の逓減指数（0.5＝重ねるほど飽和）。</summary>
        public readonly float coverageExponent;
        /// <summary>冗長性の逓減基数（1台喪失あたりの残存失敗確率＝小さいほど早く冗長になる）。</summary>
        public readonly float redundancyBase;
        /// <summary>誤探知率の基礎倍率（ノイズ×感度 に掛ける）。</summary>
        public readonly float falseAlarmScale;
        /// <summary>データ統合の精度向上の逓減基数（追加1台あたりの残り誤差比＝小さいほど速く改善・飽和）。</summary>
        public readonly float fusionBase;
        /// <summary>被探知リスクの基礎倍率（出力×敵ESM に掛ける）。</summary>
        public readonly float counterRiskScale;

        public SensorNetworkParams(float rangeScale, float signatureExponent, float coverageScale,
            float coverageExponent, float redundancyBase, float falseAlarmScale, float fusionBase,
            float counterRiskScale)
        {
            this.rangeScale = Mathf.Max(0f, rangeScale);
            this.signatureExponent = Mathf.Clamp(signatureExponent, 0f, 1f);
            this.coverageScale = Mathf.Max(0f, coverageScale);
            this.coverageExponent = Mathf.Clamp(coverageExponent, 0f, 1f);
            this.redundancyBase = Mathf.Clamp(redundancyBase, 0f, 1f);
            this.falseAlarmScale = Mathf.Max(0f, falseAlarmScale);
            this.fusionBase = Mathf.Clamp(fusionBase, 0f, 1f);
            this.counterRiskScale = Mathf.Max(0f, counterRiskScale);
        }

        /// <summary>既定＝距離倍率1・信号指数0.5・カバー倍率0.1・カバー指数0.5・冗長基数0.5・誤報倍率1・統合基数0.5・被探知倍率1。</summary>
        public static SensorNetworkParams Default =>
            new SensorNetworkParams(1f, 0.5f, 0.1f, 0.5f, 0.5f, 1f, 0.5f, 1f);
    }

    /// <summary>
    /// センサー網（索敵網）の純ロジック。複数センサーを重ね合わせて探知範囲を作り、冗長性で一部喪失に耐え、
    /// データ統合で精度を上げる。一方で active sensor は自らも電波を出すため敵の電子支援（ESM）に晒され（被探知）、
    /// 感度を上げると背景ノイズによる誤探知も増える。カバー率の裏返しが索敵網の穴。
    /// 盤面非依存の plain 引数のみ（座標・コンポーネント非依存）。乱数を持たず、必要なら外から roll を与える。
    /// 実効値パターン（与えた基準値は非破壊）。入力は全て clamp。純ロジック（非 MonoBehaviour・test-first）。
    /// 分担：<see cref="SensorPicketRules"/>（哨戒線の早期警戒＝前方展開で接近を早く知る）とは別。
    /// こちらは「索敵網そのものの構築・能力（カバー率/冗長/統合/誤報/被探知）」に特化。
    /// </summary>
    public static class SensorNetworkRules
    {
        /// <summary>
        /// 探知距離＝rangeScale × センサ出力 × 目標信号強度^signatureExponent。
        /// 出力が高く目標が大きいほど遠くで探知できるが、信号強度は指数<1 で逓減。
        /// </summary>
        public static float DetectionRange(float sensorPower, float targetSignature, SensorNetworkParams p)
        {
            float power = Mathf.Max(0f, sensorPower);
            float sig = Mathf.Max(0f, targetSignature);
            return p.rangeScale * power * Mathf.Pow(sig, p.signatureExponent);
        }

        public static float DetectionRange(float sensorPower, float targetSignature)
            => DetectionRange(sensorPower, targetSignature, SensorNetworkParams.Default);

        /// <summary>
        /// 網のカバー率 0..1＝clamp01(coverageScale × 個別範囲 × 数^coverageExponent × 重複効率)。
        /// 数を増やし範囲を広げるほど覆えるが、重複が多い（overlapFactor 低）と無駄になり、数は指数<1 で飽和。
        /// </summary>
        public static float NetworkCoverage(int sensorCount, float individualRange, float overlapFactor, SensorNetworkParams p)
        {
            int count = Mathf.Max(0, sensorCount);
            float range = Mathf.Max(0f, individualRange);
            float overlap = Mathf.Clamp01(overlapFactor);
            return Mathf.Clamp01(p.coverageScale * range * Mathf.Pow(count, p.coverageExponent) * overlap);
        }

        public static float NetworkCoverage(int sensorCount, float individualRange, float overlapFactor)
            => NetworkCoverage(sensorCount, individualRange, overlapFactor, SensorNetworkParams.Default);

        /// <summary>
        /// 冗長性 0..1＝1 - redundancyBase^数。センサが多いほど一部喪失に強い（全滅確率が指数的に下がる）。
        /// 0台で 0、台数が増えるほど 1 に漸近。
        /// </summary>
        public static float Redundancy(int sensorCount, SensorNetworkParams p)
        {
            int count = Mathf.Max(0, sensorCount);
            return Mathf.Clamp01(1f - Mathf.Pow(p.redundancyBase, count));
        }

        public static float Redundancy(int sensorCount)
            => Redundancy(sensorCount, SensorNetworkParams.Default);

        /// <summary>
        /// 誤探知率 0..1＝clamp01(falseAlarmScale × 背景ノイズ × 感度)。
        /// 感度を上げるほど見落としは減るが、背景ノイズを拾って誤報も増える（トレードオフ）。
        /// </summary>
        public static float FalseAlarmRate(float backgroundNoise, float sensitivity, SensorNetworkParams p)
        {
            float noise = Mathf.Clamp01(backgroundNoise);
            float sens = Mathf.Clamp01(sensitivity);
            return Mathf.Clamp01(p.falseAlarmScale * noise * sens);
        }

        public static float FalseAlarmRate(float backgroundNoise, float sensitivity)
            => FalseAlarmRate(backgroundNoise, sensitivity, SensorNetworkParams.Default);

        /// <summary>索敵網の穴 0..1＝1 - カバー率。覆えていない領域の割合（敵がここを抜ける）。</summary>
        public static float CoverageGap(float networkCoverage, SensorNetworkParams p)
        {
            return Mathf.Clamp01(1f - Mathf.Clamp01(networkCoverage));
        }

        public static float CoverageGap(float networkCoverage)
            => CoverageGap(networkCoverage, SensorNetworkParams.Default);

        /// <summary>
        /// データ統合による精度 0..1＝個別精度 + (1-個別精度) × (1 - fusionBase^(数-1))。
        /// 複数センサーを突き合わせるほど誤差が減るが、追加1台の寄与は逓減（収穫逓減）。1台では個別精度のまま。
        /// </summary>
        public static float DataFusionAccuracy(int sensorCount, float individualAccuracy, SensorNetworkParams p)
        {
            int count = Mathf.Max(0, sensorCount);
            float acc = Mathf.Clamp01(individualAccuracy);
            if (count <= 1) return acc;
            float gain = 1f - Mathf.Pow(p.fusionBase, count - 1);
            return Mathf.Clamp01(acc + (1f - acc) * gain);
        }

        public static float DataFusionAccuracy(int sensorCount, float individualAccuracy)
            => DataFusionAccuracy(sensorCount, individualAccuracy, SensorNetworkParams.Default);

        /// <summary>
        /// 被探知リスク 0..1＝clamp01(counterRiskScale × センサ出力 × 敵ESM)。
        /// active sensor は強く照射するほど自分の位置を敵の電子支援に晒す（撃てば見つかる）。
        /// </summary>
        public static float CounterDetectionRisk(float sensorPower, float enemyEsm, SensorNetworkParams p)
        {
            float power = Mathf.Clamp01(sensorPower);
            float esm = Mathf.Clamp01(enemyEsm);
            return Mathf.Clamp01(p.counterRiskScale * power * esm);
        }

        public static float CounterDetectionRisk(float sensorPower, float enemyEsm)
            => CounterDetectionRisk(sensorPower, enemyEsm, SensorNetworkParams.Default);

        /// <summary>
        /// 目標を探知したか＝距離が探知距離内で、その margin（余裕度 0..1）が roll 以上なら true（決定論）。
        /// 距離が探知距離を超えれば常に false。roll は外から注入（乱数を持たない）。
        /// </summary>
        public static bool IsTargetDetected(float detectionRange, float distance, float roll)
        {
            float range = Mathf.Max(0f, detectionRange);
            float dist = Mathf.Max(0f, distance);
            if (dist > range) return false;
            if (range <= 0f) return false;
            float margin = Mathf.Clamp01(1f - dist / range);
            return margin >= Mathf.Clamp01(roll);
        }
    }
}
