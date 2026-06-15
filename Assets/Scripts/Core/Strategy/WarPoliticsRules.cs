using UnityEngine;

namespace Ginei
{
    /// <summary>出兵の政治の調整係数（アムリッツァ型＝人気取りの大遠征）。</summary>
    public readonly struct WarPoliticsParams
    {
        /// <summary>出兵誘因が立ち始める支持率の閾値（これ未満で「戦争で挽回」が魅力になる）。</summary>
        public readonly float desperationThreshold;
        /// <summary>勝利の支持回復の最大幅（戦争規模最大のとき）。</summary>
        public readonly float victoryBounceScale;
        /// <summary>敗北の支持喪失の最大幅（規模と損耗に比例）。</summary>
        public readonly float defeatPenaltyScale;
        /// <summary>政権崩壊とみなす支持率の下限。</summary>
        public readonly float collapseThreshold;

        public WarPoliticsParams(float desperationThreshold, float victoryBounceScale,
                                 float defeatPenaltyScale, float collapseThreshold)
        {
            this.desperationThreshold = Mathf.Clamp01(desperationThreshold);
            this.victoryBounceScale = Mathf.Max(0f, victoryBounceScale);
            this.defeatPenaltyScale = Mathf.Max(0f, defeatPenaltyScale);
            this.collapseThreshold = Mathf.Clamp01(collapseThreshold);
        }

        /// <summary>既定＝誘因閾値0.4・勝利回復幅0.3・敗北喪失幅0.5・崩壊閾値0.15。</summary>
        public static WarPoliticsParams Default => new WarPoliticsParams(0.4f, 0.3f, 0.5f, 0.15f);
    }

    /// <summary>
    /// 出兵の政治の純ロジック（アムリッツァ型）。支持率の低迷した政権ほど、選挙が近いほど、
    /// 「戦争で挽回」の誘因が強くなる＝軍事的合理性でなく政治的必要から出兵が決まる。
    /// 勝てば支持は跳ね（規模に比例）、負ければ規模×損耗に比例して支持が崩れ、閾値を割れば政権は倒れる。
    /// 政党・選挙の仕組みは <see cref="PartyRules"/>、戦争目標・厭戦は <see cref="WarGoalRules"/> が担い、
    /// ここは「内政事情→出兵誘因→戦果→政権」の写像のみを扱う。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class WarPoliticsRules
    {
        /// <summary>
        /// 出兵誘因（0..1）。支持率が閾値未満のとき不足分に比例して立ち、選挙の近さ electionProximity
        /// （0=遠い..1=目前）で増幅される（不足×(0.5+0.5×近さ)）。支持が閾値以上なら0＝健全な政権は博打を打たない。
        /// </summary>
        public static float WarIncentive(float support, float electionProximity, WarPoliticsParams p)
        {
            float s = Mathf.Clamp01(support);
            if (s >= p.desperationThreshold) return 0f;
            float shortfall = (p.desperationThreshold - s) / Mathf.Max(1e-4f, p.desperationThreshold);
            return Mathf.Clamp01(shortfall * (0.5f + 0.5f * Mathf.Clamp01(electionProximity)));
        }

        public static float WarIncentive(float support, float electionProximity)
            => WarIncentive(support, electionProximity, WarPoliticsParams.Default);

        /// <summary>勝利後の支持率（0..1）＝現支持＋勝利回復幅×戦争規模(0..1)。大勝利ほど大きく跳ねる。</summary>
        public static float SupportAfterVictory(float support, float warScale, WarPoliticsParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(support) + p.victoryBounceScale * Mathf.Clamp01(warScale));
        }

        public static float SupportAfterVictory(float support, float warScale)
            => SupportAfterVictory(support, warScale, WarPoliticsParams.Default);

        /// <summary>
        /// 敗北後の支持率（0..1）＝現支持−喪失幅×戦争規模×（0.5＋0.5×損耗率 casualtyRatio(0..1)）。
        /// 大規模な出兵で大損害を出すほど支持の崩落が深い（アムリッツァ）。
        /// </summary>
        public static float SupportAfterDefeat(float support, float warScale, float casualtyRatio, WarPoliticsParams p)
        {
            float severity = Mathf.Clamp01(warScale) * (0.5f + 0.5f * Mathf.Clamp01(casualtyRatio));
            return Mathf.Clamp01(Mathf.Clamp01(support) - p.defeatPenaltyScale * severity);
        }

        public static float SupportAfterDefeat(float support, float warScale, float casualtyRatio)
            => SupportAfterDefeat(support, warScale, casualtyRatio, WarPoliticsParams.Default);

        /// <summary>政権が倒れるか＝支持率が崩壊閾値未満。</summary>
        public static bool GovernmentFalls(float support, WarPoliticsParams p)
        {
            return Mathf.Clamp01(support) < p.collapseThreshold;
        }

        public static bool GovernmentFalls(float support) => GovernmentFalls(support, WarPoliticsParams.Default);

        /// <summary>
        /// 博打としての期待支持変動＝勝率 winChance(0..1) で重みづけた勝敗後支持の期待値−現支持。
        /// 正なら出兵は政権延命の期待値プラス（だから追い詰められた政権は無謀な遠征を選ぶ）。
        /// </summary>
        public static float ExpectedSupportSwing(float support, float warScale, float casualtyRatio, float winChance, WarPoliticsParams p)
        {
            float w = Mathf.Clamp01(winChance);
            float expected = w * SupportAfterVictory(support, warScale, p)
                           + (1f - w) * SupportAfterDefeat(support, warScale, casualtyRatio, p);
            return expected - Mathf.Clamp01(support);
        }

        public static float ExpectedSupportSwing(float support, float warScale, float casualtyRatio, float winChance)
            => ExpectedSupportSwing(support, warScale, casualtyRatio, winChance, WarPoliticsParams.Default);
    }
}
