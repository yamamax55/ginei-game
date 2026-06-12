using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 先物市場（#1933 FUTR・<see cref="FuturesMarketRules"/>）を固定する：先物価格とベーシス/コンタンゴ(FUTR-1)、ヘッジの正味
    /// エクスポージャ(FUTR-2)、評価損益とレバレッジ(FUTR-3)、証拠金と強制清算(FUTR-4)、建玉と平均ベーシス(FUTR-5)。
    /// </summary>
    public class FuturesMarketTests
    {
        [Test]
        public void FairPrice_Basis_Contango()
        {
            // 現物100・保有コスト5%・1年＝先物105（コンタンゴ）
            Assert.AreEqual(105f, FuturesMarketRules.FairPrice(100f, 0.05f, 1f), 1e-3f);
            Assert.AreEqual(5f, FuturesMarketRules.Basis(105f, 100f), 1e-3f);
            Assert.IsTrue(FuturesMarketRules.IsContango(105f, 100f));
            Assert.IsTrue(FuturesMarketRules.IsBackwardation(95f, 100f)); // 逆ザヤ＝品薄
        }

        [Test]
        public void ProfitLoss_ByDirection()
        {
            var lng = new FuturesContract("物資", contractPrice: 10f, quantity: 100f, isLong: true);
            var sht = new FuturesContract("物資", contractPrice: 10f, quantity: 100f, isLong: false);
            // 価格12へ：ロングは+200、ショートは-200
            Assert.AreEqual(200f, FuturesMarketRules.ProfitLoss(lng, 12f), 1e-3f);
            Assert.AreEqual(-200f, FuturesMarketRules.ProfitLoss(sht, 12f), 1e-3f);
        }

        [Test]
        public void Hedge_NetExposure_OffsetsSpot()
        {
            // 現物ロング100をショート先物100で完全ヘッジ＝正味0
            var shortFut = new FuturesContract("物資", 10f, 100f, isLong: false);
            Assert.AreEqual(0f, FuturesMarketRules.NetExposure(100f, shortFut), 1e-3f);
            Assert.AreEqual(1f, FuturesMarketRules.HedgeRatio(100f, shortFut), 1e-3f);
            // 半分だけヘッジ＝正味50・ヘッジ比率0.5
            var halfHedge = new FuturesContract("物資", 10f, 50f, isLong: false);
            Assert.AreEqual(50f, FuturesMarketRules.NetExposure(100f, halfHedge), 1e-3f);
            Assert.AreEqual(0.5f, FuturesMarketRules.HedgeRatio(100f, halfHedge), 1e-3f);
        }

        [Test]
        public void Leverage_SmallMargin_BigPosition()
        {
            var c = new FuturesContract("物資", 10f, 100f, isLong: true, margin: 100f);
            float notional = FuturesMarketRules.Notional(c, 10f); // 1000
            Assert.AreEqual(1000f, notional, 1e-3f);
            Assert.AreEqual(10f, FuturesMarketRules.Leverage(notional, c.margin), 1e-3f); // 10倍
            Assert.AreEqual(50f, FuturesMarketRules.RequiredMargin(1000f, 0.05f), 1e-3f);
        }

        [Test]
        public void MarginCall_OnLossBelowMaintenance()
        {
            // ロング100・約定10・証拠金100。価格9へ＝評価損-100＝有効証拠金0＜維持(900*0.05=45)＝強制清算
            var c = new FuturesContract("物資", 10f, 100f, isLong: true, margin: 100f);
            Assert.IsTrue(FuturesMarketRules.IsMarginCall(c, 9f, 0.05f));
            Assert.AreEqual(0f, FuturesMarketRules.Equity(c, 9f), 1e-3f);
            // 価格9.8なら有効証拠金80＞維持(980*0.05=49)＝清算されない
            Assert.IsFalse(FuturesMarketRules.IsMarginCall(c, 9.8f, 0.05f));
        }

        [Test]
        public void Market_OpenInterest_AverageBasis()
        {
            var a = new FuturesContract("物資", contractPrice: 105f, quantity: 100f, isLong: true);
            var b = new FuturesContract("物資", contractPrice: 95f, quantity: 50f, isLong: false);
            var market = new List<FuturesContract> { a, b };
            Assert.AreEqual(150f, FuturesMarketRules.OpenInterest(market), 1e-3f);       // 建玉合計
            // 平均ベーシス＝((105-100)+(95-100))/2 = 0
            Assert.AreEqual(0f, FuturesMarketRules.AverageBasis(market, 100f), 1e-3f);
            Assert.AreEqual(0f, FuturesMarketRules.OpenInterest(null), 1e-3f);           // null安全
        }
    }
}
