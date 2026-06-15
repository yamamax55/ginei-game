using UnityEngine;

namespace Ginei
{
    /// <summary>通信・指揮系統の調整係数。</summary>
    public readonly struct CommsParams
    {
        /// <summary>距離1あたりの基礎遅延時間。</summary>
        public readonly float delayPerDistance;
        /// <summary>妨害が遅延を増幅する最大倍率（妨害1で遅延がこの倍になる）。</summary>
        public readonly float jamDelayMultiplier;
        /// <summary>通信途絶とみなす妨害強度の閾値。</summary>
        public readonly float cutoffThreshold;
        /// <summary>命令の価値が半減する「遅延×戦況テンポ」の積。</summary>
        public readonly float stalenessHalfLife;

        public CommsParams(float delayPerDistance, float jamDelayMultiplier, float cutoffThreshold, float stalenessHalfLife)
        {
            this.delayPerDistance = Mathf.Max(0f, delayPerDistance);
            this.jamDelayMultiplier = Mathf.Max(1f, jamDelayMultiplier);
            this.cutoffThreshold = Mathf.Clamp01(cutoffThreshold);
            this.stalenessHalfLife = Mathf.Max(0.0001f, stalenessHalfLife);
        }

        /// <summary>既定＝遅延0.1/距離・妨害倍率3・途絶閾値0.8・価値半減積5。</summary>
        public static CommsParams Default => new CommsParams(0.1f, 3f, 0.8f, 5f);
    }

    /// <summary>
    /// 通信・指揮系統の純ロジック。命令の到達は距離と妨害で遅れ、遅れた命令は戦況の速さに応じて
    /// 価値が腐る（半減期カーブ）＝「届いた時には手遅れ」。妨害が閾値を超えれば途絶＝現場は
    /// 受領済みの最終命令と自律性で戦うしかない（自律ドクトリンの値は <see cref="AutonomyRules"/> 側が
    /// 出し、ここは read-only で受ける）。電子戦の妨害強度の算出はバックログ別テーマ（ElectronicWarfare）。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CommunicationsRules
    {
        /// <summary>命令の到達遅延＝距離×基礎遅延×（1＋妨害×(倍率−1)）。妨害ゼロなら距離なり。</summary>
        public static float CommandDelay(float distance, float jamming, CommsParams p)
        {
            float baseDelay = Mathf.Max(0f, distance) * p.delayPerDistance;
            float jamFactor = 1f + Mathf.Clamp01(jamming) * (p.jamDelayMultiplier - 1f);
            return baseDelay * jamFactor;
        }

        public static float CommandDelay(float distance, float jamming)
            => CommandDelay(distance, jamming, CommsParams.Default);

        /// <summary>通信途絶か＝妨害強度が閾値以上（命令はもう届かない）。</summary>
        public static bool IsCutOff(float jamming, CommsParams p)
        {
            return Mathf.Clamp01(jamming) >= p.cutoffThreshold;
        }

        public static bool IsCutOff(float jamming) => IsCutOff(jamming, CommsParams.Default);

        /// <summary>
        /// 命令の鮮度（0..1）＝半減期カーブ。「遅延×戦況テンポ tempo(0..1)」が stalenessHalfLife で
        /// 価値半減。静的な戦況（tempo≈0）なら古い命令も使える。
        /// </summary>
        public static float OrderFreshness(float delay, float tempo, CommsParams p)
        {
            float staleness = Mathf.Max(0f, delay) * Mathf.Clamp01(tempo);
            return 1f / (1f + staleness / p.stalenessHalfLife);
        }

        public static float OrderFreshness(float delay, float tempo)
            => OrderFreshness(delay, tempo, CommsParams.Default);

        /// <summary>
        /// 指揮実効度（0..1）。通信が生きていれば命令の鮮度、途絶なら現場の自律性 autonomy(0..1) が
        /// そのまま実効度＝集権軍は頭を断たれると止まり、自律軍は途絶でも戦える。
        /// </summary>
        public static float CommandEffectiveness(float distance, float jamming, float tempo, float autonomy, CommsParams p)
        {
            if (IsCutOff(jamming, p)) return Mathf.Clamp01(autonomy);
            float freshness = OrderFreshness(CommandDelay(distance, jamming, p), tempo, p);
            // 通信下でも自律性は下支えする（命令が腐っていても現場判断で補える）
            return Mathf.Max(freshness, Mathf.Clamp01(autonomy) * freshness + Mathf.Clamp01(autonomy) * (1f - freshness) * 0.5f);
        }

        public static float CommandEffectiveness(float distance, float jamming, float tempo, float autonomy)
            => CommandEffectiveness(distance, jamming, tempo, autonomy, CommsParams.Default);
    }
}
