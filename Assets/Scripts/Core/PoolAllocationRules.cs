using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// プールの引き出しと配り（DIST-4・#2112・純計算・非破壊）。余剰ノードから引く量と不足ノードへ配る量を出す。
    /// 配りは `SupplyAllocationRules.AllocateProportional`#2105 に委譲（ソルバ不使用＝比例配分）。test-first。
    /// </summary>
    public static class PoolAllocationRules
    {
        /// <summary>各余剰ノードから引く量＝surplus_i×(transportable/総余剰)。合計＝transportable（≤各surplus）。</summary>
        public static float[] Pulls(IReadOnlyList<float> surpluses, float transportable)
        {
            int n = surpluses?.Count ?? 0;
            var result = new float[n];
            if (n == 0) return result;
            float total = 0f;
            for (int i = 0; i < n; i++) total += Mathf.Max(0f, surpluses[i]);
            if (total <= 0f) return result;
            float t = Mathf.Max(0f, transportable);
            for (int i = 0; i < n; i++) result[i] = Mathf.Max(0f, surpluses[i]) * (t / total);
            return result;
        }

        /// <summary>各不足ノードへ配る量＝配送量を不足へ比例配分（需要上限クランプ）。</summary>
        public static float[] Receives(IReadOnlyList<float> deficits, float delivered)
            => SupplyAllocationRules.AllocateProportional(delivered, deficits);
    }
}
