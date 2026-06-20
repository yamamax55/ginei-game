using UnityEngine;

namespace Ginei
{
    /// <summary>亜空間跳躍（ワープ航行）の調整係数。</summary>
    public readonly struct HyperspaceJumpParams
    {
        /// <summary>充填時間の基準係数（距離/出力に掛ける）。</summary>
        public readonly float chargeBase;
        /// <summary>跳躍精度に効く距離の重み（大きいほど遠距離で精度が落ちる）。</summary>
        public readonly float distanceFactor;
        /// <summary>到着誤差の基準係数。</summary>
        public readonly float errorScale;
        /// <summary>跳躍直後の無防備時間の基準係数。</summary>
        public readonly float vulnerabilityBase;
        /// <summary>連続跳躍の負荷の基準係数。</summary>
        public readonly float strainBase;
        /// <summary>奇襲跳躍の利得の最大値（1超）。</summary>
        public readonly float surpriseGain;

        public HyperspaceJumpParams(float chargeBase, float distanceFactor, float errorScale,
            float vulnerabilityBase, float strainBase, float surpriseGain)
        {
            this.chargeBase = Mathf.Max(0f, chargeBase);
            this.distanceFactor = Mathf.Max(0f, distanceFactor);
            this.errorScale = Mathf.Max(0f, errorScale);
            this.vulnerabilityBase = Mathf.Max(0f, vulnerabilityBase);
            this.strainBase = Mathf.Max(0f, strainBase);
            this.surpriseGain = Mathf.Max(1f, surpriseGain);
        }

        /// <summary>既定＝充填1・距離重み1・誤差1・無防備1・負荷1・奇襲利得1。</summary>
        public static HyperspaceJumpParams Default => new HyperspaceJumpParams(1f, 1f, 1f, 1f, 1f, 1f);
    }

    /// <summary>
    /// 亜空間跳躍（ワープ航行）による戦略移動の純ロジック。跳躍の充填時間（距離/機関出力）、跳躍距離と
    /// 精度（遠距離ほど誤差が大きい）、跳躍直後の無防備（システム再起動）、連続跳躍の機関負荷、奇襲跳躍の
    /// 有利、乗員練度による無防備短縮を扱う。銀英伝の艦隊移動（回廊・ワープ）を想定。
    /// 倍率/時間は基準値に掛けて使う（実効値パターン・基準非破壊）。決定論（乱数なし）・入力 clamp。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class HyperspaceJumpRules
    {
        /// <summary>
        /// 跳躍の充填時間＝跳躍距離/機関出力 × chargeBase。出力が高いほど速く充填する。
        /// 出力0は最小値1へクランプ（ゼロ割回避）。
        /// </summary>
        public static float ChargeTime(float jumpDistance, float driveOutput, HyperspaceJumpParams p)
        {
            float dist = Mathf.Max(0f, jumpDistance);
            float output = Mathf.Max(1f, driveOutput);
            return dist / output * p.chargeBase;
        }

        public static float ChargeTime(float jumpDistance, float driveOutput)
            => ChargeTime(jumpDistance, driveOutput, HyperspaceJumpParams.Default);

        /// <summary>
        /// 跳躍精度（0..1）＝航法精度/(1+距離×distanceFactor)。遠距離ほど分母が増え精度が落ちる。
        /// </summary>
        public static float JumpAccuracy(float jumpDistance, float navigationQuality, HyperspaceJumpParams p)
        {
            float dist = Mathf.Max(0f, jumpDistance);
            float nav = Mathf.Clamp01(navigationQuality);
            return Mathf.Clamp01(nav / (1f + dist * p.distanceFactor));
        }

        public static float JumpAccuracy(float jumpDistance, float navigationQuality)
            => JumpAccuracy(jumpDistance, navigationQuality, HyperspaceJumpParams.Default);

        /// <summary>
        /// 到着地点の誤差＝(1−精度)×距離×errorScale。精度が低く遠いほど大きくズレる。
        /// </summary>
        public static float ArrivalError(float jumpAccuracy, float jumpDistance, HyperspaceJumpParams p)
        {
            float acc = Mathf.Clamp01(jumpAccuracy);
            float dist = Mathf.Max(0f, jumpDistance);
            return (1f - acc) * dist * p.errorScale;
        }

        public static float ArrivalError(float jumpAccuracy, float jumpDistance)
            => ArrivalError(jumpAccuracy, jumpDistance, HyperspaceJumpParams.Default);

        /// <summary>
        /// 跳躍直後の無防備時間（システム再起動）＝距離/(1+システム堅牢性) × vulnerabilityBase。
        /// 堅牢なシステムほど早く立ち上がる。
        /// </summary>
        public static float PostJumpVulnerability(float jumpDistance, float systemRobustness, HyperspaceJumpParams p)
        {
            float dist = Mathf.Max(0f, jumpDistance);
            float robust = Mathf.Max(0f, systemRobustness);
            return dist / (1f + robust) * p.vulnerabilityBase;
        }

        public static float PostJumpVulnerability(float jumpDistance, float systemRobustness)
            => PostJumpVulnerability(jumpDistance, systemRobustness, HyperspaceJumpParams.Default);

        /// <summary>
        /// 連続跳躍の機関負荷＝連続跳躍数/(1+冷却) × strainBase。冷却能力が高いほど負荷が下がる。
        /// </summary>
        public static float ConsecutiveJumpStrain(float jumpsInSequence, float driveCooling, HyperspaceJumpParams p)
        {
            float jumps = Mathf.Max(0f, jumpsInSequence);
            float cooling = Mathf.Max(0f, driveCooling);
            return jumps / (1f + cooling) * p.strainBase;
        }

        public static float ConsecutiveJumpStrain(float jumpsInSequence, float driveCooling)
            => ConsecutiveJumpStrain(jumpsInSequence, driveCooling, HyperspaceJumpParams.Default);

        /// <summary>
        /// 奇襲跳躍の利得（−1..1）＝誤差が小さく敵が無警戒なら正の利得、誤差が大きいか敵が警戒済みなら不利。
        /// (1−敵警戒度−誤差度) を −1..1 へクランプし surpriseGain で増幅。誤差度は arrivalError をそのまま 0..1 扱い。
        /// </summary>
        public static float SurpriseJumpBonus(float arrivalError, float enemyPreparedness, HyperspaceJumpParams p)
        {
            float errDeg = Mathf.Clamp01(Mathf.Max(0f, arrivalError));
            float prep = Mathf.Clamp01(enemyPreparedness);
            float raw = 1f - prep - errDeg;
            return Mathf.Clamp(raw * p.surpriseGain, -1f, 1f);
        }

        public static float SurpriseJumpBonus(float arrivalError, float enemyPreparedness)
            => SurpriseJumpBonus(arrivalError, enemyPreparedness, HyperspaceJumpParams.Default);

        /// <summary>
        /// 跳躍直後の戦闘準備度（0..1）＝無防備時間を乗員練度で短縮した残存準備度。
        /// readiness=1 なら完全準備(1)、readiness=0 なら無防備時間が長いほど準備度が落ちる。
        /// 1/(1+無防備×(1−練度)) で 0..1 に収める（無防備0なら常に1）。
        /// </summary>
        public static float JumpExitReadiness(float postJumpVulnerability, float crewReadiness, HyperspaceJumpParams p)
        {
            float vuln = Mathf.Max(0f, postJumpVulnerability);
            float crew = Mathf.Clamp01(crewReadiness);
            return Mathf.Clamp01(1f / (1f + vuln * (1f - crew)));
        }

        public static float JumpExitReadiness(float postJumpVulnerability, float crewReadiness)
            => JumpExitReadiness(postJumpVulnerability, crewReadiness, HyperspaceJumpParams.Default);

        /// <summary>既定の安全閾値（誤差・負荷ともこの値以下なら安全）。</summary>
        public const float DefaultSafeThreshold = 1f;

        /// <summary>
        /// 跳躍が安全か＝到着誤差と連続跳躍負荷がともに threshold 以下なら true。
        /// </summary>
        public static bool IsJumpSafe(float arrivalError, float consecutiveJumpStrain, float threshold)
        {
            float th = Mathf.Max(0f, threshold);
            float err = Mathf.Max(0f, arrivalError);
            float strain = Mathf.Max(0f, consecutiveJumpStrain);
            return err <= th && strain <= th;
        }

        public static bool IsJumpSafe(float arrivalError, float consecutiveJumpStrain)
            => IsJumpSafe(arrivalError, consecutiveJumpStrain, DefaultSafeThreshold);
    }
}
