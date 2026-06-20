using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 連結領域の供給プール集計（DIST-3・#2112・純ロジック）。総余剰/総不足を出し、回廊容量で律速、輸送ロスで目減りさせる。
    /// test-first。
    /// </summary>
    public static class DistributionPoolRules
    {
        /// <summary>領域の総余剰＝Σ max(0, 生産−需要)。</summary>
        public static float TotalSurplus(IReadOnlyList<float> production, IReadOnlyList<float> demand)
        {
            int n = production?.Count ?? 0;
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                float d = (demand != null && i < demand.Count) ? demand[i] : 0f;
                sum += SupplyBalanceRules.Surplus(production[i], d);
            }
            return sum;
        }

        /// <summary>領域の総不足＝Σ max(0, 需要−生産)。</summary>
        public static float TotalDeficit(IReadOnlyList<float> production, IReadOnlyList<float> demand)
        {
            int n = production?.Count ?? 0;
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                float d = (demand != null && i < demand.Count) ? demand[i] : 0f;
                sum += SupplyBalanceRules.Deficit(production[i], d);
            }
            return sum;
        }

        /// <summary>輸送可能量＝min(総余剰, 回廊容量)。</summary>
        public static float Transportable(float totalSurplus, float throughputCap)
            => Mathf.Max(0f, Mathf.Min(Mathf.Max(0f, totalSurplus), Mathf.Max(0f, throughputCap)));

        /// <summary>配送量＝輸送可能量×(1−輸送ロス)。</summary>
        public static float Delivered(float transportable, float loss)
            => Mathf.Max(0f, transportable) * (1f - Mathf.Clamp01(loss));

        /// <summary>領域の不足充足率＝clamp01(配送/総不足)。総不足0以下は1。</summary>
        public static float FillRate(float delivered, float totalDeficit)
            => totalDeficit <= 0f ? 1f : Mathf.Clamp01(Mathf.Max(0f, delivered) / totalDeficit);
    }
}
