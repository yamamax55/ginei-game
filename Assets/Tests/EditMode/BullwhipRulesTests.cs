using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ブルウィップ効果（#1114）の純ロジック検証＝1段増幅・段数での指数的増幅・発注変動・在庫振動・情報共有/平準化の緩和。</summary>
    public class BullwhipRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>1段増幅＝在庫調整と予測（リードタイム）で変動を膨らませる（≥1・既定 safety0.5/lead0.25）。</summary>
        [Test]
        public void AmplificationPerStage_ExceedsOneFromStockAndLeadTime()
        {
            // demandVariance=0：varianceNudge=0、gain=0.5×0.5 + 1×0.25×(0.5+0) = 0.25+0.125 = 0.375、amp=1.375
            float amp = BullwhipRules.AmplificationPerStage(demandVariance: 0f, safetyStockFactor: 0.5f, leadTime: 1f);
            Assert.AreEqual(1.375f, amp, Eps);
            // 在庫調整もリードタイムも無ければ素通り（増幅1）
            Assert.AreEqual(1f, BullwhipRules.AmplificationPerStage(0f, 0f, 0f), Eps);
            // 1未満にはならない
            Assert.GreaterOrEqual(BullwhipRules.AmplificationPerStage(5f, 1f, 3f), 1f);
        }

        /// <summary>N段上流＝末端の変動が段数を経て指数的に増幅する（鞭の先より根元が大きく振れる）。</summary>
        [Test]
        public void UpstreamVariance_AmplifiesExponentiallyWithStages()
        {
            // amp=1.5、end=10：1段=15、2段=22.5、3段=33.75
            Assert.AreEqual(15f, BullwhipRules.UpstreamVariance(10f, 1, 1.5f), Eps);
            Assert.AreEqual(22.5f, BullwhipRules.UpstreamVariance(10f, 2, 1.5f), Eps);
            Assert.AreEqual(33.75f, BullwhipRules.UpstreamVariance(10f, 3, 1.5f), Eps);
            // 段数で単調に増幅＝上流ほど大きく振れる
            Assert.Greater(BullwhipRules.UpstreamVariance(10f, 4, 1.5f), BullwhipRules.UpstreamVariance(10f, 1, 1.5f));
            // 0段なら末端そのもの
            Assert.AreEqual(10f, BullwhipRules.UpstreamVariance(10f, 0, 1.5f), Eps);
            // 増幅率1（素通り）なら段数によらず末端のまま
            Assert.AreEqual(10f, BullwhipRules.UpstreamVariance(10f, 5, 1f), Eps);
        }

        /// <summary>発注変動＝予測誤差とまとめ発注が実需より発注を揺らす（既定 batchingWeight=0.5）。</summary>
        [Test]
        public void OrderVariability_GrowsWithForecastErrorAndBatching()
        {
            // actual=10, error=0.5, batch=2：inflate=1+0.5+2×0.5=2.5、=25
            Assert.AreEqual(25f, BullwhipRules.OrderVariability(actualDemandVariance: 10f, forecastError: 0.5f, batchingFactor: 2f), Eps);
            // 誤差もバッチも無ければ実需＝発注変動
            Assert.AreEqual(10f, BullwhipRules.OrderVariability(10f, 0f, 0f), Eps);
            // 実需≦発注変動（増幅は下回らない）
            Assert.GreaterOrEqual(BullwhipRules.OrderVariability(10f, 0.3f, 1f), 10f);
        }

        /// <summary>在庫振動＝上流変動が大きいほど過剰在庫と欠品を大きく往復する。</summary>
        [Test]
        public void InventorySwing_ScalesWithUpstreamVariance()
        {
            Assert.AreEqual(33.75f, BullwhipRules.InventorySwing(33.75f), Eps);
            Assert.Greater(BullwhipRules.InventorySwing(33.75f), BullwhipRules.InventorySwing(15f));
            // 負入力は0クランプ
            Assert.AreEqual(0f, BullwhipRules.InventorySwing(-5f), Eps);
        }

        /// <summary>情報共有による緩和＝末端需要を上流が直接見れば増幅が止まる（共有1で消滅）。</summary>
        [Test]
        public void MitigationByInfoSharing_VisibilityTamesTheWhip()
        {
            // base=33.75, share=0.5：33.75×0.5 = 16.875
            Assert.AreEqual(16.875f, BullwhipRules.MitigationByInfoSharing(33.75f, 0.5f), Eps);
            // 共有0なら無緩和
            Assert.AreEqual(33.75f, BullwhipRules.MitigationByInfoSharing(33.75f, 0f), Eps);
            // 完全共有で増幅が消える＝末端へ漸近（0）
            Assert.AreEqual(0f, BullwhipRules.MitigationByInfoSharing(33.75f, 1f), Eps);
        }

        /// <summary>発注平準化による緩和＝発注をならして鞭を抑える（floor まで・実需分は残る）。</summary>
        [Test]
        public void MitigationBySmoothing_LevelingReducesToFloor()
        {
            // base=100, smooth=0.5：reduction=0.5×(1-0.2)=0.4、100×0.6 = 60
            Assert.AreEqual(60f, BullwhipRules.MitigationBySmoothing(100f, 0.5f), Eps);
            // 平準化0なら無緩和
            Assert.AreEqual(100f, BullwhipRules.MitigationBySmoothing(100f, 0f), Eps);
            // 完全平準化でも floor(0.2) ぶんは残る＝実需の変動は消えない
            Assert.AreEqual(20f, BullwhipRules.MitigationBySmoothing(100f, 1f), Eps);
        }

        /// <summary>多段の鞭＝段数で膨らんだ変動を情報共有が抑える＝増幅と緩和を一度に担保。</summary>
        [Test]
        public void EndToEnd_StagesAmplifyThenInfoSharingTames()
        {
            float amp = BullwhipRules.AmplificationPerStage(0f, 0.5f, 1f);   // 1.375
            float upstream = BullwhipRules.UpstreamVariance(10f, 3, amp);     // 末端10が3段で膨らむ
            Assert.Greater(upstream, 10f, "上流ほど大きく振れる");
            float tamed = BullwhipRules.MitigationByInfoSharing(upstream, 0.8f);
            Assert.Less(tamed, upstream, "情報共有が鞭を抑える");
        }
    }
}
