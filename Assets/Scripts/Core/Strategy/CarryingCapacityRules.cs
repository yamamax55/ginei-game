using UnityEngine;

namespace Ginei
{
    /// <summary>食糧天井（収容力）の調整係数。</summary>
    public readonly struct CarryingCapacityParams
    {
        /// <summary>収容力の基準スケール（農業産出1・肥沃度1・技術1のとき養える人口の天井）。</summary>
        public readonly float capacityScale;
        /// <summary>収容力に対する土地肥沃度の寄与重み（肥沃な土地ほど多く養える）。</summary>
        public readonly float fertilityWeight;
        /// <summary>収容力に対する技術の寄与重み（技術が天井を押し上げる＝マルサスの罠の出口）。</summary>
        public readonly float techWeight;
        /// <summary>生存圧の非線形指数（天井超過がどれだけ急に効くか＝1超で苦しさが跳ねる）。</summary>
        public readonly float pressureExponent;
        /// <summary>生存圧のスケール（超過1あたりの圧の強さ）。</summary>
        public readonly float pressureScale;
        /// <summary>収容力の漸増基準率（投資1のとき per dt・等差的にゆっくり＝食糧は等差級数）。</summary>
        public readonly float capacityGrowthRate;
        /// <summary>過剰人口（飢餓リスク）と判定する食糧ストレス比の既定しきい値。</summary>
        public readonly float overcapacityThreshold;

        public CarryingCapacityParams(float capacityScale, float fertilityWeight, float techWeight,
            float pressureExponent, float pressureScale, float capacityGrowthRate, float overcapacityThreshold)
        {
            this.capacityScale = Mathf.Max(0f, capacityScale);
            this.fertilityWeight = Mathf.Clamp01(fertilityWeight);
            this.techWeight = Mathf.Clamp01(techWeight);
            this.pressureExponent = Mathf.Max(1f, pressureExponent);
            this.pressureScale = Mathf.Max(0f, pressureScale);
            this.capacityGrowthRate = Mathf.Max(0f, capacityGrowthRate);
            this.overcapacityThreshold = Mathf.Max(0f, overcapacityThreshold);
        }

        /// <summary>
        /// 既定＝収容力スケール1・肥沃度重み0.5・技術重み0.5・生存圧指数2.0・生存圧スケール1.0・
        /// 収容力漸増率0.05・過剰判定しきい値1.1。
        /// </summary>
        public static CarryingCapacityParams Default =>
            new CarryingCapacityParams(1f, 0.5f, 0.5f, 2f, 1f, 0.05f, 1.1f);
    }

    /// <summary>
    /// 食糧天井（収容力）の純ロジック（MALT-1 #1574・マルサス『人口論』参考）。人口は等比級数で増え
    /// 食糧は等差級数でしか増えない＝食糧産出が人口の天井（carrying capacity）を決め、人口がその天井を
    /// 超えると食糧が逼迫して生存圧が<b>非線形</b>に高まる（マルサスの罠）。農業産出・土地肥沃度・技術から
    /// <b>収容力</b>を出し、人口÷収容力＝<b>食糧ストレス比 FoodStressRatio</b>（1.0で天井ぴったり・1超で逼迫）を
    /// 算出する。FoodStressRatio は <see cref="MalthusianCheckRules"/>（同 EPIC・人口チェック＝飢饉/疫病/晩婚で
    /// 人口を天井へ引き戻す抑制の出力先）の主要入力になる。
    /// <see cref="DemographicsRules"/>（年齢コホートの人口動態）とは別＝こちらは食糧の収容限界そのもの、
    /// <see cref="ResourceProductionRules"/>（資源の時間産出）とも別＝こちらは産出を人口で割った逼迫度を出す層。
    /// 全入力クランプ・人口/収容力は微小下限で0割回避・乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CarryingCapacityRules
    {
        /// <summary>収容力・人口の0割回避用の微小下限。</summary>
        public const float Epsilon = 1e-4f;

        /// <summary>
        /// 収容力（carrying capacity・0..capacityScale）＝農業産出を土台に、土地肥沃度と技術が天井を押し上げる。
        /// 農業産出0なら養えない＝収容力0。肥沃度・技術は (1−重み)+重み×値 の乗数で効く＝農業産出が支配的で
        /// 肥沃度と技術が倍率を付ける（技術が天井を上げる＝マルサスの罠を緩める唯一の出口）。
        /// </summary>
        public static float CarryingCapacity(float agriculturalOutput, float landFertility, float techLevel,
            CarryingCapacityParams p)
        {
            float output = Mathf.Clamp01(agriculturalOutput);
            float fertility = Mathf.Clamp01(landFertility);
            float tech = Mathf.Clamp01(techLevel);
            float fertilityMul = (1f - p.fertilityWeight) + p.fertilityWeight * fertility;
            float techMul = (1f - p.techWeight) + p.techWeight * tech;
            return Mathf.Max(0f, p.capacityScale * output * fertilityMul * techMul);
        }

        public static float CarryingCapacity(float agriculturalOutput, float landFertility, float techLevel)
            => CarryingCapacity(agriculturalOutput, landFertility, techLevel, CarryingCapacityParams.Default);

        /// <summary>
        /// 食糧ストレス比 FoodStressRatio＝人口÷収容力。1.0で天井ぴったり・1超で逼迫（人口過剰）・1未満は余裕。
        /// 収容力は微小下限でクランプして0割回避＝収容力ほぼ0で人口があれば大きな逼迫値を返す。マルサス層の主要出力。
        /// </summary>
        public static float FoodStressRatio(float population, float carryingCapacity)
        {
            float pop = Mathf.Max(0f, population);
            float cap = Mathf.Max(Epsilon, carryingCapacity);
            return pop / cap;
        }

        /// <summary>
        /// 食糧余剰＝収容力−人口（正なら養える余裕・負なら不足＝天井超過の人口）。
        /// FoodStressRatio が比なのに対し、こちらは絶対量の差で「あと何人養えるか／何人溢れているか」を出す。
        /// </summary>
        public static float FoodSurplus(float carryingCapacity, float population)
        {
            float cap = Mathf.Max(0f, carryingCapacity);
            float pop = Mathf.Max(0f, population);
            return cap - pop;
        }

        /// <summary>
        /// 生存圧（subsistence pressure・0..）＝食糧ストレスが生存への圧迫へ転じる度合い。比1.0未満は天井内＝圧0、
        /// 1.0超過分が <see cref="CarryingCapacityParams.pressureExponent"/> 乗で非線形に効く（天井を超えると急に苦しい
        /// ＝マルサスの罠の痛み）。<see cref="MalthusianCheckRules"/> の抑制強度の素になる。
        /// </summary>
        public static float SubsistencePressure(float foodStressRatio, CarryingCapacityParams p)
        {
            float ratio = Mathf.Max(0f, foodStressRatio);
            if (ratio <= 1f) return 0f; // 天井内＝生存圧なし
            float excess = ratio - 1f;  // 天井からの超過分
            return Mathf.Max(0f, p.pressureScale * Mathf.Pow(excess, p.pressureExponent));
        }

        public static float SubsistencePressure(float foodStressRatio)
            => SubsistencePressure(foodStressRatio, CarryingCapacityParams.Default);

        /// <summary>
        /// マルサスの罠の度合い（0..1）＝食糧天井に張り付いた状態の強さ。人口が天井に達するほど（ストレス比が
        /// 1へ近づく/超える）罠が深まるが、技術成長 techGrowth が人口増を上回って天井を押し上げる間は罠が緩む
        /// （技術だけが罠の出口）。天井近接度×(1−技術成長) ＝技術が止まると人口が天井へ張り付く。
        /// </summary>
        public static float MalthusianTrap(float foodStressRatio, float techGrowth)
        {
            float proximity = Mathf.Clamp01(Mathf.Max(0f, foodStressRatio)); // 天井近接度（1で天井・以上は飽和）
            float escape = Mathf.Clamp01(techGrowth); // 技術が人口増を上回って天井を押し上げる分
            return Mathf.Clamp01(proximity * (1f - escape));
        }

        /// <summary>
        /// 収容力の漸増（開墾・技術改良で天井が時間でゆっくり上がる＝食糧は等差級数でしか増えない）。
        /// 投資 investment に比例して capacityGrowthRate×dt ぶん足す＝爆発的でない緩やかな線形成長。
        /// </summary>
        public static float CapacityGrowthTick(float capacity, float investment, float dt, CarryingCapacityParams p)
        {
            float cap = Mathf.Max(0f, capacity);
            float inv = Mathf.Clamp01(investment);
            float growth = p.capacityGrowthRate * inv * Mathf.Max(0f, dt);
            return Mathf.Max(0f, cap + growth);
        }

        public static float CapacityGrowthTick(float capacity, float investment, float dt)
            => CapacityGrowthTick(capacity, investment, dt, CarryingCapacityParams.Default);

        /// <summary>
        /// 過剰人口（飢餓リスク）判定＝食糧ストレス比がしきい値以上なら true。天井ぴったり（1.0）より少し上に
        /// 余裕しきい値（既定1.1）を置き、わずかな超過は許容して恒常的な過剰だけを拾う。
        /// </summary>
        public static bool IsOvercapacity(float foodStressRatio, float threshold)
        {
            return Mathf.Max(0f, foodStressRatio) >= Mathf.Max(0f, threshold);
        }

        public static bool IsOvercapacity(float foodStressRatio, CarryingCapacityParams p)
            => IsOvercapacity(foodStressRatio, p.overcapacityThreshold);

        public static bool IsOvercapacity(float foodStressRatio)
            => IsOvercapacity(foodStressRatio, CarryingCapacityParams.Default);

        /// <summary>
        /// 安全人口（飢饉に備えた最適人口）＝収容力×(1−バッファ比)。天井いっぱいに人を養うのは凶作で即飢饉＝危険
        /// なので、余裕（バッファ）を残した水準を返す。バッファ比0なら天井ぴったり・大きいほど保守的。
        /// </summary>
        public static float OptimalPopulation(float carryingCapacity, float bufferRatio)
        {
            float cap = Mathf.Max(0f, carryingCapacity);
            float buffer = Mathf.Clamp01(bufferRatio);
            return Mathf.Max(0f, cap * (1f - buffer));
        }
    }
}
