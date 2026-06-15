using UnityEngine;

namespace Ginei
{
    /// <summary>マルサスチェックの調整係数（MALT-2 #1575）。食糧逼迫が出生死亡をどれだけ変調するかの幅。</summary>
    public readonly struct MalthusianCheckParams
    {
        /// <summary>チェックが発動し始める食糧ストレス比（これ未満は無変調＝1.0）。</summary>
        public readonly float checkThreshold;
        /// <summary>予防的妨げ＝逼迫最大時に出生率をどれだけ下げるか（最大の出生抑制割合）。</summary>
        public readonly float maxBirthSuppression;
        /// <summary>積極的妨げ＝逼迫最大時に死亡率をどれだけ上げるか（最大の死亡増割合）。</summary>
        public readonly float maxDeathInflation;
        /// <summary>飢饉死亡が立ち上がる食糧ストレス比（深刻な不足の閾値）。</summary>
        public readonly float famineThreshold;
        /// <summary>飢饉時・救援ゼロのときの人口喪失の最大割合。</summary>
        public readonly float maxFamineMortality;
        /// <summary>救援が飢饉死亡を削る最大割合（救援能力最大でこれだけ和らぐ）。</summary>
        public readonly float reliefMitigation;
        /// <summary>人口を収容力へ引き戻すマルサス均衡の強さ（per-tick の収束率）。</summary>
        public readonly float equilibriumRate;

        public MalthusianCheckParams(float checkThreshold, float maxBirthSuppression, float maxDeathInflation,
                                     float famineThreshold, float maxFamineMortality, float reliefMitigation,
                                     float equilibriumRate)
        {
            this.checkThreshold = Mathf.Max(0f, checkThreshold);
            this.maxBirthSuppression = Mathf.Clamp01(maxBirthSuppression);
            this.maxDeathInflation = Mathf.Max(0f, maxDeathInflation);
            this.famineThreshold = Mathf.Max(0f, famineThreshold);
            this.maxFamineMortality = Mathf.Clamp01(maxFamineMortality);
            this.reliefMitigation = Mathf.Clamp01(reliefMitigation);
            this.equilibriumRate = Mathf.Clamp01(equilibriumRate);
        }

        /// <summary>
        /// 既定＝チェック発動0.8/出生抑制最大60%/死亡増最大50%/飢饉閾値1.2/飢饉死亡最大25%/救援緩和80%/均衡収束10%。
        /// </summary>
        public static MalthusianCheckParams Default =>
            new MalthusianCheckParams(0.8f, 0.6f, 0.5f, 1.2f, 0.25f, 0.8f, 0.1f);
    }

    /// <summary>
    /// マルサスチェック（マルサスの妨げ）の純ロジック（MALT-2 #1575・マルサス『人口論』参考）。
    /// 食糧ストレス比 foodStressratio（人口÷収容力＝食糧天井。1超で逼迫）が高まると、
    /// <b>予防的妨げ</b>（晩婚・出生抑制で出生率↓）と<b>積極的妨げ</b>（飢饉・疫病・戦争で死亡率↑）が働き、
    /// 人口を食糧水準へ自動的に引き戻す＝負のフィードバック。基準値（平時の出生死亡）は壊さず<b>変調倍率</b>を返す（実効値パターン）。
    /// 平時の出生死亡（VitalRates）は <see cref="DemographicsRules"/> が、FoodStressRatio の算出は同EPICの
    /// CarryingCapacityRules（収容力・食糧天井）が、災害ショックとしての飢饉は <see cref="DisasterRules"/> が担い、
    /// ここは「食糧逼迫が出生を抑え死亡を増やし人口を食糧水準へ戻す」変調係数だけを出す。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MalthusianCheckRules
    {
        /// <summary>
        /// 予防的妨げ＝食糧逼迫で出生率を下げる係数（0..1、1.0が抑制なし）。
        /// 食糧ストレス比が checkThreshold を超えたぶんだけ晩婚・出生抑制が進み、逼迫最大で maxBirthSuppression まで下がる。
        /// </summary>
        public static float PreventiveCheck(float foodStressRatio, MalthusianCheckParams p)
        {
            float fsr = Mathf.Max(0f, foodStressRatio);
            float over = fsr - p.checkThreshold;
            if (over <= 0f) return 1f;
            // 閾値超過を 0..1 へ正規化（閾値から逼迫2.0までで飽和）。
            float intensity = Mathf.Clamp01(over / Mathf.Max(0.0001f, 2f - p.checkThreshold));
            return 1f - p.maxBirthSuppression * intensity;
        }

        public static float PreventiveCheck(float foodStressRatio)
            => PreventiveCheck(foodStressRatio, MalthusianCheckParams.Default);

        /// <summary>
        /// 積極的妨げ＝食糧逼迫で死亡率を上げる係数（1.0以上）。
        /// 閾値超過ぶんで飢饉・疫病が死亡を押し上げ、逼迫最大で (1+maxDeathInflation) 倍まで増える。
        /// </summary>
        public static float PositiveCheck(float foodStressRatio, MalthusianCheckParams p)
        {
            float fsr = Mathf.Max(0f, foodStressRatio);
            float over = fsr - p.checkThreshold;
            if (over <= 0f) return 1f;
            float intensity = Mathf.Clamp01(over / Mathf.Max(0.0001f, 2f - p.checkThreshold));
            return 1f + p.maxDeathInflation * intensity;
        }

        public static float PositiveCheck(float foodStressRatio)
            => PositiveCheck(foodStressRatio, MalthusianCheckParams.Default);

        /// <summary>
        /// 出生率への変調倍率（基準非破壊）。基準出生率に掛けて実効出生率を得る＝予防的妨げそのもの。
        /// baseBirthRate は参照しない（倍率を返す）が、実効値パターンの呼び出し意図を明示するため受ける。
        /// </summary>
        public static float BirthRateModifier(float baseBirthRate, float foodStressRatio, MalthusianCheckParams p)
        {
            return PreventiveCheck(foodStressRatio, p);
        }

        public static float BirthRateModifier(float baseBirthRate, float foodStressRatio)
            => BirthRateModifier(baseBirthRate, foodStressRatio, MalthusianCheckParams.Default);

        /// <summary>死亡率への変調倍率（基準非破壊）。基準死亡率に掛けて実効死亡率を得る＝積極的妨げそのもの。</summary>
        public static float DeathRateModifier(float foodStressRatio, MalthusianCheckParams p)
        {
            return PositiveCheck(foodStressRatio, p);
        }

        public static float DeathRateModifier(float foodStressRatio)
            => DeathRateModifier(foodStressRatio, MalthusianCheckParams.Default);

        /// <summary>
        /// 純人口圧＝出生抑制ぶん(1−予防係数)＋死亡増ぶん(積極係数−1)。
        /// 食糧水準へ引き戻す力の総量（0で無圧・大きいほど強く人口を抑える）。
        /// </summary>
        public static float NetPopulationPressure(float foodStressRatio, MalthusianCheckParams p)
        {
            float birthSuppression = 1f - PreventiveCheck(foodStressRatio, p);
            float deathInflation = PositiveCheck(foodStressRatio, p) - 1f;
            return birthSuppression + deathInflation;
        }

        public static float NetPopulationPressure(float foodStressRatio)
            => NetPopulationPressure(foodStressRatio, MalthusianCheckParams.Default);

        /// <summary>
        /// チェック発動の深刻度（0..1）。食糧ストレス比が threshold を超えたぶんを 0..1 へ正規化。
        /// 0なら未発動・1で最大逼迫（threshold〜2.0で飽和）。
        /// </summary>
        public static float CheckSeverity(float foodStressRatio, float threshold)
        {
            float fsr = Mathf.Max(0f, foodStressRatio);
            float th = Mathf.Max(0f, threshold);
            float over = fsr - th;
            if (over <= 0f) return 0f;
            return Mathf.Clamp01(over / Mathf.Max(0.0001f, 2f - th));
        }

        /// <summary>
        /// 飢饉死亡＝深刻な食糧不足（famineThreshold 超）の人口喪失割合（0..1）。
        /// famineThreshold を境に立ち上がり、逼迫最大で maxFamineMortality に達する。救援能力(0..1)が緩和倍率(1−reliefMitigation×救援)で和らげる。
        /// </summary>
        public static float FamineMortality(float foodStressRatio, float reliefCapacity, MalthusianCheckParams p)
        {
            float fsr = Mathf.Max(0f, foodStressRatio);
            float over = fsr - p.famineThreshold;
            if (over <= 0f) return 0f;
            float intensity = Mathf.Clamp01(over / Mathf.Max(0.0001f, 2f - p.famineThreshold));
            float relief = 1f - p.reliefMitigation * Mathf.Clamp01(reliefCapacity);
            return p.maxFamineMortality * intensity * relief;
        }

        public static float FamineMortality(float foodStressRatio, float reliefCapacity)
            => FamineMortality(foodStressRatio, reliefCapacity, MalthusianCheckParams.Default);

        /// <summary>
        /// マルサス均衡の引き戻し＝人口を収容力（食糧天井）へ近づける1tick分の人口変化量（負＝減少／正＝増加余地）。
        /// 人口が収容力を超えていれば負（妨げで減る）、下回れば正（回復余地）。equilibriumRate×dt の率で収束。
        /// </summary>
        public static float EquilibriumPull(float population, float carryingCapacity, float dt, MalthusianCheckParams p)
        {
            float pop = Mathf.Max(0f, population);
            float cap = Mathf.Max(0f, carryingCapacity);
            float gap = cap - pop; // 正＝余裕・負＝過剰
            return gap * p.equilibriumRate * Mathf.Max(0f, dt);
        }

        public static float EquilibriumPull(float population, float carryingCapacity, float dt)
            => EquilibriumPull(population, carryingCapacity, dt, MalthusianCheckParams.Default);

        /// <summary>マルサス的危機（飢饉）判定＝食糧ストレス比が threshold を超えたか。</summary>
        public static bool IsMalthusianCrisis(float foodStressRatio, float threshold)
        {
            return Mathf.Max(0f, foodStressRatio) > Mathf.Max(0f, threshold);
        }
    }
}
