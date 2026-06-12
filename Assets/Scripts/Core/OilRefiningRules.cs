using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 石油精製のロジック（東証33業種「石油・石炭製品」・#2024・純ロジック・唯一の窓口）：精製マージン＝クラックスプレッド（製品
    /// −原油・OIL-1）／精製の投入産出（OIL-2）／製油所利益＝高固定費（OIL-3）／在庫評価損益＝原油価格変動のタイムラグで益/損
    /// （OIL-4）。原油は資源（#92）、製品は市場（#179）・化学(#2024)へ接続（read-only/接続のみ）。マクロ近似。test-first。
    /// </summary>
    public static class OilRefiningRules
    {
        /// <summary>クラックスプレッド＝製品価格−原油費（精製マージン。市況で乱高下）。</summary>
        public static float CrackSpread(float productPrice, float crudeCost)
            => productPrice - crudeCost;

        /// <summary>精製産出＝原油投入×歩留まり（原油を石油製品に変換）。</summary>
        public static float RefinedOutput(float crudeInput, float yieldRate)
            => Mathf.Max(0f, crudeInput) * Mathf.Clamp01(yieldRate);

        /// <summary>製油所利益＝処理量×クラックスプレッド−固定費（高固定費＝稼働率が利益を決める）。</summary>
        public static float RefineryProfit(float throughput, float crackSpread, float fixedCost)
            => Mathf.Max(0f, throughput) * crackSpread - Mathf.Max(0f, fixedCost);

        /// <summary>在庫評価損益＝原油在庫×価格変化率（原油高で評価益・原油安で評価損＝精製業の在庫タイムラグ）。</summary>
        public static float InventoryGainLoss(float crudeInventory, float priceChangeRatio)
            => Mathf.Max(0f, crudeInventory) * priceChangeRatio;
    }
}
