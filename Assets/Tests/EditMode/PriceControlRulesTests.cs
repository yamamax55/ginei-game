using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>価格統制の純ロジックのテスト（既定Paramsで期待値固定・統制の自己破壊と闇値プレミアムを担保）。</summary>
    public class PriceControlRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>品不足＝統制価格が均衡を下回るほど棚が空く＝抑えた分が不足になる。</summary>
        [Test]
        public void Shortage_統制が均衡を下回るほど不足が深い()
        {
            // 統制60／均衡100＝(100-60)/100=0.4
            Assert.AreEqual(0.4f, PriceControlRules.Shortage(60f, 100f), Eps);
            // 統制が均衡以上＝抑えていない＝不足0
            Assert.AreEqual(0f, PriceControlRules.Shortage(120f, 100f), Eps);
        }

        /// <summary>行列＝品不足を時間で配給＝金の代わりに時間で払う（既定 queueRate=2）。</summary>
        [Test]
        public void QueueLength_不足に比例して行列が伸びる()
        {
            Assert.AreEqual(0.8f, PriceControlRules.QueueLength(0.4f), Eps); // 2*0.4
        }

        /// <summary>闇値プレミアム＝取り締まりが厳しいほどリスク料が乗って跳ねる。</summary>
        [Test]
        public void BlackMarketPremium_取締が厳しいほど闇値が高くつく()
        {
            // 取締なし＝3*0.4*1/1.5=0.8
            Assert.AreEqual(0.8f, PriceControlRules.BlackMarketPremium(0.4f, 0f), Eps);
            // 取締最強＝3*0.4*1.5/1.5=1.2（統制を強めるほど闇値が上がる）
            Assert.AreEqual(1.2f, PriceControlRules.BlackMarketPremium(0.4f, 1f), Eps);
            // 不足0＝買えるなら闇市の出番なし
            Assert.AreEqual(0f, PriceControlRules.BlackMarketPremium(0f, 1f), Eps);
        }

        /// <summary>統制の自己破壊＝採算割れの統制価格は生産者を撤退させ供給を縮める。</summary>
        [Test]
        public void SupplyDestructionTick_採算割れで供給が縮む()
        {
            // 統制50<コスト100＝割れ幅0.5＝0.1*0.5*100*1=5減＝95
            Assert.AreEqual(95f, PriceControlRules.SupplyDestructionTick(100f, 50f, 100f, 1f), Eps);
            // 統制=コスト＝採算成立＝供給は減らない
            Assert.AreEqual(100f, PriceControlRules.SupplyDestructionTick(100f, 100f, 100f, 1f), Eps);
        }

        /// <summary>失敗度＝闇価格との乖離が大きいほど統制は形骸（既定 failureHalfPremium=1）。</summary>
        [Test]
        public void ControlFailureIndex_闇値の乖離で統制が形骸化する()
        {
            Assert.AreEqual(0.5f, PriceControlRules.ControlFailureIndex(1f), Eps);  // 肩で0.5
            Assert.AreEqual(0.75f, PriceControlRules.ControlFailureIndex(3f), Eps); // 大乖離で1へ漸近
            Assert.AreEqual(0f, PriceControlRules.ControlFailureIndex(0f), Eps);    // 乖離なし＝統制有効
        }

        /// <summary>安さの幻想＝買えれば安いが買えない＝入手率で割り引く＝紙の上の安さ。</summary>
        [Test]
        public void ConsumerSurplusIllusion_買えなければ安さは幻()
        {
            // 見かけ余剰(100-60)=40を入手率0.5で割り引く＝20
            Assert.AreEqual(20f, PriceControlRules.ConsumerSurplusIllusion(60f, 100f, 0.5f), Eps);
            // 全く買えない＝余剰0（公示の安さは絵に描いた餅）
            Assert.AreEqual(0f, PriceControlRules.ConsumerSurplusIllusion(60f, 100f, 0f), Eps);
        }

        /// <summary>統制を強めるほど闇値プレミアムが上がり失敗度も上がる＝抑えた歪みが噴出する一貫性。</summary>
        [Test]
        public void 統制強化が闇値と失敗度を同時に押し上げる()
        {
            float premLoose = PriceControlRules.BlackMarketPremium(0.4f, 0f);
            float premTight = PriceControlRules.BlackMarketPremium(0.4f, 1f);
            Assert.Greater(premTight, premLoose);
            Assert.Greater(
                PriceControlRules.ControlFailureIndex(premTight),
                PriceControlRules.ControlFailureIndex(premLoose));
        }
    }
}
