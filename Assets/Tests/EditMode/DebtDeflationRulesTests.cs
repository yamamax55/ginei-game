using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>負債デフレーション（#1619）の純ロジック検証。実質債務膨張・投げ売り・スパイラル・債務減免を担保。</summary>
    public class DebtDeflationRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>物価1.0なら実質債務＝名目、物価が下がるほど実質負担が膨らむ。</summary>
        [Test]
        public void RealDebtBurden_物価下落で実質負担が膨らむ()
        {
            // 物価1.0＝名目どおり
            Assert.AreEqual(100f, DebtDeflationRules.RealDebtBurden(100f, 1.0f), Eps);
            // 物価0.5＝実質2倍
            Assert.AreEqual(200f, DebtDeflationRules.RealDebtBurden(100f, 0.5f), Eps);
            // 物価が下がるほど重い
            Assert.Greater(DebtDeflationRules.RealDebtBurden(100f, 0.5f),
                           DebtDeflationRules.RealDebtBurden(100f, 0.8f));
        }

        /// <summary>物価水準は MinPriceLevel でクランプし0割を防ぐ。</summary>
        [Test]
        public void RealDebtBurden_物価ゼロは下限クランプで0割回避()
        {
            float expected = 100f / DebtDeflationRules.MinPriceLevel;
            Assert.AreEqual(expected, DebtDeflationRules.RealDebtBurden(100f, 0f), Eps);
        }

        /// <summary>物価20%下落で実質債務は25%増（1/(1−0.2)−1）＝下落が努力を裏切る。</summary>
        [Test]
        public void RealDebtChange_物価下落率に対し実質債務が非線形に膨張()
        {
            Assert.AreEqual(0f, DebtDeflationRules.RealDebtChange(0f), Eps);
            Assert.AreEqual(0.25f, DebtDeflationRules.RealDebtChange(0.2f), Eps);
            // 下落が大きいほど膨張率も大きい（非線形）
            Assert.Greater(DebtDeflationRules.RealDebtChange(0.5f),
                           DebtDeflationRules.RealDebtChange(0.2f) * 2f);
        }

        /// <summary>実質負担が重く流動性が逼迫するほど投げ売り圧力が増す。物価どおり(1.0)＋流動性0なら圧力0。</summary>
        [Test]
        public void DistressSelling_実質負担と流動性逼迫で投げ売りが強まる()
        {
            // 実質負担1.0(=名目どおり)・流動性逼迫なし＝投げ売り圧0
            Assert.AreEqual(0f, DebtDeflationRules.DistressSelling(1.0f, 0f), Eps);
            // 既定 distressScale=0.6: 実質負担2.0(超過1.0)・流動性0 → 0.6
            Assert.AreEqual(0.6f, DebtDeflationRules.DistressSelling(2.0f, 0f), Eps);
            // 流動性逼迫を足すと増える（liquidityWeight=0.4）
            Assert.AreEqual(0.6f + 0.4f, DebtDeflationRules.DistressSelling(2.0f, 1.0f), Eps);
        }

        /// <summary>薄い市場(depth=0)ほど投げ売りが物価を強く押し下げる。厚い市場(depth=1)は等倍。</summary>
        [Test]
        public void PriceImpactOfSelling_薄市場ほど物価下落が増幅()
        {
            // 厚い市場＝等倍（thinMarketImpact は効かない）
            Assert.AreEqual(0.5f, DebtDeflationRules.PriceImpactOfSelling(0.5f, 1f), Eps);
            // 薄い市場＝thinMarketImpact=1.5 倍 → 0.5*1.5=0.75
            Assert.AreEqual(0.75f, DebtDeflationRules.PriceImpactOfSelling(0.5f, 0f), Eps);
            // 薄いほど物価インパクトが大きい
            Assert.Greater(DebtDeflationRules.PriceImpactOfSelling(0.5f, 0.2f),
                           DebtDeflationRules.PriceImpactOfSelling(0.5f, 0.8f));
        }

        /// <summary>スパイラル1tickで物価が下がり、債務負荷が重いほど深く下げる。物価は下限クランプ。</summary>
        [Test]
        public void DeflationSpiralTick_物価が押し下げられ自己強化する()
        {
            // 既定 spiralGain=0.5: 負荷0.4・dt1 → priceDrop=0.4*0.5=0.2 → 1.0*0.8=0.8
            Assert.AreEqual(0.8f, DebtDeflationRules.DeflationSpiralTick(1.0f, 0.4f, 1f), Eps);
            // 反復すると物価が下がり続ける＝努力が傷を深める
            float p1 = DebtDeflationRules.DeflationSpiralTick(1.0f, 0.4f, 1f);
            float p2 = DebtDeflationRules.DeflationSpiralTick(p1, 0.4f, 1f);
            Assert.Less(p2, p1);
            // 下限クランプで無限デフレを防ぐ
            float low = DebtDeflationRules.DeflationSpiralTick(0.1f, 1f, 1f);
            Assert.GreaterOrEqual(low, DebtDeflationRules.MinPriceLevel);
        }

        /// <summary>物価が1を割り債務負荷が閾値以上で悪循環突入。物価が下がっていなければ突入しない。</summary>
        [Test]
        public void IsDebtDeflation_物価下落と重い債務で突入判定()
        {
            // 物価0.8・負荷0.6・既定閾値0.5 → 突入
            Assert.IsTrue(DebtDeflationRules.IsDebtDeflation(0.8f, 0.6f));
            // 物価1.0以上は突入しない（デフレでない）
            Assert.IsFalse(DebtDeflationRules.IsDebtDeflation(1.2f, 0.9f));
            // 負荷が軽ければ突入しない
            Assert.IsFalse(DebtDeflationRules.IsDebtDeflation(0.8f, 0.2f));
        }

        /// <summary>債務減免は実質負担を直接軽くしてループの起点を断つ。</summary>
        [Test]
        public void DebtRelief_減免で実質負担が軽くなる()
        {
            // 減免0＝据え置き
            Assert.AreEqual(200f, DebtDeflationRules.DebtRelief(0f, 200f), Eps);
            // 減免30%＝負担0.7倍
            Assert.AreEqual(140f, DebtDeflationRules.DebtRelief(0.3f, 200f), Eps);
            // 全額減免＝0
            Assert.AreEqual(0f, DebtDeflationRules.DebtRelief(1f, 200f), Eps);
        }

        /// <summary>累積深刻度は反復で飽和的に深まり、債務減免後の投げ売り圧は軽くなる（出口の検証）。</summary>
        [Test]
        public void SpiralSeverity_反復で深まり減免が緩和する()
        {
            // 0回は深刻度0
            Assert.AreEqual(0f, DebtDeflationRules.SpiralSeverity(0, 0.4f), Eps);
            // 1回＝負荷ぶん（1−(1−0.4)^1＝0.4）
            Assert.AreEqual(0.4f, DebtDeflationRules.SpiralSeverity(1, 0.4f), Eps);
            // 反復で深まる（抜け出しにくくなる）
            Assert.Greater(DebtDeflationRules.SpiralSeverity(3, 0.4f),
                           DebtDeflationRules.SpiralSeverity(1, 0.4f));
            // 減免すると実質負担（名目=1.0基準の比率）が下がり投げ売り圧も下がる＝出口。
            // 物価が半減すると実質負担比は2.0（名目の2倍）、50%減免で1.0（名目どおり）へ。
            float burdenBefore = 2.0f;                                       // 物価半減時の実質負担比
            float burdenAfter = DebtDeflationRules.DebtRelief(0.5f, burdenBefore); // 1.0（名目どおり＝超過なし）
            Assert.Less(DebtDeflationRules.DistressSelling(burdenAfter, 0f),
                        DebtDeflationRules.DistressSelling(burdenBefore, 0f));
        }
    }
}
