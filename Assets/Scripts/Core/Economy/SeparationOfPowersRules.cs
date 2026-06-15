using UnityEngine;

namespace Ginei
{
    /// <summary>三権（#171）。権力を分立させて相互に抑制均衡させる三つの府。</summary>
    public enum Branch
    {
        立法, // 法を定める府
        行政, // 法を執行する府
        司法  // 法を裁く府
    }

    /// <summary>三権分立の調整係数。</summary>
    public readonly struct SeparationParams
    {
        /// <summary>停滞とみなす均衡度のしきい値（三すくみ＝均衡が高いほど合意形成が難しい）。</summary>
        public readonly float gridlockThreshold;

        public SeparationParams(float gridlockThreshold)
        {
            this.gridlockThreshold = Mathf.Clamp01(gridlockThreshold);
        }

        /// <summary>既定＝均衡度0.7以上で停滞とみなす。</summary>
        public static SeparationParams Default => new SeparationParams(0.7f);
    }

    /// <summary>
    /// 三権分立の純ロジック（#171）。立法・行政・司法（<see cref="Branch"/>）の権力配分から、
    /// 均衡度（三府が拮抗しているか）・一極集中の専制リスク・三すくみの停滞を導く。
    /// 権力が一府に偏れば専制（均衡不成立）、三府が拮抗すれば抑制が効くが過度だと決められない停滞に陥る。
    /// すべて plain 引数で完結（基準値非破壊・決定論）。test-first。
    /// </summary>
    public static class SeparationOfPowersRules
    {
        /// <summary>
        /// 三権の均衡度 0..1。三府の権力が完全に拮抗（1/3ずつ）なら1.0、一府に完全集中なら0.0。
        /// 各府のシェアの最大偏差を均等配分(1/3)で正規化して 1 から引く。負値は0へクランプ。
        /// 総和が0（全府0）なら均衡を測れない＝0を返す。
        /// </summary>
        public static float CheckBalance(float legislative, float executive, float judicial)
        {
            float l = Mathf.Max(0f, legislative);
            float e = Mathf.Max(0f, executive);
            float j = Mathf.Max(0f, judicial);
            float sum = l + e + j;
            if (sum <= 0f) return 0f;

            // 各府のシェア（0..1）
            float sl = l / sum;
            float se = e / sum;
            float sj = j / sum;

            const float even = 1f / 3f;
            // 均等配分からの最大偏差（0=完全均等 〜 2/3=完全集中）
            float maxDev = Mathf.Max(Mathf.Abs(sl - even), Mathf.Max(Mathf.Abs(se - even), Mathf.Abs(sj - even)));
            // 完全集中時の偏差(2/3)で正規化＝偏差0で均衡1.0・最大偏差で0.0
            const float maxPossibleDev = 2f / 3f;
            float balance = 1f - maxDev / maxPossibleDev;
            return Mathf.Clamp01(balance);
        }

        /// <summary>
        /// 一極集中の専制リスク 0..1。三府の最大シェアが均等配分(1/3)を超える分を、
        /// 完全集中(1.0)までの幅で正規化＝均等で0・一府独占で1。総和0なら測れず0。
        /// </summary>
        public static float TyrannyRisk(float legislative, float executive, float judicial)
        {
            float l = Mathf.Max(0f, legislative);
            float e = Mathf.Max(0f, executive);
            float j = Mathf.Max(0f, judicial);
            float sum = l + e + j;
            if (sum <= 0f) return 0f;

            float maxShare = Mathf.Max(l, Mathf.Max(e, j)) / sum;
            const float even = 1f / 3f;
            // 均等(1/3)で0、独占(1.0)で1へ正規化
            float risk = (maxShare - even) / (1f - even);
            return Mathf.Clamp01(risk);
        }

        /// <summary>
        /// 三すくみの停滞か。均衡度が <see cref="SeparationParams.gridlockThreshold"/> 以上なら
        /// 三府が拮抗して互いに足を引っ張り合い決められない＝true。専制（集中）では均衡が低く成立しない。
        /// </summary>
        public static bool IsGridlocked(float legislative, float executive, float judicial, SeparationParams p)
        {
            float balance = CheckBalance(legislative, executive, judicial);
            return balance >= p.gridlockThreshold;
        }

        /// <summary>既定パラメータ版。</summary>
        public static bool IsGridlocked(float legislative, float executive, float judicial)
            => IsGridlocked(legislative, executive, judicial, SeparationParams.Default);
    }
}
