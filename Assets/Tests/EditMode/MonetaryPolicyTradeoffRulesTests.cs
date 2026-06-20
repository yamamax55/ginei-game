using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>金融政策トレードオフ（MonetaryPolicyTradeoffRules）の純ロジック検証。</summary>
    public class MonetaryPolicyTradeoffRulesTests
    {
        const float Tol = 1e-5f;

        [Test]
        public void Investment_RateEqualsNeutral_IsOne()
        {
            var p = MonetaryPolicyTradeoffRules.Params.Default;
            Assert.AreEqual(1f, MonetaryPolicyTradeoffRules.InvestmentFactor(p.neutralRate, p), Tol);
        }

        [Test]
        public void Investment_LowerRate_RaisesFactor()
        {
            var p = MonetaryPolicyTradeoffRules.Params.Default;
            float high = MonetaryPolicyTradeoffRules.InvestmentFactor(0.4f, p);
            float low = MonetaryPolicyTradeoffRules.InvestmentFactor(0.1f, p);
            Assert.Greater(low, high);
        }

        [Test]
        public void Investment_Clamped_ZeroToTwo()
        {
            var p = MonetaryPolicyTradeoffRules.Params.Default;
            // 極端に高い金利 → 0 下限
            Assert.AreEqual(0f, MonetaryPolicyTradeoffRules.InvestmentFactor(100f, p), Tol);
            // 極端に低い（負の）金利 → 2 上限
            Assert.AreEqual(2f, MonetaryPolicyTradeoffRules.InvestmentFactor(-100f, p), Tol);
        }

        [Test]
        public void Investment_Deterministic()
        {
            float a = MonetaryPolicyTradeoffRules.InvestmentFactor(0.2f);
            float b = MonetaryPolicyTradeoffRules.InvestmentFactor(0.2f);
            Assert.AreEqual(a, b, Tol);
        }

        [Test]
        public void InflationPressure_RisesWithOutputGap()
        {
            var p = MonetaryPolicyTradeoffRules.Params.Default;
            float lowGap = MonetaryPolicyTradeoffRules.InflationPressure(p.neutralRate, 0.2f, p);
            float highGap = MonetaryPolicyTradeoffRules.InflationPressure(p.neutralRate, 0.8f, p);
            Assert.Greater(highGap, lowGap);
        }

        [Test]
        public void InflationPressure_FallsWithHigherRate()
        {
            var p = MonetaryPolicyTradeoffRules.Params.Default;
            float lowRate = MonetaryPolicyTradeoffRules.InflationPressure(0.25f, 0.6f, p);
            float highRate = MonetaryPolicyTradeoffRules.InflationPressure(0.45f, 0.6f, p);
            Assert.Greater(lowRate, highRate);
        }

        [Test]
        public void InflationPressure_Clamped01()
        {
            var p = MonetaryPolicyTradeoffRules.Params.Default;
            // 高金利で大幅に冷やす → 0 下限
            Assert.AreEqual(0f, MonetaryPolicyTradeoffRules.InflationPressure(10f, 0.5f, p), Tol);
            // 需要最大・低金利 → 1 上限
            Assert.AreEqual(1f, MonetaryPolicyTradeoffRules.InflationPressure(-10f, 1f, p), Tol);
        }

        [Test]
        public void TaylorRate_RisesWhenInflationAboveTarget()
        {
            var p = MonetaryPolicyTradeoffRules.Params.Default;
            float atTarget = MonetaryPolicyTradeoffRules.TaylorRate(0.05f, 0.05f, 0f, p);
            float above = MonetaryPolicyTradeoffRules.TaylorRate(0.15f, 0.05f, 0f, p);
            Assert.Greater(above, atTarget);
        }

        [Test]
        public void TaylorRate_RisesWithOutputGap()
        {
            var p = MonetaryPolicyTradeoffRules.Params.Default;
            float lowGap = MonetaryPolicyTradeoffRules.TaylorRate(0.05f, 0.05f, 0.1f, p);
            float highGap = MonetaryPolicyTradeoffRules.TaylorRate(0.05f, 0.05f, 0.4f, p);
            Assert.Greater(highGap, lowGap);
        }

        [Test]
        public void TaylorRate_Clamped01()
        {
            var p = MonetaryPolicyTradeoffRules.Params.Default;
            // 大幅なインフレ超過 → 1 上限
            Assert.AreEqual(1f, MonetaryPolicyTradeoffRules.TaylorRate(2f, 0f, 1f, p), Tol);
            // 大幅なデフレ → 0 下限
            Assert.AreEqual(0f, MonetaryPolicyTradeoffRules.TaylorRate(-2f, 0f, -1f, p), Tol);
        }

        [Test]
        public void DefaultOverloads_MatchExplicitParams()
        {
            var p = MonetaryPolicyTradeoffRules.Params.Default;
            Assert.AreEqual(
                MonetaryPolicyTradeoffRules.InvestmentFactor(0.15f, p),
                MonetaryPolicyTradeoffRules.InvestmentFactor(0.15f), Tol);
            Assert.AreEqual(
                MonetaryPolicyTradeoffRules.InflationPressure(0.3f, 0.5f, p),
                MonetaryPolicyTradeoffRules.InflationPressure(0.3f, 0.5f), Tol);
            Assert.AreEqual(
                MonetaryPolicyTradeoffRules.TaylorRate(0.1f, 0.05f, 0.2f, p),
                MonetaryPolicyTradeoffRules.TaylorRate(0.1f, 0.05f, 0.2f), Tol);
        }
    }
}
