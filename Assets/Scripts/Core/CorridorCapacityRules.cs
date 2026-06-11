using UnityEngine;

namespace Ginei
{
    /// <summary>回廊補給スループットの調整係数（#1367）。通商回廊／要衝回廊の基準容量・インフラの効き・混雑の傾き・飽和閾値。ctor で全値をクランプする。</summary>
    public readonly struct CorridorCapacityParams
    {
        /// <summary>通商回廊の基準輸送容量（0..1・太い航路＝大量に運べる）。</summary>
        public readonly float commerceBaseCapacity;
        /// <summary>要衝回廊の基準輸送容量（0..1・狭い隘路＝容量が限られる）。通商より小さく設定する。</summary>
        public readonly float chokeBaseCapacity;
        /// <summary>インフラ（航路整備）が容量へ上乗せする最大ぶん（0..1）。基準＋インフラ×これ。</summary>
        public readonly float infrastructureBonus;
        /// <summary>渋滞ペナルティの傾き（容量超過1あたりどれだけ効率が落ちるか）。</summary>
        public readonly float congestionStrength;
        /// <summary>飽和とみなす需要／容量の比（これを超えると回廊が需要過多で詰まる）。</summary>
        public readonly float saturationThreshold;

        public CorridorCapacityParams(float commerceBaseCapacity, float chokeBaseCapacity,
                                      float infrastructureBonus, float congestionStrength, float saturationThreshold)
        {
            this.commerceBaseCapacity = Mathf.Clamp01(commerceBaseCapacity);
            this.chokeBaseCapacity = Mathf.Clamp01(chokeBaseCapacity);
            this.infrastructureBonus = Mathf.Clamp01(infrastructureBonus);
            this.congestionStrength = Mathf.Max(0f, congestionStrength);
            this.saturationThreshold = Mathf.Max(0.01f, saturationThreshold);
        }

        /// <summary>
        /// 既定＝通商基準容量0.8／要衝基準容量0.3／インフラ上乗せ0.2／渋滞傾き1.0／飽和閾値1.0。
        /// </summary>
        public static CorridorCapacityParams Default
            => new CorridorCapacityParams(0.8f, 0.3f, 0.2f, 1f, 1f);
    }

    /// <summary>
    /// 回廊補給スループットの純ロジック（CRV-4 #1367・兵站・唯一の窓口）。
    /// <b>回廊（航路）には輸送容量の限界があり、太い通商回廊は大量に運べ、狭い要衝回廊は容量が限られる。</b>
    /// 複数の需要が一つの回廊に集中して容量を超えると、補給が比例配分（按分）され<b>全員が不足する＝兵站のボトルネック</b>。
    /// <see cref="Corridor"/> の <c>CorridorType{通商,要衝}</c>（通商=太い・要衝=狭い）を引数で受ける想定。
    /// TransportCostRules（回廊の連続コスト・生成済み）／<see cref="TransshipmentRules"/>（ハブの集約・生成済み）とは別＝
    /// こちらは<b>回廊の輸送容量と超過時の補給配分（スループットのボトルネック）</b>。
    /// 最弱回廊が経路全体の律速になる点は <see cref="ChainFragilityRules"/>（ボトルネック＝最弱ノードで切れる）と整合し、
    /// 補給そのものの到達/遮断は <see cref="SupplyRules"/>（補給）が扱う＝本ルールは回廊容量と按分配分に特化。
    /// 盤面（<see cref="GalaxyMap"/> 等）に依存せず plain 引数で受ける。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CorridorCapacityRules
    {
        /// <summary>
        /// 回廊の輸送容量（0..1）＝回廊種別（通商か要衝か）の基準容量＋インフラ（航路整備）の上乗せ。
        /// <b>通商回廊は大容量・要衝回廊は小容量</b>（通商＞要衝）。infrastructure はクランプ。
        /// </summary>
        public static float CorridorThroughput(bool corridorTypeIsCommerce, float infrastructure, CorridorCapacityParams p)
        {
            float baseCap = corridorTypeIsCommerce ? p.commerceBaseCapacity : p.chokeBaseCapacity;
            float infra = Mathf.Clamp01(infrastructure);
            return Mathf.Clamp01(baseCap + infra * p.infrastructureBonus);
        }

        /// <summary>既定係数での回廊容量（0..1）。</summary>
        public static float CorridorThroughput(bool corridorTypeIsCommerce, float infrastructure)
            => CorridorThroughput(corridorTypeIsCommerce, infrastructure, CorridorCapacityParams.Default);

        /// <summary>
        /// 需要／容量の比（≥0）＝総需要が回廊容量を超えるか。1.0でちょうど満杯、1.0超＝容量超過（ボトルネック）。
        /// 容量0は需要があれば実質無限大とみなし大きな値を返す（詰まり）。両入力はクランプ。
        /// </summary>
        public static float DemandVsCapacity(float totalDemand, float throughput)
        {
            float demand = Mathf.Clamp01(totalDemand);
            float cap = Mathf.Clamp01(throughput);
            if (cap <= 0f) return demand > 0f ? 999f : 0f; // 容量ゼロに需要が来れば即パンク
            return demand / cap;
        }

        /// <summary>
        /// 複数の需要を容量内で按分配分する（手書きループ・null/空安全）。
        /// 総需要が容量以下ならそのまま満たし、<b>容量を超えたら容量／総需要の比で全員が比例して不足する</b>
        /// （兵站のボトルネック＝一つの回廊に需要が集中すると皆が同率で欠乏）。返り値は各需要に割り当てられた供給量。
        /// </summary>
        public static float[] SupplyAllocation(float[] demands, float throughput)
        {
            if (demands == null || demands.Length == 0) return new float[0];
            float cap = Mathf.Clamp01(throughput);

            float total = 0f;
            for (int i = 0; i < demands.Length; i++)
                total += Mathf.Max(0f, demands[i]);

            float[] alloc = new float[demands.Length];
            if (total <= 0f) return alloc; // 需要なし＝全員0

            // 容量内なら満額、超過なら比率で按分（全員が同率で不足）。
            float ratio = total <= cap ? 1f : cap / total;
            for (int i = 0; i < demands.Length; i++)
                alloc[i] = Mathf.Max(0f, demands[i]) * ratio;
            return alloc;
        }

        /// <summary>
        /// 渋滞・遅延の効率倍率（0..1・1=渋滞なし）＝需要／容量比が1.0を超えると詰まりで効率が落ちる。
        /// 比1.0以下はペナルティなし（1.0）、超過ぶんに <see cref="CorridorCapacityParams.congestionStrength"/> を掛けて差し引く。
        /// </summary>
        public static float CongestionPenalty(float demandVsCapacity, CorridorCapacityParams p)
        {
            float ratio = Mathf.Max(0f, demandVsCapacity);
            float over = ratio - 1f;
            if (over <= 0f) return 1f; // 容量内は遅延なし
            return Mathf.Clamp01(1f - over * p.congestionStrength);
        }

        /// <summary>既定係数での渋滞倍率（0..1）。</summary>
        public static float CongestionPenalty(float demandVsCapacity)
            => CongestionPenalty(demandVsCapacity, CorridorCapacityParams.Default);

        /// <summary>
        /// 優先度の高い補給を先に通す配分（手書きループ・null/空安全）＝限られた容量を優先度順に割り当てる。
        /// 各需要を priority（0..1・高いほど先）で重み付けし、重みの大きい順に容量を食わせる（軍需を民需に優先 等）。
        /// 容量が尽きたら以降の低優先度は満たされない＝<b>狭い回廊では優先度の低い補給から欠乏する</b>。
        /// priorities が null か長さ不一致なら全員等優先（=<see cref="SupplyAllocation"/> と同じ按分）にフォールバック。
        /// </summary>
        public static float[] PriorityRouting(float[] demands, float[] priorities, float throughput)
        {
            if (demands == null || demands.Length == 0) return new float[0];
            if (priorities == null || priorities.Length != demands.Length)
                return SupplyAllocation(demands, throughput); // 優先度なし＝等しく按分

            int n = demands.Length;
            float remaining = Mathf.Clamp01(throughput);
            float[] alloc = new float[n];

            // 優先度の高い順に容量を割り当てる（選択ソート的な手書きループ＝LINQ不可）。
            bool[] done = new bool[n];
            for (int pass = 0; pass < n; pass++)
            {
                // 未処理のうち最高優先度を探す。
                int best = -1;
                float bestPri = float.NegativeInfinity;
                for (int i = 0; i < n; i++)
                {
                    if (done[i]) continue;
                    float pri = Mathf.Clamp01(priorities[i]);
                    if (pri > bestPri)
                    {
                        bestPri = pri;
                        best = i;
                    }
                }
                if (best < 0) break;
                done[best] = true;

                float want = Mathf.Max(0f, demands[best]);
                float give = Mathf.Min(want, remaining); // 残容量内で満たす
                alloc[best] = give;
                remaining = Mathf.Max(0f, remaining - give);
            }
            return alloc;
        }

        /// <summary>
        /// 経路全体のボトルネック容量（0..1）＝狭い要衝回廊が経路全体の律速になる（最弱の回廊が全体を決める）。
        /// 迂回路の厚み alternativeRoutes（0..1）があれば律速が緩む（代替経路で逃がせる）＝
        /// <c>throughput ＋ 余地×alternativeRoutes</c>。代替0なら回廊容量そのまま（最弱で切れる）。
        /// <see cref="ChainFragilityRules.SinglePointRisk"/>（最弱ノードで切れる）と同方針。
        /// </summary>
        public static float ChokepointBottleneck(float throughput, float alternativeRoutes)
        {
            float cap = Mathf.Clamp01(throughput);
            float alt = Mathf.Clamp01(alternativeRoutes);
            float headroom = 1f - cap;          // 容量を1.0へ近づける余地
            return Mathf.Clamp01(cap + headroom * alt); // 迂回路があるほどボトルネックが緩む
        }

        /// <summary>
        /// インフラ投資で回廊容量を時間で広げる（0..1）＝航路整備で容量が育つ。
        /// 1.0へ近づくほど伸びしろが縮み逓減（<c>investment×(1−throughput)×dt</c>）。dt はフレーム非依存。
        /// </summary>
        public static float CapacityExpansion(float throughput, float investment, float dt)
        {
            float cap = Mathf.Clamp01(throughput);
            float inv = Mathf.Clamp01(investment);
            float t = Mathf.Max(0f, dt);
            float headroom = 1f - cap;          // 残りの伸びしろ＝飽和で逓減
            return Mathf.Clamp01(cap + inv * headroom * t);
        }

        /// <summary>
        /// 回廊が需要過多で飽和したか＝需要／容量比が飽和閾値 threshold を超えたら true（詰まって補給が滞る）。
        /// threshold≤0 は需要があれば即飽和扱い。<see cref="DemandVsCapacity"/> を内部で使う。
        /// </summary>
        public static bool IsCorridorSaturated(float totalDemand, float throughput, float threshold)
        {
            float ratio = DemandVsCapacity(totalDemand, throughput);
            float thr = Mathf.Max(0f, threshold);
            if (thr <= 0f) return Mathf.Clamp01(totalDemand) > 0f;
            return ratio > thr;
        }
    }
}
