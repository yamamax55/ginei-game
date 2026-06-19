using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 阻止攻撃（インターディクション）の純ロジック。
    /// 前線の敵と直接戦うのでなく、後方で敵の補給・増援・移動を叩いて
    /// 前線への到達を妨げ、兵糧攻め的に弱らせる。回廊の要所を扼す。
    ///
    /// 分担（重複実装しない）：
    /// - <see cref="CommerceRaidingRules"/>（通商破壊＝船団狩り）とは別＝
    ///   こちらは回廊の要所を扼す「面的遮断」（個々の船団撃破でなく流量制限）。
    /// - <see cref="SupplyRules"/>（補給の面の到達）とは別＝
    ///   こちらは敵補給の「能動的遮断」（自分の補給でなく敵補給を断つ側）。
    /// 盤面非依存の plain 引数・乱数なし・実効値パターン（基準値非破壊）。
    /// </summary>
    public static class InterdictionRules
    {
        /// <summary>阻止攻撃の係数。トップレベル readonly struct・全 Clamp。</summary>
        public readonly struct InterdictionParams
        {
            /// <summary>阻止部隊戦力の効き（要所支配の利き）。</summary>
            public readonly float controlScale;
            /// <summary>どんなに扼しても漏れ通る補給の下限割合（0..1）。</summary>
            public readonly float throughputFloor;
            /// <summary>要所支配×規模あたりの増援遅延単位。</summary>
            public readonly float delayPerControl;
            /// <summary>補給不足による前線弱体の進行率。</summary>
            public readonly float starvationRate;
            /// <summary>持続遮断による累積消耗率。</summary>
            public readonly float attritionRate;
            /// <summary>後方進出による孤立リスクの効き。</summary>
            public readonly float exposureScale;
            /// <summary>護衛の対抗効率（つくほど阻止が難しい）。</summary>
            public readonly float escortMitigation;
            /// <summary>補給線を断ったとみなす補給減少の閾値（0..1）。</summary>
            public readonly float severThreshold;

            public InterdictionParams(
                float controlScale,
                float throughputFloor,
                float delayPerControl,
                float starvationRate,
                float attritionRate,
                float exposureScale,
                float escortMitigation,
                float severThreshold)
            {
                this.controlScale = Mathf.Max(0f, controlScale);
                this.throughputFloor = Mathf.Clamp01(throughputFloor);
                this.delayPerControl = Mathf.Max(0f, delayPerControl);
                this.starvationRate = Mathf.Max(0f, starvationRate);
                this.attritionRate = Mathf.Max(0f, attritionRate);
                this.exposureScale = Mathf.Max(0f, exposureScale);
                this.escortMitigation = Mathf.Max(0f, escortMitigation);
                this.severThreshold = Mathf.Clamp01(severThreshold);
            }

            public static InterdictionParams Default => new InterdictionParams(
                controlScale: 1.5f,
                throughputFloor: 0.1f,
                delayPerControl: 10f,
                starvationRate: 0.5f,
                attritionRate: 0.3f,
                exposureScale: 1.0f,
                escortMitigation: 1.0f,
                severThreshold: 0.7f);
        }

        /// <summary>
        /// 要所を扼して流量を制限する度合い（0..1）。
        /// 阻止戦力（×利き）と回廊流量の比＝戦力が大きいほど扼せる。
        /// </summary>
        public static float ChokePointControl(float interdictorStrength, float corridorTraffic, InterdictionParams p)
        {
            float s = Mathf.Max(0f, interdictorStrength) * p.controlScale;
            float t = Mathf.Max(0f, corridorTraffic);
            float denom = s + t;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(s / denom);
        }

        public static float ChokePointControl(float interdictorStrength, float corridorTraffic)
            => ChokePointControl(interdictorStrength, corridorTraffic, InterdictionParams.Default);

        /// <summary>
        /// 敵前線への補給到達の減少割合（0..1）。
        /// 要所支配が高いほど補給が届かなくなるが、下限ぶんは必ず漏れ通る。
        /// </summary>
        public static float SupplyThroughputReduction(float chokePointControl, InterdictionParams p)
        {
            float c = Mathf.Clamp01(chokePointControl);
            return Mathf.Clamp01(c * (1f - p.throughputFloor));
        }

        public static float SupplyThroughputReduction(float chokePointControl)
            => SupplyThroughputReduction(chokePointControl, InterdictionParams.Default);

        /// <summary>
        /// 増援の到着遅延。要所支配×規模（規模は平方根で逓減）。
        /// 大軍ほど詰まりやすく遅れる。
        /// </summary>
        public static float ReinforcementDelay(float chokePointControl, float reinforcementSize, InterdictionParams p)
        {
            float c = Mathf.Clamp01(chokePointControl);
            float size = Mathf.Max(0f, reinforcementSize);
            return c * p.delayPerControl * Mathf.Sqrt(size);
        }

        public static float ReinforcementDelay(float chokePointControl, float reinforcementSize)
            => ReinforcementDelay(chokePointControl, reinforcementSize, InterdictionParams.Default);

        /// <summary>
        /// 前線が補給不足で弱る進行量（dt 比例）。
        /// 補給減少×消費規模×進行率×時間＝兵糧攻めの進み。
        /// </summary>
        public static float FrontlineStarvation(float supplyReduction, float frontlineConsumption, float dt, InterdictionParams p)
        {
            float r = Mathf.Clamp01(supplyReduction);
            float consume = Mathf.Max(0f, frontlineConsumption);
            float t = Mathf.Max(0f, dt);
            return r * consume * p.starvationRate * t;
        }

        public static float FrontlineStarvation(float supplyReduction, float frontlineConsumption, float dt)
            => FrontlineStarvation(supplyReduction, frontlineConsumption, dt, InterdictionParams.Default);

        /// <summary>
        /// 阻止部隊が後方に出て孤立するリスク（0..1）。
        /// 敵の対応（×利き）と自分の後方位置の堅さ（戻りやすさ）の比。
        /// </summary>
        public static float InterdictionExposure(float interdictorPosition, float enemyResponse, InterdictionParams p)
        {
            float pos = Mathf.Max(0f, interdictorPosition);
            float resp = Mathf.Max(0f, enemyResponse) * p.exposureScale;
            float denom = pos + resp;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(resp / denom);
        }

        public static float InterdictionExposure(float interdictorPosition, float enemyResponse)
            => InterdictionExposure(interdictorPosition, enemyResponse, InterdictionParams.Default);

        /// <summary>
        /// 持続的遮断で前線が累積的に消耗する割合（0..1）。
        /// 補給減少×累積率×持続時間。
        /// </summary>
        public static float CumulativeAttrition(float supplyReduction, float duration, InterdictionParams p)
        {
            float r = Mathf.Clamp01(supplyReduction);
            float dur = Mathf.Max(0f, duration);
            return Mathf.Clamp01(r * p.attritionRate * dur);
        }

        public static float CumulativeAttrition(float supplyReduction, float duration)
            => CumulativeAttrition(supplyReduction, duration, InterdictionParams.Default);

        /// <summary>
        /// 護衛がつくと阻止が難しくなる＝護衛込みの実効阻止度（0..1）。
        /// 阻止戦力（×利き）と護衛（×対抗効率）の比＝護衛が厚いほど扼せない。
        /// </summary>
        public static float EscortCounter(float interdictorStrength, float convoyEscort, InterdictionParams p)
        {
            float s = Mathf.Max(0f, interdictorStrength) * p.controlScale;
            float e = Mathf.Max(0f, convoyEscort) * p.escortMitigation;
            float denom = s + e;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(s / denom);
        }

        public static float EscortCounter(float interdictorStrength, float convoyEscort)
            => EscortCounter(interdictorStrength, convoyEscort, InterdictionParams.Default);

        /// <summary>
        /// 補給線を断ったか（補給減少が閾値以上）。
        /// </summary>
        public static bool IsSupplyLineSevered(float supplyReduction, InterdictionParams p)
        {
            return Mathf.Clamp01(supplyReduction) >= p.severThreshold;
        }

        public static bool IsSupplyLineSevered(float supplyReduction)
            => IsSupplyLineSevered(supplyReduction, InterdictionParams.Default);
    }
}
