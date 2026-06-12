using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 配送計画（SCM-5・#2105・純ロジック）。生産地の余剰を不足地へ回廊で送る簡易配送（貪欲・集約）。
    /// 補給線#94 の容量で律速、通商破壊#95 で遮断。全網最適化はしない。test-first。
    /// </summary>
    public static class DistributionPlanRules
    {
        /// <summary>需給ポジション＝生産−地元需要（余剰+/不足−）。</summary>
        public static float NetPosition(float production, float localDemand)
            => production - localDemand;

        /// <summary>配送量＝blocked（通商破壊）なら0／min(余剰, 不足, 回廊容量)。</summary>
        public static float Deliver(float surplus, float deficit, float lineCapacity, bool blocked)
        {
            if (blocked) return 0f;
            return Mathf.Max(0f, Mathf.Min(Mathf.Min(Mathf.Max(0f, surplus), Mathf.Max(0f, deficit)), Mathf.Max(0f, lineCapacity)));
        }
    }
}
