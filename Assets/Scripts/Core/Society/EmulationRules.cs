using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 金銭的模倣の調整値（VEBL-2 #1597・ヴェブレン『有閑階級の理論』金銭的競争心 pecuniary emulation）。
    /// 上を真似る模倣圧の効き・消費規範の底上げ速度・下方カスケードの減衰・見栄負担の重み・安定低下/需要押し上げの係数。
    /// 既定は <see cref="Default"/>。全入力はクランプ・乱数なし決定論。
    /// </summary>
    public readonly struct EmulationParams
    {
        /// <summary>上位消費×地位願望→模倣圧の効き（大きいほど上を真似たくなる）。</summary>
        public readonly float emulationGain;
        /// <summary>消費規範が模倣圧へ寄る速さ（/戦略秒＝底上げの速度）。</summary>
        public readonly float normShiftSpeed;
        /// <summary>身の丈を超えた見栄負担の重み（規範−所得の超過分への倍率）。</summary>
        public readonly float keepingUpWeight;
        /// <summary>見栄負担→安定低下の係数（背伸びの不満が安定を削る強さ）。</summary>
        public readonly float erosionScale;
        /// <summary>消費規範→需要押し上げの係数（規範の底上げが需要を押す＝経済への正の面）。</summary>
        public readonly float demandScale;

        public EmulationParams(float emulationGain, float normShiftSpeed, float keepingUpWeight, float erosionScale, float demandScale)
        {
            this.emulationGain = Mathf.Max(0f, emulationGain);
            this.normShiftSpeed = Mathf.Max(0f, normShiftSpeed);
            this.keepingUpWeight = Mathf.Max(0f, keepingUpWeight);
            this.erosionScale = Mathf.Max(0f, erosionScale);
            this.demandScale = Mathf.Max(0f, demandScale);
        }

        /// <summary>
        /// 既定＝模倣圧の効き1・規範底上げ速度1・見栄負担の重み1.5（超過分を割増で痛く）・安定低下係数1・需要押し上げ係数0.5。
        /// </summary>
        public static EmulationParams Default => new EmulationParams(1f, 1f, 1.5f, 1f, 0.5f);
    }

    /// <summary>
    /// 金銭的模倣カスケードの純ロジック（VEBL-2 #1597・ヴェブレン『有閑階級の理論』金銭的競争心 pecuniary emulation・唯一の窓口）。
    /// 「人は一つ上の階級の消費を真似る」＝地位の階段を模倣が駆け上がり、上位の消費規範が下方へ<b>滝のように波及</b>して
    /// 消費水準を底上げする。だが規範に追いつく<b>見栄消費</b>が身の丈（所得）を超えると社会の安定を削る（背伸びの不満）。
    /// みなが上を追うと水準だけ上がって相対地位は変わらない＝<b>地位のランニングマシン</b>。
    /// <see cref="MarketRules"/>（財の需給均衡）とは別＝地位模倣による消費規範の階層伝播。
    /// 同EPICの <see cref="VeblenGoodsRules"/>（地位財そのものの効用）・<see cref="RedistributionRules"/>（階級別の税負担）とも分担：
    /// ここは「上を真似る消費規範のカスケードと見栄の負担」だけを扱う。純ロジック test-first・乱数なし決定論。
    /// 調整値は <see cref="EmulationParams"/> に集約（既定 <see cref="EmulationParams.Default"/>）。
    /// </summary>
    public static class EmulationRules
    {
        /// <summary>
        /// 模倣圧力（VEBL-2 #1597）＝上位階級の消費水準×地位上昇願望（0..1）。
        /// 上を見るほど（上位消費が高いほど）、上に行きたいほど（願望が高いほど）真似たくなる＝積で底上げ。
        /// 両者の積を emulationGain 倍して 0..1 にクランプ。どちらかが0なら模倣は起きない。
        /// </summary>
        public static float EmulationPressure(float upperClassConsumption, float statusAspiration, EmulationParams p)
        {
            float u = Mathf.Clamp01(upperClassConsumption);
            float a = Mathf.Clamp01(statusAspiration);
            return Mathf.Clamp01(u * a * p.emulationGain);
        }

        /// <summary>
        /// 消費規範の底上げ（VEBL-2 #1597）＝模倣圧が現在の消費規範を時間で押し上げる（下位が上位の水準へ寄る）。
        /// 現規範を模倣圧（目標水準）へ MoveTowards で寄せる（移動量＝差×底上げ速度×dt＝指数的に近づく）。
        /// 模倣圧が現規範より低ければ寄り戻る（上を見なくなれば規範も下がる）。dt≤0 は不変。0..1 にクランプ。
        /// </summary>
        public static float ConsumptionNormShift(float currentNorm, float emulationPressure, float dt, EmulationParams p)
        {
            float norm = Mathf.Clamp01(currentNorm);
            if (dt <= 0f) return norm;
            float target = Mathf.Clamp01(emulationPressure);
            float step = Mathf.Abs(target - norm) * p.normShiftSpeed * dt;
            return Mathf.Clamp01(Mathf.MoveTowards(norm, target, step));
        }

        /// <summary>
        /// 下方カスケード（VEBL-2 #1597）＝上位の消費規範が階層を下る（classRank が低い）ほど薄れながら波及する（滝の伝播）。
        /// classRank 1=最上位（そのまま）・0=最下位（最も減衰）。波及量＝topConsumption×(1−(1−classRank)×decay)。
        /// decay 0で減衰なし（全階級が同じ規範を共有）・1で最下位は完全に薄れる。0..1 にクランプ。
        /// </summary>
        public static float CascadeDownward(float topConsumption, float classRank, float decay)
        {
            float top = Mathf.Clamp01(topConsumption);
            float rank = Mathf.Clamp01(classRank);
            float d = Mathf.Clamp01(decay);
            float attenuation = 1f - (1f - rank) * d; // 上ほど1・下ほど薄れる
            return Mathf.Clamp01(top * attenuation);
        }

        /// <summary>
        /// 見栄消費の負担（VEBL-2 #1597）＝消費規範に追いつくための見栄消費が所得（身の丈）を超えた分の負担。
        /// 規範＞所得のときだけ超過分（規範−所得）に keepingUpWeight を掛けて負担とする（身の丈を超えた見栄）。
        /// 規範≤所得なら負担0（背伸びしていない）。0..1 にクランプ。
        /// </summary>
        public static float KeepingUpCost(float consumptionNorm, float ownIncome, EmulationParams p)
        {
            float norm = Mathf.Clamp01(consumptionNorm);
            float income = Mathf.Clamp01(ownIncome);
            float overshoot = Mathf.Max(0f, norm - income); // 身の丈を超えた分だけ
            return Mathf.Clamp01(overshoot * p.keepingUpWeight);
        }

        /// <summary>
        /// 安定の侵食（VEBL-2 #1597）＝見栄の負担が社会の安定を削る（背伸びの不満）。
        /// 見栄負担に erosionScale を掛けて 0..1 の安定低下量とする。負担0なら侵食0。
        /// 呼び出し側が安定度から差し引く（基準非破壊・実効値パターン）。
        /// </summary>
        public static float StabilityErosion(float keepingUpCost, EmulationParams p)
            => Mathf.Clamp01(Mathf.Clamp01(keepingUpCost) * p.erosionScale);

        /// <summary>
        /// 需要の押し上げ（VEBL-2 #1597・経済への正の面）＝消費規範の底上げが需要を押し上げる。
        /// 消費規範に demandScale を掛けた正の需要ブースト（0..1）。規範が高いほど消費が増える。
        /// 見栄の負担（安定低下）と表裏＝同じ規範底上げが需要を生み、同時に背伸びの不満も生む。
        /// </summary>
        public static float DemandBoost(float consumptionNorm, EmulationParams p)
            => Mathf.Clamp01(Mathf.Clamp01(consumptionNorm) * p.demandScale);

        /// <summary>
        /// 地位のランニングマシン（VEBL-2 #1597）＝みなが上を追うと消費水準だけ上がって相対地位は変わらない。
        /// 模倣圧×dt ぶん「上がった水準（絶対消費の増分）」を返すが、これは相対地位を1ミリも動かさない徒労。
        /// 返り値は消費水準の上昇量（>0で水準は上がる）。相対地位は不変＝走り続けても同じ場所（地位のトレッドミル）。
        /// </summary>
        public static float StatusTreadmill(float emulationPressure, float dt, EmulationParams p)
        {
            if (dt <= 0f) return 0f;
            float pressure = Mathf.Clamp01(emulationPressure);
            return Mathf.Clamp01(pressure * p.normShiftSpeed * dt); // 水準は上がるが相対地位は変わらない
        }

        /// <summary>
        /// 見栄の暴走判定（VEBL-2 #1597）＝消費規範が所得基盤（身の丈）を threshold ぶん超えたら見栄スパイラル。
        /// consumptionNorm−incomeBase が threshold を上回れば true（身の丈を超えた見栄消費の暴走）。
        /// </summary>
        public static bool IsConspicuousSpiral(float consumptionNorm, float incomeBase, float threshold)
        {
            float norm = Mathf.Clamp01(consumptionNorm);
            float income = Mathf.Clamp01(incomeBase);
            return (norm - income) > Mathf.Max(0f, threshold);
        }
    }
}
