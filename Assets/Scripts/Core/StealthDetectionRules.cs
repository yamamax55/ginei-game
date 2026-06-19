using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ステルスと探知の調整値（#隠密艦）。被探知性の最大値・探知の効きやすさ・破れの効きなど。
    /// すべて ctor で clamp し、基準値を非破壊で扱う（実効値パターン）。
    /// </summary>
    public readonly struct StealthDetectionParams
    {
        /// <summary>シグネチャ（被探知性）の上限。1で従来どおり、低くするとステルスが効きにくくなる。</summary>
        public readonly float maxSignature;
        /// <summary>距離による探知減衰の効き（大きいほど遠距離で探知しにくい）。</summary>
        public readonly float distanceFalloff;
        /// <summary>センサー感度の倍率（探知確率に掛かる）。</summary>
        public readonly float sensorGain;
        /// <summary>ステルスの破れ（発砲・急機動による露呈）の効き。</summary>
        public readonly float breakWeight;
        /// <summary>探知の積み重ね（観察時間）の効き。大きいほど長時間で見つかりやすい。</summary>
        public readonly float cumulativeWeight;

        public StealthDetectionParams(float maxSignature, float distanceFalloff, float sensorGain,
            float breakWeight, float cumulativeWeight)
        {
            this.maxSignature = Mathf.Clamp(maxSignature, 0f, 1f);
            this.distanceFalloff = Mathf.Clamp(distanceFalloff, 0f, 10f);
            this.sensorGain = Mathf.Clamp(sensorGain, 0f, 10f);
            this.breakWeight = Mathf.Clamp01(breakWeight);
            this.cumulativeWeight = Mathf.Clamp01(cumulativeWeight);
        }

        /// <summary>既定：上限1・距離減衰1・感度1・破れ0.5・積み重ね0.5。</summary>
        public static StealthDetectionParams Default =>
            new StealthDetectionParams(DefaultMaxSignature, DefaultDistanceFalloff, DefaultSensorGain,
                DefaultBreakWeight, DefaultCumulativeWeight);

        public const float DefaultMaxSignature = 1f;
        public const float DefaultDistanceFalloff = 1f;
        public const float DefaultSensorGain = 1f;
        public const float DefaultBreakWeight = 0.5f;
        public const float DefaultCumulativeWeight = 0.5f;
    }

    /// <summary>
    /// ステルスと探知（隠密艦の被探知と発見）の純ロジック（#隠密艦）。
    /// ステルス艦（低被探知性艦）と探知側の駆け引きを扱う：被探知性（シグネチャ）の低減、
    /// 探知側のセンサー感度、距離による探知確率、複数センサー帯域（光学/熱/電波）での探知、
    /// ステルスの破れ（発砲・機動による自己露呈）、探知の積み重ね（長く見れば見つかる）。
    /// 決定論（乱数は roll 引数のみ）。入力は全て clamp。実効値パターン。test-first。
    /// </summary>
    public static class StealthDetectionRules
    {
        /// <summary>基本シグネチャ×(1-ステルス形状)×(1-放射管理)で被探知性（既定Params）。</summary>
        public static float Signature(float baseSignature, float stealthShaping, float emissionControl)
            => Signature(baseSignature, stealthShaping, emissionControl, StealthDetectionParams.Default);

        /// <summary>
        /// 被探知性（シグネチャ）。基本シグネチャをステルス形状と放射管理で乗算的に低減する。
        /// どちらも1（完璧）なら0（探知不能）へ近づく。0..maxSignature に clamp。
        /// </summary>
        public static float Signature(float baseSignature, float stealthShaping, float emissionControl,
            StealthDetectionParams p)
        {
            float bs = Mathf.Clamp01(baseSignature);
            float shaping = Mathf.Clamp01(stealthShaping);
            float emission = Mathf.Clamp01(emissionControl);
            float sig = bs * (1f - shaping) * (1f - emission);
            return Mathf.Clamp(sig, 0f, p.maxSignature);
        }

        /// <summary>シグネチャ×感度/(1+距離)で探知確率（既定Params）。</summary>
        public static float DetectionProbability(float signature, float sensorSensitivity, float distance)
            => DetectionProbability(signature, sensorSensitivity, distance, StealthDetectionParams.Default);

        /// <summary>
        /// 探知確率。被探知性が高く・センサー感度が高く・距離が近いほど高い。
        /// `prob = clamp01(signature × sensorSensitivity × sensorGain / (1 + distance × distanceFalloff))`。
        /// </summary>
        public static float DetectionProbability(float signature, float sensorSensitivity, float distance,
            StealthDetectionParams p)
        {
            float sig = Mathf.Clamp01(signature);
            float sens = Mathf.Clamp01(sensorSensitivity);
            float dist = Mathf.Max(0f, distance);
            float denom = 1f + dist * p.distanceFalloff;
            float prob = sig * sens * p.sensorGain / denom;
            return Mathf.Clamp01(prob);
        }

        /// <summary>複数帯域の独立探知を合成（既定Params）。</summary>
        public static float MultiBandDetection(float opticalDetect, float thermalDetect, float radarDetect)
            => MultiBandDetection(opticalDetect, thermalDetect, radarDetect, StealthDetectionParams.Default);

        /// <summary>
        /// 複数センサー帯域（光学/熱/電波）の独立探知を合成。どれか1つでも捉えれば見つかる
        /// ＝補集合の積（全て見逃す確率を1から引く）。`1 - (1-o)(1-t)(1-r)`。0..1。
        /// </summary>
        public static float MultiBandDetection(float opticalDetect, float thermalDetect, float radarDetect,
            StealthDetectionParams p)
        {
            float o = Mathf.Clamp01(opticalDetect);
            float t = Mathf.Clamp01(thermalDetect);
            float r = Mathf.Clamp01(radarDetect);
            float missAll = (1f - o) * (1f - t) * (1f - r);
            return Mathf.Clamp01(1f - missAll);
        }

        /// <summary>発砲×急機動でステルスの一時的破れ（既定Params）。</summary>
        public static float StealthBreak(float weaponsFire, float hardManeuver)
            => StealthBreak(weaponsFire, hardManeuver, StealthDetectionParams.Default);

        /// <summary>
        /// ステルスの一時的破れ（自己露呈）。発砲と急機動はそれぞれ露呈を増やす。
        /// 補集合の積で合成（どちらかが露呈させれば破れる）＝`1 - (1-fire·w)(1-maneuver·w)`。0..1。
        /// </summary>
        public static float StealthBreak(float weaponsFire, float hardManeuver, StealthDetectionParams p)
        {
            float fire = Mathf.Clamp01(weaponsFire) * p.breakWeight;
            float maneuver = Mathf.Clamp01(hardManeuver) * p.breakWeight;
            float stay = (1f - fire) * (1f - maneuver);
            return Mathf.Clamp01(1f - stay);
        }

        /// <summary>瞬間探知×観察時間で探知の積み重ね（既定Params）。</summary>
        public static float CumulativeDetection(float instantDetection, float observationTime)
            => CumulativeDetection(instantDetection, observationTime, StealthDetectionParams.Default);

        /// <summary>
        /// 探知の積み重ね（長く見れば見つかる）。瞬間探知確率を観察時間ぶん積み上げる
        /// ＝毎単位時間で見逃す確率の累乗の補集合。`1 - (1 - instant·w)^time`（Pow使用）。0..1。
        /// </summary>
        public static float CumulativeDetection(float instantDetection, float observationTime,
            StealthDetectionParams p)
        {
            float instant = Mathf.Clamp01(instantDetection) * p.cumulativeWeight;
            float time = Mathf.Max(0f, observationTime);
            float missOnce = Mathf.Clamp01(1f - instant);
            float missAll = Mathf.Pow(missOnce, time);
            return Mathf.Clamp01(1f - missAll);
        }

        /// <summary>(1-シグネチャ)×(1-敵警戒)で隠密艦の奇襲成立（既定Params）。</summary>
        public static float AmbushSurprise(float signature, float enemyAlertness)
            => AmbushSurprise(signature, enemyAlertness, StealthDetectionParams.Default);

        /// <summary>
        /// 隠密艦の奇襲成立度。被探知性が低く・敵の警戒が低いほど成立しやすい。
        /// `(1 - signature) × (1 - enemyAlertness)`。0..1。
        /// </summary>
        public static float AmbushSurprise(float signature, float enemyAlertness, StealthDetectionParams p)
        {
            float sig = Mathf.Clamp01(signature);
            float alert = Mathf.Clamp01(enemyAlertness);
            return Mathf.Clamp01((1f - sig) * (1f - alert));
        }

        /// <summary>センサー世代/(センサー+ステルス世代)で対ステルス技術の優位（既定Params）。</summary>
        public static float CounterStealthTech(float sensorAdvancement, float stealthGeneration)
            => CounterStealthTech(sensorAdvancement, stealthGeneration, StealthDetectionParams.Default);

        /// <summary>
        /// 対ステルス技術の優位。センサー世代がステルス世代より進んでいれば1へ近づく（互角で0.5）。
        /// `sensor / (sensor + stealth)`。両者0なら互角の0.5。0..1。
        /// </summary>
        public static float CounterStealthTech(float sensorAdvancement, float stealthGeneration,
            StealthDetectionParams p)
        {
            float sensor = Mathf.Clamp01(sensorAdvancement);
            float stealth = Mathf.Clamp01(stealthGeneration);
            float sum = sensor + stealth;
            if (sum <= 0f) return 0.5f; // 双方ゼロ＝互角
            return Mathf.Clamp01(sensor / sum);
        }

        /// <summary>
        /// 探知されたか（決定論）。探知確率を 0..1 の roll と比較する。
        /// `roll &lt; detectionProbability` で探知成立。roll 注入で再現可能。
        /// </summary>
        public static bool IsDetected(float detectionProbability, float roll)
        {
            float prob = Mathf.Clamp01(detectionProbability);
            float r = Mathf.Clamp01(roll);
            return r < prob;
        }
    }
}
