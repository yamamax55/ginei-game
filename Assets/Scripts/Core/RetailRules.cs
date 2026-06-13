using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 小売り（小売業）のロジック（#2017・純ロジック・唯一の窓口）。完成品を仕入れて消費者に売る B2C＝経済の出口：仕入と粗利
    /// （RTL-1）／在庫回転と欠品・廃棄（RTL-2）／バイイングパワー＝規模で安く仕入れる（RTL-3）／価格弾力性・特売（RTL-4）／
    /// 店舗と商圏（RTL-5）。商品はメーカー（#2016）・商社（#1027）から仕入れ、売上は消費 C（#1951）・消費者の購買力（#1969/#1996）
    /// に支えられる。市場（#179）・商圏（#109）へ接続（read-only/接続のみ）。マクロ近似（SKU micro は持たない）。test-first。
    /// </summary>
    public static class RetailRules
    {
        /// <summary>既定の値入率（マークアップ＝仕入原価への上乗せ）。</summary>
        public const float DefaultMarkupRate = 0.4f;

        /// <summary>バイイングパワーの最大仕入値引き率（規模が十分大きいときの上限）。</summary>
        public const float DefaultBuyingPowerMax = 0.2f;

        /// <summary>既定の価格弾力性（値下げ率に対する数量の伸び）。</summary>
        public const float DefaultPriceElasticity = 1.5f;

        // ===== RTL-1 仕入と粗利 =====

        /// <summary>販売価格＝仕入原価×(1＋値入率)（マークアップ）。</summary>
        public static float RetailPrice(float costOfGoods, float markupRate)
            => Mathf.Max(0f, costOfGoods) * (1f + Mathf.Max(0f, markupRate));

        /// <summary>1単位の粗利＝販売価格−仕入原価。</summary>
        public static float GrossMargin(float price, float cost) => price - cost;

        /// <summary>粗利率＝粗利/販売価格（薄利多売ほど低い）。価格0以下は0。</summary>
        public static float GrossMarginRate(float price, float cost)
            => price <= 0f ? 0f : (price - cost) / price;

        /// <summary>粗利益＝販売数×1単位粗利。</summary>
        public static float GrossProfit(float unitsSold, float price, float cost)
            => Mathf.Max(0f, unitsSold) * GrossMargin(price, cost);

        // ===== RTL-2 在庫回転と欠品/廃棄 =====

        /// <summary>在庫回転率＝売上数量/平均在庫（高いほど効率的に在庫を捌く）。在庫0以下は0。</summary>
        public static float InventoryTurnover(float sales, float avgInventory)
            => avgInventory <= 0f ? 0f : Mathf.Max(0f, sales) / avgInventory;

        /// <summary>販売数＝需要と在庫の小さい方（在庫切れなら需要を取りこぼす）。</summary>
        public static float UnitsSold(float demand, float inventory)
            => Mathf.Min(Mathf.Max(0f, demand), Mathf.Max(0f, inventory));

        /// <summary>欠品の機会損失＝(需要−在庫)×1単位粗利（売れたはずの粗利を逃す）。非負。</summary>
        public static float StockoutLoss(float demand, float available, float unitMargin)
            => Mathf.Max(0f, Mathf.Max(0f, demand) - Mathf.Max(0f, available)) * Mathf.Max(0f, unitMargin);

        /// <summary>廃棄ロス＝売れ残り×廃棄率×1単位原価（生鮮など売れ残りを捨てる損）。</summary>
        public static float WasteLoss(float unsold, float unitCost, float spoilRate)
            => Mathf.Max(0f, unsold) * Mathf.Clamp01(spoilRate) * Mathf.Max(0f, unitCost);

        // ===== RTL-3 バイイングパワー =====

        /// <summary>
        /// 仕入値引き率＝最大値引き×(1−基準量/max(基準量, 仕入量))。仕入量が基準で0、大きいほど上限へ近づく
        /// （大手ほど安く仕入れる＝規模の経済）。基準量0以下は0。
        /// </summary>
        public static float BuyingDiscount(float purchaseVolume, float referenceVolume, float maxDiscount)
        {
            if (referenceVolume <= 0f) return 0f;
            float v = Mathf.Max(0f, purchaseVolume);
            float factor = 1f - referenceVolume / Mathf.Max(referenceVolume, v);
            return Mathf.Clamp01(maxDiscount) * Mathf.Clamp01(factor);
        }

        /// <summary>実効仕入原価＝定価原価×(1−値引き率)（バイイングパワーで安く仕入れる）。</summary>
        public static float EffectiveCostOfGoods(float listCost, float discount)
            => Mathf.Max(0f, listCost) * (1f - Mathf.Clamp01(discount));

        // ===== RTL-4 価格弾力性・特売 =====

        /// <summary>
        /// 価格変動後の需要＝基準需要×(1−弾力性×価格変化率)。価格変化率が負（値下げ）なら需要増、正（値上げ）なら需要減。非負。
        /// </summary>
        public static float DemandAfterPriceChange(float baseDemand, float priceChangeRatio, float elasticity)
            => Mathf.Max(0f, Mathf.Max(0f, baseDemand) * (1f - elasticity * priceChangeRatio));

        // ===== RTL-5 店舗と商圏 =====

        /// <summary>店舗売上（数量）＝1店舗あたり商圏需要×店舗数×取り込み率（出店で商圏需要を取り込む）。</summary>
        public static float StoreSales(float catchmentDemandPerStore, int storeCount, float captureRate)
            => Mathf.Max(0f, catchmentDemandPerStore) * Mathf.Max(0, storeCount) * Mathf.Clamp01(captureRate);

        /// <summary>損益分岐点（数量）＝固定費/1単位粗利（これだけ売れば固定費を回収）。粗利0以下は超大（届かない）。</summary>
        public static float BreakEvenUnits(float fixedCost, float unitMargin)
            => unitMargin <= 0f ? 999999f : Mathf.Max(0f, fixedCost) / unitMargin;

        /// <summary>店舗利益＝販売数×1単位粗利−固定費（家賃・人件費）。負＝赤字店舗。</summary>
        public static float StoreProfit(float salesUnits, float unitMargin, float fixedCost)
            => Mathf.Max(0f, salesUnits) * unitMargin - Mathf.Max(0f, fixedCost);
    }
}
