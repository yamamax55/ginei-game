using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    public class PriceLevelRulesTests
    {
        private const float Tol = 1e-5f;

        [Test]
        public void MoreMoney_HigherEquilibriumPrice()
        {
            float low = PriceLevelRules.EquilibriumPrice(10f, 10f);
            float high = PriceLevelRules.EquilibriumPrice(20f, 10f);
            Assert.Greater(high, low);
        }

        [Test]
        public void MoreOutput_LowerPrice()
        {
            float few = PriceLevelRules.EquilibriumPrice(10f, 10f);
            float many = PriceLevelRules.EquilibriumPrice(10f, 20f);
            Assert.Less(many, few);
        }

        [Test]
        public void EquilibriumPrice_Formula()
        {
            // M=10 V=1 Y=10 → P=1
            float p = PriceLevelRules.EquilibriumPrice(10f, 10f);
            Assert.AreEqual(1f, p, Tol);
        }

        [Test]
        public void EquilibriumPrice_ClampedToBounds()
        {
            var prm = PriceLevelRules.Params.Default;
            // 巨額の貨幣 → 上限
            float hi = PriceLevelRules.EquilibriumPrice(1e9f, 1f, prm);
            Assert.AreEqual(prm.maxPrice, hi, Tol);
            // 貨幣ゼロ → 下限
            float lo = PriceLevelRules.EquilibriumPrice(0f, 1000f, prm);
            Assert.AreEqual(prm.minPrice, lo, Tol);
        }

        [Test]
        public void ZeroOutput_Guarded()
        {
            // 実質産出ゼロでも発散・例外なし（上限へクランプ）
            var prm = PriceLevelRules.Params.Default;
            float p = PriceLevelRules.EquilibriumPrice(10f, 0f, prm);
            Assert.AreEqual(prm.maxPrice, p, Tol);
        }

        [Test]
        public void Tick_MovesTowardEquilibrium()
        {
            float next = PriceLevelRules.Tick(1f, 5f, 1f);
            Assert.Greater(next, 1f);
            Assert.Less(next, 5f + Tol);
        }

        [Test]
        public void Tick_NoOvershootInOneStep()
        {
            // adjustSpeed×dt が1を超えても行き過ぎない（Clamp01）
            float up = PriceLevelRules.Tick(1f, 5f, 100f);
            Assert.LessOrEqual(up, 5f + Tol);
            float down = PriceLevelRules.Tick(5f, 1f, 100f);
            Assert.GreaterOrEqual(down, 1f - Tol);
        }

        [Test]
        public void Tick_ConvergesToward_Down()
        {
            float next = PriceLevelRules.Tick(5f, 1f, 0.5f);
            Assert.Less(next, 5f);
            Assert.Greater(next, 1f - Tol);
        }

        [Test]
        public void Tick_ClampedToBounds()
        {
            var prm = PriceLevelRules.Params.Default;
            // 均衡が上限なら結果も上限を超えない
            float next = PriceLevelRules.Tick(prm.maxPrice, 1e9f, 1f, prm);
            Assert.LessOrEqual(next, prm.maxPrice + Tol);
        }

        [Test]
        public void Tick_Deterministic()
        {
            float a = PriceLevelRules.Tick(2f, 8f, 0.3f);
            float b = PriceLevelRules.Tick(2f, 8f, 0.3f);
            Assert.AreEqual(a, b, Tol);
        }

        [Test]
        public void InflationRate_SignCorrect()
        {
            // 上昇 → 正
            Assert.Greater(PriceLevelRules.InflationRate(1f, 1.1f), 0f);
            // 下落 → 負
            Assert.Less(PriceLevelRules.InflationRate(1f, 0.9f), 0f);
            // 不変 → 0
            Assert.AreEqual(0f, PriceLevelRules.InflationRate(2f, 2f), Tol);
        }

        [Test]
        public void InflationRate_Magnitude()
        {
            // 1→1.1 は10%
            Assert.AreEqual(0.1f, PriceLevelRules.InflationRate(1f, 1.1f), Tol);
        }

        [Test]
        public void InflationRate_ZeroOldGuarded()
        {
            // 旧物価ゼロでも発散・例外なし
            float r = PriceLevelRules.InflationRate(0f, 1f);
            Assert.IsFalse(float.IsNaN(r));
            Assert.IsFalse(float.IsInfinity(r));
        }
    }
}
