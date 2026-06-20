using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦闘ストレス＝連戦・長時間戦闘の累積疲労（心理的消耗）のパラメータ。
    /// readonly struct・全フィールド ctor でクランプ・盤面非依存の plain データ。
    /// </summary>
    public readonly struct CombatStressParams
    {
        /// <summary>ストレス上限（これを基準に各種しきい値を割合で扱う）。</summary>
        public readonly float maxStress;
        /// <summary>交戦強度1・1秒あたりの基礎蓄積量。</summary>
        public readonly float accumulationRate;
        /// <summary>休息品質1・1秒あたりの基礎回復量。</summary>
        public readonly float recoveryRate;
        /// <summary>能力劣化の最大カット率（高ストレスでこれだけ命中/判断が落ちる・0..1）。</summary>
        public readonly float maxPerformanceLoss;
        /// <summary>判断ミスの最大確率（高ストレスでこれだけ判断を誤る・0..1）。</summary>
        public readonly float maxJudgmentError;
        /// <summary>ストレスが士気を削る最大割合（士気基準値に対する係数・0..1）。</summary>
        public readonly float maxMoralePenalty;
        /// <summary>劣化曲線の指数（>1で限界近くまで耐えてから急落、<1で早く効く）。</summary>
        public readonly float degradationExponent;

        public CombatStressParams(
            float maxStress,
            float accumulationRate,
            float recoveryRate,
            float maxPerformanceLoss,
            float maxJudgmentError,
            float maxMoralePenalty,
            float degradationExponent)
        {
            this.maxStress = Mathf.Max(1f, maxStress);
            this.accumulationRate = Mathf.Max(0f, accumulationRate);
            this.recoveryRate = Mathf.Max(0f, recoveryRate);
            this.maxPerformanceLoss = Mathf.Clamp01(maxPerformanceLoss);
            this.maxJudgmentError = Mathf.Clamp01(maxJudgmentError);
            this.maxMoralePenalty = Mathf.Clamp01(maxMoralePenalty);
            this.degradationExponent = Mathf.Clamp(degradationExponent, 0.1f, 8f);
        }

        /// <summary>既定値。maxStress=100・蓄積3/s・回復5/s・最大カット0.5・判断ミス0.6・士気減0.4・指数1.5。</summary>
        public static CombatStressParams Default => new CombatStressParams(
            maxStress: 100f,
            accumulationRate: 3f,
            recoveryRate: 5f,
            maxPerformanceLoss: 0.5f,
            maxJudgmentError: 0.6f,
            maxMoralePenalty: 0.4f,
            degradationExponent: 1.5f);
    }

    /// <summary>
    /// 戦闘ストレス（連戦・長時間戦闘の累積疲労）の純ロジック。すべて static・純関数。
    /// 兵は戦闘でストレスを溜め、命中・判断・士気が落ち、限界を超えると戦闘不能（消耗）。
    /// 休息で回復し、歴戦兵（練度）はストレス耐性が高い。実効値パターン（基準値は非破壊）。
    ///
    /// 責務分担（read-only 相当でストレス係数を算出するだけ）：
    /// - <see cref="FleetSustainment"/>（継戦・兵站自立）／<see cref="FleetMorale"/>（士気の実体）は
    ///   ここでは状態を持たず、本ルールが返す倍率・ペナルティを乗算/減算する側。
    /// - 既存に物理的疲労（FatigueRules 等）がある場合は責務を分け、本ルールは
    ///   「連戦の心理的累積疲労（戦闘ストレス反応）」に特化する。
    /// 盤面非依存の plain 引数のみ・乱数なし（必要箇所は float roll を受ける）。
    /// </summary>
    public static class CombatStressRules
    {
        // --- 蓄積・回復 ---------------------------------------------------

        /// <summary>戦闘でストレスが溜まる（交戦強度0..1・dt秒ぶん）。0..maxStress にクランプ。</summary>
        public static float StressAccumulation(float currentStress, float combatIntensity, float dt, CombatStressParams p)
        {
            float s = Mathf.Clamp(currentStress, 0f, p.maxStress);
            float intensity = Mathf.Clamp01(combatIntensity);
            float t = Mathf.Max(0f, dt);
            float next = s + p.accumulationRate * intensity * t;
            return Mathf.Clamp(next, 0f, p.maxStress);
        }

        public static float StressAccumulation(float currentStress, float combatIntensity, float dt)
            => StressAccumulation(currentStress, combatIntensity, dt, CombatStressParams.Default);

        /// <summary>休息で回復する（休息品質0..1・dt秒ぶん）。0..maxStress にクランプ。</summary>
        public static float StressRecovery(float currentStress, float restQuality, float dt, CombatStressParams p)
        {
            float s = Mathf.Clamp(currentStress, 0f, p.maxStress);
            float quality = Mathf.Clamp01(restQuality);
            float t = Mathf.Max(0f, dt);
            float next = s - p.recoveryRate * quality * t;
            return Mathf.Clamp(next, 0f, p.maxStress);
        }

        public static float StressRecovery(float currentStress, float restQuality, float dt)
            => StressRecovery(currentStress, restQuality, dt, CombatStressParams.Default);

        // --- 能力・判断への影響 -------------------------------------------

        /// <summary>
        /// ストレスで能力（命中/判断）が落ちる実効倍率（1.0=無傷→1-maxPerformanceLoss=限界）。
        /// 基準値非破壊＝呼び出し側がこの倍率を乗算する。
        /// </summary>
        public static float PerformanceDegradation(float stress, CombatStressParams p)
        {
            float ratio = Mathf.Clamp01(stress / p.maxStress);
            float curve = Mathf.Pow(ratio, p.degradationExponent);
            float factor = 1f - p.maxPerformanceLoss * curve;
            return Mathf.Clamp(factor, 1f - p.maxPerformanceLoss, 1f);
        }

        public static float PerformanceDegradation(float stress)
            => PerformanceDegradation(stress, CombatStressParams.Default);

        /// <summary>高ストレスで判断ミスが増える確率（0..maxJudgmentError）。乱数なし＝確率値を返すだけ。</summary>
        public static float JudgmentImpairment(float stress, CombatStressParams p)
        {
            float ratio = Mathf.Clamp01(stress / p.maxStress);
            float curve = Mathf.Pow(ratio, p.degradationExponent);
            return Mathf.Clamp(p.maxJudgmentError * curve, 0f, p.maxJudgmentError);
        }

        public static float JudgmentImpairment(float stress)
            => JudgmentImpairment(stress, CombatStressParams.Default);

        /// <summary>累積ストレスが士気を削る割合（0..maxMoralePenalty）。呼び出し側が士気から減じる。</summary>
        public static float MoralePenaltyFromStress(float stress, CombatStressParams p)
        {
            float ratio = Mathf.Clamp01(stress / p.maxStress);
            return Mathf.Clamp(p.maxMoralePenalty * ratio, 0f, p.maxMoralePenalty);
        }

        public static float MoralePenaltyFromStress(float stress)
            => MoralePenaltyFromStress(stress, CombatStressParams.Default);

        // --- 練度（耐性） -------------------------------------------------

        /// <summary>
        /// 歴戦兵はストレス耐性が高い（練度0..1→耐性倍率0.5..1.0）。
        /// 限界しきい値に乗じて「より高いストレスまで耐える」を表す。
        /// </summary>
        public static float VeterancyResilience(float veterancy)
        {
            float v = Mathf.Clamp01(veterancy);
            return Mathf.Lerp(0.5f, 1f, v);
        }

        // --- 限界・消耗判定 -----------------------------------------------

        /// <summary>
        /// 限界（戦闘ストレス反応＝戦闘不能）に達したか。
        /// resilience（<see cref="VeterancyResilience"/> 由来0.5..1.0）が高いほど耐えるしきい値が上がる。
        /// </summary>
        public static bool BreakingPoint(float stress, float resilience, CombatStressParams p)
        {
            float s = Mathf.Clamp(stress, 0f, p.maxStress);
            float res = Mathf.Clamp(resilience, 0.5f, 1f);
            // 基礎は maxStress の9割で限界、耐性ぶん上限へ近づく（最大は maxStress）。
            float threshold = Mathf.Min(p.maxStress, p.maxStress * 0.9f * res + p.maxStress * 0.1f);
            return s >= threshold;
        }

        public static bool BreakingPoint(float stress, float resilience)
            => BreakingPoint(stress, resilience, CombatStressParams.Default);

        /// <summary>戦闘疲労で戦力にならない状態か（ストレスが割合しきい値0..1以上）。</summary>
        public static bool IsCombatExhausted(float stress, float threshold, CombatStressParams p)
        {
            float s = Mathf.Clamp(stress, 0f, p.maxStress);
            float t = Mathf.Clamp01(threshold) * p.maxStress;
            return s >= t;
        }

        public static bool IsCombatExhausted(float stress, float threshold)
            => IsCombatExhausted(stress, threshold, CombatStressParams.Default);
    }
}
