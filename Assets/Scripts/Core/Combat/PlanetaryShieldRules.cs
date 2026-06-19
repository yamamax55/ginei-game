using UnityEngine;

namespace Ginei
{
    /// <summary>惑星防御シールド（惑星規模の固定防御フィールド）の調整係数。</summary>
    public readonly struct PlanetaryShieldParams
    {
        /// <summary>発生器1基・出力1あたりの被覆寄与（数×出力×これ÷サイズ＝被覆率の素）。</summary>
        public readonly float coveragePerGenerator;
        /// <summary>シールド被覆1あたりの爆撃吸収強度（被覆×これで軌道爆撃を吸収）。</summary>
        public readonly float absorptionStrength;
        /// <summary>被覆1・展開秒1あたりのエネルギー消費（被覆×時間×これ）。</summary>
        public readonly float energyPerCoverage;
        /// <summary>集中攻撃が局所過負荷へ寄与する感度（被覆が高いほど耐える）。</summary>
        public readonly float overloadSensitivity;
        /// <summary>惑星砲1門あたりの反撃火力（砲×被覆健在×これ）。</summary>
        public readonly float counterfirePerGun;

        public PlanetaryShieldParams(float coveragePerGenerator, float absorptionStrength,
                                     float energyPerCoverage, float overloadSensitivity, float counterfirePerGun)
        {
            this.coveragePerGenerator = Mathf.Max(0f, coveragePerGenerator);
            this.absorptionStrength = Mathf.Clamp01(absorptionStrength);
            this.energyPerCoverage = Mathf.Max(0f, energyPerCoverage);
            this.overloadSensitivity = Mathf.Max(0f, overloadSensitivity);
            this.counterfirePerGun = Mathf.Max(0f, counterfirePerGun);
        }

        /// <summary>既定＝被覆寄与0.01・吸収強度0.9・消費0.5/被覆/秒・過負荷感度1.0・反撃2.0/門。</summary>
        public static PlanetaryShieldParams Default
            => new PlanetaryShieldParams(0.01f, 0.9f, 0.5f, 1f, 2f);
    }

    /// <summary>
    /// 惑星防御シールド（planetary shield／惑星全体を覆う防御フィールド）の純ロジック。シールド
    /// 発生器の数と出力で被覆率が決まり、軌道爆撃を吸収する。展開しているあいだエネルギーを消費し、
    /// 集中攻撃を受けると局所的に過負荷してシールドの穴（軌道侵入の隙）が空く。被覆が健在なあいだは
    /// シールド下から惑星砲で反撃でき、被覆が薄い箇所では発生器自体が露出して脆弱になる。
    /// <c>ShieldTechRules</c>（艦の防御スクリーン）とは別系統＝こちらは惑星規模の固定防御。実際の
    /// ダメージ適用や包囲解決は Game/戦略層が担い、ここは盤面非依存の plain 引数・乱数なし。
    /// 実効値パターン（基準値非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PlanetaryShieldRules
    {
        /// <summary>
        /// シールド被覆率（0..1）＝発生器数×出力×被覆寄与÷惑星サイズ。発生器が多く出力が高いほど被覆が増え、
        /// 惑星が大きいほど覆い切れず被覆は薄まる。発生器0や出力0なら被覆0。
        /// 発生器数 0..1000、出力 0..100、サイズは下限1.0でクランプ。
        /// </summary>
        public static float ShieldCoverage(float generatorCount, float generatorPower, float planetSize, PlanetaryShieldParams p)
        {
            float gen = Mathf.Clamp(generatorCount, 0f, 1000f);
            float power = Mathf.Clamp(generatorPower, 0f, 100f);
            float size = Mathf.Max(1f, planetSize);
            return Mathf.Clamp01(gen * power * p.coveragePerGenerator / size);
        }

        public static float ShieldCoverage(float generatorCount, float generatorPower, float planetSize)
            => ShieldCoverage(generatorCount, generatorPower, planetSize, PlanetaryShieldParams.Default);

        /// <summary>
        /// 軌道爆撃の吸収量（≥0）＝被弾火力×被覆×吸収強度。被覆が高く吸収強度が高いほど多くを止める。
        /// 吸収は被弾火力を超えない（吸収しきった先は防がない＝最大で全量）。被覆0なら吸収0。
        /// </summary>
        public static float BombardmentAbsorption(float shieldCoverage, float incomingFirepower, PlanetaryShieldParams p)
        {
            float cov = Mathf.Clamp01(shieldCoverage);
            float incoming = Mathf.Max(0f, incomingFirepower);
            float absorbed = incoming * cov * p.absorptionStrength;
            return Mathf.Clamp(absorbed, 0f, incoming);
        }

        public static float BombardmentAbsorption(float shieldCoverage, float incomingFirepower)
            => BombardmentAbsorption(shieldCoverage, incomingFirepower, PlanetaryShieldParams.Default);

        /// <summary>
        /// エネルギー消費（≥0）＝被覆×展開秒数×消費係数。広く長く展開するほど食う。被覆0や展開0なら消費0。
        /// 展開秒は下限0でクランプ。
        /// </summary>
        public static float EnergyDraw(float shieldCoverage, float sustainedDuration, PlanetaryShieldParams p)
        {
            float cov = Mathf.Clamp01(shieldCoverage);
            float duration = Mathf.Max(0f, sustainedDuration);
            return Mathf.Max(0f, cov * duration * p.energyPerCoverage);
        }

        public static float EnergyDraw(float shieldCoverage, float sustainedDuration)
            => EnergyDraw(shieldCoverage, sustainedDuration, PlanetaryShieldParams.Default);

        /// <summary>
        /// 局所過負荷（0..1）＝集中攻撃が一点へ火力を束ねた負荷。負荷＝被弾火力×集中度×感度、これを
        /// (負荷＋被覆耐性)で正規化。被覆が高いほど耐え（過負荷↓）、集中度が高く火力が大きいほど過負荷↑。
        /// 被弾0や集中0なら過負荷0。集中度 0..1。
        /// </summary>
        public static float LocalOverload(float incomingFirepower, float shieldCoverage, float focusedAttack, PlanetaryShieldParams p)
        {
            float incoming = Mathf.Max(0f, incomingFirepower);
            float cov = Mathf.Clamp01(shieldCoverage);
            float focus = Mathf.Clamp01(focusedAttack);
            float load = incoming * focus * p.overloadSensitivity;
            if (load <= 0f) return 0f;
            float resistance = cov * incoming; // 被覆ぶんの耐性（火力規模に応じた防護）
            return Mathf.Clamp01(load / (load + resistance));
        }

        public static float LocalOverload(float incomingFirepower, float shieldCoverage, float focusedAttack)
            => LocalOverload(incomingFirepower, shieldCoverage, focusedAttack, PlanetaryShieldParams.Default);

        /// <summary>
        /// シールドの穴（0..1）＝局所過負荷×(1−冗長性)。発生器が冗長なら穴は塞がり、冗長が無いと
        /// 過負荷がそのまま軌道侵入の隙になる。過負荷0なら穴0。冗長性 0..1。
        /// </summary>
        public static float ShieldGap(float localOverload, float generatorRedundancy, PlanetaryShieldParams p)
        {
            float overload = Mathf.Clamp01(localOverload);
            float redundancy = Mathf.Clamp01(generatorRedundancy);
            return Mathf.Clamp01(overload * (1f - redundancy));
        }

        public static float ShieldGap(float localOverload, float generatorRedundancy)
            => ShieldGap(localOverload, generatorRedundancy, PlanetaryShieldParams.Default);

        /// <summary>
        /// シールド下からの反撃火力（≥0）＝惑星砲門数×被覆健在×反撃係数。被覆が健在なほど安全に撃てる
        /// （シールドが盾になる）。被覆0だと無防備で反撃も成立しない。砲門数は下限0でクランプ。
        /// </summary>
        public static float UnderShieldCounterfire(float planetaryDefenseGuns, float shieldCoverage, PlanetaryShieldParams p)
        {
            float guns = Mathf.Max(0f, planetaryDefenseGuns);
            float cov = Mathf.Clamp01(shieldCoverage);
            return Mathf.Max(0f, guns * cov * p.counterfirePerGun);
        }

        public static float UnderShieldCounterfire(float planetaryDefenseGuns, float shieldCoverage)
            => UnderShieldCounterfire(planetaryDefenseGuns, shieldCoverage, PlanetaryShieldParams.Default);

        /// <summary>
        /// 発生器の脆弱性（0..1）＝露出度×(1−被覆)。被覆が薄い箇所ほど発生器が外気に晒され狙われる。
        /// 被覆1.0で完全防護（脆弱性0）、被覆0なら露出がそのまま脆弱性。露出度 0..1。
        /// </summary>
        public static float GeneratorVulnerability(float generatorExposure, float shieldCoverage, PlanetaryShieldParams p)
        {
            float exposure = Mathf.Clamp01(generatorExposure);
            float cov = Mathf.Clamp01(shieldCoverage);
            return Mathf.Clamp01(exposure * (1f - cov));
        }

        public static float GeneratorVulnerability(float generatorExposure, float shieldCoverage)
            => GeneratorVulnerability(generatorExposure, shieldCoverage, PlanetaryShieldParams.Default);

        /// <summary>
        /// 惑星が防護されているか＝被覆が穴を上回り、かつ被覆がしきい以上。被覆が薄いか穴が被覆と並ぶほど
        /// 開けば軌道侵入が成立（false）。
        /// </summary>
        public static bool IsPlanetShielded(float shieldCoverage, float shieldGap, float threshold)
        {
            float cov = Mathf.Clamp01(shieldCoverage);
            float gap = Mathf.Clamp01(shieldGap);
            return cov >= threshold && cov > gap;
        }

        /// <summary>既定閾値0.5で惑星防護を判定（被覆が半分未満なら穴次第で侵入可）。</summary>
        public static bool IsPlanetShielded(float shieldCoverage, float shieldGap)
            => IsPlanetShielded(shieldCoverage, shieldGap, 0.5f);
    }
}
