using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 空間裁定を固定する：価格差は安い側と高い側の差、裁定の利得は価格差−輸送費（輸送費を超えて
    /// はじめて儲かる）、成立判定、価格収束（安い方↑高い方↓＝一物一価へ）、収束先に輸送費ぶんが残る、
    /// 市場統合度。境界を担保（#1075）。
    /// </summary>
    public class SpatialArbitrageRulesTests
    {
        private static readonly SpatialArbitrageParams P = SpatialArbitrageParams.Default;
        // 収束速度0.1/tick・統合度基準100

        [Test]
        public void PriceGap_HighMinusLow()
        {
            Assert.AreEqual(40f, SpatialArbitrageRules.PriceGap(60f, 100f), 1e-4f); // 安60→高100＝差40
            Assert.AreEqual(0f, SpatialArbitrageRules.PriceGap(100f, 60f), 1e-5f);  // 逆転は0
        }

        [Test]
        public void ArbitrageProfit_OnlyWhenGapExceedsTransport()
        {
            // 価格差40・輸送費10＝利幅30×数量5=150（差が輸送費を超えて儲かる）
            Assert.AreEqual(150f, SpatialArbitrageRules.ArbitrageProfit(40f, 10f, 5f), 1e-4f);
            // 価格差10・輸送費10＝利幅0＝割に合わない
            Assert.AreEqual(0f, SpatialArbitrageRules.ArbitrageProfit(10f, 10f, 5f), 1e-5f);
            // 輸送費が価格差を超える＝損は0でクランプ
            Assert.AreEqual(0f, SpatialArbitrageRules.ArbitrageProfit(5f, 10f, 5f), 1e-5f);
        }

        [Test]
        public void IsArbitrageViable_RequiresGapStrictlyAboveTransport()
        {
            Assert.IsTrue(SpatialArbitrageRules.IsArbitrageViable(40f, 10f));  // 超える＝成立
            Assert.IsFalse(SpatialArbitrageRules.IsArbitrageViable(10f, 10f)); // 等しい＝利幅ゼロ＝不成立
            Assert.IsFalse(SpatialArbitrageRules.IsArbitrageViable(5f, 10f));  // 足りない
        }

        [Test]
        public void ConvergenceTick_PushesPricesTogether()
        {
            // 安60/高100＝差40。0.1×取引量1×dt1＝差の10%=4 を半分ずつ寄せる
            SpatialArbitrageRules.ConvergenceTick(60f, 100f, 1f, 1f, out float buy, out float sell, P);
            Assert.AreEqual(62f, buy, 1e-4f);   // 安い方を押し上げる
            Assert.AreEqual(98f, sell, 1e-4f);  // 高い方を押し下げる
            Assert.Less(sell - buy, 40f);       // 価格差は縮む（一物一価へ）
        }

        [Test]
        public void ConvergenceTick_NoChangeWhenNoGap()
        {
            // 価格差なし＝裁定の駆動力なし
            SpatialArbitrageRules.ConvergenceTick(80f, 80f, 5f, 1f, out float buy, out float sell, P);
            Assert.AreEqual(80f, buy, 1e-5f);
            Assert.AreEqual(80f, sell, 1e-5f);
            // 逆転（安>高）も変化なし
            SpatialArbitrageRules.ConvergenceTick(100f, 60f, 5f, 1f, out buy, out sell, P);
            Assert.AreEqual(100f, buy, 1e-5f);
            Assert.AreEqual(60f, sell, 1e-5f);
        }

        [Test]
        public void ConvergenceTick_DoesNotOvershoot()
        {
            // 速度・取引量が大きくても価格差を超えて寄せない＝同価で合流（逆転しない）
            SpatialArbitrageRules.ConvergenceTick(60f, 100f, 1000f, 1f, out float buy, out float sell, P);
            Assert.AreEqual(80f, buy, 1e-4f);
            Assert.AreEqual(80f, sell, 1e-4f);
        }

        [Test]
        public void EquilibriumGap_LeavesTransportCost()
        {
            // 収束しても輸送費ぶんの差は残る（完全には一致しない）
            Assert.AreEqual(10f, SpatialArbitrageRules.EquilibriumGap(10f), 1e-4f);
            Assert.AreEqual(0f, SpatialArbitrageRules.EquilibriumGap(-5f), 1e-5f); // 負はクランプ
        }

        [Test]
        public void MarketIntegration_HigherWhenGapsNearTransportCost()
        {
            // 各ペアとも価格差＝輸送費＝超過ゼロ＝完全統合（裁定が出尽くした経済圏）
            float integrated = SpatialArbitrageRules.MarketIntegration(
                new float[] { 10f, 20f }, new float[] { 10f, 20f }, P);
            Assert.AreEqual(1f, integrated, 1e-4f);

            // 輸送費を超える価格差が残る＝未統合（基準100に対し超過50＝統合度0.5）
            float fragmented = SpatialArbitrageRules.MarketIntegration(
                new float[] { 60f }, new float[] { 10f }, P);
            Assert.AreEqual(0.5f, fragmented, 1e-4f);

            // 空配列＝統合度1
            Assert.AreEqual(1f, SpatialArbitrageRules.MarketIntegration(new float[0], new float[0], P), 1e-5f);
        }
    }
}
