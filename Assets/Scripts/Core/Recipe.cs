using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>レシピの1投入（BOM-2・#2098）。品目id×産出1あたりの投入量。</summary>
    public struct RecipeInput
    {
        public int commodityId;
        public float quantity; // 産出1あたりの投入量

        public RecipeInput(int commodityId, float quantity)
        {
            this.commodityId = commodityId;
            this.quantity = quantity;
        }
    }

    /// <summary>
    /// レシピ（部品表＝BOM・BOM-2・#2098・純データ）。1つの産出品目を作るための投入リスト＋歩留まり。
    /// 例＝建材←木材×2／衣類←布×2／食品←穀物×1。レオンチェフ型（固定係数）。test-first。
    /// </summary>
    public class Recipe
    {
        public int outputId;                        // 産出品目
        public List<RecipeInput> inputs = new List<RecipeInput>(); // 投入（空＝原材料の採取）
        public float yield = 1f;                    // 歩留まり（良品率）

        public Recipe() { }

        public Recipe(int outputId, float yield = 1f)
        {
            this.outputId = outputId;
            this.yield = yield;
        }

        /// <summary>投入を追加（流暢に組める）。</summary>
        public Recipe AddInput(int commodityId, float quantity)
        {
            inputs.Add(new RecipeInput(commodityId, Mathf.Max(0f, quantity)));
            return this;
        }
    }

    /// <summary>
    /// レシピ台帳（BOM-2・#2098・static・唯一の窓口）。レシピを登録し、産出品目から引く。
    /// Core 純ロジック・test-first。
    /// </summary>
    public static class RecipeBook
    {
        static readonly List<Recipe> recipes = new List<Recipe>();

        public static IReadOnlyList<Recipe> All => recipes;

        public static Recipe Register(Recipe r)
        {
            if (r != null) recipes.Add(r);
            return r;
        }

        /// <summary>指定品目を作るレシピ（最初の1件・無ければ null）。</summary>
        public static Recipe ForOutput(int commodityId)
        {
            for (int i = 0; i < recipes.Count; i++)
                if (recipes[i].outputId == commodityId) return recipes[i];
            return null;
        }

        public static void Clear() => recipes.Clear();
    }
}
