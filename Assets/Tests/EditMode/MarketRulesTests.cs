using NUnit.Framework;
using UnityEngine;
using Ginei;
using MParams = Ginei.MarketRules.MarketParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 市場経済（M-1 需給価格 #180・M-2 経済→支持 #181）を固定する：均衡価格（高騰/下落/均衡/クランプ/供給0）、
    /// 価格収束（Tick）、基準価格固定版 Tick、生活水準の充足率、生活水準→支持の係数（窮乏で負・豊かさで正）。
    /// </summary>
    public class MarketRulesTests
    {
        // 均衡価格：需要=供給で基準価格に戻る（中立）
        [Test]
        public void ClearingPrice_BalancedSupplyDemand_ReturnsBasePrice()
        {
            var p = MParams.Default;
            float price = MarketRules.ClearingPrice(100f, 100f, 10f, p);
            Assert.AreEqual(10f, price, 1e-4f);
        }

        // 高騰：需要>供給で基準価格より上、下落：供給>需要で基準価格より下
        [Test]
        public void ClearingPrice_DemandHigh_Rises_SupplyHigh_Falls()
        {
            var p = MParams.Default;
            float high = MarketRules.ClearingPrice(50f, 100f, 10f, p); // 需要過多
            float low = MarketRules.ClearingPrice(100f, 50f, 10f, p);  // 供給過多
            Assert.Greater(high, 10f);
            Assert.Less(low, 10f);
        }

        // クランプ：極端な需給でも basePrice×(min..max) に収まる（青天井/床抜け防止）
        [Test]
        public void ClearingPrice_ClampsToMinMaxRatio()
        {
            var p = MParams.Default; // min0.25 / max4
            float bp = 10f;
            float capped = MarketRules.ClearingPrice(1f, 100000f, bp, p);   // 需要爆発
            float floored = MarketRules.ClearingPrice(100000f, 1f, bp, p);  // 供給爆発
            Assert.AreEqual(bp * p.maxPriceRatio, capped, 1e-3f); // 天井=40
            Assert.AreEqual(bp * p.minPriceRatio, floored, 1e-3f); // 床=2.5
        }

        // 境界：供給0で需要ありは天井、需要も0は基準価格（取引なし＝中立）
        [Test]
        public void ClearingPrice_ZeroSupply_Boundary()
        {
            var p = MParams.Default;
            float scarce = MarketRules.ClearingPrice(0f, 50f, 10f, p);
            float none = MarketRules.ClearingPrice(0f, 0f, 10f, p);
            Assert.AreEqual(10f * p.maxPriceRatio, scarce, 1e-4f); // 希少＝天井
            Assert.AreEqual(10f, none, 1e-4f);                     // 取引なし＝基準価格
        }

        // Tick：価格が均衡へ寄る（基準価格固定版＝供給=需要で basePrice へ戻る）
        [Test]
        public void Tick_WithGood_ConvergesTowardBasePrice()
        {
            var p = MParams.Default;
            var good = new Good(GoodType.物資, 10f);
            var m = new Market(GoodType.物資, 100f, 100f, 40f); // 均衡なのに価格が高い→下がるはず
            float before = m.price;
            MarketRules.Tick(m, good, p, 0.5f);
            Assert.Less(m.price, before);    // basePrice(10) へ寄る
            Assert.GreaterOrEqual(m.price, 10f); // 行き過ぎない
            Assert.GreaterOrEqual(m.price, 0f);  // 負にならない
        }

        // Tick：dt<=0 や null は無変化・無例外（境界）
        [Test]
        public void Tick_NonPositiveDt_NoChange()
        {
            var p = MParams.Default;
            var m = new Market(GoodType.燃料, 50f, 100f, 10f);
            float before = m.price;
            MarketRules.Tick(m, p, 0f);
            Assert.AreEqual(before, m.price, 1e-4f);
            Assert.DoesNotThrow(() => MarketRules.Tick(null, p, 1f));
            Assert.DoesNotThrow(() => MarketRules.Tick(m, null, p, 1f)); // Good null 版
        }

        // 生活水準：充足率（need の半分消費で0.5・満たせば1・need0は1）＋0..1クランプ
        [Test]
        public void StandardOfLiving_FulfillmentRatio_Clamped()
        {
            var p = MParams.Default;
            Assert.AreEqual(1f, MarketRules.StandardOfLiving(100f, 100f, p), 1e-4f); // 充足
            Assert.AreEqual(0.5f, MarketRules.StandardOfLiving(50f, 100f, p), 1e-4f); // 半分
            Assert.AreEqual(1f, MarketRules.StandardOfLiving(200f, 100f, p), 1e-4f); // 過剰でも上限1
            Assert.AreEqual(1f, MarketRules.StandardOfLiving(0f, 0f, p), 1e-4f);     // need0＝充足
            Assert.AreEqual(0f, MarketRules.StandardOfLiving(0f, 100f, p), 1e-4f);   // 窮乏
        }

        // 生活水準→支持：0.5で中立0、豊かさで正、窮乏で負（M-2 経済が革命を生む）
        [Test]
        public void SoLToSupport_NeutralAtHalf_PositiveWhenRich_NegativeWhenPoor()
        {
            var p = MParams.Default; // supportSwing 1
            Assert.AreEqual(0f, MarketRules.SoLToSupport(0.5f, p), 1e-4f);  // 中立
            Assert.AreEqual(p.supportSwing, MarketRules.SoLToSupport(1f, p), 1e-4f);   // 豊か＝+swing
            Assert.AreEqual(-p.supportSwing, MarketRules.SoLToSupport(0f, p), 1e-4f);  // 窮乏＝−swing
            // クランプ：範囲外の生活水準は端で頭打ち
            Assert.AreEqual(p.supportSwing, MarketRules.SoLToSupport(5f, p), 1e-4f);
            Assert.AreEqual(-p.supportSwing, MarketRules.SoLToSupport(-5f, p), 1e-4f);
        }

        // Params：上限が下限以上に正規化される（min>max で渡しても破綻しない）
        [Test]
        public void MarketParams_NormalizesMaxAtLeastMin()
        {
            var p = new MParams(1f, 2f, 0.5f, 1f, 1f); // min2 > max0.5
            Assert.GreaterOrEqual(p.maxPriceRatio, p.minPriceRatio);
        }
    }
}
