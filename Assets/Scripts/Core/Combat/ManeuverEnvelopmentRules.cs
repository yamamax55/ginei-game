using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 機動包囲の調整パラメータ（実効値パターン・基準値非破壊）。
    /// ctor で全フィールドをクランプし不正値を弾く。
    /// </summary>
    public readonly struct ManeuverEnvelopmentParams
    {
        /// <summary>進捗→側背面露出の傾き（>1 で露出が進捗より速く立ち上がる）。</summary>
        public readonly float flankExposureScale;
        /// <summary>側背面奪取で封じられる敵火力の最大割合(0..1)。</summary>
        public readonly float firepowerDenialMax;
        /// <summary>突破脱出の基礎確率(0..1)。</summary>
        public readonly float breakoutBase;
        /// <summary>包囲された側の機動が突破に効く度合い(0..1)。</summary>
        public readonly float mobilityBreakoutScale;
        /// <summary>回り込み時間×敵反応速度→裏をかかれるリスクの傾き(0..1)。</summary>
        public readonly float reactionRiskScale;
        /// <summary>包囲成立とみなす進捗のしきい値(0..1)。</summary>
        public readonly float envelopThreshold;

        public ManeuverEnvelopmentParams(
            float flankExposureScale,
            float firepowerDenialMax,
            float breakoutBase,
            float mobilityBreakoutScale,
            float reactionRiskScale,
            float envelopThreshold)
        {
            this.flankExposureScale = Mathf.Clamp(flankExposureScale, 0.1f, 4f);
            this.firepowerDenialMax = Mathf.Clamp01(firepowerDenialMax);
            this.breakoutBase = Mathf.Clamp01(breakoutBase);
            this.mobilityBreakoutScale = Mathf.Clamp01(mobilityBreakoutScale);
            this.reactionRiskScale = Mathf.Clamp01(reactionRiskScale);
            this.envelopThreshold = Mathf.Clamp01(envelopThreshold);
        }

        public static ManeuverEnvelopmentParams Default =>
            new ManeuverEnvelopmentParams(1.2f, 0.85f, 0.5f, 0.4f, 0.5f, 0.7f);
    }

    /// <summary>
    /// 機動包囲＝機動で敵の側面・背後に回り込む戦術プロセスの純ロジック（static）。
    /// 正面火力でなく機動で側背面を奪い、敵が火力を向けられない状態を作る。
    /// ただし回り込みには時間が要り、敵の対応（裏返し）で崩れうる。
    ///
    /// 分担：
    /// - <c>EncirclementRules</c>（包囲度・降伏の抽象的解決）とは別＝こちらは「側背面を機動で奪う過程」に特化。
    /// - <c>EnvelopmentRules</c>（もしあれば）とは別＝戦術機動の進捗/利得モデル。
    /// - <c>TurningMovementRules</c>（敵連絡線を脅かす作戦級の大迂回）とは別＝戦術的包囲。
    /// 盤面非依存・plain 引数・乱数なし（必要なら roll を渡す）。
    /// </summary>
    public static class ManeuverEnvelopmentRules
    {
        // ---- 回り込みの進捗 ----
        /// <summary>
        /// 回り込みの進捗(0..1)を1tickぶん進める。
        /// 機動速度×経過時間 を回り込みに要する距離で割る（速いほど・近いほど速く進む）。
        /// </summary>
        public static float EnvelopmentProgress(float currentProgress, float maneuverSpeed, float distance, float dt)
        {
            currentProgress = Mathf.Clamp01(currentProgress);
            maneuverSpeed = Mathf.Max(0f, maneuverSpeed);
            // 距離0は瞬時到達（即・完了側へ）。下限で0除算回避。
            distance = Mathf.Max(0.01f, distance);
            dt = Mathf.Max(0f, dt);
            float gain = (maneuverSpeed * dt) / distance;
            return Mathf.Clamp01(currentProgress + gain);
        }

        // ---- 側背面の露出 ----
        /// <summary>包囲が進むほど敵の側背面が晒される(0..1)。進捗を scale 倍してクランプ。</summary>
        public static float FlankExposure(float envelopmentProgress, ManeuverEnvelopmentParams p)
        {
            envelopmentProgress = Mathf.Clamp01(envelopmentProgress);
            return Mathf.Clamp01(envelopmentProgress * p.flankExposureScale);
        }

        public static float FlankExposure(float envelopmentProgress) =>
            FlankExposure(envelopmentProgress, ManeuverEnvelopmentParams.Default);

        // ---- 火力封殺 ----
        /// <summary>側背面を取ると敵が火力を正面へ向けられない度合い(0..最大封殺割合)。</summary>
        public static float FirepowerDenial(float flankExposure, ManeuverEnvelopmentParams p)
        {
            flankExposure = Mathf.Clamp01(flankExposure);
            return Mathf.Clamp01(flankExposure * p.firepowerDenialMax);
        }

        public static float FirepowerDenial(float flankExposure) =>
            FirepowerDenial(flankExposure, ManeuverEnvelopmentParams.Default);

        // ---- 包囲圧力 ----
        /// <summary>
        /// 包囲の圧力(0..1)。包囲進捗 × 包む側の兵力割合（多くで包むほど圧力が高い）。
        /// encirclingFraction は包囲側／総兵力 の割合(0..1)。
        /// </summary>
        public static float EncirclementPressure(float envelopmentProgress, float encirclingFraction)
        {
            envelopmentProgress = Mathf.Clamp01(envelopmentProgress);
            encirclingFraction = Mathf.Clamp01(encirclingFraction);
            return Mathf.Clamp01(envelopmentProgress * encirclingFraction);
        }

        // ---- 突破脱出 ----
        /// <summary>
        /// 包囲された側の突破脱出の可能性(0..1)。圧力が高いほど下がり、被包囲側の機動が高いほど上がる。
        /// = clamp01( (breakoutBase + 機動×mobilityScale) × (1 - 圧力) )
        /// </summary>
        public static float BreakoutChance(float encirclementPressure, float encircledMobility, ManeuverEnvelopmentParams p)
        {
            encirclementPressure = Mathf.Clamp01(encirclementPressure);
            encircledMobility = Mathf.Clamp01(encircledMobility);
            float capacity = Mathf.Clamp01(p.breakoutBase + encircledMobility * p.mobilityBreakoutScale);
            return Mathf.Clamp01(capacity * (1f - encirclementPressure));
        }

        public static float BreakoutChance(float encirclementPressure, float encircledMobility) =>
            BreakoutChance(encirclementPressure, encircledMobility, ManeuverEnvelopmentParams.Default);

        // ---- 逆機動リスク ----
        /// <summary>
        /// 回り込み中に敵が対応して裏をかかれるリスク(0..1)。
        /// 回り込み時間が長く、敵の反応速度が速いほど高い。= clamp01( maneuverTime × enemyReactionSpeed × scale )
        /// </summary>
        public static float CounterManeuverRisk(float maneuverTime, float enemyReactionSpeed, ManeuverEnvelopmentParams p)
        {
            maneuverTime = Mathf.Max(0f, maneuverTime);
            enemyReactionSpeed = Mathf.Max(0f, enemyReactionSpeed);
            return Mathf.Clamp01(maneuverTime * enemyReactionSpeed * p.reactionRiskScale);
        }

        public static float CounterManeuverRisk(float maneuverTime, float enemyReactionSpeed) =>
            CounterManeuverRisk(maneuverTime, enemyReactionSpeed, ManeuverEnvelopmentParams.Default);

        // ---- 包囲の正味利得 ----
        /// <summary>
        /// 包囲の正味利得(0..1)。火力封殺の利得から逆機動リスクを差し引く（負はゼロでクランプ）。
        /// 敵が速く対応すればリスクが利得を食い潰す。
        /// </summary>
        public static float EnvelopmentAdvantage(float firepowerDenial, float counterManeuverRisk)
        {
            firepowerDenial = Mathf.Clamp01(firepowerDenial);
            counterManeuverRisk = Mathf.Clamp01(counterManeuverRisk);
            return Mathf.Clamp01(firepowerDenial - counterManeuverRisk);
        }

        // ---- 包囲成立 ----
        /// <summary>進捗がしきい値以上で包囲成立（bool）。</summary>
        public static bool IsEnveloped(float envelopmentProgress, float threshold)
        {
            envelopmentProgress = Mathf.Clamp01(envelopmentProgress);
            threshold = Mathf.Clamp01(threshold);
            return envelopmentProgress >= threshold;
        }

        /// <summary>既定しきい値（<see cref="ManeuverEnvelopmentParams.Default"/>）で包囲成立判定。</summary>
        public static bool IsEnveloped(float envelopmentProgress) =>
            IsEnveloped(envelopmentProgress, ManeuverEnvelopmentParams.Default.envelopThreshold);
    }
}
