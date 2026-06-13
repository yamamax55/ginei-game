using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 小売り（小売業・#2017・<see cref="RetailRules"/>）を固定する：仕入と粗利(RTL-1)、在庫回転と欠品/廃棄(RTL-2)、
    /// バイイングパワー(RTL-3)、価格弾力性・特売(RTL-4)、店舗と商圏(RTL-5)。
    /// </summary>
    public class RetailTests
    {
        // ===== RTL-1 仕入と粗利 =====
        [Test]
        public void Markup_AndGrossProfit()
        {
            Assert.AreEqual(140f, RetailRules.RetailPrice(100f, 0.4f), 1e-3f);   // 仕入100に40%上乗せ
            Assert.AreEqual(40f, RetailRules.GrossMargin(140f, 100f), 1e-3f);
            Assert.AreEqual(0.2857f, RetailRules.GrossMarginRate(140f, 100f), 1e-3f);
            Assert.AreEqual(2000f, RetailRules.GrossProfit(50f, 140f, 100f), 1e-3f);
        }

        // ===== RTL-2 在庫回転と欠品/廃棄 =====
        [Test]
        public void Inventory_TurnoverStockoutWaste()
        {
            Assert.AreEqual(12f, RetailRules.InventoryTurnover(1200f, 100f), 1e-3f);
            // 在庫切れ：需要600・在庫500→500しか売れず100を取りこぼす
            Assert.AreEqual(500f, RetailRules.UnitsSold(600f, 500f), 1e-3f);
            Assert.AreEqual(4000f, RetailRules.StockoutLoss(600f, 500f, 40f), 1e-3f); // 機会損失
            Assert.AreEqual(500f, RetailRules.WasteLoss(50f, 100f, 0.1f), 1e-3f);     // 廃棄ロス
        }

        // ===== RTL-3 バイイングパワー =====
        [Test]
        public void BuyingPower_LargerBuysCheaper()
        {
            Assert.AreEqual(0f, RetailRules.BuyingDiscount(100f, 100f, 0.2f), 1e-4f);   // 基準量＝値引きなし
            Assert.AreEqual(0.1f, RetailRules.BuyingDiscount(200f, 100f, 0.2f), 1e-4f); // 2倍＝半分の値引き
            Assert.AreEqual(0.15f, RetailRules.BuyingDiscount(400f, 100f, 0.2f), 1e-4f);// 4倍＝より大きい値引き
            Assert.AreEqual(90f, RetailRules.EffectiveCostOfGoods(100f, 0.1f), 1e-3f);  // 定価100を1割引で仕入れ
        }

        // ===== RTL-4 価格弾力性・特売 =====
        [Test]
        public void PriceElasticity_DiscountRaisesDemand()
        {
            // 2割引（−0.2）×弾力性1.5＝需要+30%
            Assert.AreEqual(1300f, RetailRules.DemandAfterPriceChange(1000f, -0.2f, 1.5f), 1e-2f);
            // 1割値上げ→需要−15%
            Assert.AreEqual(850f, RetailRules.DemandAfterPriceChange(1000f, 0.1f, 1.5f), 1e-2f);
            // 極端な値上げでも需要は0でクランプ
            Assert.AreEqual(0f, RetailRules.DemandAfterPriceChange(1000f, 1.0f, 1.5f), 1e-3f);
        }

        // ===== RTL-5 店舗と商圏 =====
        [Test]
        public void Stores_SalesAndBreakEven()
        {
            // 商圏需要1000/店×5店×取込率0.3 = 1500
            Assert.AreEqual(1500f, RetailRules.StoreSales(1000f, 5, 0.3f), 1e-3f);
            Assert.AreEqual(50f, RetailRules.BreakEvenUnits(2000f, 40f), 1e-3f); // 固定費2000/粗利40
            // 1500販売×粗利40−固定費2000 = 58000
            Assert.AreEqual(58000f, RetailRules.StoreProfit(1500f, 40f, 2000f), 1e-1f);
            // 損益分岐未満なら赤字
            Assert.AreEqual(-400f, RetailRules.StoreProfit(40f, 40f, 2000f), 1e-1f);
        }
    }
}
