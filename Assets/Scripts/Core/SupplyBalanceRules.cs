using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 惑星の品目需給バランス（DIST-1・#2112・純ロジック）。生産−需要で余剰/不足を出す。
    /// `DistributionPlanRules.NetPosition`#2105 の余剰/不足分解。test-first。
    /// </summary>
    public static class SupplyBalanceRules
    {
        /// <summary>余剰＝max(0, 生産−需要)。</summary>
        public static float Surplus(float production, float demand)
            => Mathf.Max(0f, production - demand);

        /// <summary>不足＝max(0, 需要−生産)。</summary>
        public static float Deficit(float production, float demand)
            => Mathf.Max(0f, demand - production);

        /// <summary>ネットポジション＝生産−需要（余剰+/不足−）。</summary>
        public static float NetPosition(float production, float demand)
            => production - demand;
    }
}
