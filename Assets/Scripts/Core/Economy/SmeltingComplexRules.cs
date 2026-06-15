using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 鉱業精錬コンビナート（垂直統合）のロジック（業種細分化・鉱山 #2018 ×非鉄金属 #2024 の統合サブ業種・#2025・純ロジック・唯一の窓口）：
    /// 鉱石→金属の一貫産出（SMLT-1）／自山鉱の調達コスト優位（SMLT-2＝市場で買わず自前で掘る）／採掘＋製錬の一貫マージン（SMLT-3）／利益（SMLT-4）。
    /// 鉱山（#2018）と製錬所（非鉄#2024）を一社に束ねる垂直統合＝自前の鉱石（captive ore）で市況に左右されにくい安定マージンを得る。マクロ近似。test-first。
    /// </summary>
    public static class SmeltingComplexRules
    {
        /// <summary>一貫産出＝鉱石投入×回収率（採掘から製錬まで一社で通すぶんロスが読める）。</summary>
        public static float IntegratedOutput(float oreInput, float recoveryRate)
            => Mathf.Max(0f, oreInput) * Mathf.Clamp01(recoveryRate);

        /// <summary>自山鉱の調達優位＝(市場鉱石価格−自前鉱石原価)×鉱石量（市場で買わず自前で掘るぶんのコスト優位）。非負。</summary>
        public static float CaptiveOreSavings(float marketOreCost, float internalOreCost, float oreVolume)
            => Mathf.Max(0f, marketOreCost - internalOreCost) * Mathf.Max(0f, oreVolume);

        /// <summary>一貫マージン＝金属価格−採掘コスト−製錬コスト（川上から川下まで取り込む統合マージン）。</summary>
        public static float IntegratedMargin(float metalPrice, float miningCost, float smeltingCost)
            => metalPrice - miningCost - smeltingCost;

        /// <summary>コンビナート利益＝金属売上−採掘コスト−製錬コスト−固定費。</summary>
        public static float ComplexProfit(float metalRevenue, float miningCost, float smeltingCost, float fixedCost)
            => metalRevenue - Mathf.Max(0f, miningCost) - Mathf.Max(0f, smeltingCost) - Mathf.Max(0f, fixedCost);
    }
}
