using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 縦深打撃（ディープ・ストライク）の純ロジック。
    /// 前線で押し合うのでなく、機動部隊が敵の後方深くへ突進し、
    /// 補給拠点・司令部・生産中枢を直接叩く。混乱と麻痺を与えるが、
    /// 突出した部隊は補給線が伸びて孤立リスクを負う。
    ///
    /// 分担（重複実装しない）：
    /// - <see cref="InterdictionRules"/>（阻止攻撃＝後方で補給の流量を扼す面的遮断）とは別＝
    ///   こちらは後方中枢への「直接打撃」（流量制限でなく拠点破壊）。
    /// - TurningMovementRules（敵連絡線を脅かす迂回＝間接的に退却を強いる）とは別＝
    ///   こちらは連絡線でなく後方中枢そのものを叩く正面突破型。
    /// - ManeuverEnvelopmentRules（包囲＝敵を取り囲んで殲滅）とは別＝
    ///   こちらは囲まず突き抜けて中枢を麻痺させる縦深突進。
    /// 盤面非依存の plain 引数・乱数なし・実効値パターン（基準値非破壊）。
    /// </summary>
    public static class DeepStrikeRules
    {
        /// <summary>縦深打撃の係数。トップレベル readonly struct・全 Clamp。</summary>
        public readonly struct DeepStrikeParams
        {
            /// <summary>打撃部隊機動の効き（突破力の利き）。</summary>
            public readonly float penetrationScale;
            /// <summary>どんなに弱い縦深でも侵入できる最小深度（0..1）。</summary>
            public readonly float depthFloor;
            /// <summary>後方目標価値→全体混乱への変換係数（価値の正規化込み）。</summary>
            public readonly float disruptionScale;
            /// <summary>侵入深度あたりの補給線の伸び。</summary>
            public readonly float tetherScale;
            /// <summary>突出による孤立リスクの効き。</summary>
            public readonly float isolationScale;
            /// <summary>奇襲突進の衝撃の効き。</summary>
            public readonly float shockScale;
            /// <summary>補給線が伸びきったとみなす下限（割り算の保護）。</summary>
            public readonly float tetherRangeFloor;
            /// <summary>後方が崩壊したとみなす混乱の閾値（0..1）。</summary>
            public readonly float collapseThreshold;

            public DeepStrikeParams(
                float penetrationScale,
                float depthFloor,
                float disruptionScale,
                float tetherScale,
                float isolationScale,
                float shockScale,
                float tetherRangeFloor,
                float collapseThreshold)
            {
                this.penetrationScale = Mathf.Max(0f, penetrationScale);
                this.depthFloor = Mathf.Clamp01(depthFloor);
                this.disruptionScale = Mathf.Max(0f, disruptionScale);
                this.tetherScale = Mathf.Max(0f, tetherScale);
                this.isolationScale = Mathf.Max(0f, isolationScale);
                this.shockScale = Mathf.Max(0f, shockScale);
                this.tetherRangeFloor = Mathf.Max(0.0001f, tetherRangeFloor);
                this.collapseThreshold = Mathf.Clamp01(collapseThreshold);
            }

            public static DeepStrikeParams Default => new DeepStrikeParams(
                penetrationScale: 1.2f,
                depthFloor: 0.1f,
                disruptionScale: 0.01f,
                tetherScale: 1.0f,
                isolationScale: 1.0f,
                shockScale: 1.0f,
                tetherRangeFloor: 0.05f,
                collapseThreshold: 0.7f);
        }

        /// <summary>
        /// 敵後方へどれだけ深く侵入できるか（0..1）。
        /// 打撃部隊機動（×利き）と敵縦深防御の比＝機動が勝るほど深く突き抜ける。
        /// 最弱の縦深でも depthFloor ぶんは必ず侵入できる。
        /// </summary>
        public static float PenetrationDepth(float strikeForceMobility, float enemyDefenseDepth, DeepStrikeParams p)
        {
            float m = Mathf.Max(0f, strikeForceMobility) * p.penetrationScale;
            float d = Mathf.Max(0f, enemyDefenseDepth);
            float denom = m + d;
            float depth = denom <= 0f ? 0f : m / denom;
            return Mathf.Clamp01(Mathf.Max(depth, p.depthFloor));
        }

        public static float PenetrationDepth(float strikeForceMobility, float enemyDefenseDepth)
            => PenetrationDepth(strikeForceMobility, enemyDefenseDepth, DeepStrikeParams.Default);

        /// <summary>
        /// 叩いた後方目標の価値。侵入が深いほど中枢（司令部・生産中枢）に届く。
        /// 侵入深度×目標の重要度。
        /// </summary>
        public static float RearTargetValue(float penetrationDepth, float targetImportance)
        {
            float pen = Mathf.Clamp01(penetrationDepth);
            float imp = Mathf.Max(0f, targetImportance);
            return pen * imp;
        }

        /// <summary>
        /// 後方破壊が敵全体に与える混乱・麻痺（0..1）。
        /// 後方目標価値×敵の後方依存度×変換係数。中枢依存が高いほど麻痺する。
        /// </summary>
        public static float SystemicDisruption(float rearTargetValue, float enemyDependency, DeepStrikeParams p)
        {
            float v = Mathf.Max(0f, rearTargetValue);
            float dep = Mathf.Clamp01(enemyDependency);
            return Mathf.Clamp01(v * dep * p.disruptionScale);
        }

        public static float SystemicDisruption(float rearTargetValue, float enemyDependency)
            => SystemicDisruption(rearTargetValue, enemyDependency, DeepStrikeParams.Default);

        /// <summary>
        /// 突出で伸びきった自軍の補給線。深く入るほど補給範囲を超えて伸びる。
        /// 侵入深度×伸び／自軍補給範囲（≧1 で補給範囲を超過＝危険域）。
        /// </summary>
        public static float SupplyTether(float penetrationDepth, float ownSupplyRange, DeepStrikeParams p)
        {
            float pen = Mathf.Clamp01(penetrationDepth);
            float range = Mathf.Max(p.tetherRangeFloor, ownSupplyRange);
            return pen * p.tetherScale / range;
        }

        public static float SupplyTether(float penetrationDepth, float ownSupplyRange)
            => SupplyTether(penetrationDepth, ownSupplyRange, DeepStrikeParams.Default);

        /// <summary>
        /// 深入りした打撃部隊が包囲・孤立するリスク（0..1）。
        /// 侵入深度×敵予備兵力×利き＝深く入るほど、敵予備が厚いほど閉じ込められる。
        /// </summary>
        public static float IsolationRisk(float penetrationDepth, float enemyReserves, DeepStrikeParams p)
        {
            float pen = Mathf.Clamp01(penetrationDepth);
            float res = Mathf.Clamp01(enemyReserves);
            return Mathf.Clamp01(pen * res * p.isolationScale);
        }

        public static float IsolationRisk(float penetrationDepth, float enemyReserves)
            => IsolationRisk(penetrationDepth, enemyReserves, DeepStrikeParams.Default);

        /// <summary>
        /// 奇襲的な突進の衝撃（0..1）。
        /// 突進速度×奇襲度×利き＝速く不意を突くほど混乱が増幅される。
        /// </summary>
        public static float ShockEffect(float penetrationSpeed, float surprise, DeepStrikeParams p)
        {
            float speed = Mathf.Clamp01(penetrationSpeed);
            float surp = Mathf.Clamp01(surprise);
            return Mathf.Clamp01(speed * surp * p.shockScale);
        }

        public static float ShockEffect(float penetrationSpeed, float surprise)
            => ShockEffect(penetrationSpeed, surprise, DeepStrikeParams.Default);

        /// <summary>
        /// 縦深打撃の正味価値。後方混乱の利得から孤立リスクを差し引く。
        /// 麻痺させても孤立すれば帳消し＝突出と戦果のトレードオフ。
        /// </summary>
        public static float DeepStrikeNetValue(float systemicDisruption, float isolationRisk)
        {
            float gain = Mathf.Clamp01(systemicDisruption);
            float risk = Mathf.Clamp01(isolationRisk);
            return Mathf.Clamp(gain - risk, -1f, 1f);
        }

        /// <summary>
        /// 敵後方が崩壊したか（混乱が閾値以上）。
        /// </summary>
        public static bool IsRearCollapsed(float systemicDisruption, DeepStrikeParams p)
        {
            return Mathf.Clamp01(systemicDisruption) >= p.collapseThreshold;
        }

        public static bool IsRearCollapsed(float systemicDisruption)
            => IsRearCollapsed(systemicDisruption, DeepStrikeParams.Default);
    }
}
