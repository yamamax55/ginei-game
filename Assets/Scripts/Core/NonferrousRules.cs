using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 非鉄金属メーカーのロジック（東証33業種「非鉄金属」・#2024・純ロジック・唯一の窓口）：製錬の投入産出＝鉱石→地金（NFE-1）／
    /// 製錬マージン＝地金価格−鉱石費−製錬料（NFE-2・TC/RC）／製錬利益（NFE-3）。価格は国際市況（LME）の price taker。鉱石は鉱山
    /// （#2018）、地金は下流製造（#2016/#2024）へ接続。マクロ近似。test-first。
    /// </summary>
    public static class NonferrousRules
    {
        /// <summary>製錬産出＝鉱石精鉱×回収率（鉱石を溶かして地金に＝品位ぶんが取れる）。</summary>
        public static float SmeltedMetal(float oreConcentrate, float recoveryRate)
            => Mathf.Max(0f, oreConcentrate) * Mathf.Clamp01(recoveryRate);

        /// <summary>製錬マージン（地金あたり）＝地金価格−鉱石費−製錬料（TC/RC）。LME市況で地金価格が動く。</summary>
        public static float SmeltingMargin(float metalPrice, float oreCost, float treatmentCharge)
            => metalPrice - oreCost - treatmentCharge;

        /// <summary>製錬利益＝地金産出×製錬マージン−固定費（市況連動で利益が振れる）。</summary>
        public static float NonferrousProfit(float metalOutput, float smeltingMargin, float fixedCost)
            => Mathf.Max(0f, metalOutput) * smeltingMargin - Mathf.Max(0f, fixedCost);
    }
}
