using UnityEngine;

namespace Ginei
{
    /// <summary>賄賂の費用/効果パラメータ。</summary>
    public readonly struct BribeParams
    {
        public readonly float baseCost;       // 賄賂の基礎額
        public readonly float costPerTier;    // 上官の階級（tier）あたりの上乗せ（上位ほど高い）
        public readonly float favorPerCost;   // 金額あたりの親愛上昇（0..1の強度）
        public readonly float maxFavorGain;   // 1回の親愛上昇の上限
        public readonly float exposureChance; // 露見する確率（0..1・露見で逆効果）

        public BribeParams(float baseCost, float costPerTier, float favorPerCost, float maxFavorGain, float exposureChance)
        {
            this.baseCost = baseCost;
            this.costPerTier = costPerTier;
            this.favorPerCost = favorPerCost;
            this.maxFavorGain = maxFavorGain;
            this.exposureChance = exposureChance;
        }

        /// <summary>既定：基礎2000＋階級×500／親愛は金額×0.00005（上限0.15）／露見20%。</summary>
        public static BribeParams Default => new BribeParams(2000f, 500f, 0.00005f, 0.15f, 0.2f);
    }

    /// <summary>
    /// 上官への賄賂の純ロジック（決定論）。私財から支払う費用・露見判定（roll注入）・成功時の親愛上昇・
    /// 露見時の遺恨上昇を算出する唯一の窓口。財布（<see cref="Person.wealth"/>）や関係グラフ（<see cref="PersonRelationGraph"/>）の
    /// 書き換えは呼び出し側（<see cref="ProtagonistCareerDirector"/>）が行い、本ルールは金額と量だけを担う。test-first。
    /// </summary>
    public static class BriberyRules
    {
        /// <summary>賄賂の費用＝基礎＋上官の階級×係数（非負整数）。上位の上官ほど高くつく。</summary>
        public static int Cost(int superiorTier, BribeParams p)
            => Mathf.Max(0, Mathf.RoundToInt(p.baseCost + p.costPerTier * Mathf.Max(0, superiorTier)));

        /// <summary>私財で賄賂を賄えるか。</summary>
        public static bool CanAfford(float cash, int superiorTier, BribeParams p)
            => cash >= Cost(superiorTier, p);

        /// <summary>露見したか（roll&lt;露見確率）。露見すると逆効果（遺恨）。</summary>
        public static bool IsExposed(float roll, BribeParams p)
            => roll < Mathf.Clamp01(p.exposureChance);

        /// <summary>成功時の親愛上昇＝金額×係数（上限 maxFavorGain・0..1）。</summary>
        public static float FavorGain(int amount, BribeParams p)
            => Mathf.Clamp(Mathf.Max(0, amount) * Mathf.Max(0f, p.favorPerCost), 0f, Mathf.Max(0f, p.maxFavorGain));

        /// <summary>露見時の遺恨上昇（成功時の親愛上昇と同量を悪感情へ）。</summary>
        public static float ResentmentGain(int amount, BribeParams p) => FavorGain(amount, p);

        /// <summary>関係強度を加減して 0..1 にクランプ。</summary>
        public static float NewStrength(float current, float delta)
            => Mathf.Clamp01(current + delta);
    }
}
