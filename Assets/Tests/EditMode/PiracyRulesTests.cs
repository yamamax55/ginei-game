using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 宇宙海賊を固定する：治安の空白×流量で湧き・略奪で複利に肥え・哨戒で削れる、交易被害は勢力比、
    /// 封じ込め判定（討伐が成長を上回る努力水準）。境界を担保。
    /// </summary>
    public class PiracyRulesTests
    {
        private static readonly PiracyParams P = PiracyParams.Default;
        // 湧き0.05/略奪成長0.03/討伐0.1/被害上限0.4

        [Test]
        public void PiracyTick_SpawnsInSecurityVacuum()
        {
            // 治安0・流量1・哨戒0・dt1：湧き0.05（既存0なら肥えも討伐も0）
            Assert.AreEqual(0.05f, PiracyRules.PiracyTick(0f, 0f, 1f, 0f, 1f, P), 1e-5f);
            // 治安1＝湧かない
            Assert.AreEqual(0f, PiracyRules.PiracyTick(0f, 1f, 1f, 0f, 1f, P), 1e-5f);
            // 交易が無ければ湧かない（獲物がいない）
            Assert.AreEqual(0f, PiracyRules.PiracyTick(0f, 0f, 0f, 0f, 1f, P), 1e-5f);
        }

        [Test]
        public void PiracyTick_GrowsCompoundWhenUnchecked()
        {
            // 既存10・哨戒0：肥え0.03×10=0.3 が湧きに上乗せ
            float next = PiracyRules.PiracyTick(10f, 0f, 1f, 0f, 1f, P);
            Assert.AreEqual(10f + 0.05f + 0.3f, next, 1e-4f);
        }

        [Test]
        public void PiracyTick_SuppressedByPatrol()
        {
            // 既存10・治安1・哨戒1：湧き0・肥え0・討伐0.1×10=1.0 → 9
            Assert.AreEqual(9f, PiracyRules.PiracyTick(10f, 1f, 1f, 1f, 1f, P), 1e-4f);
            // 下限0
            Assert.AreEqual(0f, PiracyRules.PiracyTick(0.05f, 1f, 0f, 1f, 100f, P), 1e-5f);
        }

        [Test]
        public void TradeLossRatio_ByDominance()
        {
            // 海賊と哨戒が同勢力＝支配度0.5＝被害0.2
            Assert.AreEqual(0.2f, PiracyRules.TradeLossRatio(10f, 10f, P), 1e-5f);
            // 哨戒なし＝上限0.4
            Assert.AreEqual(0.4f, PiracyRules.TradeLossRatio(10f, 0f, P), 1e-5f);
            // 海賊なし＝無傷
            Assert.AreEqual(0f, PiracyRules.TradeLossRatio(0f, 10f, P), 1e-5f);
        }

        [Test]
        public void IsContained_EffortThreshold()
        {
            // suppression(0.1)×e > lootGrowth(0.03)×(1−e) → e > 0.03/0.13 ≈ 0.2308
            Assert.IsFalse(PiracyRules.IsContained(0.5f, 0.2f, P));
            Assert.IsTrue(PiracyRules.IsContained(0.5f, 0.25f, P));
            Assert.IsTrue(PiracyRules.IsContained(0.5f, 1f, P));
            Assert.IsFalse(PiracyRules.IsContained(0.5f, 0f, P)); // 放置＝育つ
        }
    }
}
