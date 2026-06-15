using UnityEngine;

namespace Ginei
{
    /// <summary>戦場の霧の調整係数（会戦の可視性レイヤー）。</summary>
    public readonly struct FogOfWarParams
    {
        /// <summary>可視性が半減する距離（これだけ離れると距離成分が0.5）。</summary>
        public readonly float halfVisibilityDistance;
        /// <summary>偵察ゼロでも見える最低可視性（至近の素の見え）。</summary>
        public readonly float baseVisibility;
        /// <summary>可視性0のときの推定誤差の最大割合（±maxError）。</summary>
        public readonly float maxError;
        /// <summary>情報の時間減衰の強さ（1/(1+k×age）の k）。</summary>
        public readonly float decayRate;

        public FogOfWarParams(float halfVisibilityDistance, float baseVisibility, float maxError, float decayRate)
        {
            this.halfVisibilityDistance = Mathf.Max(0.01f, halfVisibilityDistance);
            this.baseVisibility = Mathf.Clamp01(baseVisibility);
            this.maxError = Mathf.Max(0f, maxError);
            this.decayRate = Mathf.Max(0f, decayRate);
        }

        /// <summary>既定＝半減距離20・基礎可視0.15・最大誤差0.7・減衰0.1。</summary>
        public static FogOfWarParams Default => new FogOfWarParams(20f, 0.15f, 0.7f, 0.1f);
    }

    /// <summary>
    /// 戦場の霧の純ロジック＝会戦で敵戦力・位置がどう「見えるか」のレイヤー。距離で可視性が下がり偵察で晴れ、
    /// 隠蔽（地形遮蔽×電波管制）で探知が縮む。可視性が低いほど推定戦力にブレが乗り、古い情報ほど信頼が落ちる。
    /// 乱数は roll(0..1) 引数で決定論的に振る＝同じ入力なら同じ霧。真値 trueStrength は非破壊（実効値パターン）。
    /// 偵察の推定誤差そのものを扱う <see cref="ReconRules"/> とは別＝こちらは会戦の物理的な見え方のレイヤー。
    /// 認知バイアスの <c>AvailabilityBiasRules</c>/<c>OverconfidenceBiasRules</c> とも別物（あれは主観の歪み）。
    /// 盤面非依存の plain 引数・純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FogOfWarRules
    {
        /// <summary>
        /// 可視性 0..1。距離で 1/(1+d/half) と下がり、偵察精度 reconLevel(0..1) で baseVisibility..1 へ底上げ。
        /// 近く＆偵察が効くほど霧が晴れる。
        /// </summary>
        public static float Visibility(float distance, float reconLevel, FogOfWarParams p)
        {
            float d = Mathf.Max(0f, distance);
            float distFactor = 1f / (1f + d / p.halfVisibilityDistance); // 距離成分（0..1）：近いほど1
            float recon = Mathf.Clamp01(reconLevel);
            float floor = p.baseVisibility + (1f - p.baseVisibility) * recon; // 偵察で底上げした最低可視
            // 距離成分を floor..1 の範囲へ写す＝偵察が効くほど遠くても見える
            float vis = Mathf.Lerp(floor, 1f, distFactor);
            return Mathf.Clamp01(vis);
        }

        public static float Visibility(float distance, float reconLevel)
            => Visibility(distance, reconLevel, FogOfWarParams.Default);

        /// <summary>推定誤差の割合（0..maxError）。可視性が低いほど大きく＝見えないほど推定が外れる。</summary>
        public static float EstimationError(float visibility, FogOfWarParams p)
        {
            return p.maxError * (1f - Mathf.Clamp01(visibility));
        }

        public static float EstimationError(float visibility) => EstimationError(visibility, FogOfWarParams.Default);

        /// <summary>
        /// 敵戦力の点推定＝真値に ±estimationError の幅を乗せる。roll∈[0,1] を [-1,1] のバイアスへ写し
        /// （roll=0.5 で真値・roll=1 で過大評価上端・roll=0 で過小評価下端）決定論的に振る。負にはならない。
        /// </summary>
        public static float PerceivedStrength(float trueStrength, float estimationError, float roll)
        {
            float err = Mathf.Max(0f, estimationError);
            float bias = Mathf.Clamp01(roll) * 2f - 1f; // [0,1] → [-1,1]
            return Mathf.Max(0f, trueStrength * (1f + err * bias));
        }

        /// <summary>隠蔽度 0..1＝地形遮蔽 terrainCover と電波管制 emcon の相乗（どちらも要る＝積）。</summary>
        public static float ConcealmentBonus(float terrainCover, float emcon)
        {
            float cover = Mathf.Clamp01(terrainCover);
            float ec = Mathf.Clamp01(emcon);
            // 相補積：1 - (1-cover)(1-ec)＝どちらかが効けば隠れ、両方で深まる
            return Mathf.Clamp01(1f - (1f - cover) * (1f - ec));
        }

        /// <summary>探知距離＝基礎探知距離を隠蔽で縮める（隠蔽1で 0 まで縮む）。</summary>
        public static float DetectionRange(float baseRange, float concealment)
        {
            float br = Mathf.Max(0f, baseRange);
            return br * (1f - Mathf.Clamp01(concealment));
        }

        /// <summary>情報の信頼度 0..1＝時間減衰 1/(1+k×age）。古い情報ほど信頼が落ちる。</summary>
        public static float IntelDecay(float intelAge, FogOfWarParams p)
        {
            float age = Mathf.Max(0f, intelAge);
            return Mathf.Clamp01(1f / (1f + p.decayRate * age));
        }

        public static float IntelDecay(float intelAge) => IntelDecay(intelAge, FogOfWarParams.Default);

        /// <summary>
        /// 奇襲有利 0..1＝こちらが相手を見えていて（visibility 高）相手からこちらが見えていない
        /// （enemyVisibilityOfYou 低）ほど大きい。一方的な視認が奇襲を生む。
        /// </summary>
        public static float SurpriseFactor(float visibility, float enemyVisibilityOfYou)
        {
            float you = Mathf.Clamp01(visibility);
            float them = Mathf.Clamp01(enemyVisibilityOfYou);
            return Mathf.Clamp01(you * (1f - them));
        }

        /// <summary>霧中判定＝可視性が閾値未満なら true（敵を捕捉しきれていない）。</summary>
        public static bool IsFogged(float visibility, float threshold)
        {
            return Mathf.Clamp01(visibility) < threshold;
        }
    }
}
