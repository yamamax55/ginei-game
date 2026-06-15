using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 高所優位パラメータ（重力井戸・軌道上位）。readonly struct・トップレベル。
    /// ctor で全値をクランプ。Default で標準値。
    /// </summary>
    public readonly struct HighGroundParams
    {
        /// <summary>この高度差で優位が概ね飽和する基準（軌道高度差のスケール）。</summary>
        public readonly float altitudeScale;
        /// <summary>位置エネルギー優位の最大上乗せ（射撃/突撃倍率の上限ぶん）。</summary>
        public readonly float energyGain;
        /// <summary>重力井戸ペナルティの最大削減割合（0..0.9＝最大90%減速）。</summary>
        public readonly float maxGravityPenalty;
        /// <summary>艦質量がペナルティに効く感度（重いほど沈む）。</summary>
        public readonly float massSensitivity;
        /// <summary>離脱の自由の基準値（拮抗時の退きやすさ）。</summary>
        public readonly float escapeBase;
        /// <summary>軌道砲撃ボーナスの最大上乗せ。</summary>
        public readonly float bombardGain;
        /// <summary>低位側の死力（背水の陣）補正の最大上乗せ。</summary>
        public readonly float desperationGain;
        /// <summary>井戸に降りる燃料コストの基準係数。</summary>
        public readonly float descentCostScale;

        public HighGroundParams(
            float altitudeScale,
            float energyGain,
            float maxGravityPenalty,
            float massSensitivity,
            float escapeBase,
            float bombardGain,
            float desperationGain,
            float descentCostScale)
        {
            this.altitudeScale = Mathf.Max(0.01f, altitudeScale);
            this.energyGain = Mathf.Clamp(energyGain, 0f, 2f);
            this.maxGravityPenalty = Mathf.Clamp(maxGravityPenalty, 0f, 0.9f);
            this.massSensitivity = Mathf.Clamp(massSensitivity, 0f, 4f);
            this.escapeBase = Mathf.Clamp01(escapeBase);
            this.bombardGain = Mathf.Clamp(bombardGain, 0f, 2f);
            this.desperationGain = Mathf.Clamp(desperationGain, 0f, 2f);
            this.descentCostScale = Mathf.Clamp(descentCostScale, 0f, 4f);
        }

        public static HighGroundParams Default => new HighGroundParams(
            altitudeScale: 1000f,
            energyGain: 0.5f,
            maxGravityPenalty: 0.6f,
            massSensitivity: 1f,
            escapeBase: 0.5f,
            bombardGain: 0.75f,
            desperationGain: 0.5f,
            descentCostScale: 1f);
    }

    /// <summary>
    /// 高所優位ロジック＝宇宙戦・惑星近傍の「軌道上位（重力井戸の上）を取る側の優位」の純数値モデル。
    /// 軌道高位は位置エネルギー・一方的砲撃・離脱の自由を得る。深い井戸に降りた側は機動を縛られ不利で、
    /// 不利な低位側は死力（背水）を尽くす。
    ///
    /// 分担：地形そのものは <see cref="TerrainRules"/>、重力地形の物理運動は <see cref="BlackHole"/>、
    /// 惑星攻城の制空権/侵略は <see cref="PlanetSiegeRules"/> が担う。本ルールはそれらとは別＝
    /// 戦術的な「高所優位」の係数モデル（盤面非依存・plain 引数・実効値パターン）。
    /// </summary>
    public static class HighGroundRules
    {
        /// <summary>
        /// 軌道高度差から位置優位 0..1。altitudeDifference>0＝自軍が上位。
        /// 負（自軍が低位）は 0。altitudeScale で飽和（差≈scale で約0.5）。
        /// </summary>
        public static float OrbitalAdvantage(float altitudeDifference, HighGroundParams p)
        {
            float d = Mathf.Max(0f, altitudeDifference);
            // 飽和カーブ d/(d+scale)。Log/Exp 不使用。
            return d / (d + p.altitudeScale);
        }

        public static float OrbitalAdvantage(float altitudeDifference)
            => OrbitalAdvantage(altitudeDifference, HighGroundParams.Default);

        /// <summary>
        /// 位置エネルギーによる射撃/突撃の優位倍率（1.0〜1+energyGain）。
        /// orbitalAdvantage(0..1) を線形に energyGain ぶん上乗せ。
        /// </summary>
        public static float EnergyAdvantage(float orbitalAdvantage, HighGroundParams p)
        {
            float a = Mathf.Clamp01(orbitalAdvantage);
            return 1f + a * p.energyGain;
        }

        public static float EnergyAdvantage(float orbitalAdvantage)
            => EnergyAdvantage(orbitalAdvantage, HighGroundParams.Default);

        /// <summary>
        /// 重力井戸に降りた側の機動ペナルティ倍率（0.1〜1.0／実効速度に乗算）。
        /// wellDepth(0..1＝井戸の深さ)×shipMass の感度で減速。重い艦ほど沈む。1.0=無ペナルティ。
        /// </summary>
        public static float GravityPenalty(float wellDepth, float shipMass, HighGroundParams p)
        {
            float depth = Mathf.Clamp01(wellDepth);
            float mass = Mathf.Max(0f, shipMass);
            // 質量で重みづけ（mass=1 で等倍）。massSensitivity で感度。
            float massFactor = 1f + (mass - 1f) * p.massSensitivity * 0.25f;
            massFactor = Mathf.Clamp(massFactor, 0.5f, 2f);
            float drop = Mathf.Clamp01(depth * massFactor) * p.maxGravityPenalty;
            return Mathf.Clamp(1f - drop, 0.1f, 1f);
        }

        public static float GravityPenalty(float wellDepth, float shipMass)
            => GravityPenalty(wellDepth, shipMass, HighGroundParams.Default);

        /// <summary>
        /// 上位を取る側の離脱の自由 0..1（不利なら退ける度合い）。
        /// 拮抗で escapeBase、上位ほど 1 へ伸び、低位（advantage 0）は escapeBase 未満へ落ちない。
        /// </summary>
        public static float EscapeFreedom(float orbitalAdvantage, HighGroundParams p)
        {
            float a = Mathf.Clamp01(orbitalAdvantage);
            // base から上位ぶん (1-base) を線形に伸ばす。
            return Mathf.Clamp01(p.escapeBase + a * (1f - p.escapeBase));
        }

        public static float EscapeFreedom(float orbitalAdvantage)
            => EscapeFreedom(orbitalAdvantage, HighGroundParams.Default);

        /// <summary>
        /// 井戸に降りる燃料/機動コスト 0..1。wellDepth が深いほど高く、fuelReserve が多いほど相対的に安い。
        /// </summary>
        public static float DescentCost(float wellDepth, float fuelReserve, HighGroundParams p)
        {
            float depth = Mathf.Clamp01(wellDepth);
            float fuel = Mathf.Max(0f, fuelReserve);
            float raw = depth * p.descentCostScale;
            // 燃料が多いほど相対コストが下がる（fuel=1 で等倍、fuel→大で軽い）。
            float relief = 1f / (1f + fuel);
            return Mathf.Clamp01(raw * relief);
        }

        public static float DescentCost(float wellDepth, float fuelReserve)
            => DescentCost(wellDepth, fuelReserve, HighGroundParams.Default);

        /// <summary>
        /// 軌道からの一方的砲撃の有利倍率（1.0〜1+bombardGain）。
        /// orbitalAdvantage と targetExposure(0..1＝低位側の被曝露面積)の積で効く。
        /// </summary>
        public static float BombardmentBonus(float orbitalAdvantage, float targetExposure, HighGroundParams p)
        {
            float a = Mathf.Clamp01(orbitalAdvantage);
            float e = Mathf.Clamp01(targetExposure);
            return 1f + a * e * p.bombardGain;
        }

        public static float BombardmentBonus(float orbitalAdvantage, float targetExposure)
            => BombardmentBonus(orbitalAdvantage, targetExposure, HighGroundParams.Default);

        /// <summary>
        /// 不利な低位側の死力（背水の陣）補正倍率（1.0〜1+desperationGain）。
        /// gravityPenalty が強い（=値が低い）ほど、defenderResolve(0..1)が高いほど死力が出る。
        /// </summary>
        public static float LowGroundDesperation(float gravityPenalty, float defenderResolve, HighGroundParams p)
        {
            // gravityPenalty は 0.1..1.0 の倍率。不利度 = 1 - 倍率（0..0.9）。
            float disadvantage = Mathf.Clamp01(1f - Mathf.Clamp(gravityPenalty, 0.1f, 1f));
            float resolve = Mathf.Clamp01(defenderResolve);
            return 1f + disadvantage * resolve * p.desperationGain;
        }

        public static float LowGroundDesperation(float gravityPenalty, float defenderResolve)
            => LowGroundDesperation(gravityPenalty, defenderResolve, HighGroundParams.Default);

        /// <summary>
        /// 高所優位を確保したか＝軌道高度差が threshold を超えて自軍が上位にあるか。
        /// </summary>
        public static bool HasHighGround(float altitudeDifference, float threshold)
        {
            return altitudeDifference > Mathf.Max(0f, threshold);
        }
    }
}
