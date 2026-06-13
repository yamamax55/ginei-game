using UnityEngine;

namespace Ginei
{
    /// <summary>社会保護制度の内生的成長の調整係数（POLA-5 #1602・ポランニー『大転換』二重運動の保護側）。</summary>
    public readonly struct SocialProtectionParams
    {
        /// <summary>市場圧力×不安定化→保護需要の感度（商品化のストレスがどれだけ政治的需要に変わるか）。</summary>
        public readonly float demandSensitivity;
        /// <summary>保護制度が需要へ向けて積み上がる基礎速度（年あたり）。</summary>
        public readonly float buildupRate;
        /// <summary>危機（需要が高い）ほど積み上がりが速くなる増幅（需要に比例して加速）。</summary>
        public readonly float crisisAcceleration;
        /// <summary>ラチェット抵抗の基礎強度（既存保護そのものが持つ撤廃への粘り）。</summary>
        public readonly float ratchetBase;
        /// <summary>受益者が撤廃の盾になる強さ（vestedBeneficiaries→抵抗の上乗せ）。</summary>
        public readonly float vestedShield;
        /// <summary>保護が縮む基礎速度（年あたり・抵抗を上回る政治力の分だけ）。</summary>
        public readonly float decayRate;
        /// <summary>保護水準→市場効率低下の強さ（安定と効率のトレードオフ）。</summary>
        public readonly float efficiencyTradeoff;
        /// <summary>保護が不安定化を和らげ安定度に寄与する強さ。</summary>
        public readonly float stabilityScale;
        /// <summary>過保護で硬直化したとみなす既定の保護水準しきい値。</summary>
        public readonly float overprotectThreshold;

        public SocialProtectionParams(float demandSensitivity, float buildupRate, float crisisAcceleration,
                                      float ratchetBase, float vestedShield, float decayRate,
                                      float efficiencyTradeoff, float stabilityScale, float overprotectThreshold)
        {
            this.demandSensitivity = Mathf.Max(0f, demandSensitivity);
            this.buildupRate = Mathf.Max(0f, buildupRate);
            this.crisisAcceleration = Mathf.Max(0f, crisisAcceleration);
            this.ratchetBase = Mathf.Clamp01(ratchetBase);
            this.vestedShield = Mathf.Max(0f, vestedShield);
            this.decayRate = Mathf.Max(0f, decayRate);
            this.efficiencyTradeoff = Mathf.Clamp01(efficiencyTradeoff);
            this.stabilityScale = Mathf.Max(0f, stabilityScale);
            this.overprotectThreshold = Mathf.Clamp01(overprotectThreshold);
        }

        /// <summary>既定＝需要感度1.0・積上速度0.5・危機加速1.0・ラチェット基礎0.5・受益者の盾0.4・
        /// 縮小速度0.2・効率低下0.6・安定寄与0.5・過保護しきい値0.8。</summary>
        public static SocialProtectionParams Default =>
            new SocialProtectionParams(1.0f, 0.5f, 1.0f, 0.5f, 0.4f, 0.2f, 0.6f, 0.5f, 0.8f);
    }

    /// <summary>
    /// 社会保護制度の内生的成長の純ロジック（POLA-5 #1602・ポランニー『大転換』の<b>二重運動</b>の保護側）。
    /// 市場の自己調整（商品化・不安定化）が社会を脅かすと、社会は政治を通じて自己防衛の保護制度
    /// （労働規制・社会保障・関税）を build する＝自発的な反作用。「市場の圧力が保護需要を生み、保護は危機で
    /// 素早く積み上がり、撤廃には強い抵抗を示す＝二重運動のラチェット（一度できた保護は外しにくい）」を式にする。
    /// 税による所得の再分配は <see cref="RedistributionRules"/>、市場が労働・土地・貨幣を商品化する擬制商品の側は
    /// <see cref="FictitiousCommodityRules"/>（同EPIC POLA・市場化の圧力＝こちらの入力 marketPressure/dislocation の源）、
    /// 内政の安定度収束は <see cref="GovernanceRules"/> が扱い、ここは二重運動の<b>保護側ラチェット</b>
    /// （市場圧力→保護需要→制度建設→撤廃抵抗）のみを扱う。係数は基準値に掛けて使う（実効値パターン・基準非破壊）。
    /// 乱数なし・決定論。全入力クランプ。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SocialProtectionRules
    {
        /// <summary>
        /// 保護への政治的需要（0..1）。市場圧力（marketPressure 0..1＝市場の自己調整の強さ）と
        /// 不安定化（dislocation 0..1＝商品化が生む生活の混乱）の積に感度を掛けて出す
        /// ＝どちらかがゼロなら需要は生じない（市場圧力があり、かつそれが生活を脅かして初めて社会が動く）。
        /// </summary>
        public static float ProtectionDemand(float marketPressure, float dislocation, SocialProtectionParams p)
        {
            float m = Mathf.Clamp01(marketPressure);
            float d = Mathf.Clamp01(dislocation);
            return Mathf.Clamp01(p.demandSensitivity * m * d);
        }

        public static float ProtectionDemand(float marketPressure, float dislocation)
            => ProtectionDemand(marketPressure, dislocation, SocialProtectionParams.Default);

        /// <summary>
        /// 保護制度の積み上がり（1tick後の保護水準 0..1）。需要が現在保護を上回るぶんへ向けて build され、
        /// 危機（需要が高い）ほど速度が加速する（buildupRate×(1+crisisAcceleration×demand)）＝
        /// 危機で素早く積み上がる二重運動の反作用。dt は年単位。
        /// </summary>
        public static float ProtectionBuildup(float currentProtection, float demand, float dt, SocialProtectionParams p)
        {
            float current = Mathf.Clamp01(currentProtection);
            float dem = Mathf.Clamp01(demand);
            if (dem <= current) return current; // 需要が満たされていれば積み増さない
            float speed = p.buildupRate * (1f + p.crisisAcceleration * dem); // 危機ほど速い
            float step = Mathf.Clamp01(speed * Mathf.Max(0f, dt));
            return Mathf.Clamp01(Mathf.Lerp(current, dem, step));
        }

        public static float ProtectionBuildup(float currentProtection, float demand, float dt)
            => ProtectionBuildup(currentProtection, demand, dt, SocialProtectionParams.Default);

        /// <summary>
        /// 既存保護の撤廃抵抗（0..1＝ラチェット）。保護が高いほど、また受益者（vestedBeneficiaries 0..1）が
        /// 多いほど強い＝一度できた保護は受益者が盾になって外しにくい。0なら自由に撤廃でき、1なら不可逆に近い。
        /// </summary>
        public static float RatchetResistance(float currentProtection, float vestedBeneficiaries, SocialProtectionParams p)
        {
            float prot = Mathf.Clamp01(currentProtection);
            float vested = Mathf.Clamp01(vestedBeneficiaries);
            return Mathf.Clamp01(prot * (p.ratchetBase + p.vestedShield * vested));
        }

        public static float RatchetResistance(float currentProtection, float vestedBeneficiaries)
            => RatchetResistance(currentProtection, vestedBeneficiaries, SocialProtectionParams.Default);

        /// <summary>
        /// 保護の縮小（1tick後の保護水準 0..1）。抵抗（ratchetResistance 0..1）を上回る政治力の分だけ
        /// 少しずつ縮む＝(1−抵抗)に比例して減るので、ラチェットが強いほど減りにくい（抵抗1で不動）。dt は年単位。
        /// </summary>
        public static float ProtectionDecay(float currentProtection, float ratchetResistance, float dt, SocialProtectionParams p)
        {
            float current = Mathf.Clamp01(currentProtection);
            float resist = Mathf.Clamp01(ratchetResistance);
            float effectiveRate = p.decayRate * (1f - resist); // 抵抗が高いほど縮みにくい
            float drop = effectiveRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(current - drop);
        }

        public static float ProtectionDecay(float currentProtection, float ratchetResistance, float dt)
            => ProtectionDecay(currentProtection, ratchetResistance, dt, SocialProtectionParams.Default);

        /// <summary>
        /// 保護による市場効率の低下（0..1の倍率＝1.0で無損失）。保護水準が高いほど効率は下がる
        /// （安定と効率のトレードオフ＝規制・関税は市場の調整を鈍らせる）。産出などへ掛ける実効値。
        /// </summary>
        public static float MarketEfficiencyTradeoff(float protectionLevel, SocialProtectionParams p)
        {
            float prot = Mathf.Clamp01(protectionLevel);
            return Mathf.Clamp01(1f - p.efficiencyTradeoff * prot);
        }

        public static float MarketEfficiencyTradeoff(float protectionLevel)
            => MarketEfficiencyTradeoff(protectionLevel, SocialProtectionParams.Default);

        /// <summary>
        /// 保護が安定度に寄与する量（0..1）。保護水準が高く、かつ和らげるべき不安定化（dislocation 0..1）が
        /// 大きいほど寄与は大きい＝保護は不安定化を吸収して安定をもたらす（守るべき混乱がなければ寄与も小さい）。
        /// <see cref="GovernanceRules"/> の安定度へ足し込む係数。
        /// </summary>
        public static float StabilityGain(float protectionLevel, float dislocation, SocialProtectionParams p)
        {
            float prot = Mathf.Clamp01(protectionLevel);
            float d = Mathf.Clamp01(dislocation);
            return Mathf.Clamp01(p.stabilityScale * prot * d);
        }

        public static float StabilityGain(float protectionLevel, float dislocation)
            => StabilityGain(protectionLevel, dislocation, SocialProtectionParams.Default);

        /// <summary>
        /// 二重運動の緊張（0..1）。市場自由化（marketLiberalization 0..1）と保護水準が同時に高いほど大きい
        /// ＝市場化の力と社会防衛の力がせめぎ合うほど政治的緊張が高まる（どちらか一方が弱ければ緊張は小さい）。
        /// 内部勢力・政争の火種の係数。
        /// </summary>
        public static float DoubleMovementTension(float marketLiberalization, float protectionLevel)
        {
            float lib = Mathf.Clamp01(marketLiberalization);
            float prot = Mathf.Clamp01(protectionLevel);
            return Mathf.Clamp01(lib * prot);
        }

        /// <summary>
        /// 過保護で硬直化したか（保護水準がしきい値を超えたか）。閾値を超えると効率が大きく損なわれ
        /// 経済が硬直する＝守りすぎは活力を失わせる（既定しきい値は <see cref="SocialProtectionParams"/>）。
        /// </summary>
        public static bool IsOverprotected(float protectionLevel, float threshold)
            => Mathf.Clamp01(protectionLevel) >= Mathf.Clamp01(threshold);

        public static bool IsOverprotected(float protectionLevel)
            => IsOverprotected(protectionLevel, SocialProtectionParams.Default.overprotectThreshold);
    }
}
