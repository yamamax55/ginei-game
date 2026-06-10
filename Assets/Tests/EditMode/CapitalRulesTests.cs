using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// ピケティ r>g（#917）を固定する：r>g で集中ドリフトが正、g>r で負、累進資本課税は集中を下げる、
    /// Tick の更新と 0..1 クランプ、格差→反乱圧力の閾値と飽和。決定論。
    /// </summary>
    public class CapitalRulesTests
    {
        private static CapitalRules.Params P => CapitalRules.Params.Default;

        [Test]
        public void ConcentrationDrift_PositiveWhenReturnExceedsGrowth()
        {
            var s = new CapitalState { capitalReturn = 0.06f, growthRate = 0.02f }; // r>g
            Assert.Greater(CapitalRules.ConcentrationDrift(s, 1f, P), 0f);
        }

        [Test]
        public void ConcentrationDrift_NegativeWhenGrowthExceedsReturn()
        {
            var s = new CapitalState { capitalReturn = 0.01f, growthRate = 0.05f }; // g>r
            Assert.Less(CapitalRules.ConcentrationDrift(s, 1f, P), 0f);
        }

        [Test]
        public void ConcentrationDrift_NullSafe()
        {
            Assert.AreEqual(0f, CapitalRules.ConcentrationDrift(null, 1f, P), 1e-6f);
        }

        [Test]
        public void CapitalTaxEffect_ReducesAndClampsRate()
        {
            float none = CapitalRules.CapitalTaxEffect(0f, P);
            float some = CapitalRules.CapitalTaxEffect(0.5f, P);
            float over = CapitalRules.CapitalTaxEffect(2f, P); // クランプ＝1扱い
            Assert.AreEqual(0f, none, 1e-6f);
            Assert.Greater(some, 0f);
            Assert.AreEqual(CapitalRules.CapitalTaxEffect(1f, P), over, 1e-6f); // 1超は飽和
        }

        [Test]
        public void Tick_RaisesConcentrationUnderHighReturn()
        {
            var s = new CapitalState { capitalReturn = 0.08f, growthRate = 0.02f, wealthConcentration = 0.4f };
            CapitalRules.Tick(s, 0f, 1f, P); // 無課税＝集中↑
            Assert.Greater(s.wealthConcentration, 0.4f);
        }

        [Test]
        public void Tick_ProgressiveTaxLowersConcentration()
        {
            var s = new CapitalState { capitalReturn = 0.03f, growthRate = 0.02f, wealthConcentration = 0.5f };
            CapitalRules.Tick(s, 1f, 1f, P); // 強い累進課税＝集中↓
            Assert.Less(s.wealthConcentration, 0.5f);
        }

        [Test]
        public void Tick_ClampsToUnitRange()
        {
            var hi = new CapitalState { capitalReturn = 0.9f, growthRate = 0f, wealthConcentration = 0.99f };
            CapitalRules.Tick(hi, 0f, 1f, P);
            Assert.LessOrEqual(hi.wealthConcentration, 1f);
            Assert.GreaterOrEqual(hi.wealthConcentration, 0f);

            var lo = new CapitalState { capitalReturn = 0f, growthRate = 0.9f, wealthConcentration = 0.01f };
            CapitalRules.Tick(lo, 1f, 1f, P);
            Assert.GreaterOrEqual(lo.wealthConcentration, 0f);
            Assert.LessOrEqual(lo.wealthConcentration, 1f);
        }

        [Test]
        public void InequalityUnrest_ZeroBelowThreshold()
        {
            // 既定閾値 0.5 未満＝反乱圧力なし
            Assert.AreEqual(0f, CapitalRules.InequalityUnrest(0.3f), 1e-6f);
        }

        [Test]
        public void InequalityUnrest_RisesAboveThreshold()
        {
            float mid = CapitalRules.InequalityUnrest(0.7f);
            float high = CapitalRules.InequalityUnrest(0.9f);
            Assert.Greater(mid, 0f);
            Assert.Greater(high, mid); // 集中が高いほど反乱圧力↑
        }

        [Test]
        public void InequalityUnrest_SaturatesAtOne()
        {
            // 感度を極端に大きくしても 0..1 にクランプ＝飽和
            var loud = CapitalRules.Params.Default;
            loud.unrestScale = 100f;
            Assert.AreEqual(1f, CapitalRules.InequalityUnrest(1f, loud), 1e-6f);
        }
    }
}
