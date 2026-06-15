using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// MRP所要量展開（SCM-1・#2105・純ロジック）。最終需要を `RecipeBook`#2098 で再帰的に逆算し、
    /// 全品目の総所要量（gross requirements）を出す。歩留まり考慮（grossOut=demand/yield→子需要=grossOut×投入量）。
    /// レオンチェフのBOM展開＝集約（個体ロットへ降りない）。循環/深さガード付き。test-first。
    /// </summary>
    public static class RequirementsExplosionRules
    {
        public const int DefaultMaxDepth = 16;

        /// <summary>単一の最終需要を展開し、品目→総所要の新規 dict を返す。</summary>
        public static Dictionary<int, float> Explode(int outputId, float demand, int maxDepth = DefaultMaxDepth)
        {
            var acc = new Dictionary<int, float>();
            Accumulate(outputId, demand, acc, maxDepth);
            return acc;
        }

        /// <summary>最終需要を既存 dict へ加算展開（複数の最終需要を1つへ集約できる）。</summary>
        public static void Accumulate(int outputId, float demand, Dictionary<int, float> acc, int maxDepth = DefaultMaxDepth)
        {
            if (acc == null || demand <= 0f || maxDepth < 0) return;
            acc.TryGetValue(outputId, out float cur);
            acc[outputId] = cur + demand;

            Recipe r = RecipeBook.ForOutput(outputId);
            if (r == null) return; // 原材料/葉＝これ以上展開しない

            float yield = Mathf.Clamp01(r.yield);
            float grossOut = yield > 0f ? demand / yield : demand;
            for (int i = 0; i < r.inputs.Count; i++)
            {
                var inp = r.inputs[i];
                if (inp.quantity <= 0f) continue;
                Accumulate(inp.commodityId, grossOut * inp.quantity, acc, maxDepth - 1);
            }
        }
    }
}
