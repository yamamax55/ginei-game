using UnityEngine;

namespace Ginei
{
    /// <summary>積み替えハブの調整係数（規模の経済・周辺減衰・混雑閾値・産出波及）。ctor で全値をクランプする。</summary>
    public readonly struct TransshipmentParams
    {
        /// <summary>規模の経済の効き（処理量がコスト低減へ転じる強さ・大きいほど集約の恩恵が大きい）。</summary>
        public readonly float scaleEconomyStrength;
        /// <summary>周辺コスト低減の距離減衰。この距離でハブの恩恵が半減する（大きいほど遠くまで効く）。</summary>
        public readonly float neighborFalloff;
        /// <summary>産出への波及係数（コスト低減1あたり産出倍率がどれだけ増えるか）。</summary>
        public readonly float outputElasticity;
        /// <summary>混雑が始まる処理量／能力の比（これを超えると効率が落ち始める＝ハブの飽和点）。</summary>
        public readonly float congestionThreshold;
        /// <summary>混雑ペナルティの強さ（閾値超過ぶんに掛かる効率低下の傾き）。</summary>
        public readonly float congestionStrength;
        /// <summary>ハブ重力（正のフィードバック）のゲイン＝荷が集まるほど効率→さらに荷を呼ぶ強さ。</summary>
        public readonly float gravityGain;

        public TransshipmentParams(float scaleEconomyStrength, float neighborFalloff, float outputElasticity,
                                   float congestionThreshold, float congestionStrength, float gravityGain)
        {
            this.scaleEconomyStrength = Mathf.Clamp01(scaleEconomyStrength);
            this.neighborFalloff = Mathf.Max(0.01f, neighborFalloff);
            this.outputElasticity = Mathf.Max(0f, outputElasticity);
            this.congestionThreshold = Mathf.Clamp01(congestionThreshold);
            this.congestionStrength = Mathf.Max(0f, congestionStrength);
            this.gravityGain = Mathf.Max(0f, gravityGain);
        }

        /// <summary>
        /// 既定＝規模の経済0.5／周辺減衰0.4／産出波及0.5／混雑閾値0.8／混雑強さ1.0／重力ゲイン0.6。
        /// </summary>
        public static TransshipmentParams Default
            => new TransshipmentParams(0.5f, 0.4f, 0.5f, 0.8f, 1f, 0.6f);
    }

    /// <summary>
    /// 積み替えハブ（コンテナのハブ港）の純ロジック（CNTR-2 #1612・レビンソン『コンテナ物語』参考）。
    /// ハブ星系の積み替え能力 <c>hubCapacity</c> への投資で周辺一帯の輸送コストが下がり、星系の産出倍率が上がる。
    /// 核心＝<b>ハブに荷が集まるほど規模の経済で単位コストが下がり、さらに荷を呼ぶ正のフィードバック。ただし
    /// 能力を超える流入は混雑で効率が逓減する（飽和）</b>＝集約と規模の経済 vs 混雑のせめぎ合いを式に出す。
    /// <see cref="LogisticsRules"/>（版図の連結成分＝一体化度）とは別＝こちらは<b>物流ハブの集約効果と規模の経済</b>。
    /// 同EPIC の TransportCostRules（回廊単位の輸送コスト）・<see cref="ChokepointValueRules"/>（要衝の戦略価値）とも
    /// 分担：ここは<b>ハブ拠点の集約・規模の経済・混雑</b>のみを扱う。盤面（<see cref="GalaxyMap"/> 等）に依存せず
    /// plain 引数で受ける。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TransshipmentRules
    {
        /// <summary>
        /// ハブの実効処理量（0..1）＝積み替え能力と需要の<b>小さい方が律速</b>。能力があっても荷が無ければ動かず、
        /// 荷があっても能力が無ければ捌けない（リービッヒの最小律と同型）。両入力はクランプ。
        /// </summary>
        public static float HubThroughput(float hubCapacity, float demand)
        {
            return Mathf.Min(Mathf.Clamp01(hubCapacity), Mathf.Clamp01(demand));
        }

        /// <summary>
        /// 規模の経済による単位コスト低減（0..1）＝処理量が多いほどコストが下がる。逓減つき（√で頭打ち）＝
        /// 集約は効くが青天井ではない。<c>強さ×√throughput</c>。throughput=1 で最大＝strength。
        /// </summary>
        public static float ScaleEconomyFactor(float throughput, TransshipmentParams p)
        {
            float t = Mathf.Clamp01(throughput);
            return Mathf.Clamp01(p.scaleEconomyStrength * Mathf.Sqrt(t));
        }

        /// <summary>既定係数での規模の経済（0..1）。</summary>
        public static float ScaleEconomyFactor(float throughput)
            => ScaleEconomyFactor(throughput, TransshipmentParams.Default);

        /// <summary>
        /// 周辺の輸送コスト低減（0..1）＝ハブの能力に比例し、ハブから遠いほど薄れる。距離0でハブ能力ぶん満額、
        /// 距離=falloff で半減（falloff/(falloff+距離)）。能力・距離はクランプ。
        /// </summary>
        public static float NeighborCostReduction(float hubCapacity, float distance, TransshipmentParams p)
        {
            float cap = Mathf.Clamp01(hubCapacity);
            float d = Mathf.Max(0f, distance);
            float f = p.neighborFalloff;
            return Mathf.Clamp01(cap * (f / (f + d)));
        }

        /// <summary>既定係数での周辺コスト低減（0..1）。</summary>
        public static float NeighborCostReduction(float hubCapacity, float distance)
            => NeighborCostReduction(hubCapacity, distance, TransshipmentParams.Default);

        /// <summary>
        /// コスト低減が星系の産出倍率へ波及（≥1.0）＝低減ぶん×弾性を1.0に上乗せ。低減0で等倍1.0、
        /// 低減が大きいほど産出が増える。<see cref="GovernanceRules.OutputFactor"/> 等へ掛ける係数を想定。
        /// </summary>
        public static float OutputBoost(float costReduction, TransshipmentParams p)
        {
            float r = Mathf.Clamp01(costReduction);
            return 1f + r * p.outputElasticity;
        }

        /// <summary>既定係数での産出倍率（≥1.0）。</summary>
        public static float OutputBoost(float costReduction)
            => OutputBoost(costReduction, TransshipmentParams.Default);

        /// <summary>
        /// 混雑による効率倍率（0..1・1=無混雑）＝処理量／能力が混雑閾値を超えるとハブが飽和して効率が落ちる。
        /// 閾値内は1.0（恩恵そのまま）、超過ぶんに <c>congestionStrength</c> を掛けて差し引く。能力0は完全混雑0扱い。
        /// </summary>
        public static float CongestionPenalty(float throughput, float capacity, TransshipmentParams p)
        {
            float t = Mathf.Clamp01(throughput);
            float cap = Mathf.Clamp01(capacity);
            if (cap <= 0f) return t > 0f ? 0f : 1f; // 能力ゼロに荷が来れば即パンク
            float load = t / cap; // 能力に対する負荷
            float over = load - p.congestionThreshold;
            if (over <= 0f) return 1f; // 飽和点まではペナルティなし
            return Mathf.Clamp01(1f - over * p.congestionStrength);
        }

        /// <summary>既定係数での混雑倍率（0..1）。</summary>
        public static float CongestionPenalty(float throughput, float capacity)
            => CongestionPenalty(throughput, capacity, TransshipmentParams.Default);

        /// <summary>
        /// ハブ重力（正のフィードバック）の1tick後の積み替え能力（0..1）＝荷が集まるほど効率が上がり、さらに荷を
        /// 呼んで能力が育つ。蓄積速度は <c>gravityGain×regionalTraffic×(混雑余地)</c>。能力が混雑閾値へ近づくほど
        /// 余地が縮み頭打ち＝集まるが青天井にならない。dt はフレーム非依存。
        /// </summary>
        public static float HubGravityTick(float hubCapacity, float regionalTraffic, float dt, TransshipmentParams p)
        {
            float cap = Mathf.Clamp01(hubCapacity);
            float traffic = Mathf.Clamp01(regionalTraffic);
            float t = Mathf.Max(0f, dt);
            // 混雑閾値を頭打ちラインとし、そこへの余地ぶんだけ伸びる（飽和で逓減）。
            float headroom = Mathf.Clamp01(p.congestionThreshold - cap);
            float growth = p.gravityGain * traffic * headroom * t;
            return Mathf.Clamp01(cap + growth);
        }

        /// <summary>既定係数でのハブ重力tick（0..1）。</summary>
        public static float HubGravityTick(float hubCapacity, float regionalTraffic, float dt)
            => HubGravityTick(hubCapacity, regionalTraffic, dt, TransshipmentParams.Default);

        /// <summary>
        /// ハブ投資が引き合うか＝周辺コスト低減から得られる便益（能力×流量×規模の経済）が投資コストを上回るか。
        /// 便益＝<c>throughput × ScaleEconomyFactor(throughput)</c>（処理量＝能力と流量の律速）。
        /// </summary>
        public static bool HubViability(float hubCapacity, float investmentCost, float traffic, TransshipmentParams p)
        {
            float throughput = HubThroughput(hubCapacity, traffic);
            float benefit = throughput * ScaleEconomyFactor(throughput, p);
            return benefit > Mathf.Max(0f, investmentCost);
        }

        /// <summary>既定係数でのハブ投資採算判定。</summary>
        public static bool HubViability(float hubCapacity, float investmentCost, float traffic)
            => HubViability(hubCapacity, investmentCost, traffic, TransshipmentParams.Default);

        /// <summary>
        /// 混雑前の最適積み替え能力（0..1）＝需要を捌ききりつつ混雑閾値を超えない能力。需要が能力でちょうど
        /// 混雑閾値の負荷になる点＝<c>demand/congestionThreshold</c>（クランプ）。閾値が緩いほど小さな能力で足り、
        /// 厳しいほど大きな能力が要る。
        /// </summary>
        public static float OptimalHubCapacity(float demand, float congestionThreshold)
        {
            float d = Mathf.Clamp01(demand);
            float thr = Mathf.Clamp01(congestionThreshold);
            if (thr <= 0f) return 1f; // 閾値0＝すぐ混む＝能力を最大に振る
            return Mathf.Clamp01(d / thr);
        }
    }
}
