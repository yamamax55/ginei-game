using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 艦の就役（戦力化）プロセスの調整値（#就役）。艤装・初期不具合・乗員習熟・慣熟航海・
    /// 戦力化・就役所要・量産習熟の各係数。全フィールドは ctor で Clamp 済み。
    /// </summary>
    public readonly struct ShipCommissioningParams
    {
        /// <summary>初期不具合の上限（設計/品質が最悪でもこれ以上にしない）。</summary>
        public readonly float maxTeethingProblems;
        /// <summary>乗員習熟の逓減指数（&lt;1＝早く立ち上がり伸びが鈍る）。</summary>
        public readonly float familiarizationExponent;
        /// <summary>慣熟航海で不具合が進捗を削る重み（0..1）。</summary>
        public readonly float teethingPenaltyWeight;
        /// <summary>就役所要時間の基準（成熟・効率1.0のときの所要）。</summary>
        public readonly float baseCommissioningTime;
        /// <summary>量産習熟の上限（学習曲線の漸近値・1以上）。</summary>
        public readonly float maxSerialLearning;
        /// <summary>量産習熟の効き（数の効きの逓増度・大きいほど早く上限へ）。</summary>
        public readonly float serialLearningRate;
        /// <summary>就役判定の既定閾値（戦力化がこれ以上で就役）。</summary>
        public readonly float commissionThreshold;

        public ShipCommissioningParams(
            float maxTeethingProblems,
            float familiarizationExponent,
            float teethingPenaltyWeight,
            float baseCommissioningTime,
            float maxSerialLearning,
            float serialLearningRate,
            float commissionThreshold)
        {
            this.maxTeethingProblems = Mathf.Clamp01(maxTeethingProblems);
            this.familiarizationExponent = Mathf.Clamp(familiarizationExponent, 0.1f, 1f);
            this.teethingPenaltyWeight = Mathf.Clamp01(teethingPenaltyWeight);
            this.baseCommissioningTime = Mathf.Max(0f, baseCommissioningTime);
            this.maxSerialLearning = Mathf.Max(1f, maxSerialLearning);
            this.serialLearningRate = Mathf.Clamp01(serialLearningRate);
            this.commissionThreshold = Mathf.Clamp01(commissionThreshold);
        }

        public const float DefaultMaxTeethingProblems = 0.8f;
        public const float DefaultFamiliarizationExponent = 0.5f;
        public const float DefaultTeethingPenaltyWeight = 0.7f;
        public const float DefaultBaseCommissioningTime = 30f;
        public const float DefaultMaxSerialLearning = 1.5f;
        public const float DefaultSerialLearningRate = 0.1f;
        public const float DefaultCommissionThreshold = 0.7f;

        /// <summary>既定：不具合上限0.8・習熟逓減0.5・不具合重み0.7・基準所要30・量産上限1.5/効き0.1・就役閾値0.7。</summary>
        public static ShipCommissioningParams Default => new ShipCommissioningParams(
            DefaultMaxTeethingProblems,
            DefaultFamiliarizationExponent,
            DefaultTeethingPenaltyWeight,
            DefaultBaseCommissioningTime,
            DefaultMaxSerialLearning,
            DefaultSerialLearningRate,
            DefaultCommissionThreshold);
    }

    /// <summary>
    /// 新造艦が「戦力」として就役するまでの純ロジック（#就役）。建造（<see cref="ShipyardRules"/>）が
    /// 完成させた船体に対し、こちらは<b>完成後の戦力化</b>＝艤装の完了・初期不具合の洗い出し・乗員の習熟・
    /// 慣熟航海・戦力化の度合い・就役所要・量産による習熟効果を扱う。決定論・実効値パターン・入力 Clamp。
    /// </summary>
    public static class ShipCommissioningRules
    {
        /// <summary>艤装の進捗（船体完成×艤装搭載）。0..1。</summary>
        public static float FittingOutProgress(float hullCompletion, float equipmentInstalled)
            => FittingOutProgress(hullCompletion, equipmentInstalled, ShipCommissioningParams.Default);

        public static float FittingOutProgress(float hullCompletion, float equipmentInstalled, ShipCommissioningParams p)
        {
            float hull = Mathf.Clamp01(hullCompletion);
            float equip = Mathf.Clamp01(equipmentInstalled);
            return Mathf.Clamp01(hull * equip);
        }

        /// <summary>初期不具合（(1-設計成熟)×(1-製造品質)）。0..上限。</summary>
        public static float TeethingProblems(float designMaturity, float buildQuality)
            => TeethingProblems(designMaturity, buildQuality, ShipCommissioningParams.Default);

        public static float TeethingProblems(float designMaturity, float buildQuality, ShipCommissioningParams p)
        {
            float maturity = Mathf.Clamp01(designMaturity);
            float quality = Mathf.Clamp01(buildQuality);
            float raw = (1f - maturity) * (1f - quality);
            return Mathf.Clamp(raw, 0f, p.maxTeethingProblems);
        }

        /// <summary>乗員の習熟（経験×訓練時間・逓減）。0..1。</summary>
        public static float CrewFamiliarization(float crewExperience, float trainingTime)
            => CrewFamiliarization(crewExperience, trainingTime, ShipCommissioningParams.Default);

        public static float CrewFamiliarization(float crewExperience, float trainingTime, ShipCommissioningParams p)
        {
            float exp = Mathf.Clamp01(crewExperience);
            float time = Mathf.Clamp01(trainingTime);
            float linear = exp * time;
            // 逓減（指数<1）＝早く立ち上がり伸びが鈍る。
            return Mathf.Clamp01(Mathf.Pow(linear, p.familiarizationExponent));
        }

        /// <summary>慣熟航海の進捗（艤装−不具合×重み）。0..1。</summary>
        public static float ShakedownProgress(float fittingOutProgress, float teethingProblems)
            => ShakedownProgress(fittingOutProgress, teethingProblems, ShipCommissioningParams.Default);

        public static float ShakedownProgress(float fittingOutProgress, float teethingProblems, ShipCommissioningParams p)
        {
            float fit = Mathf.Clamp01(fittingOutProgress);
            float teeth = Mathf.Clamp01(teethingProblems);
            float penalty = teeth * p.teethingPenaltyWeight;
            return Mathf.Clamp01(fit - penalty);
        }

        /// <summary>戦力化の度合い（慣熟×習熟）。0..1。</summary>
        public static float CombatReadiness(float shakedownProgress, float crewFamiliarization)
            => CombatReadiness(shakedownProgress, crewFamiliarization, ShipCommissioningParams.Default);

        public static float CombatReadiness(float shakedownProgress, float crewFamiliarization, ShipCommissioningParams p)
        {
            float shake = Mathf.Clamp01(shakedownProgress);
            float crew = Mathf.Clamp01(crewFamiliarization);
            return Mathf.Clamp01(shake * crew);
        }

        /// <summary>就役所要時間（(1+不成熟)/造船所効率×基準）。0以上。</summary>
        public static float CommissioningTime(float designMaturity, float shipyardEfficiency)
            => CommissioningTime(designMaturity, shipyardEfficiency, ShipCommissioningParams.Default);

        public static float CommissioningTime(float designMaturity, float shipyardEfficiency, ShipCommissioningParams p)
        {
            float maturity = Mathf.Clamp01(designMaturity);
            // 効率は0割回避のため下限を確保。
            float eff = Mathf.Max(0.01f, shipyardEfficiency);
            float immaturity = 1f - maturity;
            return Mathf.Max(0f, p.baseCommissioningTime * (1f + immaturity) / eff);
        }

        /// <summary>量産による習熟効果（学習曲線・逓増だが上限）。1..上限。</summary>
        public static float SerialProductionLearning(int unitsBuilt)
            => SerialProductionLearning(unitsBuilt, ShipCommissioningParams.Default);

        public static float SerialProductionLearning(int unitsBuilt, ShipCommissioningParams p)
        {
            int units = unitsBuilt < 0 ? 0 : unitsBuilt;
            float max = p.maxSerialLearning;
            // 1隻目＝1.0、隻数で漸近的に max へ（Log/Exp 禁止のため有理飽和を使う）。
            // extra = units-1（追加生産数）。saturate = (rate*extra)/(1+rate*extra) ∈ [0,1)。
            float extra = Mathf.Max(0f, units - 1);
            float x = p.serialLearningRate * extra;
            float saturate = x / (1f + x);
            return Mathf.Clamp(1f + (max - 1f) * saturate, 1f, max);
        }

        /// <summary>就役したか（戦力化が閾値以上）。既定閾値委譲版。</summary>
        public static bool IsCommissioned(float combatReadiness)
            => IsCommissioned(combatReadiness, ShipCommissioningParams.DefaultCommissionThreshold);

        public static bool IsCommissioned(float combatReadiness, float threshold)
        {
            float readiness = Mathf.Clamp01(combatReadiness);
            float th = Mathf.Clamp01(threshold);
            return readiness >= th;
        }
    }
}
