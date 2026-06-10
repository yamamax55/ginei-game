using NUnit.Framework;
using UnityEngine;
using Ginei;
using SParams = Ginei.StockMarketRules.StockParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 株式市場（CAP-1 #185）を固定する：適正株価（心理中立/強気/弱気・クランプ・収益0）、配当利回り（株価0境界）、
    /// 暴落リスク（弱気×割高で高・割安/強気で低・クランプ）、株価収束（Tick・null/dt境界）、Params正規化。
    /// </summary>
    public class StockMarketRulesTests
    {
        // 適正株価：心理中立(0.5)で 収益×PER（倍率1.0）
        [Test]
        public void FairPrice_NeutralSentiment_EarningsTimesPer()
        {
            var p = SParams.Default; // PER15
            float price = StockMarketRules.FairPrice(10f, 0.5f, p);
            Assert.AreEqual(10f * p.basePer, price, 1e-4f); // 150
        }

        // 適正株価：強気で割高・弱気で割安（中立を挟んで上下）
        [Test]
        public void FairPrice_Bullish_Rises_Bearish_Falls()
        {
            var p = SParams.Default;
            float neutral = StockMarketRules.FairPrice(10f, 0.5f, p);
            float bull = StockMarketRules.FairPrice(10f, 1f, p);
            float bear = StockMarketRules.FairPrice(10f, 0f, p);
            Assert.Greater(bull, neutral);
            Assert.Less(bear, neutral);
        }

        // 境界：収益0は株価0（無価値）
        [Test]
        public void FairPrice_ZeroEarnings_ReturnsZero()
        {
            var p = SParams.Default;
            Assert.AreEqual(0f, StockMarketRules.FairPrice(0f, 1f, p), 1e-4f);
            Assert.AreEqual(0f, StockMarketRules.FairPrice(-5f, 0.5f, p), 1e-4f); // 負収益もクランプして0
        }

        // クランプ：心理倍率では届かない極端でも上下限に収まる＋心理が0..1にクランプ
        [Test]
        public void FairPrice_ClampsSentimentAndPrice()
        {
            var p = SParams.Default; // min0.25 / max4
            // 心理が範囲外でも端で頭打ち（強気5＝1扱い）
            float bullCapped = StockMarketRules.FairPrice(10f, 5f, p);
            float bullMax = StockMarketRules.FairPrice(10f, 1f, p);
            Assert.AreEqual(bullMax, bullCapped, 1e-4f);
            // 価格は basePrice×(min..max) の範囲内
            float baseValue = 10f * p.basePer;
            Assert.GreaterOrEqual(bullCapped, baseValue * p.minPriceRatio);
            Assert.LessOrEqual(bullCapped, baseValue * p.maxPriceRatio);
        }

        // 配当利回り：配当/株価。株価0は0（評価不能）
        [Test]
        public void DividendYield_RatioAndZeroPriceBoundary()
        {
            Assert.AreEqual(0.05f, StockMarketRules.DividendYield(5f, 100f), 1e-4f);
            Assert.AreEqual(0f, StockMarketRules.DividendYield(5f, 0f), 1e-4f);   // 株価なし
            Assert.AreEqual(0f, StockMarketRules.DividendYield(-5f, 100f), 1e-4f); // 負配当はクランプ
        }

        // 暴落リスク：弱気×割高で高、割安・強気で低、0..1クランプ
        [Test]
        public void CrashRisk_HighWhenBearishAndOvervalued()
        {
            var p = SParams.Default; // crashSensitivity 1
            float bearBubble = StockMarketRules.CrashRisk(0f, 0.5f, p);  // 弱気1×割高0.5×1
            float bullBubble = StockMarketRules.CrashRisk(1f, 0.5f, p);  // 強気＝弱気度0＝崩れない
            float undervalued = StockMarketRules.CrashRisk(0f, -0.5f, p); // 割安＝暴落要因なし
            Assert.AreEqual(0.5f, bearBubble, 1e-4f);
            Assert.AreEqual(0f, bullBubble, 1e-4f);
            Assert.AreEqual(0f, undervalued, 1e-4f);
        }

        // 暴落リスク：極端な割高でも1でクランプ
        [Test]
        public void CrashRisk_ClampsToOne()
        {
            var p = SParams.Default;
            float risk = StockMarketRules.CrashRisk(0f, 100f, p);
            Assert.AreEqual(1f, risk, 1e-4f);
        }

        // Tick：株価が適正株価へ寄る（割高なら下がる・行き過ぎない・負にならない）
        [Test]
        public void Tick_ConvergesTowardFairPrice()
        {
            var p = SParams.Default;
            float fair = StockMarketRules.FairPrice(10f, 0.5f, p); // 150
            var c = new Company(10f, 400f, 5f, 0.5f); // 割高（400>150）
            float before = c.sharePrice;
            StockMarketRules.Tick(c, p, 0.5f);
            Assert.Less(c.sharePrice, before);          // 適正へ下がる
            Assert.GreaterOrEqual(c.sharePrice, fair);  // 行き過ぎない
            Assert.GreaterOrEqual(c.sharePrice, 0f);    // 負にならない
        }

        // Tick：dt<=0 や null は無変化・無例外（境界）
        [Test]
        public void Tick_NonPositiveDtOrNull_NoChange()
        {
            var p = SParams.Default;
            var c = new Company(10f, 400f, 5f, 0.5f);
            float before = c.sharePrice;
            StockMarketRules.Tick(c, p, 0f);
            Assert.AreEqual(before, c.sharePrice, 1e-4f);
            Assert.DoesNotThrow(() => StockMarketRules.Tick(null, p, 1f));
        }

        // Params：上限が下限以上に正規化される（min>max で渡しても破綻しない）
        [Test]
        public void StockParams_NormalizesMaxAtLeastMin()
        {
            var p = new SParams(15f, 0.5f, 2f, 0.5f, 2f, 1f); // min2 > max0.5
            Assert.GreaterOrEqual(p.maxPriceRatio, p.minPriceRatio);
        }
    }
}
