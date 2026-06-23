using UnityEngine;

namespace Ginei
{
    /// <summary>通信兵科（通信の設営・運用）の調整係数。</summary>
    public readonly struct SignalCorpsParams
    {
        /// <summary>帯域容量の上限スケール（回線×インフラが満点のときの最大帯域）。</summary>
        public readonly float maxBandwidth;
        /// <summary>距離1あたりの遅延寄与（0..1 の遅延へ加算）。</summary>
        public readonly float latencyPerDistance;
        /// <summary>中継1ホップあたりの遅延寄与。</summary>
        public readonly float latencyPerHop;
        /// <summary>輻輳の感度（通信量/帯域が1を超えた超過ぶんへの倍率）。</summary>
        public readonly float congestionSensitivity;
        /// <summary>信頼性に占める代替手段（予備系統）の寄与比（残りは回線確立そのもの）。</summary>
        public readonly float fallbackWeight;
        /// <summary>通信の質を正規化するための基準スループット（これで割って0..1化）。</summary>
        public readonly float qualityThroughputRef;

        public SignalCorpsParams(float maxBandwidth, float latencyPerDistance, float latencyPerHop,
                                 float congestionSensitivity, float fallbackWeight, float qualityThroughputRef)
        {
            this.maxBandwidth = Mathf.Max(0.0001f, maxBandwidth);
            this.latencyPerDistance = Mathf.Max(0f, latencyPerDistance);
            this.latencyPerHop = Mathf.Max(0f, latencyPerHop);
            this.congestionSensitivity = Mathf.Max(0f, congestionSensitivity);
            this.fallbackWeight = Mathf.Clamp01(fallbackWeight);
            this.qualityThroughputRef = Mathf.Max(0.0001f, qualityThroughputRef);
        }

        /// <summary>既定＝最大帯域100・遅延0.01/距離・遅延0.05/ホップ・輻輳感度1・代替寄与0.4・正規化基準100。</summary>
        public static SignalCorpsParams Default => new SignalCorpsParams(100f, 0.01f, 0.05f, 1f, 0.4f, 100f);
    }

    /// <summary>
    /// 軍の通信兵科の純ロジック。回線の確立（機材×技量）→帯域容量（回線×インフラ）→遅延（距離×中継）
    /// →通信量による輻輳→実効スループット→代替手段（予備系統×即応）→信頼性→総合的な通信の質。
    /// 機材が良く腕が立てば回線は太く確立し、距離と中継で遅れ、通信量が帯域を超えれば輻輳し、
    /// 途絶しても予備系統と即応で信頼性を保つ＝「通信の確立と維持」を係数化する。
    /// 乱数なし・決定論。実効値パターン（入力は非破壊・全て clamp）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SignalCorpsRules
    {
        /// <summary>回線確立（0..1）＝機材の質×通信兵の技量の幾何平均。どちらか欠けると確立しきれない。</summary>
        public static float LinkEstablishment(float equipmentQuality, float operatorSkill, SignalCorpsParams p)
        {
            float eq = Mathf.Clamp01(equipmentQuality);
            float sk = Mathf.Clamp01(operatorSkill);
            return Mathf.Clamp01(Mathf.Sqrt(eq * sk));
        }

        public static float LinkEstablishment(float equipmentQuality, float operatorSkill)
            => LinkEstablishment(equipmentQuality, operatorSkill, SignalCorpsParams.Default);

        /// <summary>帯域容量＝回線確立×インフラ×最大帯域。回線が太くインフラが整うほど大容量。</summary>
        public static float Bandwidth(float linkEstablishment, float infrastructure, SignalCorpsParams p)
        {
            float link = Mathf.Clamp01(linkEstablishment);
            float infra = Mathf.Clamp01(infrastructure);
            return Mathf.Max(0f, link * infra * p.maxBandwidth);
        }

        public static float Bandwidth(float linkEstablishment, float infrastructure)
            => Bandwidth(linkEstablishment, infrastructure, SignalCorpsParams.Default);

        /// <summary>通信遅延（0..1）＝距離×中継ホップ数で累積（クランプ）。近接・直結ほど低遅延。</summary>
        public static float Latency(float distance, float relayHops, SignalCorpsParams p)
        {
            float d = Mathf.Max(0f, distance);
            float hops = Mathf.Max(0f, relayHops);
            return Mathf.Clamp01(d * p.latencyPerDistance + hops * p.latencyPerHop);
        }

        public static float Latency(float distance, float relayHops)
            => Latency(distance, relayHops, SignalCorpsParams.Default);

        /// <summary>
        /// 輻輳係数（0..1）＝通信量/帯域が1を超えた超過ぶん×感度。帯域内なら輻輳0、溢れるほど詰まる。
        /// 帯域0は完全輻輳（1）。
        /// </summary>
        public static float CongestionFactor(float trafficVolume, float bandwidth, SignalCorpsParams p)
        {
            float traffic = Mathf.Max(0f, trafficVolume);
            float bw = Mathf.Max(0f, bandwidth);
            if (bw <= 0.0001f) return traffic > 0f ? 1f : 0f;
            float ratio = traffic / bw;
            float overload = Mathf.Max(0f, ratio - 1f);
            return Mathf.Clamp01(overload * p.congestionSensitivity);
        }

        public static float CongestionFactor(float trafficVolume, float bandwidth)
            => CongestionFactor(trafficVolume, bandwidth, SignalCorpsParams.Default);

        /// <summary>実効スループット＝帯域×(1−輻輳)。詰まるほど実効が落ちる。</summary>
        public static float ThroughputEffective(float bandwidth, float congestionFactor, SignalCorpsParams p)
        {
            float bw = Mathf.Max(0f, bandwidth);
            float cong = Mathf.Clamp01(congestionFactor);
            return Mathf.Max(0f, bw * (1f - cong));
        }

        public static float ThroughputEffective(float bandwidth, float congestionFactor)
            => ThroughputEffective(bandwidth, congestionFactor, SignalCorpsParams.Default);

        /// <summary>代替手段（0..1）＝予備系統×即応（通信兵の機転）の幾何平均。途絶時の生命線。</summary>
        public static float FallbackCapability(float backupSystems, float operatorImprovisation, SignalCorpsParams p)
        {
            float backup = Mathf.Clamp01(backupSystems);
            float improv = Mathf.Clamp01(operatorImprovisation);
            return Mathf.Clamp01(Mathf.Sqrt(backup * improv));
        }

        public static float FallbackCapability(float backupSystems, float operatorImprovisation)
            => FallbackCapability(backupSystems, operatorImprovisation, SignalCorpsParams.Default);

        /// <summary>
        /// 通信の信頼性（0..1）＝回線確立に代替手段を重み fallbackWeight で混ぜる。
        /// 回線が細くても予備系統が厚ければ信頼性を下支えする＝途絶しても維持できる。
        /// </summary>
        public static float Reliability(float linkEstablishment, float fallbackCapability, SignalCorpsParams p)
        {
            float link = Mathf.Clamp01(linkEstablishment);
            float fallback = Mathf.Clamp01(fallbackCapability);
            return Mathf.Clamp01(link * (1f - p.fallbackWeight) + fallback * p.fallbackWeight);
        }

        public static float Reliability(float linkEstablishment, float fallbackCapability)
            => Reliability(linkEstablishment, fallbackCapability, SignalCorpsParams.Default);

        /// <summary>
        /// 総合的な通信の質（0..1）＝正規化スループット×(1−遅延)×信頼性。
        /// 太く・速く・信頼できる三拍子で初めて高品質。どれか欠けると質は落ちる。
        /// </summary>
        public static float CommunicationQuality(float throughputEffective, float latency, float reliability, SignalCorpsParams p)
        {
            float thr = Mathf.Clamp01(Mathf.Max(0f, throughputEffective) / p.qualityThroughputRef);
            float lat = Mathf.Clamp01(latency);
            float rel = Mathf.Clamp01(reliability);
            return Mathf.Clamp01(thr * (1f - lat) * rel);
        }

        public static float CommunicationQuality(float throughputEffective, float latency, float reliability)
            => CommunicationQuality(throughputEffective, latency, reliability, SignalCorpsParams.Default);
    }
}
