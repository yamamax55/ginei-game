using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// バブル価格解離（MNIA-2 #1622）の純ロジックテスト。既定 Params 具体値で期待値を固定し、
    /// 熱狂による吊り上げ（バブル倍率）・崩壊による底割れ（オーバーシュート）・解離度判定・実需回帰を担保。
    /// </summary>
    public class BubblePriceRulesTests
    {
        private const float Eps = 1e-4f;

        /// <summary>熱狂0で1.0倍・最大で maxBubbleMultiplier(5)・中間0.5は非線形(2乗)で2.0倍。</summary>
        [Test]
        public void BubbleMultiplier_熱狂で実需の何倍にも吊り上がる()
        {
            Assert.AreEqual(1f, BubblePriceRules.BubbleMultiplier(0f), Eps);    // 熱狂なし＝実需どおり
            Assert.AreEqual(5f, BubblePriceRules.BubbleMultiplier(1f), Eps);    // 最大熱狂＝5倍
            Assert.AreEqual(2f, BubblePriceRules.BubbleMultiplier(0.5f), Eps);  // 1+4*0.5^2=2.0（極まってから跳ねる）
        }

        /// <summary>市場価格＝実需×バブル倍率。熱狂0.5なら実需100が200へ膨らむ。</summary>
        [Test]
        public void MarketPrice_実需にバブル倍率を掛ける()
        {
            Assert.AreEqual(100f, BubblePriceRules.MarketPrice(100f, 0f), Eps);   // 熱狂なし＝実需100
            Assert.AreEqual(200f, BubblePriceRules.MarketPrice(100f, 0.5f), Eps); // 100×2.0
            Assert.AreEqual(500f, BubblePriceRules.MarketPrice(100f, 1f), Eps);   // 100×5.0
        }

        /// <summary>崩壊0で1.0倍・最深で minOvershootMultiplier(0.4)・中間0.5は非線形(2乗)で0.85倍＝1.0未満。</summary>
        [Test]
        public void OvershootMultiplier_崩壊で適正を下回る底割れ()
        {
            Assert.AreEqual(1f, BubblePriceRules.OvershootMultiplier(0f), Eps);     // 崩壊なし＝適正どおり
            Assert.AreEqual(0.4f, BubblePriceRules.OvershootMultiplier(1f), Eps);   // 最深崩壊＝0.4倍（底割れ）
            Assert.AreEqual(0.85f, BubblePriceRules.OvershootMultiplier(0.5f), Eps);// 1-0.6*0.5^2=0.85（1.0未満）
            Assert.Less(BubblePriceRules.OvershootMultiplier(0.5f), 1f);            // 底割れは常に1.0未満
        }

        /// <summary>崩壊後の底割れ価格＝実需×底割れ倍率。最深崩壊で実需100が適正以下の40へ。</summary>
        [Test]
        public void CrashPrice_実需すら下回って底割れする()
        {
            Assert.AreEqual(40f, BubblePriceRules.CrashPrice(100f, 1f), Eps);    // 100×0.4＝適正100を下回る
            Assert.AreEqual(85f, BubblePriceRules.CrashPrice(100f, 0.5f), Eps);  // 100×0.85
            Assert.Less(BubblePriceRules.CrashPrice(100f, 0.5f), 100f);          // 実需より下＝下側オーバーシュート
        }

        /// <summary>解離度＝市場価格/実需−1。バブルで正・底割れで負・実需≤0は評価不能で0。</summary>
        [Test]
        public void Deviation_実需からの乖離を符号付きで返す()
        {
            Assert.AreEqual(1f, BubblePriceRules.Deviation(200f, 100f), Eps);     // 割高＝+100%
            Assert.AreEqual(-0.6f, BubblePriceRules.Deviation(40f, 100f), Eps);   // 割安＝−60%（底割れ）
            Assert.AreEqual(0f, BubblePriceRules.Deviation(200f, 0f), Eps);       // 実需なし＝評価不能で0
        }

        /// <summary>バブル判定＝解離度が閾値超で割高なら true。割安（負の解離）はバブルでない。</summary>
        [Test]
        public void IsBubble_閾値超の割高だけをバブルとする()
        {
            Assert.IsTrue(BubblePriceRules.IsBubble(1f, 0.5f));    // +100% > 閾値50%＝バブル
            Assert.IsFalse(BubblePriceRules.IsBubble(0.3f, 0.5f)); // +30% は閾値未満＝バブルでない
            Assert.IsFalse(BubblePriceRules.IsBubble(-0.5f, 0.5f));// 割安はバブルでない
        }

        /// <summary>実需回帰＝行き過ぎた価格を実需へ引き戻す。バブルも底割れもいずれ均衡へ。</summary>
        [Test]
        public void MeanReversionTick_行き過ぎは時間で実需へ戻る()
        {
            // step=|100-200|*2*1=200 ≥ 差100 ＝ 一気に実需へ到達
            Assert.AreEqual(100f, BubblePriceRules.MeanReversionTick(200f, 100f, 1f), Eps);
            // 緩い speed0.5：step=100*0.5*0.1=5＝200→195（少しだけ下げて寄る）
            Assert.AreEqual(195f, BubblePriceRules.MeanReversionTick(200f, 100f, 0.1f, 0.5f), Eps);
            // 底割れ側も実需へ上昇：step=|100-40|*2*0.1=12＝40→52
            Assert.AreEqual(52f, BubblePriceRules.MeanReversionTick(40f, 100f, 0.1f, 2f), Eps);
            // dt≤0 は据え置き
            Assert.AreEqual(200f, BubblePriceRules.MeanReversionTick(200f, 100f, 0f), Eps);
        }

        /// <summary>入力クランプ：負の実需/価格は0扱い・強度は0..1へ丸め（範囲外でも例外なく安全）。</summary>
        [Test]
        public void 入力は安全にクランプされる()
        {
            Assert.AreEqual(0f, BubblePriceRules.MarketPrice(-50f, 0.5f), Eps);             // 負の実需＝0
            Assert.AreEqual(5f, BubblePriceRules.BubbleMultiplier(2f), Eps);               // 強度>1は1へ＝5倍
            Assert.AreEqual(1f, BubblePriceRules.BubbleMultiplier(-1f), Eps);              // 強度<0は0へ＝1倍
            Assert.AreEqual(0.4f, BubblePriceRules.OvershootMultiplier(2f), Eps);          // 崩壊>1は1へ＝0.4倍
        }
    }
}
