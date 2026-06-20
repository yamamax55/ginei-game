using UnityEngine;

namespace Ginei
{
    /// <summary>電子妨害戦（ジャミング）の調整係数。マジックナンバー禁止＝ここに集約。全フィールド ctor で Clamp。</summary>
    public readonly struct JammingWarfareParams
    {
        /// <summary>妨害出力を敵センサー劣化へ写すときの基準倍率（0..1）。</summary>
        public readonly float degradeScale;
        /// <summary>センサー劣化を敵命中率低下へ写す重み（0..1）。</summary>
        public readonly float accuracyWeight;
        /// <summary>妨害出力を誘導兵器の撹乱へ写す重み（0..1）。</summary>
        public readonly float guidanceWeight;
        /// <summary>妨害出力を敵通信・連携の混乱へ写す重み（0..1）。</summary>
        public readonly float commsWeight;
        /// <summary>妨害出力を自位置の暴露へ写す倍率（0..1・強く妨害するほど己の電波が目立つ）。</summary>
        public readonly float exposureScale;
        /// <summary>距離減衰の強さ（0..1・大きいほど遠方で妨害出力が急減）。代数式の減衰係数。</summary>
        public readonly float distanceFalloff;
        /// <summary>妨害が効いていると判定するセンサー劣化の既定閾値（0..1）。</summary>
        public readonly float effectiveThreshold;

        public JammingWarfareParams(float degradeScale, float accuracyWeight, float guidanceWeight,
                                    float commsWeight, float exposureScale, float distanceFalloff,
                                    float effectiveThreshold)
        {
            this.degradeScale = Mathf.Clamp01(degradeScale);
            this.accuracyWeight = Mathf.Clamp01(accuracyWeight);
            this.guidanceWeight = Mathf.Clamp01(guidanceWeight);
            this.commsWeight = Mathf.Clamp01(commsWeight);
            this.exposureScale = Mathf.Clamp01(exposureScale);
            this.distanceFalloff = Mathf.Clamp01(distanceFalloff);
            this.effectiveThreshold = Mathf.Clamp01(effectiveThreshold);
        }

        /// <summary>
        /// 既定＝劣化倍率1.0/命中重み1.0/誘導重み1.0/通信重み1.0/暴露倍率1.0/距離減衰1.0/効力閾値0.3。
        /// </summary>
        public static JammingWarfareParams Default =>
            new JammingWarfareParams(1f, 1f, 1f, 1f, 1f, 1f, 0.3f);
    }

    /// <summary>
    /// 電子妨害戦（ジャミング）の純ロジック。能動的に電波を放射して敵のレーダー・通信・誘導を妨げ、
    /// 命中率・指揮連携・誘導兵器を低下させる。だが強く妨害するほど自分の位置が暴露され、敵の対抗妨害
    /// （カウンタージャミング）と綱引きになる＝撃てば己も目立つ。
    /// <see cref="SignalIntelligenceRules"/>（受動的な通信傍受・暗号解読）とは別＝こちらは能動的な電波妨害。
    /// <see cref="FogOfWarRules"/>（物理的な可視性・斥候の目視）とも別＝電子戦レイヤー。
    /// 盤面非依存の plain 引数。値は徹底して clamp（符号付きは −1..1）・乱数なし決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。各メソッドは Params 明示版＋Default 委譲版を持つ。
    /// </summary>
    public static class JammingWarfareRules
    {
        /// <summary>
        /// 妨害出力 0..1：放射源の強さ emitterStrength を距離で減衰させた実効出力。proximity は近接度
        /// （1=至近・0=遠方）。distanceFalloff が距離減衰の強さ。代数式（Log/Exp 不使用）：
        /// emitter × proximity / (proximity + distanceFalloff × (1 − proximity))。
        /// distanceFalloff=0 なら減衰なし（出力一定）、=1 なら proximity に比例して直線減衰。
        /// </summary>
        public static float JammingPower(float emitterStrength, float proximity, JammingWarfareParams p)
        {
            float emitter = Mathf.Clamp01(emitterStrength);
            float prox = Mathf.Clamp01(proximity);
            float denom = prox + p.distanceFalloff * (1f - prox);
            if (denom <= 0f) return 0f; // proximity=0 かつ falloff=0 は遠方で出力ゼロ扱い
            float falloff = prox / denom;
            return Mathf.Clamp01(emitter * falloff);
        }

        public static float JammingPower(float emitterStrength, float proximity)
            => JammingPower(emitterStrength, proximity, JammingWarfareParams.Default);

        /// <summary>
        /// 敵センサー劣化 0..1：妨害出力 jammingPower が敵レーダー・センサーをどれだけ盲目化するか。
        /// 敵の対妨害装備 enemySensorHardening（耐妨害強化）が高いほど抗える。
        /// jammingPower × (1 − enemySensorHardening) × degradeScale。
        /// </summary>
        public static float SensorDegradation(float jammingPower, float enemySensorHardening, JammingWarfareParams p)
        {
            float jp = Mathf.Clamp01(jammingPower);
            float hard = Mathf.Clamp01(enemySensorHardening);
            return Mathf.Clamp01(jp * (1f - hard) * p.degradeScale);
        }

        public static float SensorDegradation(float jammingPower, float enemySensorHardening)
            => SensorDegradation(jammingPower, enemySensorHardening, JammingWarfareParams.Default);

        /// <summary>
        /// 敵命中率低下 0..1：センサー劣化 sensorDegradation から導く敵の照準精度の低下。
        /// sensorDegradation × accuracyWeight。盲目化した敵は当てられない。
        /// </summary>
        public static float AccuracyReduction(float sensorDegradation, JammingWarfareParams p)
        {
            float degr = Mathf.Clamp01(sensorDegradation);
            return Mathf.Clamp01(degr * p.accuracyWeight);
        }

        public static float AccuracyReduction(float sensorDegradation)
            => AccuracyReduction(sensorDegradation, JammingWarfareParams.Default);

        /// <summary>
        /// 誘導兵器の撹乱 0..1：妨害出力 jammingPower が敵のミサイル誘導をどれだけ逸らすか。
        /// シーカー性能 missileSeekerQuality（耐妨害シーカー）が高いほど食らいつき逸れにくい。
        /// jammingPower × (1 − missileSeekerQuality) × guidanceWeight。
        /// </summary>
        public static float GuidanceDisruption(float jammingPower, float missileSeekerQuality, JammingWarfareParams p)
        {
            float jp = Mathf.Clamp01(jammingPower);
            float seeker = Mathf.Clamp01(missileSeekerQuality);
            return Mathf.Clamp01(jp * (1f - seeker) * p.guidanceWeight);
        }

        public static float GuidanceDisruption(float jammingPower, float missileSeekerQuality)
            => GuidanceDisruption(jammingPower, missileSeekerQuality, JammingWarfareParams.Default);

        /// <summary>
        /// 敵通信・連携の混乱 0..1：妨害出力 jammingPower が敵の指揮通信網をどれだけ寸断するか。
        /// 通信冗長性 enemyCommsRedundancy（多重化・耐妨害リンク）が高いほど混乱しにくい。
        /// jammingPower × (1 − enemyCommsRedundancy) × commsWeight。
        /// </summary>
        public static float CommsDisruption(float jammingPower, float enemyCommsRedundancy, JammingWarfareParams p)
        {
            float jp = Mathf.Clamp01(jammingPower);
            float redundancy = Mathf.Clamp01(enemyCommsRedundancy);
            return Mathf.Clamp01(jp * (1f - redundancy) * p.commsWeight);
        }

        public static float CommsDisruption(float jammingPower, float enemyCommsRedundancy)
            => CommsDisruption(jammingPower, enemyCommsRedundancy, JammingWarfareParams.Default);

        /// <summary>
        /// 自位置の暴露 0..1：妨害出力 jammingPower が己の電波放射源として敵に検知される度合い。
        /// 強く妨害するほど目立つ＝撃てば己も暴れる。jammingPower × exposureScale。
        /// </summary>
        public static float EmissionExposure(float jammingPower, JammingWarfareParams p)
        {
            float jp = Mathf.Clamp01(jammingPower);
            return Mathf.Clamp01(jp * p.exposureScale);
        }

        public static float EmissionExposure(float jammingPower)
            => EmissionExposure(jammingPower, JammingWarfareParams.Default);

        /// <summary>
        /// 妨害合戦の優劣 −1..1：自軍妨害 ownJamming と敵の対抗妨害 enemyCounterJamming の綱引き。
        /// 正＝自軍が電子戦で優勢／負＝敵に圧されている／0＝拮抗。ownJamming − enemyCounterJamming を −1..1 にクランプ。
        /// Params 非依存（純粋な差分）。
        /// </summary>
        public static float CounterJamming(float ownJamming, float enemyCounterJamming)
        {
            float own = Mathf.Clamp01(ownJamming);
            float enemy = Mathf.Clamp01(enemyCounterJamming);
            return Mathf.Clamp(own - enemy, -1f, 1f);
        }

        /// <summary>
        /// 妨害が効いている判定：センサー劣化 sensorDegradation が threshold 以上なら妨害有効（true）。
        /// </summary>
        public static bool IsJammingEffective(float sensorDegradation, float threshold)
        {
            return Mathf.Clamp01(sensorDegradation) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値（<see cref="JammingWarfareParams.effectiveThreshold"/>）での妨害有効判定。</summary>
        public static bool IsJammingEffective(float sensorDegradation)
            => IsJammingEffective(sensorDegradation, JammingWarfareParams.Default.effectiveThreshold);
    }
}
