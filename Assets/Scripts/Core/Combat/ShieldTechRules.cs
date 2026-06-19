using UnityEngine;

namespace Ginei
{
    /// <summary>防御スクリーン（中和磁場・対ビーム防御）の調整係数。</summary>
    public readonly struct ShieldTechParams
    {
        /// <summary>発生器出力×場の効率→スクリーン強度の換算係数（出力100×効率1.0×これ＝100強度の素）。</summary>
        public readonly float strengthScale;
        /// <summary>吸収飽和の基準（被弾エネがスクリーン強度×これに達すると吸収が半減点）。</summary>
        public readonly float absorptionReference;
        /// <summary>強度1あたりの過負荷耐性（スクリーン強度×これがダウン閾値）。</summary>
        public readonly float overloadPerStrength;
        /// <summary>強度1・電力予備1あたり・dtあたりの再生量（リチャージ速度の素）。</summary>
        public readonly float rechargePerStrength;
        /// <summary>攻撃へ電力を回したぶん防御配分が痩せる感度（0..1）。</summary>
        public readonly float diversionSensitivity;

        public ShieldTechParams(float strengthScale, float absorptionReference,
                                float overloadPerStrength, float rechargePerStrength, float diversionSensitivity)
        {
            this.strengthScale = Mathf.Max(0f, strengthScale);
            this.absorptionReference = Mathf.Max(0.0001f, absorptionReference);
            this.overloadPerStrength = Mathf.Max(0f, overloadPerStrength);
            this.rechargePerStrength = Mathf.Max(0f, rechargePerStrength);
            this.diversionSensitivity = Mathf.Clamp01(diversionSensitivity);
        }

        /// <summary>既定＝強度換算1.0・吸収基準0.5・過負荷2.0/強度・再生0.0001/強度/予備/dt・配分感度1.0。</summary>
        public static ShieldTechParams Default
            => new ShieldTechParams(1f, 0.5f, 2f, 0.0001f, 1f);
    }

    /// <summary>
    /// 防御スクリーン（shield / 中和磁場・対ビーム防御）の純ロジック。発生器の出力と場の効率で
    /// スクリーン強度が決まり、被弾エネルギーの一部を吸収する。吸収しきれないぶんは装甲へ漏れ
    /// （bleed-through）、被弾が過負荷閾値を超えるとスクリーンは崩壊（collapse）する。崩壊後は
    /// 強度×電力予備で再生（recharge）する。攻撃に電力を回すと防御配分が痩せる（出力配分の争い）。
    /// 銀英伝の中和磁場（艦をビームから守るフィールド）を想定。実際のダメージ適用は Game 層
    /// （<c>FleetStrength.TakeDamage</c> 等）が担い、ここは盤面非依存の plain 引数・乱数なし。
    /// 実効値パターン（基準値非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ShieldTechRules
    {
        /// <summary>
        /// スクリーン強度（≥0）＝発生器出力×場の効率×換算係数。出力は 0..100、効率は 0..1 入力。
        /// 出力か効率が0なら強度0（積）。
        /// </summary>
        public static float ShieldStrength(float generatorOutput, float fieldEfficiency, ShieldTechParams p)
        {
            float output = Mathf.Clamp(generatorOutput, 0f, 100f);
            float eff = Mathf.Clamp01(fieldEfficiency);
            return Mathf.Max(0f, output * eff * p.strengthScale);
        }

        public static float ShieldStrength(float generatorOutput, float fieldEfficiency)
            => ShieldStrength(generatorOutput, fieldEfficiency, ShieldTechParams.Default);

        /// <summary>
        /// 吸収割合（0..1）＝飽和容量÷(飽和容量＋被弾エネ)。容量＝強度×吸収基準。被弾が小さいほど
        /// ほぼ全吸収、被弾が容量に並ぶと0.5、被弾が大きいほど吸収割合は下がる。強度0なら吸収0。
        /// </summary>
        public static float EnergyAbsorption(float incomingEnergy, float shieldStrength, ShieldTechParams p)
        {
            float incoming = Mathf.Max(0f, incomingEnergy);
            float strength = Mathf.Max(0f, shieldStrength);
            float capacity = strength * p.absorptionReference;
            if (capacity <= 0f) return 0f;
            return Mathf.Clamp01(capacity / (capacity + incoming));
        }

        public static float EnergyAbsorption(float incomingEnergy, float shieldStrength)
            => EnergyAbsorption(incomingEnergy, shieldStrength, ShieldTechParams.Default);

        /// <summary>
        /// 過負荷閾値（≥0）＝スクリーン強度×過負荷耐性。被弾エネがこれを超えるとスクリーンは崩壊する。
        /// 強度が高いほど閾値も高い（頑丈）。
        /// </summary>
        public static float OverloadThreshold(float shieldStrength, ShieldTechParams p)
            => Mathf.Max(0f, shieldStrength) * p.overloadPerStrength;

        public static float OverloadThreshold(float shieldStrength)
            => OverloadThreshold(shieldStrength, ShieldTechParams.Default);

        /// <summary>
        /// 崩壊度合い（0..1）＝閾値超過ぶん÷閾値。閾値以下なら0（持ちこたえる）、閾値の2倍以上で1（完全崩壊）。
        /// bool 的に使うなら閾値超過＝崩壊。閾値0なら（被弾があれば）即崩壊1。
        /// </summary>
        public static float ShieldCollapse(float incomingEnergy, float overloadThreshold, ShieldTechParams p)
        {
            float incoming = Mathf.Max(0f, incomingEnergy);
            float threshold = Mathf.Max(0f, overloadThreshold);
            if (threshold <= 0f) return incoming > 0f ? 1f : 0f;
            if (incoming <= threshold) return 0f;
            return Mathf.Clamp01((incoming - threshold) / threshold);
        }

        public static float ShieldCollapse(float incomingEnergy, float overloadThreshold)
            => ShieldCollapse(incomingEnergy, overloadThreshold, ShieldTechParams.Default);

        /// <summary>
        /// 再生速度（≥0）＝強度×電力予備×再生係数×dt。強度が高く電力予備が潤沢なほど速く回復する。
        /// 強度は 0..100、電力予備は 0..100 入力。どちらかが0なら回復しない（積）。
        /// </summary>
        public static float RechargeRate(float shieldStrength, float powerReserve, ShieldTechParams p)
        {
            float strength = Mathf.Clamp(shieldStrength, 0f, 100f);
            float reserve = Mathf.Clamp(powerReserve, 0f, 100f);
            return Mathf.Max(0f, strength * reserve * p.rechargePerStrength);
        }

        public static float RechargeRate(float shieldStrength, float powerReserve)
            => RechargeRate(shieldStrength, powerReserve, ShieldTechParams.Default);

        /// <summary>
        /// 防御への電力配分（0..1）。総電力に対し攻撃と防御がそれぞれの要求で取り合い、攻撃が多いほど
        /// 防御配分が痩せる。素配分＝防御要求÷(攻撃要求＋防御要求)、配分感度で中立0.5へ引き寄せる。
        /// 総電力0や両要求0なら拮抗0.5。
        /// </summary>
        public static float PowerDiversion(float weaponDraw, float shieldDraw, float totalPower, ShieldTechParams p)
        {
            float weapon = Mathf.Max(0f, weaponDraw);
            float shield = Mathf.Max(0f, shieldDraw);
            float total = Mathf.Max(0f, totalPower);
            float demand = weapon + shield;
            if (total <= 0f || demand <= 0f) return 0.5f;
            float raw = shield / demand;
            return Mathf.Clamp01(Mathf.Lerp(0.5f, raw, p.diversionSensitivity));
        }

        public static float PowerDiversion(float weaponDraw, float shieldDraw, float totalPower)
            => PowerDiversion(weaponDraw, shieldDraw, totalPower, ShieldTechParams.Default);

        /// <summary>
        /// 装甲へ抜けるエネルギー（≥0）＝被弾エネ×(1−吸収割合)。吸収しきれなかった漏れ（bleed-through）。
        /// 吸収1.0なら漏れ0、吸収0なら全量が抜ける。
        /// </summary>
        public static float BleedThrough(float incomingEnergy, float energyAbsorption, ShieldTechParams p)
        {
            float incoming = Mathf.Max(0f, incomingEnergy);
            float absorb = Mathf.Clamp01(energyAbsorption);
            return Mathf.Max(0f, incoming * (1f - absorb));
        }

        public static float BleedThrough(float incomingEnergy, float energyAbsorption)
            => BleedThrough(incomingEnergy, energyAbsorption, ShieldTechParams.Default);

        /// <summary>
        /// スクリーンが持ちこたえるか＝強度がしきい以上で、かつ被弾エネが強度以下。強度が薄いか
        /// 被弾が強度を上回ると持たない（false）。
        /// </summary>
        public static bool IsShieldHolding(float shieldStrength, float incomingEnergy, float threshold)
        {
            float strength = Mathf.Max(0f, shieldStrength);
            float incoming = Mathf.Max(0f, incomingEnergy);
            return strength >= threshold && incoming <= strength;
        }

        /// <summary>既定閾値1.0でスクリーン保持を判定（強度が1未満は実質ダウン扱い）。</summary>
        public static bool IsShieldHolding(float shieldStrength, float incomingEnergy)
            => IsShieldHolding(shieldStrength, incomingEnergy, 1f);
    }
}
