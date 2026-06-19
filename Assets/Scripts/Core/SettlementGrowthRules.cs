using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 個別入植地・都市の成長の調整値。雇用・生活水準・到達性が人口流入に効く重み、自然増の効き、
    /// 過密ペナルティの成長への効き、都市化の効き、繁栄判定の既定しきい値。
    /// </summary>
    public readonly struct SettlementGrowthParams
    {
        /// <summary>雇用機会による人口流入の効き（流入率への乗算・大きいほど吸引が強い）。</summary>
        public readonly float immigrationWeight;
        /// <summary>自然増（出生−死亡）の成長率への効き（乗算）。</summary>
        public readonly float naturalGrowthWeight;
        /// <summary>過密ペナルティの成長率への効き（大きいほど過密が成長を強く削る）。</summary>
        public readonly float overcrowdingWeight;
        /// <summary>都市化の進展の効き（人口規模×経済多様性への乗算）。</summary>
        public readonly float urbanizationWeight;
        /// <summary>入植地が繁栄していると判定する既定しきい値（成長率・アメニティ共通の下限・0..1）。</summary>
        public readonly float thrivingThreshold;

        public SettlementGrowthParams(float immigrationWeight, float naturalGrowthWeight, float overcrowdingWeight,
            float urbanizationWeight, float thrivingThreshold)
        {
            this.immigrationWeight = Mathf.Max(0f, immigrationWeight);
            this.naturalGrowthWeight = Mathf.Max(0f, naturalGrowthWeight);
            this.overcrowdingWeight = Mathf.Max(0f, overcrowdingWeight);
            this.urbanizationWeight = Mathf.Max(0f, urbanizationWeight);
            this.thrivingThreshold = Mathf.Clamp01(thrivingThreshold);
        }

        /// <summary>既定：流入1.0・自然増1.0・過密1.0・都市化1.0・繁栄しきい値0.3。</summary>
        public static SettlementGrowthParams Default
            => new SettlementGrowthParams(DefaultImmigrationWeight, DefaultNaturalGrowthWeight,
                DefaultOvercrowdingWeight, DefaultUrbanizationWeight, DefaultThrivingThreshold);

        public const float DefaultImmigrationWeight = 1.0f;
        public const float DefaultNaturalGrowthWeight = 1.0f;
        public const float DefaultOvercrowdingWeight = 1.0f;
        public const float DefaultUrbanizationWeight = 1.0f;
        public const float DefaultThrivingThreshold = 0.3f;
    }

    /// <summary>
    /// 入植地の成長＝集落から都市への発展の純ロジック。人口流入（雇用機会による吸引）・出生と定着（自然増）・
    /// 過密と生活水準（環境収容力超過の罰）・都市化の段階・成長の限界（環境収容力）を扱う。
    ///
    /// <b>分担</b>：既存の人口ダイナミクス（<see cref="PopulationDynamicsRules"/>＝惑星マクロの出生死亡）とは別＝
    /// こちらは<b>個別入植地のミクロな成長</b>（集落→都市の発展）。盤面・シーンに触れない plain 引数のみ。
    /// 入力は全 clamp・符号付き出力は -1..1 にクランプ・配列 null/空安全・決定論（乱数なし）・実効値パターン。test-first。
    /// </summary>
    public static class SettlementGrowthRules
    {
        /// <summary>既定パラメータで人口流入を返す。</summary>
        public static float Immigration(float jobOpportunity, float livingStandard, float accessibility)
            => Immigration(jobOpportunity, livingStandard, accessibility, SettlementGrowthParams.Default);

        /// <summary>
        /// 雇用機会×生活水準×到達性で人口流入(0..1)を出す。どれかが欠けると流入は細る（積）。
        /// 雇用があり・暮らしやすく・たどり着きやすい入植地ほど移民が集まる。
        /// </summary>
        public static float Immigration(float jobOpportunity, float livingStandard, float accessibility,
            SettlementGrowthParams p)
        {
            float job = Mathf.Clamp01(jobOpportunity);
            float living = Mathf.Clamp01(livingStandard);
            float access = Mathf.Clamp01(accessibility);
            return Mathf.Clamp01(job * living * access * p.immigrationWeight);
        }

        /// <summary>既定パラメータで自然増を返す。</summary>
        public static float NaturalGrowth(float population, float fertilityRate, float mortalityRate)
            => NaturalGrowth(population, fertilityRate, mortalityRate, SettlementGrowthParams.Default);

        /// <summary>
        /// 出生−死亡で自然増(-1..1)を出す。人口が居てこそ意味があり（無人なら0）、出生が死亡を上回れば＋・下回れば−。
        /// 移民とは別に、定着した住民が自ら増える（または減る）ぶん。
        /// </summary>
        public static float NaturalGrowth(float population, float fertilityRate, float mortalityRate,
            SettlementGrowthParams p)
        {
            float pop = Mathf.Max(0f, population);
            if (pop <= 0f) return 0f;
            float fert = Mathf.Clamp01(fertilityRate);
            float mort = Mathf.Clamp01(mortalityRate);
            return Mathf.Clamp((fert - mort) * p.naturalGrowthWeight, -1f, 1f);
        }

        /// <summary>
        /// インフラ×食料×土地で環境収容力（その入植地が養える人口規模・0..1正規化）を出す。
        /// どれかが欠けると上限は低い（積）＝インフラ・食料供給・土地面積の最小律で成長は頭打ちになる。
        /// </summary>
        public static float CarryingCapacity(float infrastructure, float foodSupply, float landArea)
        {
            float infra = Mathf.Clamp01(infrastructure);
            float food = Mathf.Clamp01(foodSupply);
            float land = Mathf.Clamp01(landArea);
            return Mathf.Clamp01(infra * food * land);
        }

        /// <summary>既定パラメータで過密ペナルティを返す。</summary>
        public static float OvercrowdingPenalty(float population, float carryingCapacity)
            => OvercrowdingPenalty(population, carryingCapacity, SettlementGrowthParams.Default);

        /// <summary>
        /// 人口/収容力の超過ぶんを過密ペナルティ(0..1)として出す。収容力以内なら0（余裕あり）、
        /// 超過するほど生活水準が低下し1へ漸近。収容力が0なら住めない＝最大1。
        /// </summary>
        public static float OvercrowdingPenalty(float population, float carryingCapacity, SettlementGrowthParams p)
        {
            float pop = Mathf.Max(0f, population);
            float cap = Mathf.Max(0f, carryingCapacity);
            if (cap <= 0f) return pop > 0f ? 1f : 0f;
            float ratio = pop / cap;
            // 収容力以内(ratio<=1)は0、超過ぶん(ratio-1)を1へクランプ。
            return Mathf.Clamp01(ratio - 1f);
        }

        /// <summary>既定パラメータで成長率を返す。</summary>
        public static float GrowthRate(float immigration, float naturalGrowth, float overcrowdingPenalty)
            => GrowthRate(immigration, naturalGrowth, overcrowdingPenalty, SettlementGrowthParams.Default);

        /// <summary>
        /// 流入＋自然増−過密で総合成長率(-1..1)を出す。移民と自然増で増え、過密が頭を押さえる。
        /// 過密が流入・自然増を食い潰すと成長は止まり、やがてマイナス（流出・衰退）へ転じる。
        /// </summary>
        public static float GrowthRate(float immigration, float naturalGrowth, float overcrowdingPenalty,
            SettlementGrowthParams p)
        {
            float imm = Mathf.Clamp01(immigration);
            float nat = Mathf.Clamp(naturalGrowth, -1f, 1f);
            float over = Mathf.Clamp01(overcrowdingPenalty);
            float rate = imm * p.immigrationWeight + nat * p.naturalGrowthWeight - over * p.overcrowdingWeight;
            return Mathf.Clamp(rate, -1f, 1f);
        }

        /// <summary>既定パラメータで都市化を返す。</summary>
        public static float Urbanization(float population, float economicDiversity)
            => Urbanization(population, economicDiversity, SettlementGrowthParams.Default);

        /// <summary>
        /// 人口規模(0..1正規化)×経済の多様性で都市化(0..1)を出す。人口は Sqrt で逓減（集落から都市へは緩やかに進む）、
        /// 経済が多様（単一産業でない）ほど都市化が進む。人口が同じでも一次産業一辺倒の入植地は都市化しない。
        /// </summary>
        public static float Urbanization(float population, float economicDiversity, SettlementGrowthParams p)
        {
            float pop = Mathf.Clamp01(population);
            float diversity = Mathf.Clamp01(economicDiversity);
            float sizeFactor = Mathf.Sqrt(pop); // 規模の逓減（集落→都市は緩やか）
            return Mathf.Clamp01(sizeFactor * diversity * p.urbanizationWeight);
        }

        /// <summary>
        /// インフラ×都市化−過密で生活アメニティ(0..1)を出す。インフラが整い都市化が進むほど暮らしの利便は上がるが、
        /// 過密がそれを直接削る。アメニティが高い入植地ほど住民が定着し更なる移民を呼ぶ（流入の素地）。
        /// </summary>
        public static float AmenityLevel(float infrastructure, float urbanization, float overcrowdingPenalty)
        {
            float infra = Mathf.Clamp01(infrastructure);
            float urban = Mathf.Clamp01(urbanization);
            float over = Mathf.Clamp01(overcrowdingPenalty);
            return Mathf.Clamp01(infra * urban - over);
        }

        /// <summary>既定しきい値で入植地が繁栄しているか判定する。</summary>
        public static bool IsSettlementThriving(float growthRate, float amenityLevel)
            => IsSettlementThriving(growthRate, amenityLevel, SettlementGrowthParams.Default.thrivingThreshold);

        /// <summary>
        /// 入植地が繁栄しているか(bool)。成長率とアメニティの両方が threshold 以上なら繁栄＝
        /// 人が増え続け（成長）かつ暮らしやすい（アメニティ）状態。どちらか欠ければ繁栄とは言えない。
        /// </summary>
        public static bool IsSettlementThriving(float growthRate, float amenityLevel, float threshold)
        {
            float rate = Mathf.Clamp(growthRate, -1f, 1f);
            float amenity = Mathf.Clamp01(amenityLevel);
            float th = Mathf.Clamp01(threshold);
            return rate >= th && amenity >= th;
        }
    }
}
