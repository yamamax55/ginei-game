using UnityEngine;

namespace Ginei
{
    /// <summary>偵察・戦場の霧の調整係数（#119 偵察艦／戦術の霧）。</summary>
    public readonly struct ReconParams
    {
        /// <summary>偵察ゼロ時の推定誤差の最大割合（±baseError＝recon=0 で真値の±baseError まで外す）。</summary>
        public readonly float baseError;
        /// <summary>探知の基礎成功率（recon=0 でも至近なら拾える最低限の成分）。</summary>
        public readonly float baseDetect;
        /// <summary>探知が成立する最大距離（これを超えると探知率0）。</summary>
        public readonly float detectionRange;

        public ReconParams(float baseError, float baseDetect, float detectionRange)
        {
            this.baseError = Mathf.Max(0f, baseError);
            this.baseDetect = Mathf.Clamp01(baseDetect);
            this.detectionRange = Mathf.Max(0f, detectionRange);
        }

        /// <summary>既定＝誤差±60%・基礎探知0.3・探知距離30。</summary>
        public static ReconParams Default => new ReconParams(0.6f, 0.3f, 30f);
    }

    /// <summary>
    /// 偵察・戦場の霧の純ロジック（#119）。偵察精度 reconLevel(0..1) が高いほど敵戦力の推定誤差が縮み、
    /// 探知（そもそも見つけられるか）が成立しやすくなる。推定は乱数を持たず、外から与える roll（[-1,1] のバイアス、
    /// [0,1) の判定値）で決定論的に解決する＝同じ入力なら同じ霧。実効値パターン（真値 trueStrength は非破壊）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ReconRules
    {
        /// <summary>推定誤差の割合（0..baseError）。reconLevel=1 で 0（正確）、reconLevel=0 で baseError（最大）。</summary>
        public static float ErrorFraction(float reconLevel, ReconParams p)
        {
            return p.baseError * (1f - Mathf.Clamp01(reconLevel));
        }

        public static float ErrorFraction(float reconLevel) => ErrorFraction(reconLevel, ReconParams.Default);

        /// <summary>
        /// 敵戦力の点推定。roll∈[-1,1] のバイアスで真値を ±ErrorFraction の幅でずらす
        /// （roll=0 で真値・roll=+1 で過大評価の上端・roll=-1 で過小評価の下端）。負にはならない。
        /// </summary>
        public static float EstimateStrength(float trueStrength, float reconLevel, float roll, ReconParams p)
        {
            float err = ErrorFraction(reconLevel, p);
            float b = Mathf.Clamp(roll, -1f, 1f);
            return Mathf.Max(0f, trueStrength * (1f + err * b));
        }

        public static float EstimateStrength(float trueStrength, float reconLevel, float roll)
            => EstimateStrength(trueStrength, reconLevel, roll, ReconParams.Default);

        /// <summary>推定の信頼区間 [low, high]＝真値 ± ErrorFraction。low は 0 未満にならない。</summary>
        public static void EstimateBand(float trueStrength, float reconLevel, ReconParams p, out float low, out float high)
        {
            float err = ErrorFraction(reconLevel, p);
            low = Mathf.Max(0f, trueStrength * (1f - err));
            high = Mathf.Max(0f, trueStrength * (1f + err));
        }

        public static void EstimateBand(float trueStrength, float reconLevel, out float low, out float high)
            => EstimateBand(trueStrength, reconLevel, ReconParams.Default, out low, out high);

        /// <summary>推定の確からしさ 0..1（＝偵察精度そのもの。UI のゲージ表示用）。</summary>
        public static float Confidence(float reconLevel) => Mathf.Clamp01(reconLevel);

        /// <summary>
        /// 探知成功率 0..1。探知距離内の近さ（proximity）に、偵察精度で底上げした探知力を掛け、
        /// 標的のステルス（隠密性 0..1）を引く。距離が detectionRange 以上なら 0。
        /// </summary>
        public static float DetectionChance(float distance, float reconLevel, float targetStealth, ReconParams p)
        {
            if (p.detectionRange <= 0f) return 0f;
            if (distance >= p.detectionRange) return 0f;
            float proximity = Mathf.Clamp01(1f - distance / p.detectionRange);
            float sensor = p.baseDetect + (1f - p.baseDetect) * Mathf.Clamp01(reconLevel);
            float chance = proximity * sensor - Mathf.Clamp01(targetStealth);
            return Mathf.Clamp01(chance);
        }

        public static float DetectionChance(float distance, float reconLevel, float targetStealth)
            => DetectionChance(distance, reconLevel, targetStealth, ReconParams.Default);

        /// <summary>探知判定。roll∈[0,1) が探知率未満なら発見＝true（決定論）。</summary>
        public static bool IsDetected(float distance, float reconLevel, float targetStealth, float roll, ReconParams p)
        {
            return roll < DetectionChance(distance, reconLevel, targetStealth, p);
        }

        public static bool IsDetected(float distance, float reconLevel, float targetStealth, float roll)
            => IsDetected(distance, reconLevel, targetStealth, roll, ReconParams.Default);
    }
}
