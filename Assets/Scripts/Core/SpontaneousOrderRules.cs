using UnityEngine;

namespace Ginei
{
    /// <summary>自生的秩序の調整係数（ハイエク型・HAYK-6 #1556）。</summary>
    public readonly struct SpontaneousOrderParams
    {
        /// <summary>秩序が育つ速さの基礎/秒（自由×分散知識が満点でこの率で漸成＝遅い創発）。</summary>
        public readonly float formationRate;
        /// <summary>強制介入が秩序を侵食する速さ/単位介入（壊すのは速い＝非対称の核）。</summary>
        public readonly float erosionRate;
        /// <summary>侵食された秩序の自然回復/秒の基礎（育つより遅い＝回復の非対称）。</summary>
        public readonly float recoveryRate;
        /// <summary>市場効率の下駄（秩序0でも残る最低効率＝この値〜1.0の幅で秩序に比例）。</summary>
        public readonly float efficiencyFloor;
        /// <summary>根付いた秩序の頑健性の最大軽減率（成熟が侵食を最大このぶん緩める）。</summary>
        public readonly float resilienceMax;
        /// <summary>過剰設計が創発を殺す効きの強さ（介入水準が高いほど創発死＝非線形）。</summary>
        public readonly float overDesignScale;

        public SpontaneousOrderParams(float formationRate, float erosionRate, float recoveryRate,
            float efficiencyFloor, float resilienceMax, float overDesignScale)
        {
            this.formationRate = Mathf.Max(0f, formationRate);
            this.erosionRate = Mathf.Max(0f, erosionRate);
            this.recoveryRate = Mathf.Max(0f, recoveryRate);
            this.efficiencyFloor = Mathf.Clamp01(efficiencyFloor);
            this.resilienceMax = Mathf.Clamp01(resilienceMax);
            this.overDesignScale = Mathf.Max(0f, overDesignScale);
        }

        /// <summary>
        /// 既定＝育成0.02・侵食0.2・回復0.01・効率下駄0.3・頑健最大0.5・過剰設計1。
        /// 侵食0.2≫育成0.02≫回復0.01＝<b>育つのに時間がかかり壊すのは一瞬</b>の非対称を数値に固定。
        /// </summary>
        public static SpontaneousOrderParams Default
            => new SpontaneousOrderParams(0.02f, 0.2f, 0.01f, 0.3f, 0.5f, 1f);
    }

    /// <summary>
    /// 自生的秩序の脆弱性の純ロジック（ハイエク型・HAYK-6 #1556）。自生的秩序（spontaneous order＝設計されず
    /// 無数の分散した知識から自然発生的に育つ市場・慣習・法の秩序）は、<b>自由と分散知識から遅く育ち、強制介入で
    /// 速く侵食され、市場効率を落とす</b>＝育つのに時間がかかり壊すのは一瞬という非対称が核。中央計画が増えるほど
    /// 分散知識が活かされず（ハイエクの知識問題）、設計しすぎると創発そのものが死ぬ。長く根付いた慣習は多少の介入に
    /// 耐えるが、一度侵食された秩序の回復は育成より遅い。
    /// <see cref="MarketRules"/>（財の需給均衡＝価格の創発）／<see cref="GovernanceRules"/>（安定度の目標値収束）
    /// とは分担し、ここは<b>設計されない秩序が介入で侵食される脆弱性と、それに伴う市場効率の低下</b>を扱う
    /// （需給価格でも安定度収束でもなく、創発的秩序の漸成・侵食・回復の非対称が主役）。同EPIC HAYK の
    /// PlanningDriftRules（計画ドリフト＝計画が現実から乖離）／CalculationProblemRules（社会主義計算問題＝価格なき
    /// 配分の不可能性）とも分担し、ここは秩序の頑健性・回復の非対称・過剰設計ペナルティに専念する。
    /// すべて plain な float で受け渡す。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SpontaneousOrderRules
    {
        /// <summary>
        /// 秩序の漸成（dt後の order 0..1）。育成量＝育成率×自由×分散知識×dt＝<b>自由と分散した知識から時間で
        /// ゆっくり育つ</b>（設計でなく創発・遅い）。自由か分散知識のどちらかが0なら育たない（両方が要る）。
        /// </summary>
        public static float OrderFormationTick(float order, float freedom, float dispersedKnowledge,
            float dt, SpontaneousOrderParams p)
        {
            float o = Mathf.Clamp01(order);
            float fr = Mathf.Clamp01(freedom);
            float dk = Mathf.Clamp01(dispersedKnowledge);
            float step = Mathf.Max(0f, dt);

            float growth = p.formationRate * fr * dk * step;
            return Mathf.Clamp01(o + growth);
        }

        public static float OrderFormationTick(float order, float freedom, float dispersedKnowledge, float dt)
            => OrderFormationTick(order, freedom, dispersedKnowledge, dt, SpontaneousOrderParams.Default);

        /// <summary>
        /// 強制介入による侵食（侵食後の order 0..1）。侵食量＝侵食率×介入水準×現秩序＝<b>強制介入が自生的秩序を
        /// 速く壊す</b>（侵食率≫育成率＝非対称）。育つもの（秩序）が大きいほど壊される量も大きい。介入0なら不変。
        /// </summary>
        public static float InterventionErosion(float order, float interventionLevel, SpontaneousOrderParams p)
        {
            float o = Mathf.Clamp01(order);
            float iv = Mathf.Clamp01(interventionLevel);

            float erosion = p.erosionRate * iv * o;
            return Mathf.Clamp01(o - erosion);
        }

        public static float InterventionErosion(float order, float interventionLevel)
            => InterventionErosion(order, interventionLevel, SpontaneousOrderParams.Default);

        /// <summary>
        /// 市場効率（0..1）＝自生的秩序が高いほど分散知識が活きて効率が高い。efficiencyFloor〜1.0 を秩序で線形補間
        /// ＝秩序0でも下駄ぶんは残り、秩序1で満点。<b>創発的秩序が市場効率の源泉</b>（呼び出し側が産出倍率へ掛ける）。
        /// </summary>
        public static float MarketEfficiency(float order, SpontaneousOrderParams p)
        {
            float o = Mathf.Clamp01(order);
            return Mathf.Lerp(p.efficiencyFloor, 1f, o);
        }

        public static float MarketEfficiency(float order)
            => MarketEfficiency(order, SpontaneousOrderParams.Default);

        /// <summary>
        /// 分散知識の活用度（0..1）＝中央計画が増えるほど分散知識が活かされない（<b>ハイエクの知識問題</b>）。
        /// 分散知識×(1−中央計画)＝中央計画1なら現場の知識はゼロ活用（上からの設計は分散知識を再現できない）。
        /// </summary>
        public static float KnowledgeUtilization(float dispersedKnowledge, float centralPlanning)
        {
            float dk = Mathf.Clamp01(dispersedKnowledge);
            float cp = Mathf.Clamp01(centralPlanning);
            return Mathf.Clamp01(dk * (1f - cp));
        }

        /// <summary>
        /// 秩序の頑健性（0..1＝侵食をどれだけ耐えるか）＝長く育った秩序ほど多少の介入に耐える（根付いた慣習の頑健性）。
        /// 秩序×成熟度に比例して resilienceMax まで耐性が上がる＝<b>根付いた秩序は壊れにくい</b>。
        /// 呼び出し側が <see cref="InterventionErosion"/> の侵食量へ (1−resilience) を掛けて軽減する。
        /// </summary>
        public static float OrderResilience(float order, float organicMaturity, SpontaneousOrderParams p)
        {
            float o = Mathf.Clamp01(order);
            float mat = Mathf.Clamp01(organicMaturity);
            return Mathf.Clamp01(o * mat * p.resilienceMax);
        }

        public static float OrderResilience(float order, float organicMaturity)
            => OrderResilience(order, organicMaturity, SpontaneousOrderParams.Default);

        /// <summary>
        /// 侵食された秩序の回復（dt後の order 0..1）＝回復は遅い（育つのに時間がかかる非対称）。
        /// 回復量＝回復率×(1−現秩序)×dt＝伸びしろに比例して<b>ゆっくり戻る</b>（侵食率≫回復率＝壊すのは一瞬・
        /// 治すのは長い）。秩序が満点なら回復しない。
        /// </summary>
        public static float RecoveryAsymmetry(float order, float dt, SpontaneousOrderParams p)
        {
            float o = Mathf.Clamp01(order);
            float step = Mathf.Max(0f, dt);
            float recovery = p.recoveryRate * (1f - o) * step;
            return Mathf.Clamp01(o + recovery);
        }

        public static float RecoveryAsymmetry(float order, float dt)
            => RecoveryAsymmetry(order, dt, SpontaneousOrderParams.Default);

        /// <summary>
        /// 過剰設計ペナルティ（0..1＝創発がどれだけ殺されるか）＝設計しすぎると創発が死ぬ（上からの設計の限界）。
        /// 介入水準の二乗×overDesignScale＝<b>介入が高いほど非線形に創発を殺す</b>（軽い介入は害が小さいが、
        /// 過剰な設計は創発をほぼ消す）。呼び出し側が <see cref="OrderFormationTick"/> の育成を (1−penalty) で削る。
        /// </summary>
        public static float OverDesignPenalty(float interventionLevel, SpontaneousOrderParams p)
        {
            float iv = Mathf.Clamp01(interventionLevel);
            return Mathf.Clamp01(iv * iv * p.overDesignScale);
        }

        public static float OverDesignPenalty(float interventionLevel)
            => OverDesignPenalty(interventionLevel, SpontaneousOrderParams.Default);

        /// <summary>
        /// 秩序が崩壊しつつあるか（true＝介入過剰で秩序が崩壊中）。1tickの侵食量が threshold を超え、かつ介入が
        /// 秩序の自然回復を上回る（侵食率×介入>回復率）ときに成立＝<b>介入過剰で秩序が崩れていく</b>。
        /// 介入が小さく回復が侵食を上回る間は崩壊しない（自生的秩序は緩い介入には耐える）。
        /// </summary>
        public static bool IsOrderCollapsing(float order, float interventionLevel, float threshold,
            SpontaneousOrderParams p)
        {
            float o = Mathf.Clamp01(order);
            float iv = Mathf.Clamp01(interventionLevel);
            float th = Mathf.Clamp01(threshold);

            float erosion = p.erosionRate * iv * o;          // 今tickで失う秩序
            float recovery = p.recoveryRate * (1f - o);       // 今tickで取り戻す秩序
            return erosion > th && erosion > recovery;
        }

        public static bool IsOrderCollapsing(float order, float interventionLevel, float threshold)
            => IsOrderCollapsing(order, interventionLevel, threshold, SpontaneousOrderParams.Default);
    }
}
