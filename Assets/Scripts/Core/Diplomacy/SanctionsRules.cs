using UnityEngine;

namespace Ginei
{
    /// <summary>経済制裁の調整係数。</summary>
    public readonly struct SanctionsParams
    {
        /// <summary>制裁強度最大・抜け穴ゼロのとき相手産出を削る最大割合。</summary>
        public readonly float maxOutputPenalty;
        /// <summary>制裁側が自分も払うコストの係数（相手との交易依存度に掛かる）。</summary>
        public readonly float selfCostScale;
        /// <summary>抜け穴（第三国経由の迂回）が時間で広がる速度（per dt）。</summary>
        public readonly float leakageGrowthRate;
        /// <summary>制裁が「効いている」とみなす実効ペナルティの閾値。</summary>
        public readonly float effectiveThreshold;

        public SanctionsParams(float maxOutputPenalty, float selfCostScale, float leakageGrowthRate, float effectiveThreshold)
        {
            this.maxOutputPenalty = Mathf.Clamp01(maxOutputPenalty);
            this.selfCostScale = Mathf.Max(0f, selfCostScale);
            this.leakageGrowthRate = Mathf.Max(0f, leakageGrowthRate);
            this.effectiveThreshold = Mathf.Clamp01(effectiveThreshold);
        }

        /// <summary>既定＝最大ペナルティ40%・自己コスト係数0.5・抜け穴成長0.02・有効閾値0.1。</summary>
        public static SanctionsParams Default => new SanctionsParams(0.4f, 0.5f, 0.02f, 0.1f);
    }

    /// <summary>
    /// 経済制裁の純ロジック（平時の経済戦）。制裁は相手の産出を締めるが、第三国経由の抜け穴（リーク）で
    /// 効果が漏れ、しかも抜け穴は時間とともに広がる＝制裁は長引くほど鈍る。相手との交易依存度が高いほど
    /// 自国も返り血を浴びる＝制裁は無料ではない。軍事封鎖（<see cref="BlockadeRules"/>＝回廊の物理遮断）
    /// とは別系統。倍率は産出係数に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SanctionsRules
    {
        /// <summary>
        /// 相手産出への実効ペナルティ（0..maxOutputPenalty）＝制裁強度(0..1)×（1−抜け穴(0..1)）×最大幅。
        /// 抜け穴が全開なら効果ゼロ。
        /// </summary>
        public static float OutputPenalty(float severity, float leakage, SanctionsParams p)
        {
            return Mathf.Clamp01(severity) * (1f - Mathf.Clamp01(leakage)) * p.maxOutputPenalty;
        }

        public static float OutputPenalty(float severity, float leakage)
            => OutputPenalty(severity, leakage, SanctionsParams.Default);

        /// <summary>被制裁側の産出倍率（1−実効ペナルティ）。産出係数に掛けて使う。</summary>
        public static float TargetOutputFactor(float severity, float leakage, SanctionsParams p)
        {
            return 1f - OutputPenalty(severity, leakage, p);
        }

        public static float TargetOutputFactor(float severity, float leakage)
            => TargetOutputFactor(severity, leakage, SanctionsParams.Default);

        /// <summary>
        /// 制裁側の自己コスト（0..1）＝制裁強度×相手との交易依存度(0..1)×係数。
        /// 太い取引相手を締めるほど自分の腹も痛む。
        /// </summary>
        public static float SelfCost(float severity, float tradeDependence, SanctionsParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(severity) * Mathf.Clamp01(tradeDependence) * p.selfCostScale);
        }

        public static float SelfCost(float severity, float tradeDependence)
            => SelfCost(severity, tradeDependence, SanctionsParams.Default);

        /// <summary>
        /// 抜け穴の1tick後の値（0..1）。制裁が強いほど迂回の旨味が大きく、抜け穴は速く広がる
        /// （成長速度×強度×dt）。制裁を解けば広がりは止まる（強度0で成長0）。
        /// </summary>
        public static float LeakageTick(float leakage, float severity, float dt, SanctionsParams p)
        {
            float growth = p.leakageGrowthRate * Mathf.Clamp01(severity) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(leakage) + growth);
        }

        public static float LeakageTick(float leakage, float severity, float dt)
            => LeakageTick(leakage, severity, dt, SanctionsParams.Default);

        /// <summary>制裁がまだ効いているか＝実効ペナルティが閾値以上（形骸化判定）。</summary>
        public static bool IsEffective(float severity, float leakage, SanctionsParams p)
        {
            return OutputPenalty(severity, leakage, p) >= p.effectiveThreshold;
        }

        public static bool IsEffective(float severity, float leakage)
            => IsEffective(severity, leakage, SanctionsParams.Default);
    }
}
