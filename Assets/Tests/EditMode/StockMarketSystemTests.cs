using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 株式市場システム基盤（<see cref="StockMarketSystemRules"/>）を固定する：実体↔株価の連結（利潤→EPS/配当）、
    /// 時価総額/市場指数/市場心理の集約、増資で資本調達→企業の生産基盤へ投下、市場Tickで株価が適正へ。
    /// </summary>
    public class StockMarketSystemTests
    {
        private static Listing MakeListing(float employees, float sharePrice, float shares = 100f)
        {
            var e = new Enterprise(Faction.帝国, SystemType.工業, employees, capital: 1000f, productivity: 1f, wageRate: 1f);
            var c = new Company(earnings: 0f, sharePrice: sharePrice, dividend: 0f, sentiment: 0.5f);
            return new Listing(e, c, shares);
        }

        [Test]
        public void SyncEarnings_FromProfit_PerShare()
        {
            var l = MakeListing(100f, sharePrice: 10f, shares: 100f);
            // 利潤=50（売上150−賃金100・資本係数1.5）。EPS=50/100=0.5、配当=0.5*0.4=0.2
            StockMarketSystemRules.SyncEarnings(l, price: 1f, payoutRatio: 0.4f);
            Assert.AreEqual(0.5f, l.stock.earnings, 1e-3f);
            Assert.AreEqual(0.2f, l.stock.dividend, 1e-3f);
        }

        [Test]
        public void MarketCap_Index_Sentiment()
        {
            var a = MakeListing(100f, sharePrice: 10f, shares: 100f); // 時価1000
            var b = MakeListing(200f, sharePrice: 20f, shares: 50f);  // 時価1000
            a.stock.sentiment = 0.4f; b.stock.sentiment = 0.6f;
            var market = new List<Listing> { a, b };
            Assert.AreEqual(1000f, StockMarketSystemRules.MarketCap(a), 1e-3f);
            Assert.AreEqual(2000f, StockMarketSystemRules.MarketIndex(market), 1e-3f); // 1000+1000
            Assert.AreEqual(0.5f, StockMarketSystemRules.MarketSentiment(market), 1e-4f); // (0.4+0.6)/2
        }

        [Test]
        public void IssueShares_RaisesCapital_IntoEnterprise()
        {
            var l = MakeListing(100f, sharePrice: 10f, shares: 100f);
            float capBefore = l.enterprise.capital; // 1000
            float raised = StockMarketSystemRules.IssueShares(l, 50f); // 50株×10
            Assert.AreEqual(500f, raised, 1e-3f);
            Assert.AreEqual(150f, l.shares, 1e-3f);                    // 希薄化（株式数増）
            Assert.AreEqual(capBefore + 500f, l.enterprise.capital, 1e-3f); // 調達資本が生産基盤へ投下
        }

        [Test]
        public void TickMarket_ConvergesPriceTowardFair()
        {
            var l = MakeListing(100f, sharePrice: 1f, shares: 100f); // 安すぎる株価
            var market = new List<Listing> { l };
            float before = l.stock.sharePrice;
            StockMarketSystemRules.TickMarket(market, price: 1f, payoutRatio: 0.4f, StockMarketRules.StockParams.Default, dt: 0.1f);
            // 収益が付き（EPS0.5）適正株価＞1へ収束し始める＝株価が上がる
            Assert.Greater(l.stock.earnings, 0f);
            Assert.Greater(l.stock.sharePrice, before);
        }

        [Test]
        public void NullSafe()
        {
            Assert.AreEqual(0f, StockMarketSystemRules.MarketCap(null), 1e-4f);
            Assert.AreEqual(0f, StockMarketSystemRules.MarketIndex(null), 1e-4f);
            Assert.AreEqual(0.5f, StockMarketSystemRules.MarketSentiment(null), 1e-4f);
            Assert.AreEqual(0f, StockMarketSystemRules.IssueShares(null, 10f), 1e-4f);
        }
    }
}
