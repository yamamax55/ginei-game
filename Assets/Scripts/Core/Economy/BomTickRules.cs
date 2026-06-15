using System;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// BOM生産の暦境界オーケストレータ（BOM-6・#2098 配線・純ロジック）。
    /// レシピを上流→下流に1パスで流す薄い窓口（`BomProductionRules` へ委譲）。
    /// 呼び側がレシピの順序（原材料→中間財→消費財）と目標を渡す。test-first。
    /// </summary>
    public static class BomTickRules
    {
        /// <summary>1レシピを目標まで生産（在庫を破壊的に更新）。実産出を返す。</summary>
        public static float Produce(CommodityStock stock, Recipe recipe, float targetOutput)
            => BomProductionRules.Produce(recipe, stock, targetOutput);

        /// <summary>
        /// レシピ列を順に生産（上流→下流の順で渡すこと）。各レシピの目標は <paramref name="targetFor"/> で決める。
        /// </summary>
        public static void RunChain(CommodityStock stock, IReadOnlyList<Recipe> orderedRecipes, Func<Recipe, float> targetFor)
        {
            if (stock == null || orderedRecipes == null || targetFor == null) return;
            for (int i = 0; i < orderedRecipes.Count; i++)
            {
                Recipe r = orderedRecipes[i];
                if (r == null) continue;
                BomProductionRules.Produce(r, stock, targetFor(r));
            }
        }
    }
}
