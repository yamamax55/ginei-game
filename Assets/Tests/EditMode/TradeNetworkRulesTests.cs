using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>TradeNetworkRules（交易ネットワーク収益）の EditMode テスト。</summary>
    public class TradeNetworkRulesTests
    {
        [Test]
        public void Deterministic_SameInputsSameOutput()
        {
            float a = TradeNetworkRules.RouteIncome(10f, 20f, 5f, 0.8f);
            float b = TradeNetworkRules.RouteIncome(10f, 20f, 5f, 0.8f);
            Assert.AreEqual(a, b, 1e-5f);
        }

        [Test]
        public void Distance_ReducesIncome()
        {
            float near = TradeNetworkRules.RouteIncome(10f, 10f, 1f, 1f);
            float far = TradeNetworkRules.RouteIncome(10f, 10f, 20f, 1f);
            Assert.Less(far, near);
        }

        [Test]
        public void Openness_ScalesLinearly()
        {
            float full = TradeNetworkRules.RouteIncome(10f, 10f, 2f, 1f);
            float half = TradeNetworkRules.RouteIncome(10f, 10f, 2f, 0.5f);
            Assert.AreEqual(full * 0.5f, half, 1e-5f);
        }

        [Test]
        public void Openness_ZeroGivesZero()
        {
            float v = TradeNetworkRules.RouteIncome(10f, 10f, 2f, 0f);
            Assert.AreEqual(0f, v, 1e-5f);
        }

        [Test]
        public void LargerSizes_IncreaseIncome()
        {
            float small = TradeNetworkRules.RouteIncome(5f, 5f, 2f, 1f);
            float big = TradeNetworkRules.RouteIncome(10f, 10f, 2f, 1f);
            Assert.Greater(big, small);
        }

        [Test]
        public void Clamp_ToMaxIncome()
        {
            var p = new TradeNetworkRules.Params(0f, 1f, 50f);
            // 距離減衰ゼロ・巨大規模で上限を超える入力。
            float v = TradeNetworkRules.RouteIncome(1000f, 1000f, 0f, 1f, p);
            Assert.AreEqual(50f, v, 1e-5f);
        }

        [Test]
        public void NegativeInputs_TreatedAsZero()
        {
            float v = TradeNetworkRules.RouteIncome(-10f, 20f, 5f, 1f);
            Assert.AreEqual(0f, v, 1e-5f);
        }

        [Test]
        public void NegativeDistance_ClampedToZero()
        {
            float neg = TradeNetworkRules.RouteIncome(10f, 10f, -5f, 1f);
            float zero = TradeNetworkRules.RouteIncome(10f, 10f, 0f, 1f);
            Assert.AreEqual(zero, neg, 1e-5f);
        }

        [Test]
        public void Openness_AboveOneClamped()
        {
            float over = TradeNetworkRules.RouteIncome(10f, 10f, 2f, 5f);
            float one = TradeNetworkRules.RouteIncome(10f, 10f, 2f, 1f);
            Assert.AreEqual(one, over, 1e-5f);
        }

        [Test]
        public void TotalIncome_SumsIgnoringNegatives()
        {
            var list = new System.Collections.Generic.List<float> { 10f, -5f, 20f, 0f };
            float total = TradeNetworkRules.TotalIncome(list);
            Assert.AreEqual(30f, total, 1e-5f);
        }

        [Test]
        public void TotalIncome_NullIsZero()
        {
            Assert.AreEqual(0f, TradeNetworkRules.TotalIncome(null), 1e-5f);
        }
    }
}
