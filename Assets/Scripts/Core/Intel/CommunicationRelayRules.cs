using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 通信中継網（リレー）の調整係数。到達範囲・信号維持・遅延・脆弱性・迂回・維持コストの重み。
    /// </summary>
    public readonly struct CommunicationRelayParams
    {
        /// <summary>到達範囲の基礎倍率（中継ノード数×配置間隔 に掛ける）。</summary>
        public readonly float coverageScale;
        /// <summary>1段あたり遅延の基礎倍率（中継数×遅延 に掛ける）。</summary>
        public readonly float latencyScale;
        /// <summary>脆弱性の基礎倍率（ノード露出×敵到達 に掛ける）。</summary>
        public readonly float vulnerabilityScale;
        /// <summary>迂回能力の基礎倍率（代替中継×網状度 に掛ける）。</summary>
        public readonly float reroutingScale;
        /// <summary>維持コストの基礎倍率（中継数×運用テンポ に掛ける）。</summary>
        public readonly float maintenanceScale;

        public CommunicationRelayParams(float coverageScale, float latencyScale, float vulnerabilityScale,
            float reroutingScale, float maintenanceScale)
        {
            this.coverageScale = Mathf.Max(0f, coverageScale);
            this.latencyScale = Mathf.Max(0f, latencyScale);
            this.vulnerabilityScale = Mathf.Max(0f, vulnerabilityScale);
            this.reroutingScale = Mathf.Max(0f, reroutingScale);
            this.maintenanceScale = Mathf.Max(0f, maintenanceScale);
        }

        /// <summary>既定＝到達倍率1・遅延倍率1・脆弱倍率1・迂回倍率1・維持倍率1。</summary>
        public static CommunicationRelayParams Default =>
            new CommunicationRelayParams(1f, 1f, 1f, 1f, 1f);
    }

    /// <summary>
    /// 星間の長距離通信の中継網（リレー）の純ロジック。中継ノードを配置して到達範囲を伸ばし、
    /// 各段で信号を増幅して減衰に抗い、中継のたびに遅延が累積する。ノードは敵に晒されて落とされうるが、
    /// 代替中継と網状の冗長で迂回できれば網は生き残る。網の規模と運用テンポに比例して維持コストがかかる。
    /// 盤面非依存の plain 引数のみ（座標・コンポーネント非依存）。乱数を持たず決定論。
    /// 実効値パターン（与えた基準値は非破壊）。入力は全て clamp・配列 null/空 安全。
    /// 各メソッドは Params 明示版＋Default 委譲版を持つ。純ロジック（非 MonoBehaviour・test-first）。
    /// 分担：<see cref="SatelliteCommsRules"/>（衛星通信の事業＝中継器リース/収益）とは別。
    /// こちらは「中継網そのものの到達・信号維持・遅延・脆弱性・迂回・生存性・維持コスト」に特化。
    /// </summary>
    public static class CommunicationRelayRules
    {
        /// <summary>
        /// 通信到達範囲＝coverageScale × 中継ノード数 × 配置間隔。ノードを増やし間隔を広げるほど遠くへ届く。
        /// </summary>
        public static float RelayCoverage(int relayNodes, float nodeSpacing, CommunicationRelayParams p)
        {
            int nodes = Mathf.Max(0, relayNodes);
            float spacing = Mathf.Max(0f, nodeSpacing);
            return p.coverageScale * nodes * spacing;
        }

        public static float RelayCoverage(int relayNodes, float nodeSpacing)
            => RelayCoverage(relayNodes, nodeSpacing, CommunicationRelayParams.Default);

        /// <summary>
        /// 信号維持 0..1＝増幅 / (増幅 + 減衰)。増幅が減衰に勝つほど 1 に近づき、両者0なら0。
        /// </summary>
        public static float SignalAmplification(float relayGain, float signalDegradation, CommunicationRelayParams p)
        {
            float gain = Mathf.Max(0f, relayGain);
            float deg = Mathf.Max(0f, signalDegradation);
            float denom = gain + deg;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(gain / denom);
        }

        public static float SignalAmplification(float relayGain, float signalDegradation)
            => SignalAmplification(relayGain, signalDegradation, CommunicationRelayParams.Default);

        /// <summary>
        /// 累積遅延＝latencyScale × 中継数 × 1段あたり遅延。中継を経るほど遅延が積み上がる。
        /// </summary>
        public static float AccumulatedLatency(int hopCount, float perHopDelay, CommunicationRelayParams p)
        {
            int hops = Mathf.Max(0, hopCount);
            float delay = Mathf.Max(0f, perHopDelay);
            return p.latencyScale * hops * delay;
        }

        public static float AccumulatedLatency(int hopCount, float perHopDelay)
            => AccumulatedLatency(hopCount, perHopDelay, CommunicationRelayParams.Default);

        /// <summary>
        /// リレーノードの脆弱性 0..1＝clamp01(vulnerabilityScale × ノード露出 × 敵の到達)。
        /// 露出が大きく敵が届くほど落とされやすい。
        /// </summary>
        public static float RelayVulnerability(float nodeExposure, float enemyReach, CommunicationRelayParams p)
        {
            float exposure = Mathf.Clamp01(nodeExposure);
            float reach = Mathf.Clamp01(enemyReach);
            return Mathf.Clamp01(p.vulnerabilityScale * exposure * reach);
        }

        public static float RelayVulnerability(float nodeExposure, float enemyReach)
            => RelayVulnerability(nodeExposure, enemyReach, CommunicationRelayParams.Default);

        /// <summary>
        /// 迂回経路の能力 0..1＝clamp01(reroutingScale × 代替中継 × 網状度)。
        /// 代替中継が多く網が密なほど、落ちたノードを回避して経路を張り直せる。
        /// </summary>
        public static float ReroutingCapacity(float alternateRelays, float networkMesh, CommunicationRelayParams p)
        {
            float alt = Mathf.Clamp01(alternateRelays);
            float mesh = Mathf.Clamp01(networkMesh);
            return Mathf.Clamp01(p.reroutingScale * alt * mesh);
        }

        public static float ReroutingCapacity(float alternateRelays, float networkMesh)
            => ReroutingCapacity(alternateRelays, networkMesh, CommunicationRelayParams.Default);

        /// <summary>
        /// 網の生存性 0..1＝clamp01(迂回能力 × (1 - 脆弱性))。
        /// 脆くても迂回できれば生き残り、迂回できなければ脆さがそのまま響く。
        /// </summary>
        public static float NetworkSurvivability(float reroutingCapacity, float relayVulnerability, CommunicationRelayParams p)
        {
            float reroute = Mathf.Clamp01(reroutingCapacity);
            float vuln = Mathf.Clamp01(relayVulnerability);
            return Mathf.Clamp01(reroute * (1f - vuln));
        }

        public static float NetworkSurvivability(float reroutingCapacity, float relayVulnerability)
            => NetworkSurvivability(reroutingCapacity, relayVulnerability, CommunicationRelayParams.Default);

        /// <summary>
        /// 維持コスト＝maintenanceScale × 中継数 × 運用テンポ。網が大きく運用が激しいほど高くつく。
        /// </summary>
        public static float MaintenanceCost(int relayNodes, float operationalTempo, CommunicationRelayParams p)
        {
            int nodes = Mathf.Max(0, relayNodes);
            float tempo = Mathf.Max(0f, operationalTempo);
            return p.maintenanceScale * nodes * tempo;
        }

        public static float MaintenanceCost(int relayNodes, float operationalTempo)
            => MaintenanceCost(relayNodes, operationalTempo, CommunicationRelayParams.Default);

        /// <summary>
        /// リレー網の実効到達 0..1＝clamp01(到達範囲^0.5 × 信号維持 × 生存性)を 0..1 に正規化…
        /// ではなく、到達範囲は既に正規化済み(0..1)として扱い、3要素の積で実効到達を出す。
        /// 到達範囲・信号維持・生存性のどれかが欠ければ実効到達は落ちる（最弱律的）。
        /// </summary>
        public static float RelayNetworkReach(float relayCoverage, float signalAmplification,
            float networkSurvivability, CommunicationRelayParams p)
        {
            float coverage = Mathf.Clamp01(relayCoverage);
            float signal = Mathf.Clamp01(signalAmplification);
            float survive = Mathf.Clamp01(networkSurvivability);
            return Mathf.Clamp01(coverage * signal * survive);
        }

        public static float RelayNetworkReach(float relayCoverage, float signalAmplification, float networkSurvivability)
            => RelayNetworkReach(relayCoverage, signalAmplification, networkSurvivability, CommunicationRelayParams.Default);
    }
}
