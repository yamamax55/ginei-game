using UnityEngine;

namespace Ginei
{
    /// <summary>有限能力スケジューリングの調整係数（#987）。</summary>
    public readonly struct CapacitySchedulingParams
    {
        /// <summary>稼働率の上限（過負荷の頭打ち＝既定1.0＝100%超は1.0にクランプ）。</summary>
        public readonly float maxUtilization;

        public CapacitySchedulingParams(float maxUtilization)
        {
            this.maxUtilization = Mathf.Max(0f, maxUtilization);
        }

        /// <summary>既定＝稼働率上限1.0（100%）。</summary>
        public static CapacitySchedulingParams Default => new CapacitySchedulingParams(1f);
    }

    /// <summary>
    /// 有限能力スケジューリングの純ロジック（#987・制約理論TOC・唯一の窓口）。生産オーダーを有限の設備能力へ詰める
    /// ＝最も遅い工程（ボトルネック）が全体のスループットを決める（<see cref="SystemThroughput"/>＝鎖は最弱の輪で切れる）。
    /// 投入が処理を超えればボトルネックの手前に仕掛在庫（WIP）が山になる（<see cref="WipAccumulation"/>）。
    /// ボトルネックを上げると次の工程が新たな律速になる＝改善は移動する（<see cref="BottleneckElevationGain"/>）。
    /// <see cref="ProductionOrderRules"/>（#985発注＝同Wave並行の発注＝<b>入力</b>）が出すオーダーを、ここで有限設備へ詰める。
    /// <see cref="ContinuousOperationRules"/>（連続運転＝1工程の稼働継続）／<see cref="MrpRules"/>（所要量展開＝何を作るか）とは別＝
    /// こちらは<b>工程列の能力制約</b>（どこで詰まるか）を扱う。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CapacitySchedulingRules
    {
        /// <summary>
        /// ボトルネック工程のインデックス＝最も能力の低い工程（鎖は最弱の輪で切れる）。
        /// 同点は先頭側（添字が小さい方）を返す。空・null は -1。能力は負をクランプして比較。
        /// </summary>
        public static int BottleneckStage(float[] stageCapacities)
        {
            if (stageCapacities == null || stageCapacities.Length == 0) return -1;
            int idx = 0;
            float min = Mathf.Max(0f, stageCapacities[0]);
            for (int i = 1; i < stageCapacities.Length; i++)
            {
                float c = Mathf.Max(0f, stageCapacities[i]);
                if (c < min)
                {
                    min = c;
                    idx = i;
                }
            }
            return idx;
        }

        /// <summary>
        /// システム全体のスループット＝ボトルネック工程の能力（全体は最弱工程以上には流れない＝TOCの核）。
        /// 非ボトルネックがいくら速くても全体は最弱で決まる。空・null は0。
        /// </summary>
        public static float SystemThroughput(float[] stageCapacities)
        {
            int b = BottleneckStage(stageCapacities);
            if (b < 0) return 0f;
            return Mathf.Max(0f, stageCapacities[b]);
        }

        /// <summary>
        /// 仕掛在庫（WIP）の蓄積量＝（投入レート − ボトルネック能力）× dt（ボトルネックの手前に溜まる）。
        /// 投入が処理を超えた分だけ山になる。投入が能力以下なら溜まらず0（負は出さない）。dt 負はクランプ。
        /// </summary>
        public static float WipAccumulation(float inflowRate, float bottleneckCapacity, float dt)
        {
            float inflow = Mathf.Max(0f, inflowRate);
            float cap = Mathf.Max(0f, bottleneckCapacity);
            float t = Mathf.Max(0f, dt);
            float surplus = inflow - cap;
            if (surplus <= 0f) return 0f;
            return surplus * t;
        }

        /// <summary>
        /// 工程別の稼働率（0..maxUtilization）＝負荷 ÷ 能力。ボトルネックは100%・他は遊休
        /// （非ボトルネックの余力は無駄ではない＝守りの遊休）。要素ごとに loads[i]/capacities[i]。
        /// 能力≤0 の工程は0。loads が短い分は負荷0扱い。返り値は capacities と同じ長さ。
        /// </summary>
        public static float[] UtilizationByStage(float[] loads, float[] capacities, CapacitySchedulingParams p)
        {
            int n = capacities == null ? 0 : capacities.Length;
            float[] result = new float[n];
            for (int i = 0; i < n; i++)
            {
                float cap = Mathf.Max(0f, capacities[i]);
                if (cap <= 0f) { result[i] = 0f; continue; }
                float load = (loads != null && i < loads.Length) ? Mathf.Max(0f, loads[i]) : 0f;
                result[i] = Mathf.Clamp(load / cap, 0f, p.maxUtilization);
            }
            return result;
        }

        public static float[] UtilizationByStage(float[] loads, float[] capacities)
            => UtilizationByStage(loads, capacities, CapacitySchedulingParams.Default);

        /// <summary>
        /// 完成までの時間＝数量 ÷ ボトルネックのスループット（ボトルネックが律速）。
        /// スループット≤0（止まったボトルネック）は完成しない＝正の無限大。数量≤0 は0（即完了）。
        /// </summary>
        public static float ScheduleCompletion(float orderQty, float bottleneckThroughput)
        {
            float qty = Mathf.Max(0f, orderQty);
            if (qty <= 0f) return 0f;
            float tp = Mathf.Max(0f, bottleneckThroughput);
            if (tp <= 0f) return float.PositiveInfinity;
            return qty / tp;
        }

        /// <summary>
        /// ボトルネック強化の実効スループット上昇量（改善は移動する）。
        /// 現ボトルネック能力に addedCapacity を足しても、第2のボトルネック（次に遅い工程）の能力までしか伸びない
        /// ＝強化後のシステムスループットは min(現+追加, 次ボトルネック)。返り値はその差分（≥0）。
        /// 強化しすぎても次の工程が新たな律速になり頭打ち（TOC＝改善は次へ移る）。各値は負をクランプ。
        /// </summary>
        public static float BottleneckElevationGain(float currentBottleneck, float addedCapacity, float secondBottleneck)
        {
            float cur = Mathf.Max(0f, currentBottleneck);
            float add = Mathf.Max(0f, addedCapacity);
            float second = Mathf.Max(0f, secondBottleneck);
            float raised = cur + add;
            // 強化後のシステムスループットは次ボトルネックで頭打ち
            float newThroughput = Mathf.Min(raised, second);
            // 元のスループットは現ボトルネック（次ボトルネック以下のはず）
            float gain = newThroughput - cur;
            return Mathf.Max(0f, gain);
        }
    }
}
