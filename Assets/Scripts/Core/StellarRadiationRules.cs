using UnityEngine;

namespace Ginei
{
    /// <summary>恒星放射環境の調整係数。</summary>
    public readonly struct StellarRadiationParams
    {
        /// <summary>放射線強度を抑える距離減衰の基準距離（小さいほど近接で強烈）。</summary>
        public readonly float referenceDistance;
        /// <summary>距離の二乗近似に最低限を与えて発散を防ぐ下限距離。</summary>
        public readonly float minDistance;
        /// <summary>電子機器障害の感度（放射線×非防護にこれを掛ける）。</summary>
        public readonly float disruptionGain;
        /// <summary>センサーノイズの感度（放射線強度にこれを掛ける）。</summary>
        public readonly float sensorNoiseGain;
        /// <summary>乗員被曝の蓄積係数（放射線×非防護×時間にこれを掛ける）。</summary>
        public readonly float exposureRate;
        /// <summary>逆光戦術の最大利得（恒星を真後ろにしたときの眩惑量）。</summary>
        public readonly float blindingGain;
        /// <summary>作戦行動不能とみなす性能低下の既定閾値。</summary>
        public readonly float operableThreshold;

        public StellarRadiationParams(float referenceDistance, float minDistance, float disruptionGain,
            float sensorNoiseGain, float exposureRate, float blindingGain, float operableThreshold)
        {
            this.referenceDistance = Mathf.Max(0.0001f, referenceDistance);
            this.minDistance = Mathf.Max(0.0001f, minDistance);
            this.disruptionGain = Mathf.Clamp01(disruptionGain);
            this.sensorNoiseGain = Mathf.Clamp01(sensorNoiseGain);
            this.exposureRate = Mathf.Max(0f, exposureRate);
            this.blindingGain = Mathf.Clamp01(blindingGain);
            this.operableThreshold = Mathf.Clamp01(operableThreshold);
        }

        /// <summary>既定＝基準距離1・下限0.5・障害感度1・ノイズ感度0.8・被曝率0.1・逆光利得0.5・不能閾値0.7。</summary>
        public static StellarRadiationParams Default
            => new StellarRadiationParams(1f, 0.5f, 1f, 0.8f, 0.1f, 0.5f, 0.7f);
    }

    /// <summary>
    /// 恒星放射環境（高放射線宙域）の純ロジック。恒星近傍での会戦は強烈な放射線で電子機器が障害を起こし、
    /// 遮蔽が防護を与え、センサー・通信はノイズに沈み、乗員は被曝し、艦の総合性能が落ちる。
    /// 逆に恒星を背にすれば敵センサーを逆光で眩ます戦術利得が得られる（逆光戦術）。
    /// 放射線強度は距離の二乗近似で減衰（Mathf.Pow で近似・対数/指数は使わない）。
    /// 乱数なし・決定論・入力 clamp。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class StellarRadiationRules
    {
        /// <summary>
        /// 放射線強度（0..1）＝恒星出力÷(距離/基準距離)²。近いほど・出力が大きいほど強い。
        /// 距離は二乗近似（Pow2）。下限距離で発散を防ぐ。出力0なら0。
        /// </summary>
        public static float RadiationIntensity(float stellarOutput, float distance, StellarRadiationParams p)
        {
            float output = Mathf.Clamp01(stellarOutput);
            float d = Mathf.Max(p.minDistance, Mathf.Max(0f, distance));
            float ratio = d / p.referenceDistance;
            float falloff = Mathf.Max(0.0001f, Mathf.Pow(ratio, 2f));
            return Mathf.Clamp01(output / falloff);
        }

        public static float RadiationIntensity(float stellarOutput, float distance)
            => RadiationIntensity(stellarOutput, distance, StellarRadiationParams.Default);

        /// <summary>
        /// 遮蔽の防護率（0..1）＝遮蔽÷(遮蔽＋放射線)。放射線が強いほど同じ遮蔽でも防護が薄れる。
        /// 放射線0なら（遮蔽が少しでもあれば）完全防護。遮蔽0なら防護0。
        /// </summary>
        public static float ShieldingEffectiveness(float hullShielding, float radiationIntensity, StellarRadiationParams p)
        {
            float shield = Mathf.Clamp01(hullShielding);
            float rad = Mathf.Clamp01(radiationIntensity);
            float denom = shield + rad;
            if (denom <= 0.0001f) return 0f;
            return Mathf.Clamp01(shield / denom);
        }

        public static float ShieldingEffectiveness(float hullShielding, float radiationIntensity)
            => ShieldingEffectiveness(hullShielding, radiationIntensity, StellarRadiationParams.Default);

        /// <summary>
        /// 電子機器障害（0..1）＝放射線×(1−防護)×感度。遮蔽が効くほど障害は減る。
        /// </summary>
        public static float ElectronicDisruption(float radiationIntensity, float shieldingEffectiveness, StellarRadiationParams p)
        {
            float rad = Mathf.Clamp01(radiationIntensity);
            float unshielded = 1f - Mathf.Clamp01(shieldingEffectiveness);
            return Mathf.Clamp01(rad * unshielded * p.disruptionGain);
        }

        public static float ElectronicDisruption(float radiationIntensity, float shieldingEffectiveness)
            => ElectronicDisruption(radiationIntensity, shieldingEffectiveness, StellarRadiationParams.Default);

        /// <summary>
        /// センサーノイズ（0..1）＝放射線強度×ノイズ感度。遮蔽では消えない放射ノイズ（探知劣化）。
        /// </summary>
        public static float SensorNoise(float radiationIntensity, StellarRadiationParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(radiationIntensity) * p.sensorNoiseGain);
        }

        public static float SensorNoise(float radiationIntensity)
            => SensorNoise(radiationIntensity, StellarRadiationParams.Default);

        /// <summary>
        /// 乗員被曝（0..1）＝放射線×(1−防護)×時間×蓄積率。滞在が長いほど積み上がる。
        /// </summary>
        public static float CrewExposure(float radiationIntensity, float shieldingEffectiveness, float exposureTime, StellarRadiationParams p)
        {
            float rad = Mathf.Clamp01(radiationIntensity);
            float unshielded = 1f - Mathf.Clamp01(shieldingEffectiveness);
            float t = Mathf.Max(0f, exposureTime);
            return Mathf.Clamp01(rad * unshielded * t * p.exposureRate);
        }

        public static float CrewExposure(float radiationIntensity, float shieldingEffectiveness, float exposureTime)
            => CrewExposure(radiationIntensity, shieldingEffectiveness, exposureTime, StellarRadiationParams.Default);

        /// <summary>
        /// 艦の総合性能低下（0..1）＝機器障害と乗員被曝の合成。どちらか健全でも片方が崩れれば落ちる
        /// ＝両者の劣化を 1−(1−障害)(1−被曝) で合成（独立故障の重ね合わせ）。
        /// </summary>
        public static float PerformanceDegradation(float electronicDisruption, float crewExposure, StellarRadiationParams p)
        {
            float disrupt = Mathf.Clamp01(electronicDisruption);
            float exposure = Mathf.Clamp01(crewExposure);
            return Mathf.Clamp01(1f - (1f - disrupt) * (1f - exposure));
        }

        public static float PerformanceDegradation(float electronicDisruption, float crewExposure)
            => PerformanceDegradation(electronicDisruption, crewExposure, StellarRadiationParams.Default);

        /// <summary>
        /// 逆光戦術の利得（0..1）＝恒星を背にした角度。targetSunAngle は「敵から見た恒星と自艦の角度」を
        /// 正規化した cos 値（1＝恒星が自艦の真後ろ＝敵は逆光・−1＝自艦が逆光側）。
        /// 後方に恒星があるほど（cos が大）敵センサーを眩ます。利得＝(cos+1)/2×逆光利得。
        /// </summary>
        public static float SunBlindingTactic(float targetSunAngle, StellarRadiationParams p)
        {
            float cos = Mathf.Clamp(targetSunAngle, -1f, 1f);
            float behind = (cos + 1f) * 0.5f; // -1..1 → 0..1
            return Mathf.Clamp01(behind * p.blindingGain);
        }

        public static float SunBlindingTactic(float targetSunAngle)
            => SunBlindingTactic(targetSunAngle, StellarRadiationParams.Default);

        /// <summary>放射線下で作戦行動できるか＝性能低下が閾値未満なら true。</summary>
        public static bool IsOperableInRadiation(float performanceDegradation, float threshold)
        {
            return Mathf.Clamp01(performanceDegradation) < Mathf.Clamp01(threshold);
        }

        public static bool IsOperableInRadiation(float performanceDegradation)
            => IsOperableInRadiation(performanceDegradation, StellarRadiationParams.Default.operableThreshold);
    }
}
