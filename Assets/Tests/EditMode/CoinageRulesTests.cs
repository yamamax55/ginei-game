using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 通貨改鋳と品位（#1072）を固定する：地金価値＝含有×額面、発行益は品位を下げるほど増え、
    /// 改鋳露見で信用が落ち（満貨化は遅く回復）、悪貨が良貨を駆逐（グレシャム）、信用が落ちれば
    /// 実質購買力は額面割れ、品位回復には地金注入が要る。シニョリッジと信用低下のトレードオフを担保。
    /// </summary>
    public class CoinageRulesTests
    {
        private static readonly CoinageParams P = CoinageParams.Default;
        // 地金価値1.0/鋳造費0.05/信用浸食0.5/信用回復0.15/グレシャム強度1.0

        [Test]
        public void IntrinsicValue_ContentTimesFace()
        {
            Assert.AreEqual(100f, CoinageRules.IntrinsicValue(1f, 100f, P), 1e-4f);   // 満貨＝額面ぶんの地金
            Assert.AreEqual(50f, CoinageRules.IntrinsicValue(0.5f, 100f, P), 1e-4f);  // 品位半分＝中身も半分
            Assert.AreEqual(0f, CoinageRules.IntrinsicValue(0f, 100f, P), 1e-4f);     // 銀ゼロ＝地金なし
        }

        [Test]
        public void Seigniorage_RisesAsContentFalls()
        {
            // 満貨＝額面100−地金100−鋳造費5＝−5（鋳造費ぶん赤字）
            Assert.AreEqual(-5f, CoinageRules.Seigniorage(100f, 1f, P), 1e-4f);
            // 品位0.5＝100−50−5＝45の儲け
            Assert.AreEqual(45f, CoinageRules.Seigniorage(100f, 0.5f, P), 1e-4f);
            // 銀ゼロ＝100−0−5＝95（品位を下げるほど儲かる短期の誘惑）
            Assert.AreEqual(95f, CoinageRules.Seigniorage(100f, 0f, P), 1e-4f);
            // 品位が下がるほど発行益は単調増加
            Assert.Greater(CoinageRules.Seigniorage(100f, 0.2f, P), CoinageRules.Seigniorage(100f, 0.8f, P));
        }

        [Test]
        public void DebasementTick_ErodesTrustWhenShort_RecoversWhenFull()
        {
            // 期待1.0に対し実含有0.6＝乖離0.4 → 信用1.0−0.4×0.5×1＝0.8
            Assert.AreEqual(0.8f, CoinageRules.DebasementTick(1f, 0.6f, 1f, 1f, P), 1e-4f);
            // 満貨（実≥期待）＝信用回復。0.5+ (0.2)×0.15×1＝0.53
            Assert.AreEqual(0.53f, CoinageRules.DebasementTick(0.5f, 1f, 0.8f, 1f, P), 1e-4f);
            // 浸食は回復より速い（同じ乖離量0.3で比較）
            float erode = 1f - CoinageRules.DebasementTick(1f, 0.7f, 1f, 1f, P);   // 0.3×0.5=0.15
            float recover = CoinageRules.DebasementTick(0.5f, 1f, 0.7f, 1f, P) - 0.5f; // 0.3×0.15=0.045
            Assert.Greater(erode, recover);
        }

        [Test]
        public void DebasementTick_ClampsToUnitRange()
        {
            Assert.AreEqual(0f, CoinageRules.DebasementTick(0.05f, 0f, 1f, 1f, P), 1e-4f); // 大乖離で底打ち
            Assert.AreEqual(1f, CoinageRules.DebasementTick(0.95f, 1f, 0f, 10f, P), 1e-4f); // 満貨長期で天井
        }

        [Test]
        public void GreshamEffect_BadDrivesOutGood()
        {
            // 旧貨0.9・新貨0.4＝品位差0.5ぶんの良貨が退蔵される
            Assert.AreEqual(0.5f, CoinageRules.GreshamEffect(0.4f, 0.9f, P), 1e-4f);
            // 新貨が旧貨以上＝駆逐は起きない
            Assert.AreEqual(0f, CoinageRules.GreshamEffect(0.9f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void RealPurchasingPower_FaceTimesTrust()
        {
            Assert.AreEqual(100f, CoinageRules.RealPurchasingPower(100f, 1f), 1e-4f);  // 信用満点＝額面通り
            Assert.AreEqual(60f, CoinageRules.RealPurchasingPower(100f, 0.6f), 1e-4f); // 信用失墜＝額面割れ
            Assert.AreEqual(0f, CoinageRules.RealPurchasingPower(100f, 0f), 1e-4f);    // 信用ゼロ＝額面は紙
        }

        [Test]
        public void RestorationCost_OneWayTicket()
        {
            // 品位0.4→0.9へ戻す：差0.5×流通量1000×地金1.0＝500の地金注入
            Assert.AreEqual(500f, CoinageRules.RestorationCost(0.4f, 0.9f, 1000f, P), 1e-3f);
            // 既に目標以上＝戻すコストなし（下げるのは儲かり戻すのは身銭＝片道切符）
            Assert.AreEqual(0f, CoinageRules.RestorationCost(0.9f, 0.5f, 1000f, P), 1e-4f);
        }
    }
}
