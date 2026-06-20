using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ビーム兵器（荷電粒子砲・指向性エネルギー兵器）の調整値（#ビーム兵器）。
    /// 収束効率の上限、距離減衰係数、チャージ・連射、過熱・冷却、出力制限の閾値などをまとめる。
    /// 全フィールドはコンストラクタで Clamp 済み（不正値で破綻しない）。
    /// </summary>
    public readonly struct BeamWeaponParams
    {
        /// <summary>収束効率の上限（1.0＝理想収束。これを超えて出力を増幅しない）。</summary>
        public readonly float maxFocusEfficiency;
        /// <summary>距離減衰係数 k（1/(1+k×距離)。大きいほど遠距離で急減衰＝ビームの拡散）。</summary>
        public readonly float attenuationCoefficient;
        /// <summary>チャージ係数（出力/蓄電容量に掛ける所要秒。大きいほど高出力のチャージが遅い）。</summary>
        public readonly float chargeCoefficient;
        /// <summary>チャージ時間の下限（0除算と無限連射を防ぐ最小秒）。</summary>
        public readonly float minChargeTime;
        /// <summary>過熱の蓄積係数（出力×連続射撃に掛ける）。</summary>
        public readonly float heatCoefficient;
        /// <summary>1ステップで蓄積しうる過熱の上限（暴走防止＝熱レベルは0..1）。</summary>
        public readonly float maxHeatPerStep;
        /// <summary>冷却係数（冷却能力に掛ける熱の除去率0..1）。</summary>
        public readonly float coolingCoefficient;
        /// <summary>出力制限を始める過熱の閾値（これ未満は無制限）。</summary>
        public readonly float throttleThreshold;
        /// <summary>過熱が最大（1.0）のときの最大出力制限率（0..1。1.0＝完全停止）。</summary>
        public readonly float maxThrottle;

        public BeamWeaponParams(
            float maxFocusEfficiency,
            float attenuationCoefficient,
            float chargeCoefficient,
            float minChargeTime,
            float heatCoefficient,
            float maxHeatPerStep,
            float coolingCoefficient,
            float throttleThreshold,
            float maxThrottle)
        {
            this.maxFocusEfficiency = Mathf.Clamp(maxFocusEfficiency, 0.01f, 1f);
            this.attenuationCoefficient = Mathf.Max(0f, attenuationCoefficient);
            this.chargeCoefficient = Mathf.Max(0f, chargeCoefficient);
            this.minChargeTime = Mathf.Max(0.001f, minChargeTime);
            this.heatCoefficient = Mathf.Max(0f, heatCoefficient);
            this.maxHeatPerStep = Mathf.Clamp01(maxHeatPerStep);
            this.coolingCoefficient = Mathf.Clamp01(coolingCoefficient);
            this.throttleThreshold = Mathf.Clamp01(throttleThreshold);
            this.maxThrottle = Mathf.Clamp01(maxThrottle);
        }

        /// <summary>既定：収束上限1.0・減衰k=0.01・チャージ係数0.5/下限0.1秒・熱係数0.5/上限0.5・冷却0.5・制限閾値0.7/最大1.0。</summary>
        public static BeamWeaponParams Default => new BeamWeaponParams(
            DefaultMaxFocusEfficiency,
            DefaultAttenuationCoefficient,
            DefaultChargeCoefficient,
            DefaultMinChargeTime,
            DefaultHeatCoefficient,
            DefaultMaxHeatPerStep,
            DefaultCoolingCoefficient,
            DefaultThrottleThreshold,
            DefaultMaxThrottle);

        public const float DefaultMaxFocusEfficiency = 1.0f;
        public const float DefaultAttenuationCoefficient = 0.01f;
        public const float DefaultChargeCoefficient = 0.5f;
        public const float DefaultMinChargeTime = 0.1f;
        public const float DefaultHeatCoefficient = 0.5f;
        public const float DefaultMaxHeatPerStep = 0.5f;
        public const float DefaultCoolingCoefficient = 0.5f;
        public const float DefaultThrottleThreshold = 0.7f;
        public const float DefaultMaxThrottle = 1.0f;
    }

    /// <summary>
    /// ビーム兵器（荷電粒子砲・レーザー＝指向性エネルギー兵器）の純ロジック（#ビーム兵器）。銀英伝の主砲射撃を想定。
    /// 炉出力×収束効率で<b>ビーム出力</b>を作り、距離による<b>拡散減衰</b>で着弾までに目減りさせる。
    /// 高出力ほど<b>チャージ</b>が長く<b>連射</b>が落ち、連続射撃は<b>過熱</b>を蓄積、過熱は<b>冷却</b>で下げる。
    /// 過熱が閾値を超えると<b>出力制限</b>（オーバーヒート抑制）が掛かり、最終的な<b>着弾エネルギー</b>を決める。
    /// 決定論（乱数なし）・入力は全て Clamp・実効値パターン（基準値非破壊）。Mathf.Log/Exp は使わず減衰は 1/(1+kx)。
    /// </summary>
    public static class BeamWeaponRules
    {
        // ── ビーム出力 ────────────────────────────────────────────

        /// <summary>既定パラメータでビーム出力を返す。</summary>
        public static float BeamPower(float reactorOutput, float focusEfficiency)
            => BeamPower(reactorOutput, focusEfficiency, BeamWeaponParams.Default);

        /// <summary>
        /// 炉出力×収束効率でビーム出力を返す。`power = max(0,出力) × clamp(収束, 0, maxFocus)`。
        /// 収束効率が高いほどエネルギーが束ねられ出力が上がる（上限 maxFocusEfficiency）。
        /// </summary>
        public static float BeamPower(float reactorOutput, float focusEfficiency, BeamWeaponParams p)
        {
            float output = Mathf.Max(0f, reactorOutput);
            float focus = Mathf.Clamp(focusEfficiency, 0f, p.maxFocusEfficiency);
            return output * focus;
        }

        // ── 距離減衰（拡散） ──────────────────────────────────────

        /// <summary>既定パラメータで距離減衰後の出力を返す。</summary>
        public static float RangeAttenuation(float beamPower, float distance)
            => RangeAttenuation(beamPower, distance, BeamWeaponParams.Default);

        /// <summary>
        /// 距離によるビームの拡散減衰。`power / (1 + k×距離)`（Log/Exp 不使用）。
        /// 距離0で減衰なし、遠いほど 1/(1+k×距離) で目減りする。
        /// </summary>
        public static float RangeAttenuation(float beamPower, float distance, BeamWeaponParams p)
        {
            float power = Mathf.Max(0f, beamPower);
            float d = Mathf.Max(0f, distance);
            float denom = 1f + p.attenuationCoefficient * d;
            return power / denom;
        }

        // ── チャージ・連射 ────────────────────────────────────────

        /// <summary>既定パラメータでチャージ所要時間を返す。</summary>
        public static float ChargeTime(float beamPower, float capacitorCapacity)
            => ChargeTime(beamPower, capacitorCapacity, BeamWeaponParams.Default);

        /// <summary>
        /// 出力/蓄電容量でチャージ所要時間（秒）を返す。`max(minCharge, coeff×出力/容量)`。
        /// 高出力ほど・蓄電容量が小さいほど長い（容量0は下限へ）。
        /// </summary>
        public static float ChargeTime(float beamPower, float capacitorCapacity, BeamWeaponParams p)
        {
            float power = Mathf.Max(0f, beamPower);
            float cap = Mathf.Max(0f, capacitorCapacity);
            if (cap <= 0f) return Mathf.Max(p.minChargeTime, p.chargeCoefficient > 0f && power > 0f ? float.MaxValue : p.minChargeTime);
            float t = p.chargeCoefficient * power / cap;
            return Mathf.Max(p.minChargeTime, t);
        }

        /// <summary>既定パラメータで連射レートを返す。</summary>
        public static float RateOfFire(float chargeTime)
            => RateOfFire(chargeTime, BeamWeaponParams.Default);

        /// <summary>
        /// チャージ時間の逆数的な連射レート（発/秒）。`1 / max(minCharge, チャージ時間)`。
        /// チャージが長い（＝高出力）ほど連射が落ちる（最大は 1/minChargeTime）。
        /// </summary>
        public static float RateOfFire(float chargeTime, BeamWeaponParams p)
        {
            float t = Mathf.Max(p.minChargeTime, chargeTime);
            return 1f / t;
        }

        // ── 過熱・冷却 ────────────────────────────────────────────

        /// <summary>既定パラメータで過熱の蓄積量を返す。</summary>
        public static float HeatBuildup(float beamPower, float sustainedFire)
            => HeatBuildup(beamPower, sustainedFire, BeamWeaponParams.Default);

        /// <summary>
        /// 出力×連続射撃量で過熱の蓄積量（0..maxHeatPerStep）を返す。`clamp(coeff×出力×連射, 0, maxHeatPerStep)`。
        /// 出力が高く撃ち続けるほど熱が溜まる。
        /// </summary>
        public static float HeatBuildup(float beamPower, float sustainedFire, BeamWeaponParams p)
        {
            float power = Mathf.Max(0f, beamPower);
            float fire = Mathf.Max(0f, sustainedFire);
            float heat = p.heatCoefficient * power * fire;
            return Mathf.Clamp(heat, 0f, p.maxHeatPerStep);
        }

        /// <summary>既定パラメータで冷却後の残存熱を返す。</summary>
        public static float CoolingRecovery(float heatLevel, float coolingCapacity)
            => CoolingRecovery(heatLevel, coolingCapacity, BeamWeaponParams.Default);

        /// <summary>
        /// 冷却能力で熱を下げた残存熱（0..1）を返す。`熱 × (1 - clamp01(coeff×冷却能力))`。
        /// 冷却能力が高いほど残存熱が小さくなる（冷却率は最大1.0＝完全放熱）。
        /// </summary>
        public static float CoolingRecovery(float heatLevel, float coolingCapacity, BeamWeaponParams p)
        {
            float heat = Mathf.Clamp01(heatLevel);
            float cap = Mathf.Max(0f, coolingCapacity);
            float coolRate = Mathf.Clamp01(p.coolingCoefficient * cap);
            return Mathf.Clamp01(heat * (1f - coolRate));
        }

        // ── 出力制限（オーバーヒート抑制） ──────────────────────────

        /// <summary>既定パラメータで出力制限率を返す。</summary>
        public static float ThermalThrottle(float heatLevel)
            => ThermalThrottle(heatLevel, BeamWeaponParams.Default);

        /// <summary>
        /// 過熱が閾値を超えると出力制限率（0..maxThrottle）を返す。閾値未満は0（無制限）。
        /// 閾値〜1.0 を 0〜maxThrottle へ線形補間（過熱が深いほど強く絞る）。
        /// </summary>
        public static float ThermalThrottle(float heatLevel, BeamWeaponParams p)
        {
            float heat = Mathf.Clamp01(heatLevel);
            if (heat <= p.throttleThreshold) return 0f;
            float span = 1f - p.throttleThreshold;
            if (span <= 0f) return p.maxThrottle; // 閾値1.0なら超過＝即最大制限
            float t = (heat - p.throttleThreshold) / span;
            return Mathf.Clamp(Mathf.Lerp(0f, p.maxThrottle, t), 0f, p.maxThrottle);
        }

        // ── 着弾エネルギー ────────────────────────────────────────

        /// <summary>既定パラメータで着弾エネルギーを返す。</summary>
        public static float DeliveredEnergy(float beamPower, float rangeAttenuation, float thermalThrottle)
            => DeliveredEnergy(beamPower, rangeAttenuation, thermalThrottle, BeamWeaponParams.Default);

        /// <summary>
        /// 着弾エネルギー（0..1）。`減衰後出力/出力 × (1-出力制限)` を 0..1 に正規化して返す。
        /// 出力に対する着弾の到達率（距離減衰）に、過熱による出力制限を掛けた最終効率。
        /// 出力0なら0。
        /// </summary>
        public static float DeliveredEnergy(float beamPower, float rangeAttenuation, float thermalThrottle, BeamWeaponParams p)
        {
            float power = Mathf.Max(0f, beamPower);
            if (power <= 0f) return 0f;
            float arrived = Mathf.Clamp(rangeAttenuation, 0f, power); // 着弾出力は元出力を超えない
            float reach = arrived / power; // 距離による到達率 0..1
            float throttle = Mathf.Clamp01(thermalThrottle);
            return Mathf.Clamp01(reach * (1f - throttle));
        }
    }
}
