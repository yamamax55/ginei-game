using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 射撃管制（FCS・照準）の調整値（#射撃管制）。照準解の質・距離減衰・回避影響・斉射散布・修正射撃の効きを束ねる。
    /// すべて ctor で Clamp 済み（負値・暴走を防ぐ）。
    /// </summary>
    public readonly struct FireControlParams
    {
        /// <summary>追尾時間の半飽和点（秒）。これだけ追尾すると追尾項が約0.5に達する＝照準解が固まる速さ。</summary>
        public readonly float trackingHalfTime;
        /// <summary>距離による命中減衰の係数（大きいほど遠距離で当たらない）。基礎命中＝解/(1+coef×距離)。</summary>
        public readonly float distanceCoefficient;
        /// <summary>目標速度が回避に効く強さ（速い目標ほど避けやすい）。</summary>
        public readonly float speedEvasionWeight;
        /// <summary>横方向相対運動（リード角誤差）の重み。横切る目標は照準が難しい＝縦の接近より誤差大。</summary>
        public readonly float lateralWeight;
        /// <summary>縦方向相対運動（接近/離隔）の重み。横より小さく見積もる。</summary>
        public readonly float closingWeight;
        /// <summary>砲数1門あたりが散布界に効く強さ（門数が多いほど散る・FCS精度で抑制）。</summary>
        public readonly float spreadPerGun;
        /// <summary>修正射撃（walking）の効き。前回外れ量×FCS精度×これだけ次弾命中が改善。</summary>
        public readonly float walkingGain;

        public FireControlParams(float trackingHalfTime, float distanceCoefficient, float speedEvasionWeight,
            float lateralWeight, float closingWeight, float spreadPerGun, float walkingGain)
        {
            this.trackingHalfTime = Mathf.Max(0.0001f, trackingHalfTime);
            this.distanceCoefficient = Mathf.Max(0f, distanceCoefficient);
            this.speedEvasionWeight = Mathf.Max(0f, speedEvasionWeight);
            this.lateralWeight = Mathf.Max(0f, lateralWeight);
            this.closingWeight = Mathf.Max(0f, closingWeight);
            this.spreadPerGun = Mathf.Max(0f, spreadPerGun);
            this.walkingGain = Mathf.Clamp01(walkingGain);
        }

        /// <summary>既定値。追尾半飽和2秒・距離係数0.01・速度回避0.5・横0.7/縦0.3・門あたり散布0.05・修正0.5。</summary>
        public static FireControlParams Default => new FireControlParams(
            DefaultTrackingHalfTime, DefaultDistanceCoefficient, DefaultSpeedEvasionWeight,
            DefaultLateralWeight, DefaultClosingWeight, DefaultSpreadPerGun, DefaultWalkingGain);

        public const float DefaultTrackingHalfTime = 2f;
        public const float DefaultDistanceCoefficient = 0.01f;
        public const float DefaultSpeedEvasionWeight = 0.5f;
        public const float DefaultLateralWeight = 0.7f;
        public const float DefaultClosingWeight = 0.3f;
        public const float DefaultSpreadPerGun = 0.05f;
        public const float DefaultWalkingGain = 0.5f;
    }

    /// <summary>
    /// 射撃管制（FCS・照準）の純ロジック（#射撃管制）。
    /// 「弾がどれだけのダメージを与えるか」ではなく「<b>そもそも命中するか</b>」をモデル化する＝
    /// <see cref="ShipCombat.ComputeDamage"/>（既存のダメージ式）とは別系統の照準モデル。
    /// 照準解の質（センサー×追尾時間）→距離での基礎命中→目標の回避・相対運動による低下→実効命中率、
    /// さらに斉射の散布界と修正射撃（walking）の改善を扱う。決定論（乱数は <c>roll</c> 引数のみ）・
    /// 実効値パターン（入力の基準値を破壊しない）・入力 Clamp・test-first。
    /// </summary>
    public static class FireControlRules
    {
        // ---- 照準解の質：センサー精度 × 追尾時間 ----

        /// <summary>既定パラメータ版。</summary>
        public static float TargetingSolution(float sensorQuality, float trackingTime)
            => TargetingSolution(sensorQuality, trackingTime, FireControlParams.Default);

        /// <summary>
        /// 照準解の質（0..1）を返す。`センサー精度 × 追尾項`。
        /// 追尾項＝t/(t+halfTime)（追尾し続けるほど1へ漸近・halfTime で0.5）。Log/Exp を使わない有理飽和。
        /// </summary>
        public static float TargetingSolution(float sensorQuality, float trackingTime, FireControlParams p)
        {
            float sensor = Mathf.Clamp01(sensorQuality);
            float t = Mathf.Max(0f, trackingTime);
            float track = t / (t + p.trackingHalfTime); // 0..1（単調増・漸近1）
            return Mathf.Clamp01(sensor * track);
        }

        // ---- 基礎命中率：照準解 / (1 + 距離係数 × 距離) ----

        /// <summary>既定パラメータ版。</summary>
        public static float BaseHitChance(float targetingSolution, float distance)
            => BaseHitChance(targetingSolution, distance, FireControlParams.Default);

        /// <summary>
        /// 基礎命中率（0..1）を返す。`解 / (1 + coef × 距離)`。距離0で解そのもの、遠いほど減衰。
        /// </summary>
        public static float BaseHitChance(float targetingSolution, float distance, FireControlParams p)
        {
            float solution = Mathf.Clamp01(targetingSolution);
            float d = Mathf.Max(0f, distance);
            float denom = 1f + p.distanceCoefficient * d; // ≥1
            return Mathf.Clamp01(solution / denom);
        }

        // ---- 回避ペナルティ：目標の回避 × 速度 ----

        /// <summary>既定パラメータ版。</summary>
        public static float EvasionPenalty(float targetEvasion, float targetSpeed)
            => EvasionPenalty(targetEvasion, targetSpeed, FireControlParams.Default);

        /// <summary>
        /// 命中率を下げる回避量（0..1）を返す。`回避 × (1 + speedWeight × 速度) を 0..1 にならす`。
        /// 速い目標ほど回避が効く。速度は有理飽和で 0..1 に押し込む（暴走しない）。
        /// </summary>
        public static float EvasionPenalty(float targetEvasion, float targetSpeed, FireControlParams p)
        {
            float evasion = Mathf.Clamp01(targetEvasion);
            float speed = Mathf.Max(0f, targetSpeed);
            // 速度の効きを 0..1 に飽和（w×v/(1+w×v)）。w=0 なら速度無効。
            float sw = p.speedEvasionWeight * speed;
            float speedFactor = sw / (1f + sw); // 0..1
            // 回避基礎に、速度ぶんだけ残り余地を上乗せ（回避0なら速度があっても0）。
            float penalty = evasion + (1f - evasion) * evasion * speedFactor;
            return Mathf.Clamp01(penalty);
        }

        // ---- 相対運動誤差：接近 × closingWeight ＋ 横 × lateralWeight ----

        /// <summary>既定パラメータ版。</summary>
        public static float RelativeMotionError(float closingRate, float lateralRate)
            => RelativeMotionError(closingRate, lateralRate, FireControlParams.Default);

        /// <summary>
        /// 相対運動による照準誤差（0..1）を返す。横方向（リード角）を重く見る：
        /// `raw = lateralWeight×|横| ＋ closingWeight×|接近|`、これを有理飽和で 0..1 へ。
        /// </summary>
        public static float RelativeMotionError(float closingRate, float lateralRate, FireControlParams p)
        {
            float lateral = Mathf.Abs(lateralRate);
            float closing = Mathf.Abs(closingRate);
            float raw = p.lateralWeight * lateral + p.closingWeight * closing; // ≥0
            float error = raw / (1f + raw); // 0..1 飽和
            return Mathf.Clamp01(error);
        }

        // ---- 斉射の散布界：砲数 × (1 - FCS精度) ----

        /// <summary>既定パラメータ版。</summary>
        public static float SalvoSpread(int gunCount, float fireControlQuality)
            => SalvoSpread(gunCount, fireControlQuality, FireControlParams.Default);

        /// <summary>
        /// 斉射の散布界（0..1・大きいほど散る）を返す。`(門数-1)×spreadPerGun×(1-FCS精度) を 0..1 にならす`。
        /// 1門以下は散布0。FCS精度が高いほど散らない。
        /// </summary>
        public static float SalvoSpread(int gunCount, float fireControlQuality, FireControlParams p)
        {
            int guns = Mathf.Max(0, gunCount);
            float fcs = Mathf.Clamp01(fireControlQuality);
            if (guns <= 1) return 0f;
            float raw = (guns - 1) * p.spreadPerGun * (1f - fcs); // ≥0
            float spread = raw / (1f + raw); // 0..1 飽和
            return Mathf.Clamp01(spread);
        }

        // ---- 実効命中率：基礎 × (1-回避) × (1-運動誤差) ----

        /// <summary>既定パラメータ版。</summary>
        public static float EffectiveHitChance(float baseHitChance, float evasionPenalty, float relativeMotionError)
            => EffectiveHitChance(baseHitChance, evasionPenalty, relativeMotionError, FireControlParams.Default);

        /// <summary>
        /// 実効命中率（0..1）を返す。`基礎命中 × (1-回避) × (1-運動誤差)`。
        /// 各項は乗算で重なる＝どれかが大きいと一気に当たらなくなる。
        /// </summary>
        public static float EffectiveHitChance(float baseHitChance, float evasionPenalty, float relativeMotionError, FireControlParams p)
        {
            float baseHit = Mathf.Clamp01(baseHitChance);
            float evasion = Mathf.Clamp01(evasionPenalty);
            float motion = Mathf.Clamp01(relativeMotionError);
            float eff = baseHit * (1f - evasion) * (1f - motion);
            return Mathf.Clamp01(eff);
        }

        // ---- 修正射撃（walking）：前回外れ × FCS精度 ----

        /// <summary>既定パラメータ版。</summary>
        public static float WalkingCorrection(float previousMiss, float fireControlQuality)
            => WalkingCorrection(previousMiss, fireControlQuality, FireControlParams.Default);

        /// <summary>
        /// 修正射撃による次弾命中の改善量（0..1）を返す。`前回の外れ量 × FCS精度 × walkingGain`。
        /// 前回大きく外したほど・FCSが良いほど、次の斉射で照準を寄せられる（命中率に上乗せする加算項）。
        /// </summary>
        public static float WalkingCorrection(float previousMiss, float fireControlQuality, FireControlParams p)
        {
            float miss = Mathf.Clamp01(previousMiss);
            float fcs = Mathf.Clamp01(fireControlQuality);
            float improve = miss * fcs * p.walkingGain;
            return Mathf.Clamp01(improve);
        }

        // ---- 命中判定（決定論・roll 注入） ----

        /// <summary>
        /// 命中したか。`roll < 実効命中率` で命中（roll は 0..1 へ Clamp）。乱数は呼び出し側が注入＝決定論。
        /// </summary>
        public static bool IsHit(float effectiveHitChance, float roll)
        {
            float chance = Mathf.Clamp01(effectiveHitChance);
            float r = Mathf.Clamp01(roll);
            return r < chance;
        }
    }
}
