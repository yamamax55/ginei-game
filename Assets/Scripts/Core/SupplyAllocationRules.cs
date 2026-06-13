using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 供給の配分（SCM-4・#2105・純ロジック）。供給が需要に足りないとき競合需要へ比例配分する（最適化ソルバ不使用＝貪欲・集約）。
    /// test-first。
    /// </summary>
    public static class SupplyAllocationRules
    {
        /// <summary>
        /// 限られた供給を需要へ比例配分（各需要＝supply×シェアを需要上限でクランプ）。配分量の配列を返す。
        /// </summary>
        public static float[] AllocateProportional(float supply, IReadOnlyList<float> demands)
        {
            int n = demands?.Count ?? 0;
            var result = new float[n];
            if (n == 0) return result;

            float total = 0f;
            for (int i = 0; i < n; i++) total += Mathf.Max(0f, demands[i]);
            if (total <= 0f) return result;

            float s = Mathf.Max(0f, supply);
            for (int i = 0; i < n; i++)
            {
                float d = Mathf.Max(0f, demands[i]);
                result[i] = Mathf.Min(d, s * (d / total)); // 需要上限でクランプ
            }
            return result;
        }

        /// <summary>充足率＝配分/需要（需要0以下は1）。</summary>
        public static float FillRate(float allocated, float demand)
            => demand <= 0f ? 1f : Mathf.Clamp01(Mathf.Max(0f, allocated) / demand);
    }
}
