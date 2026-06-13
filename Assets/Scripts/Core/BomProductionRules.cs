using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 汎用BOM生産エンジン（BOM-4・#2098・純ロジック）。レシピと在庫からレオンチェフ型で生産する。
    /// 実産出＝min(目標, 各投入で作れる量×歩留まり)＝最も不足する投入がボトルネック。`ProductionConstraintRules`#2084 の汎用版。
    /// 産出ぶんだけ投入を消費する（減産時は投入も減る）。test-first。
    /// </summary>
    public static class BomProductionRules
    {
        /// <summary>
        /// 投入で作れる最大の良品産出＝min over inputs(在庫/投入量)×歩留まり。投入なし（採取）は無制約。
        /// </summary>
        public static float MaxOutput(Recipe recipe, CommodityStock stock)
        {
            if (recipe == null || stock == null) return 0f;
            float maxGross = float.MaxValue;
            for (int i = 0; i < recipe.inputs.Count; i++)
            {
                var inp = recipe.inputs[i];
                if (inp.quantity <= 0f) continue; // 係数0は無制約
                float canMake = stock.Get(inp.commodityId) / inp.quantity;
                if (canMake < maxGross) maxGross = canMake;
            }
            float yield = Mathf.Clamp01(recipe.yield);
            return maxGross == float.MaxValue ? float.MaxValue : Mathf.Max(0f, maxGross) * yield;
        }

        /// <summary>
        /// 目標産出を投入制約のもとで生産＝実産出（良品）を在庫へ加算し、投入を消費。実産出を返す。
        /// </summary>
        public static float Produce(Recipe recipe, CommodityStock stock, float targetOutput)
        {
            if (recipe == null || stock == null) return 0f;
            float maxNet = MaxOutput(recipe, stock);
            float net = Mathf.Min(Mathf.Max(0f, targetOutput), maxNet);
            if (net <= 0f) return 0f;

            float yield = Mathf.Clamp01(recipe.yield);
            float gross = yield > 0f ? net / yield : net; // 良品 net を作るのに要る粗産出
            for (int i = 0; i < recipe.inputs.Count; i++)
            {
                var inp = recipe.inputs[i];
                if (inp.quantity <= 0f) continue;
                stock.Add(inp.commodityId, -gross * inp.quantity);
            }
            stock.Add(recipe.outputId, net);
            return net;
        }

        /// <summary>
        /// ボトルネックの投入品目id（投入不足で目標未達か＝<paramref name="constrained"/>）。投入なしは -1。
        /// </summary>
        public static int Bottleneck(Recipe recipe, CommodityStock stock, float targetOutput, out bool constrained)
        {
            constrained = false;
            if (recipe == null || stock == null || recipe.inputs.Count == 0) return -1;
            float yield = Mathf.Clamp01(recipe.yield);
            int binding = -1;
            float minGross = float.MaxValue;
            for (int i = 0; i < recipe.inputs.Count; i++)
            {
                var inp = recipe.inputs[i];
                if (inp.quantity <= 0f) continue;
                float canMake = stock.Get(inp.commodityId) / inp.quantity;
                if (canMake < minGross) { minGross = canMake; binding = inp.commodityId; }
            }
            float maxNet = minGross == float.MaxValue ? float.MaxValue : minGross * yield;
            constrained = maxNet < Mathf.Max(0f, targetOutput);
            return binding;
        }
    }
}
