using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 商社＝総合商社（FRM-5 #1027 / FRM-7 #1029・<see cref="TradingHouseRules"/>）を固定する：貿易仲介(TRAD-1)、資源権益(TRAD-2)、
    /// 事業投資(TRAD-3)、トレードファイナンス(TRAD-4)、ポートフォリオとリスク(TRAD-5)。
    /// </summary>
    public class TradingHouseTests
    {
        // ===== TRAD-1 貿易仲介 =====
        [Test]
        public void TradeIntermediation_ArbitrageAndCommission()
        {
            Assert.AreEqual(1000f, TradingHouseRules.ArbitrageMargin(100f, 120f, 50f), 1e-3f); // 価格差20×50
            Assert.AreEqual(180f, TradingHouseRules.Commission(6000f, 0.03f), 1e-3f);
            // 総利益＝裁定1000＋口銭(売値120×50=6000 ×3%)=180
            Assert.AreEqual(1180f, TradingHouseRules.TradeProfit(100f, 120f, 50f, 0.03f), 1e-3f);
            // 逆ザヤ（高く買い安く売る）はマイナス
            Assert.AreEqual(-500f, TradingHouseRules.ArbitrageMargin(120f, 100f, 25f), 1e-3f);
        }

        // ===== TRAD-2 資源権益 =====
        [Test]
        public void ResourceStakes_ReturnAndSecuredSupply()
        {
            Assert.AreEqual(100f, TradingHouseRules.ResourceStakeReturn(1000f, 0.1f), 1e-3f);
            Assert.AreEqual(500f, TradingHouseRules.SecuredSupply(1000f, 0.5f), 1e-3f);
            // 確保した供給を勢力の備蓄へ納入
            var stock = new ResourceStockpile();
            TradingHouseRules.DeliverSecuredSupply(stock, ResourceType.燃料, 1000f, 0.5f, 1f);
            Assert.AreEqual(500f, stock.Get(ResourceType.燃料), 1e-3f);
        }

        // ===== TRAD-3 事業投資・オーガナイザー =====
        [Test]
        public void BusinessInvestment_AndOrganizer()
        {
            Assert.AreEqual(300f, TradingHouseRules.BusinessStakeReturn(2000f, 0.15f), 1e-3f); // 出資×資本利潤率
            Assert.AreEqual(200f, TradingHouseRules.SupplyChainSynergy(4, 50f), 1e-3f);        // 結節4×50
        }

        // ===== TRAD-4 トレードファイナンス =====
        [Test]
        public void TradeFinance_IncomeAndCounterpartyRisk()
        {
            Assert.AreEqual(100f, TradingHouseRules.TradeFinanceIncome(5000f, 0.02f), 1e-3f);
            Assert.AreEqual(50f, TradingHouseRules.CounterpartyLoss(5000f, 0.01f), 1e-3f);
        }

        // ===== TRAD-5 ポートフォリオとリスク =====
        [Test]
        public void Portfolio_InventoryRiskAndDiversification()
        {
            Assert.AreEqual(-100f, TradingHouseRules.InventoryPnL(1000f, -0.1f), 1e-3f);
            // 在庫1000の商社が相場1割下落で評価損100＝自己資本が削られる
            var h = new TradingHouse("商社", 500f, 1000f);
            float pnl = TradingHouseRules.ApplyPriceShock(h, -0.1f);
            Assert.AreEqual(-100f, pnl, 1e-3f);
            Assert.AreEqual(400f, h.capital, 1e-3f);
            // 多角化指数＝1−HHI。多分野に分散するほど高い（ラーメンからミサイルまで）
            Assert.AreEqual(1000f, TradingHouseRules.TotalRevenue(new List<float> { 300f, 300f, 300f, 100f }), 1e-3f);
            Assert.AreEqual(0.72f, TradingHouseRules.DiversificationIndex(new List<float> { 300f, 300f, 300f, 100f }), 1e-3f);
            Assert.AreEqual(0f, TradingHouseRules.DiversificationIndex(new List<float> { 1000f }), 1e-3f); // 一本足＝分散ゼロ
            Assert.AreEqual(0f, TradingHouseRules.DiversificationIndex(null), 1e-4f);
        }
    }
}
