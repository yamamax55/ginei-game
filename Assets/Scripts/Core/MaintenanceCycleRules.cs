using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 整備サイクル（艦の稼働率・平時整備）のパラメータ。
    /// readonly struct・全フィールド ctor でクランプ・盤面非依存の plain データ。
    /// </summary>
    public readonly struct MaintenanceCycleParams
    {
        /// <summary>摩耗の上限（これを基準に稼働率・故障率を割合で扱う）。</summary>
        public readonly float maxWear;
        /// <summary>稼働強度1・1時間あたりの基礎摩耗蓄積量。</summary>
        public readonly float wearRate;
        /// <summary>整備努力1・dt秒あたりの基礎摩耗減少量。</summary>
        public readonly float repairRate;
        /// <summary>摩耗最大時の最大故障率（0..1）。</summary>
        public readonly float maxFailureRate;
        /// <summary>故障率曲線の指数（>1で限界近くまで耐えてから急増、<1で早く効く）。</summary>
        public readonly float failureExponent;
        /// <summary>整備規模1あたりの基礎ドック占有時間（時間）。</summary>
        public readonly float baseDowntime;
        /// <summary>整備先送り1回あたりの追加故障係数（0..1）。</summary>
        public readonly float deferralPenaltyPerSkip;

        public MaintenanceCycleParams(
            float maxWear,
            float wearRate,
            float repairRate,
            float maxFailureRate,
            float failureExponent,
            float baseDowntime,
            float deferralPenaltyPerSkip)
        {
            this.maxWear = Mathf.Max(1f, maxWear);
            this.wearRate = Mathf.Max(0f, wearRate);
            this.repairRate = Mathf.Max(0f, repairRate);
            this.maxFailureRate = Mathf.Clamp01(maxFailureRate);
            this.failureExponent = Mathf.Clamp(failureExponent, 0.1f, 8f);
            this.baseDowntime = Mathf.Max(0f, baseDowntime);
            this.deferralPenaltyPerSkip = Mathf.Clamp01(deferralPenaltyPerSkip);
        }

        /// <summary>既定値。摩耗上限100・稼働摩耗1/h・整備10/dt・最大故障0.6・指数2・基礎ドック24h・先送り0.1/回。</summary>
        public static MaintenanceCycleParams Default => new MaintenanceCycleParams(
            maxWear: 100f,
            wearRate: 1f,
            repairRate: 10f,
            maxFailureRate: 0.6f,
            failureExponent: 2f,
            baseDowntime: 24f,
            deferralPenaltyPerSkip: 0.1f);
    }

    /// <summary>
    /// 整備サイクル（艦艇の稼働率・平時整備）の純ロジック。すべて static・純関数。
    /// 艦は連続稼働で摩耗し、定期整備で稼働率を保つ。整備を怠ると故障率が上がり、
    /// 戦力にならない艦が増える。整備中の艦は戦線を離れる。実効値パターン（基準値は非破壊）。
    ///
    /// 責務分担：
    /// - <see cref="RepairRules"/>（戦闘損傷の修理・存在する場合）とは別＝本ルールは
    ///   「平時の整備による稼働率／摩耗」に特化する。
    /// - <see cref="FleetSustainment"/>（Game 層・継戦/兵站の実体）とも別＝本ルールは状態を持たず、
    ///   返す稼働率・可用戦力を呼び出し側が消費する。
    /// 盤面非依存の plain 引数のみ・乱数なし（Mathf のみ・LINQ/Log/Exp 不使用）。
    /// </summary>
    public static class MaintenanceCycleRules
    {
        // --- 摩耗の蓄積・減少 ---------------------------------------------

        /// <summary>稼働で摩耗が溜まる（稼働時間×稼働強度0..1）。0..maxWear にクランプ。</summary>
        public static float WearAccumulation(float operatingHours, float operatingIntensity, MaintenanceCycleParams p)
        {
            float hours = Mathf.Max(0f, operatingHours);
            float intensity = Mathf.Clamp01(operatingIntensity);
            float wear = p.wearRate * hours * intensity;
            return Mathf.Clamp(wear, 0f, p.maxWear);
        }

        public static float WearAccumulation(float operatingHours, float operatingIntensity)
            => WearAccumulation(operatingHours, operatingIntensity, MaintenanceCycleParams.Default);

        /// <summary>整備で摩耗が減る（整備努力0..1・dt秒ぶん）。現在の摩耗から減じて返す。0..maxWear。</summary>
        public static float WearReduction(float wear, float maintenanceEffort, float dt, MaintenanceCycleParams p)
        {
            float w = Mathf.Clamp(wear, 0f, p.maxWear);
            float effort = Mathf.Clamp01(maintenanceEffort);
            float t = Mathf.Max(0f, dt);
            float next = w - p.repairRate * effort * t;
            return Mathf.Clamp(next, 0f, p.maxWear);
        }

        public static float WearReduction(float wear, float maintenanceEffort, float dt)
            => WearReduction(wear, maintenanceEffort, dt, MaintenanceCycleParams.Default);

        // --- 稼働率・故障率 -----------------------------------------------

        /// <summary>
        /// 稼働率（0..1）。摩耗で下がり、整備品質0..1 で回復する。
        /// 摩耗が無く整備が万全なら 1.0、摩耗が上限で整備皆無なら 0 に近づく。
        /// </summary>
        public static float OperationalReadiness(float wear, float maintenanceQuality, MaintenanceCycleParams p)
        {
            float ratio = Mathf.Clamp01(wear / p.maxWear);
            float quality = Mathf.Clamp01(maintenanceQuality);
            // 摩耗ぶん稼働率が落ち、整備品質ぶん回復する。
            float baseReadiness = 1f - ratio;
            float readiness = baseReadiness + (1f - baseReadiness) * quality;
            return Mathf.Clamp01(readiness);
        }

        public static float OperationalReadiness(float wear, float maintenanceQuality)
            => OperationalReadiness(wear, maintenanceQuality, MaintenanceCycleParams.Default);

        /// <summary>摩耗が高いと故障が増える（0..maxFailureRate）。乱数なし＝確率値を返すだけ。</summary>
        public static float FailureRate(float wear, MaintenanceCycleParams p)
        {
            float ratio = Mathf.Clamp01(wear / p.maxWear);
            float curve = Mathf.Pow(ratio, p.failureExponent);
            return Mathf.Clamp(p.maxFailureRate * curve, 0f, p.maxFailureRate);
        }

        public static float FailureRate(float wear)
            => FailureRate(wear, MaintenanceCycleParams.Default);

        // --- ドック占有・先送り -------------------------------------------

        /// <summary>
        /// 整備で戦線を離れる時間（時間）。整備規模0..1 に基礎ドック時間を掛け、
        /// ドック能力1.. が大きいほど短縮する。
        /// </summary>
        public static float MaintenanceDowntime(float maintenanceScope, float dockCapacity, MaintenanceCycleParams p)
        {
            float scope = Mathf.Clamp01(maintenanceScope);
            float capacity = Mathf.Max(1f, dockCapacity);
            return Mathf.Max(0f, p.baseDowntime * scope / capacity);
        }

        public static float MaintenanceDowntime(float maintenanceScope, float dockCapacity)
            => MaintenanceDowntime(maintenanceScope, dockCapacity, MaintenanceCycleParams.Default);

        /// <summary>
        /// 整備の先送りが累積故障を招く追加故障率（0..1）。先送り回数ぶん線形に増える。
        /// 呼び出し側が <see cref="FailureRate"/> に加算する想定。
        /// </summary>
        public static float DeferredMaintenancePenalty(int skippedMaintenance, MaintenanceCycleParams p)
        {
            int skips = skippedMaintenance < 0 ? 0 : skippedMaintenance;
            return Mathf.Clamp01(p.deferralPenaltyPerSkip * skips);
        }

        public static float DeferredMaintenancePenalty(int skippedMaintenance)
            => DeferredMaintenancePenalty(skippedMaintenance, MaintenanceCycleParams.Default);

        // --- 艦隊集計・出撃可否 -------------------------------------------

        /// <summary>艦隊全体で実働できる戦力（稼働率0..1×艦隊規模）。負はクランプ。</summary>
        public static float FleetAvailability(float operationalReadiness, float fleetSize)
        {
            float readiness = Mathf.Clamp01(operationalReadiness);
            float size = Mathf.Max(0f, fleetSize);
            return readiness * size;
        }

        /// <summary>戦闘に出せる状態か（稼働率がしきい値0..1 以上）。</summary>
        public static bool IsCombatServiceable(float operationalReadiness, float threshold)
        {
            float readiness = Mathf.Clamp01(operationalReadiness);
            float t = Mathf.Clamp01(threshold);
            return readiness >= t;
        }
    }
}
