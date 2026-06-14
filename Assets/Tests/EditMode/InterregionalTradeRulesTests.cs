using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 勢力間／星系間 交易オーケストレータ（<see cref="InterregionalTradeRules"/>）を固定する：
    /// 価格差があると安い側→高い側へ財が流れ価格差が縮む（一物一価へ）／輸送コストが利鞘を超えると交易ゼロ／
    /// 供給・需要が無ければ流れない／総供給は保存／null 安全。数式は既存 Core へ委譲（二重実装しない）。
    /// </summary>
    public class InterregionalTradeRulesTests
    {
        private static readonly MarketRules.MarketParams P = MarketRules.MarketParams.Default;

        private static Market Mk(float supply, float demand, float price)
            => new Market(GoodType.物資, supply, demand, price);

        [Test]
        public void TradeFlow_FlowsFromCheapToExpensive_WhenGapExceedsTransport()
        {
            // 安い側（供給あり）→高い側（需要あり）。価格差40・輸送費10＝利鞘あり＝流れる。
            var cheap = Mk(supply: 100f, demand: 0f, price: 60f);
            var dear = Mk(supply: 0f, demand: 100f, price: 100f);
            float flow = InterregionalTradeRules.TradeFlow(cheap, dear, 10f);
            Assert.Greater(flow, 0f);
        }

        [Test]
        public void TradeFlow_Zero_WhenTransportExceedsGap()
        {
            // 価格差40だが輸送費50＝利鞘なし＝交易しない（割に合わない）。
            var cheap = Mk(100f, 0f, 60f);
            var dear = Mk(0f, 100f, 100f);
            Assert.AreEqual(0f, InterregionalTradeRules.TradeFlow(cheap, dear, 50f), 1e-5f);
            // 等しい（差=コスト）も利幅ゼロ＝不成立。
            Assert.AreEqual(0f, InterregionalTradeRules.TradeFlow(cheap, dear, 40f), 1e-5f);
        }

        [Test]
        public void TradeFlow_Zero_WhenNoSupplyOrNoDemand()
        {
            // 輸出元に供給が無い＝運ぶ物がない。
            Assert.AreEqual(0f, InterregionalTradeRules.TradeFlow(Mk(0f, 0f, 60f), Mk(0f, 100f, 100f), 10f), 1e-5f);
            // 輸入先に需要が無い＝買い手がいない。
            Assert.AreEqual(0f, InterregionalTradeRules.TradeFlow(Mk(100f, 0f, 60f), Mk(0f, 0f, 100f), 10f), 1e-5f);
        }

        [Test]
        public void TickPair_NarrowsPriceGap_AndConservesTotalSupply()
        {
            var a = Mk(supply: 100f, demand: 50f, price: 60f);  // 安い側
            var b = Mk(supply: 20f, demand: 100f, price: 100f); // 高い側
            float gapBefore = b.price - a.price;
            float totalBefore = a.supply + b.supply;

            InterregionalTradeRules.TickPair(a, b, 10f, 1f, P);

            float gapAfter = b.price - a.price;
            // 安い側に供給が流れて上がり高い側は供給が増えて下がる＝価格差が縮む。
            Assert.Less(gapAfter, gapBefore);
            // 総供給は保存（財が移動するだけ）。
            Assert.AreEqual(totalBefore, a.supply + b.supply, 1e-3f);
            // 安い側の供給が減り高い側の供給が増えた（高い側へ流れた）。
            Assert.Less(a.supply, 100f);
            Assert.Greater(b.supply, 20f);
        }

        [Test]
        public void TickPair_NoChange_WhenTransportTooHigh()
        {
            var a = Mk(100f, 50f, 60f);
            var b = Mk(20f, 100f, 100f);
            float aSupply = a.supply, bSupply = b.supply, aPrice = a.price, bPrice = b.price;

            InterregionalTradeRules.TickPair(a, b, 100f, 1f, P); // 輸送費が価格差を超える＝交易ゼロ

            Assert.AreEqual(aSupply, a.supply, 1e-5f);
            Assert.AreEqual(bSupply, b.supply, 1e-5f);
            Assert.AreEqual(aPrice, a.price, 1e-5f);
            Assert.AreEqual(bPrice, b.price, 1e-5f);
        }

        [Test]
        public void NullSafe()
        {
            Assert.AreEqual(0f, InterregionalTradeRules.TradeFlow(null, Mk(0f, 100f, 100f), 10f), 1e-5f);
            Assert.AreEqual(0f, InterregionalTradeRules.TradeFlow(Mk(100f, 0f, 60f), null, 10f), 1e-5f);
            Assert.DoesNotThrow(() => InterregionalTradeRules.TickPair(null, null, 10f, 1f));
            Assert.DoesNotThrow(() => InterregionalTradeRules.TickPair(Mk(100f, 0f, 60f), null, 10f, 1f));
        }
    }
}
