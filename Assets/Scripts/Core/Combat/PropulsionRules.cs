using UnityEngine;

namespace Ginei
{
    /// <summary>艦の推進系（核融合スラスター・推力）の運動性能の調整値。</summary>
    public readonly struct PropulsionParams
    {
        /// <summary>質量の床（0除算回避・極小質量の暴走防止）。推力重量比の分母はこれ未満にならない。</summary>
        public readonly float massFloor;
        /// <summary>推力重量比→加速度の係数（t2w 1.0でこの加速度）。</summary>
        public readonly float accelScale;
        /// <summary>推力重量比→最大速度の基準係数（抵抗0でこの速度）。</summary>
        public readonly float velocityScale;
        /// <summary>推力重量比→旋回性能の基準係数（質量0でこの旋回率）。</summary>
        public readonly float turnScale;
        /// <summary>旋回性能を鈍らせる質量ペナルティ係数（大型ほど鈍重＝分母 1+mass×これ）。</summary>
        public readonly float massTurnPenalty;
        /// <summary>推力×スロットル→燃料消費率の係数。</summary>
        public readonly float fuelScale;
        /// <summary>最適スロットルから離れるほど燃費が悪化する勾配（0で常に最良）。</summary>
        public readonly float inefficiencySlope;
        /// <summary>巡航効率の床（どれだけ外しても最低この効率は残る）。</summary>
        public readonly float minEfficiency;
        /// <summary>戦闘加速の応答の上限（推力重量比×健全性をこの値で頭打ち＝暴走防止）。</summary>
        public readonly float maxCombatResponse;
        /// <summary>機敏とみなす旋回率の既定閾値。</summary>
        public readonly float agileThreshold;

        public PropulsionParams(float massFloor, float accelScale, float velocityScale, float turnScale,
                                float massTurnPenalty, float fuelScale, float inefficiencySlope,
                                float minEfficiency, float maxCombatResponse, float agileThreshold)
        {
            this.massFloor = Mathf.Max(0.01f, massFloor);
            this.accelScale = Mathf.Max(0f, accelScale);
            this.velocityScale = Mathf.Max(0f, velocityScale);
            this.turnScale = Mathf.Max(0f, turnScale);
            this.massTurnPenalty = Mathf.Max(0f, massTurnPenalty);
            this.fuelScale = Mathf.Max(0f, fuelScale);
            this.inefficiencySlope = Mathf.Max(0f, inefficiencySlope);
            this.minEfficiency = Mathf.Clamp01(minEfficiency);
            this.maxCombatResponse = Mathf.Max(0f, maxCombatResponse);
            this.agileThreshold = Mathf.Max(0f, agileThreshold);
        }

        /// <summary>
        /// 既定＝質量床1・加速係数1.0・速度係数10・旋回係数2.0・質量旋回ペナルティ1.0・
        /// 燃料係数0.5・燃費悪化勾配1.0・効率床0.2・戦闘応答上限3.0・機敏閾値1.0。
        /// </summary>
        public static PropulsionParams Default =>
            new PropulsionParams(1f, 1f, 10f, 2f, 1f, 0.5f, 1f, 0.2f, 3f, 1f);

        public const float DefaultMassFloor = 1f;
        public const float DefaultAgileThreshold = 1f;
    }

    /// <summary>
    /// 艦の推進系（核融合スラスター・推力）の物理的運動性能の純ロジック。
    /// 推力重量比による加速・最大速度・旋回性能、燃料消費率、機関の応答性（巡航と戦闘加速）、
    /// 大型艦の鈍重さをモデル化する。<see cref="FleetMovement.GetMobilityFactor"/>（提督機動・士気・交戦で
    /// 補正した実効速度）とは<b>別系統</b>＝こちらは艦そのものの物理的推進性能（機関出力と質量の素性）。
    /// 倍率・物理量は基準値に掛けて使う想定（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PropulsionRules
    {
        /// <summary>
        /// 推力重量比（推力/質量）。質量は massFloor で床止め（0除算・極小質量の暴走を防ぐ）。
        /// 大きいほど機敏＝小型高出力艦が高く、大型艦は低い。
        /// </summary>
        public static float ThrustToWeight(float engineThrust, float shipMass, PropulsionParams p)
        {
            float thrust = Mathf.Max(0f, engineThrust);
            float mass = Mathf.Max(p.massFloor, shipMass);
            return thrust / mass;
        }

        public static float ThrustToWeight(float engineThrust, float shipMass)
            => ThrustToWeight(engineThrust, shipMass, PropulsionParams.Default);

        /// <summary>加速度＝推力重量比×accelScale。推力重量比に比例。</summary>
        public static float Acceleration(float thrustToWeight, PropulsionParams p)
        {
            return Mathf.Max(0f, thrustToWeight) * p.accelScale;
        }

        public static float Acceleration(float thrustToWeight)
            => Acceleration(thrustToWeight, PropulsionParams.Default);

        /// <summary>
        /// 最大速度＝(推力重量比×velocityScale)/(1+抵抗)。抵抗（dragFactor）が高いほど頭打ちが低い。
        /// </summary>
        public static float MaxVelocity(float thrustToWeight, float dragFactor, PropulsionParams p)
        {
            float t2w = Mathf.Max(0f, thrustToWeight);
            float drag = Mathf.Max(0f, dragFactor);
            return (t2w * p.velocityScale) / (1f + drag);
        }

        public static float MaxVelocity(float thrustToWeight, float dragFactor)
            => MaxVelocity(thrustToWeight, dragFactor, PropulsionParams.Default);

        /// <summary>
        /// 旋回性能＝(推力重量比×turnScale)/(1+質量×massTurnPenalty)。
        /// 大型（高質量）ほど分母が膨らみ鈍重になる。
        /// </summary>
        public static float TurnRate(float thrustToWeight, float shipMass, PropulsionParams p)
        {
            float t2w = Mathf.Max(0f, thrustToWeight);
            float mass = Mathf.Max(0f, shipMass);
            return (t2w * p.turnScale) / (1f + mass * p.massTurnPenalty);
        }

        public static float TurnRate(float thrustToWeight, float shipMass)
            => TurnRate(thrustToWeight, shipMass, PropulsionParams.Default);

        /// <summary>
        /// 燃料消費率＝推力×スロットル（0..1）×fuelScale。全開・高出力ほど食う。
        /// </summary>
        public static float FuelConsumption(float engineThrust, float throttle, PropulsionParams p)
        {
            float thrust = Mathf.Max(0f, engineThrust);
            float thr = Mathf.Clamp01(throttle);
            return thrust * thr * p.fuelScale;
        }

        public static float FuelConsumption(float engineThrust, float throttle)
            => FuelConsumption(engineThrust, throttle, PropulsionParams.Default);

        /// <summary>
        /// 巡航効率（0..1）＝最適スロットル（optimalThrottle）から離れるほど悪化。
        /// 1−|throttle−optimal|×inefficiencySlope を効率床（minEfficiency）で下支え。
        /// 最適点で1.0、外すほど低下＝経済巡航の意味づけ。
        /// </summary>
        public static float CruiseEfficiency(float throttle, float optimalThrottle, PropulsionParams p)
        {
            float thr = Mathf.Clamp01(throttle);
            float opt = Mathf.Clamp01(optimalThrottle);
            float deviation = Mathf.Abs(thr - opt);
            float eff = 1f - deviation * p.inefficiencySlope;
            return Mathf.Clamp(eff, p.minEfficiency, 1f);
        }

        public static float CruiseEfficiency(float throttle, float optimalThrottle)
            => CruiseEfficiency(throttle, optimalThrottle, PropulsionParams.Default);

        /// <summary>
        /// 戦闘加速の応答＝推力重量比×機関の健全性（engineHealth 0..1）。被弾で機関が傷むと応答が鈍る。
        /// 上限 maxCombatResponse で頭打ち（暴走防止）。急加速・離脱の素性。
        /// </summary>
        public static float CombatThrustResponse(float thrustToWeight, float engineHealth, PropulsionParams p)
        {
            float t2w = Mathf.Max(0f, thrustToWeight);
            float health = Mathf.Clamp01(engineHealth);
            return Mathf.Min(t2w * health, p.maxCombatResponse);
        }

        public static float CombatThrustResponse(float thrustToWeight, float engineHealth)
            => CombatThrustResponse(thrustToWeight, engineHealth, PropulsionParams.Default);

        /// <summary>旋回率が閾値以上なら機敏な艦＝true。</summary>
        public static bool IsAgile(float turnRate, float threshold)
        {
            return Mathf.Max(0f, turnRate) >= Mathf.Max(0f, threshold);
        }

        /// <summary>既定閾値（<see cref="PropulsionParams.agileThreshold"/>）での機敏判定。</summary>
        public static bool IsAgile(float turnRate, PropulsionParams p)
            => IsAgile(turnRate, p.agileThreshold);

        public static bool IsAgile(float turnRate)
            => IsAgile(turnRate, PropulsionParams.DefaultAgileThreshold);
    }
}
