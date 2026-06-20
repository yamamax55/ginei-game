using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 重力井戸パラメータ（天体重力が会戦に与える影響）。readonly struct・トップレベル。
    /// ctor で全値をクランプ。Default で標準値。Log/Exp 不使用（距離は Pow2 近似）。
    /// </summary>
    public readonly struct GravityWellParams
    {
        /// <summary>重力定数（質量→重力強度のスケール）。</summary>
        public readonly float gravConstant;
        /// <summary>距離ゼロでの発散を防ぐ軟化長（softening）。</summary>
        public readonly float softening;
        /// <summary>重力強度の上限（正規化の基準＝この値で「強い井戸」）。</summary>
        public readonly float maxStrength;
        /// <summary>軌道偏向に効く速度スケール（速いほど曲がらない）。</summary>
        public readonly float velocityScale;
        /// <summary>重力下の機動コストの最大係数。</summary>
        public readonly float maneuverScale;
        /// <summary>スイングバイ利得の最大上乗せ。</summary>
        public readonly float slingshotMaxGain;
        /// <summary>最適接近角（度）。これに近いほどスイングバイが効く。</summary>
        public readonly float optimalAngle;
        /// <summary>接近角の許容幅（度）。最適角からこの幅で利得が0へ落ちる。</summary>
        public readonly float angleTolerance;
        /// <summary>潮汐力の基準艦長（この長さで潮汐が概ね飽和方向）。</summary>
        public readonly float tidalLengthScale;
        /// <summary>潮汐損傷の最大係数。</summary>
        public readonly float tidalScale;

        public GravityWellParams(
            float gravConstant,
            float softening,
            float maxStrength,
            float velocityScale,
            float maneuverScale,
            float slingshotMaxGain,
            float optimalAngle,
            float angleTolerance,
            float tidalLengthScale,
            float tidalScale)
        {
            this.gravConstant = Mathf.Clamp(gravConstant, 0f, 1000f);
            this.softening = Mathf.Max(0.01f, softening);
            this.maxStrength = Mathf.Max(0.01f, maxStrength);
            this.velocityScale = Mathf.Max(0.01f, velocityScale);
            this.maneuverScale = Mathf.Clamp(maneuverScale, 0f, 4f);
            this.slingshotMaxGain = Mathf.Clamp(slingshotMaxGain, 0f, 2f);
            this.optimalAngle = Mathf.Clamp(optimalAngle, 0f, 180f);
            this.angleTolerance = Mathf.Clamp(angleTolerance, 1f, 180f);
            this.tidalLengthScale = Mathf.Max(0.01f, tidalLengthScale);
            this.tidalScale = Mathf.Clamp(tidalScale, 0f, 4f);
        }

        public static GravityWellParams Default => new GravityWellParams(
            gravConstant: 1f,
            softening: 100f,
            maxStrength: 100f,
            velocityScale: 1f,
            maneuverScale: 1f,
            slingshotMaxGain: 0.5f,
            optimalAngle: 90f,
            angleTolerance: 90f,
            tidalLengthScale: 1000f,
            tidalScale: 1f);
    }

    /// <summary>
    /// 重力井戸ロジック＝恒星・惑星・ブラックホールの重力が会戦に与える影響の純数値モデル。
    /// 重力強度・軌道偏向・重力下の機動コスト・スイングバイ加速・潮汐損傷・離脱難度・戦術価値・捕獲判定。
    ///
    /// 分担：重力地形の見た目/物理運動は <see cref="BlackHole"/>、軌道上位の係数は <see cref="HighGroundRules"/>
    /// が担う。本ルールはそれらとは別＝盤面非依存・plain 引数・実効値パターンの「重力井戸の数値モデル」。
    /// 決定論（乱数なし）。引数は全 clamp。符号付き出力は -1..1 に clamp。
    /// </summary>
    public static class GravityWellRules
    {
        /// <summary>
        /// 重力の強さ＝質量×G／(距離² + 軟化長²)。距離は Pow2 近似（Log/Exp 不使用）。
        /// 0..maxStrength にクランプ。近いほど・重いほど強い。
        /// </summary>
        public static float GravityStrength(float bodyMass, float distance, GravityWellParams p)
        {
            float mass = Mathf.Max(0f, bodyMass);
            float d = Mathf.Max(0f, distance);
            float denom = d * d + p.softening * p.softening;
            float g = mass * p.gravConstant / denom;
            return Mathf.Clamp(g, 0f, p.maxStrength);
        }

        public static float GravityStrength(float bodyMass, float distance)
            => GravityStrength(bodyMass, distance, GravityWellParams.Default);

        /// <summary>
        /// 軌道の曲がり 0..1＝重力／(重力 + 速度×velocityScale)。
        /// 遅い艦ほど 1 へ（引き込まれる）、速い艦ほど 0 へ（振り切る）。重力0で 0。
        /// </summary>
        public static float OrbitalDeflection(float gravityStrength, float shipVelocity, GravityWellParams p)
        {
            float g = Mathf.Max(0f, gravityStrength);
            float v = Mathf.Max(0f, shipVelocity);
            float denom = g + v * p.velocityScale;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(g / denom);
        }

        public static float OrbitalDeflection(float gravityStrength, float shipVelocity)
            => OrbitalDeflection(gravityStrength, shipVelocity, GravityWellParams.Default);

        /// <summary>
        /// 重力下の機動コスト 0..1＝重力(正規化)×maneuverScale／(1 + 推力重量比)。
        /// 強い重力ほど高く、推力重量比が高い艦ほど安い。
        /// </summary>
        public static float ManeuverCost(float gravityStrength, float thrustToWeight, GravityWellParams p)
        {
            float gn = Mathf.Clamp01(Mathf.Max(0f, gravityStrength) / p.maxStrength);
            float twr = Mathf.Max(0f, thrustToWeight);
            float cost = gn * p.maneuverScale / (1f + twr);
            return Mathf.Clamp01(cost);
        }

        public static float ManeuverCost(float gravityStrength, float thrustToWeight)
            => ManeuverCost(gravityStrength, thrustToWeight, GravityWellParams.Default);

        /// <summary>
        /// スイングバイ（重力アシスト）加速の利得倍率（1.0〜1+slingshotMaxGain）。
        /// 重力(正規化)が強く、接近角が最適角に近いほど大きい。最適角から angleTolerance 離れると利得0。
        /// </summary>
        public static float SlingshotGain(float gravityStrength, float approachAngle, GravityWellParams p)
        {
            float gn = Mathf.Clamp01(Mathf.Max(0f, gravityStrength) / p.maxStrength);
            float angle = Mathf.Clamp(approachAngle, 0f, 180f);
            // 最適角からのズレ→0..1の角効率（ズレ tolerance で0、最適で1）。
            float deviation = Mathf.Abs(angle - p.optimalAngle);
            float angleFactor = Mathf.Clamp01(1f - deviation / p.angleTolerance);
            return 1f + gn * angleFactor * p.slingshotMaxGain;
        }

        public static float SlingshotGain(float gravityStrength, float approachAngle)
            => SlingshotGain(gravityStrength, approachAngle, GravityWellParams.Default);

        /// <summary>
        /// 潮汐力による船体ストレス 0..1＝重力(正規化)×(艦長／基準艦長)×tidalScale。
        /// 強い重力勾配ほど・長い艦ほど（前後の重力差で）裂かれやすい。
        /// </summary>
        public static float TidalDamage(float gravityStrength, float shipLength, GravityWellParams p)
        {
            float gn = Mathf.Clamp01(Mathf.Max(0f, gravityStrength) / p.maxStrength);
            float lengthRatio = Mathf.Max(0f, shipLength) / p.tidalLengthScale;
            float dmg = gn * lengthRatio * p.tidalScale;
            return Mathf.Clamp01(dmg);
        }

        public static float TidalDamage(float gravityStrength, float shipLength)
            => TidalDamage(gravityStrength, shipLength, GravityWellParams.Default);

        /// <summary>
        /// 重力圏離脱の難度 0..1＝重力(正規化)／(重力(正規化) + 推力重量比)。
        /// 強い重力ほど 1 へ（抜けられない）、推力重量比が高いほど 0 へ（楽に離脱）。
        /// </summary>
        public static float EscapeDifficulty(float gravityStrength, float thrustToWeight, GravityWellParams p)
        {
            float gn = Mathf.Clamp01(Mathf.Max(0f, gravityStrength) / p.maxStrength);
            float twr = Mathf.Max(0f, thrustToWeight);
            float denom = gn + twr;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(gn / denom);
        }

        public static float EscapeDifficulty(float gravityStrength, float thrustToWeight)
            => EscapeDifficulty(gravityStrength, thrustToWeight, GravityWellParams.Default);

        /// <summary>
        /// 重力圏を利用した戦術の価値 -1..1＝(スイングバイ加速の利) −(離脱の難)。
        /// slingshotGain は倍率(1.0〜)なので 1 を引いて利得ぶんに正規化。正＝攻め時、負＝罠。
        /// </summary>
        public static float WellTacticalValue(float slingshotGain, float escapeDifficulty, GravityWellParams p)
        {
            // 利得ぶん（slingshotGain-1）を 0..maxGain → 0..1 に正規化。
            float gain = Mathf.Clamp01((Mathf.Max(0f, slingshotGain) - 1f) / Mathf.Max(0.0001f, p.slingshotMaxGain));
            float difficulty = Mathf.Clamp01(escapeDifficulty);
            return Mathf.Clamp(gain - difficulty, -1f, 1f);
        }

        public static float WellTacticalValue(float slingshotGain, float escapeDifficulty)
            => WellTacticalValue(slingshotGain, escapeDifficulty, GravityWellParams.Default);

        /// <summary>
        /// 重力に捕らわれるか＝軌道偏向と離脱難度がともに閾値を超える（引き込まれて抜けられない）。
        /// </summary>
        public static bool IsCaptured(float orbitalDeflection, float escapeDifficulty, float threshold)
        {
            float thr = Mathf.Clamp01(threshold);
            float deflect = Mathf.Clamp01(orbitalDeflection);
            float difficulty = Mathf.Clamp01(escapeDifficulty);
            return deflect > thr && difficulty > thr;
        }

        /// <summary>既定閾値 0.5 委譲版。</summary>
        public static bool IsCaptured(float orbitalDeflection, float escapeDifficulty)
            => IsCaptured(orbitalDeflection, escapeDifficulty, 0.5f);
    }
}
