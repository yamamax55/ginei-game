using UnityEngine;

namespace Ginei
{
    /// <summary>電波測位（信号源の方位探知・三角測量）の調整係数。</summary>
    public readonly struct SignalTriangulationParams
    {
        /// <summary>方位精度の利得・信号への寄与（大きいほど弱信号でも方位が締まる）。</summary>
        public readonly float accuracyGain;
        /// <summary>三角測量で1受信点増えるごとの質の伸び（受信点数の効き）。</summary>
        public readonly float receiverWeight;
        /// <summary>幾何（配置の広がり）が三角測量の質に効く度合い。</summary>
        public readonly float geometryWeight;
        /// <summary>位置標定誤差の距離スケール（距離1あたりの誤差係数）。</summary>
        public readonly float errorDistanceScale;
        /// <summary>無線封止（EMCON）が突破を妨げる重み（大きいほど突破しにくい）。</summary>
        public readonly float emconWeight;
        /// <summary>放射源識別の鋭さ（特性×ライブラリの効きを締める指数）。</summary>
        public readonly float identificationSharpness;

        public SignalTriangulationParams(float accuracyGain, float receiverWeight, float geometryWeight,
            float errorDistanceScale, float emconWeight, float identificationSharpness)
        {
            this.accuracyGain = Mathf.Clamp(accuracyGain, 0.0001f, 8f);
            this.receiverWeight = Mathf.Clamp(receiverWeight, 0.0001f, 4f);
            this.geometryWeight = Mathf.Clamp01(geometryWeight);
            this.errorDistanceScale = Mathf.Clamp(errorDistanceScale, 0.0001f, 4f);
            this.emconWeight = Mathf.Clamp(emconWeight, 0.0001f, 4f);
            this.identificationSharpness = Mathf.Clamp(identificationSharpness, 0.0001f, 4f);
        }

        /// <summary>既定＝利得2・受信点重み0.5・幾何重み0.6・距離誤差1・封止重み1・識別鋭さ1。</summary>
        public static SignalTriangulationParams Default
            => new SignalTriangulationParams(2f, 0.5f, 0.6f, 1f, 1f, 1f);
    }

    /// <summary>
    /// 電波測位の純ロジック＝敵の通信・レーダー放射を逆探知して位置を標定する（方位探知・三角測量）。
    /// 一受信点では方位線しか引けず、複数の受信点が広く散って初めて交点＝位置が締まる（基線幾何）。
    /// 標定誤差は方位精度・測量質が低いほど、また距離が遠いほど膨らむ。敵の無線封止（EMCON）には
    /// 自感度で対抗し、放射源は信号特性とライブラリ照合で識別する。短い放射は捉えにくく（即時性）、
    /// 射撃に使える標定は誤差が小さく即時性が高いときだけ成立する。
    /// 乱数なし・決定論・入力clamp。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SignalTriangulationRules
    {
        /// <summary>
        /// 方位精度（0..1）＝アンテナ利得×信号強度で締まる。弱信号は方位がぶれる。
        /// gain·strength·accuracyGain を 0..1 へクランプ。
        /// </summary>
        public static float BearingAccuracy(float antennaGain, float signalStrength, SignalTriangulationParams p)
        {
            float g = Mathf.Clamp01(antennaGain);
            float s = Mathf.Clamp01(signalStrength);
            return Mathf.Clamp01(g * s * p.accuracyGain);
        }

        public static float BearingAccuracy(float antennaGain, float signalStrength)
            => BearingAccuracy(antennaGain, signalStrength, SignalTriangulationParams.Default);

        /// <summary>
        /// 三角測量の質（0..1）。受信点が2点未満なら方位線一本＝交点を結べず0。
        /// 2点以上で「受信点数の効き×基線幾何」で締まる（広く散ると精度up）。
        /// </summary>
        public static float TriangulationQuality(int receiverCount, float baselineGeometry, SignalTriangulationParams p)
        {
            if (receiverCount < 2) return 0f;
            float geo = Mathf.Clamp01(baselineGeometry);
            // 受信点の冗長度（2点で0、増えるほど飽和的に伸びる）
            float countTerm = Mathf.Clamp01((receiverCount - 1) * p.receiverWeight);
            // 幾何が悪ければ何点あっても締まらない（重み付き平均で幾何を必須化）
            float quality = countTerm * (1f - p.geometryWeight) + countTerm * geo * p.geometryWeight;
            return Mathf.Clamp01(quality);
        }

        public static float TriangulationQuality(int receiverCount, float baselineGeometry)
            => TriangulationQuality(receiverCount, baselineGeometry, SignalTriangulationParams.Default);

        /// <summary>
        /// 位置標定誤差（0..1）＝(1-方位精度)×(1-測量質)×距離スケール。
        /// 精度・質が高いほど、近いほど誤差は小さい。距離は0..1正規化を想定。
        /// </summary>
        public static float PositionError(float bearingAccuracy, float triangulationQuality, float distance, SignalTriangulationParams p)
        {
            float acc = Mathf.Clamp01(bearingAccuracy);
            float q = Mathf.Clamp01(triangulationQuality);
            float d = Mathf.Clamp01(distance);
            float error = (1f - acc) * (1f - q) * d * p.errorDistanceScale;
            return Mathf.Clamp01(error);
        }

        public static float PositionError(float bearingAccuracy, float triangulationQuality, float distance)
            => PositionError(bearingAccuracy, triangulationQuality, distance, SignalTriangulationParams.Default);

        /// <summary>
        /// 無線封止（EMCON）突破度（0..1）＝自感度/(自感度+敵の放射規律×封止重み)。
        /// 敵が完全に黙れば（規律1）感度頼みで突破は鈍り、敵が放射するほど突破は容易。
        /// </summary>
        public static float EmconPenetration(float enemyEmissionDiscipline, float ownSensitivity, SignalTriangulationParams p)
        {
            float disc = Mathf.Clamp01(enemyEmissionDiscipline);
            float sens = Mathf.Clamp01(ownSensitivity);
            float denom = sens + disc * p.emconWeight;
            if (denom <= 0.0001f) return 0f;
            return Mathf.Clamp01(sens / denom);
        }

        public static float EmconPenetration(float enemyEmissionDiscipline, float ownSensitivity)
            => EmconPenetration(enemyEmissionDiscipline, ownSensitivity, SignalTriangulationParams.Default);

        /// <summary>
        /// 放射源の識別度（0..1）＝信号特性×信号ライブラリを鋭さ指数で締める。
        /// 特性が乏しい／ライブラリに無ければ識別は伸びない（Pow で立ち上がりを調整）。
        /// </summary>
        public static float EmitterIdentification(float signalCharacteristics, float signalLibrary, SignalTriangulationParams p)
        {
            float ch = Mathf.Clamp01(signalCharacteristics);
            float lib = Mathf.Clamp01(signalLibrary);
            float raw = ch * lib;
            return Mathf.Clamp01(Mathf.Pow(raw, p.identificationSharpness));
        }

        public static float EmitterIdentification(float signalCharacteristics, float signalLibrary)
            => EmitterIdentification(signalCharacteristics, signalLibrary, SignalTriangulationParams.Default);

        /// <summary>
        /// 測位の即時性（0..1）＝処理速度×信号持続。短い放射（持続が小）は処理が間に合わず捉えにくい。
        /// </summary>
        public static float FixTimeliness(float processingSpeed, float signalDuration, SignalTriangulationParams p)
        {
            float speed = Mathf.Clamp01(processingSpeed);
            float dur = Mathf.Clamp01(signalDuration);
            return Mathf.Clamp01(speed * dur);
        }

        public static float FixTimeliness(float processingSpeed, float signalDuration)
            => FixTimeliness(processingSpeed, signalDuration, SignalTriangulationParams.Default);

        /// <summary>
        /// 射撃に使える標定の質（0..1）＝(1-位置標定誤差)×即時性。
        /// 誤差が小さく即時性が高いときだけ高い＝「正確かつ間に合う」標定。
        /// </summary>
        public static float TargetingQuality(float positionError, float fixTimeliness, SignalTriangulationParams p)
        {
            float err = Mathf.Clamp01(positionError);
            float timely = Mathf.Clamp01(fixTimeliness);
            return Mathf.Clamp01((1f - err) * timely);
        }

        public static float TargetingQuality(float positionError, float fixTimeliness)
            => TargetingQuality(positionError, fixTimeliness, SignalTriangulationParams.Default);

        /// <summary>標定が攻撃に使えるか＝標定の質が閾値以上。</summary>
        public static bool IsFixActionable(float targetingQuality, float threshold)
        {
            return Mathf.Clamp01(targetingQuality) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値0.5で攻撃に使えるかを判定。</summary>
        public static bool IsFixActionable(float targetingQuality)
            => IsFixActionable(targetingQuality, 0.5f);
    }
}
