using UnityEngine;

namespace Ginei
{
    /// <summary>機雷原・宙雷の調整係数。</summary>
    public readonly struct MinefieldParams
    {
        /// <summary>密度1の機雷原を通過する艦が受ける損害率の上限。</summary>
        public readonly float maxTransitLossRatio;
        /// <summary>密度1の機雷原での減速率（速度がこれだけ削れる）。</summary>
        public readonly float maxSpeedPenalty;
        /// <summary>掃海が密度を削る速度（per dt・掃海努力1のとき）。</summary>
        public readonly float sweepRate;
        /// <summary>通過可能（安全化）とみなす密度の閾値。</summary>
        public readonly float clearedThreshold;

        public MinefieldParams(float maxTransitLossRatio, float maxSpeedPenalty, float sweepRate, float clearedThreshold)
        {
            this.maxTransitLossRatio = Mathf.Clamp01(maxTransitLossRatio);
            this.maxSpeedPenalty = Mathf.Clamp01(maxSpeedPenalty);
            this.sweepRate = Mathf.Max(0f, sweepRate);
            this.clearedThreshold = Mathf.Clamp01(clearedThreshold);
        }

        /// <summary>既定＝最大損害20%・最大減速60%・掃海0.1・安全閾値0.05。</summary>
        public static MinefieldParams Default => new MinefieldParams(0.2f, 0.6f, 0.1f, 0.05f);
    }

    /// <summary>
    /// 機雷原・宙雷の純ロジック（無人の地帯拒否）。敷設密度（0..1）が通過損害と減速を決め、
    /// 強行突破は出血し、掃海は時間を食う＝「血で払うか時間で払うか」。機雷は敷いた側の艦隊を
    /// 必要としない＝艦隊封鎖（<see cref="BlockadeRules"/>）と違い兵力を拘束しない代わりに敵味方を
    /// 選ばない。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MinefieldRules
    {
        /// <summary>強行通過の損害率（0..maxTransitLossRatio）＝密度に比例。通過部隊の戦力に掛ける。</summary>
        public static float TransitLossRatio(float density, MinefieldParams p)
        {
            return Mathf.Clamp01(density) * p.maxTransitLossRatio;
        }

        public static float TransitLossRatio(float density)
            => TransitLossRatio(density, MinefieldParams.Default);

        /// <summary>機雷原内の速度倍率（1−密度×最大減速）。移動速度に掛ける（慎重に進むほど≠損害減は別）。</summary>
        public static float SpeedFactor(float density, MinefieldParams p)
        {
            return 1f - Mathf.Clamp01(density) * p.maxSpeedPenalty;
        }

        public static float SpeedFactor(float density) => SpeedFactor(density, MinefieldParams.Default);

        /// <summary>
        /// 掃海の1tick後の密度（0..1）。掃海努力 sweepEffort(0..1)×掃海速度×dt で減る。
        /// 敷設追加 layingRate（per dt、0で純減）が同時に走れば綱引きになる。
        /// </summary>
        public static float DensityTick(float density, float sweepEffort, float layingRate, float dt, MinefieldParams p)
        {
            float d = Mathf.Max(0f, dt);
            float next = Mathf.Clamp01(density)
                       - p.sweepRate * Mathf.Clamp01(sweepEffort) * d
                       + Mathf.Max(0f, layingRate) * d;
            return Mathf.Clamp01(next);
        }

        public static float DensityTick(float density, float sweepEffort, float layingRate, float dt)
            => DensityTick(density, sweepEffort, layingRate, dt, MinefieldParams.Default);

        /// <summary>安全化済みか＝密度が安全閾値以下。</summary>
        public static bool IsCleared(float density, MinefieldParams p)
        {
            return Mathf.Clamp01(density) <= p.clearedThreshold;
        }

        public static bool IsCleared(float density) => IsCleared(density, MinefieldParams.Default);

        /// <summary>
        /// 完全掃海までの所要時間（敷設追加なし）。掃海努力0は無限大。
        /// 「血で払う（即時通過の損害）か時間で払う（この時間）か」の比較材料。
        /// </summary>
        public static float TimeToClear(float density, float sweepEffort, MinefieldParams p)
        {
            float target = Mathf.Max(0f, Mathf.Clamp01(density) - p.clearedThreshold);
            if (target <= 0f) return 0f;
            float rate = p.sweepRate * Mathf.Clamp01(sweepEffort);
            if (rate <= 0f) return float.PositiveInfinity;
            return target / rate;
        }

        public static float TimeToClear(float density, float sweepEffort)
            => TimeToClear(density, sweepEffort, MinefieldParams.Default);
    }
}
