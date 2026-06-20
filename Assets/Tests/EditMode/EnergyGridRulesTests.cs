using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>EnergyGridRules の EditMode テスト。</summary>
    public class EnergyGridRulesTests
    {
        private const float Tol = 1e-5f;

        [Test]
        public void SupplyRatio_Basic()
        {
            // 発電150, 需要100 → 1.5
            Assert.AreEqual(1.5f, EnergyGridRules.SupplyRatio(150f, 100f), Tol);
            Assert.AreEqual(0.5f, EnergyGridRules.SupplyRatio(50f, 100f), Tol);
        }

        [Test]
        public void SupplyRatio_ZeroDemandGuarded()
        {
            // 需要0でも例外なし（巨大だが有限）
            float r = EnergyGridRules.SupplyRatio(10f, 0f);
            Assert.IsFalse(float.IsInfinity(r));
            Assert.Greater(r, 0f);
        }

        [Test]
        public void SupplyRatio_Deterministic()
        {
            Assert.AreEqual(
                EnergyGridRules.SupplyRatio(120f, 100f),
                EnergyGridRules.SupplyRatio(120f, 100f), Tol);
        }

        [Test]
        public void BrownoutSeverity_ZeroWhenSupplyMeetsDemand()
        {
            Assert.AreEqual(0f, EnergyGridRules.BrownoutSeverity(1f), Tol);
            Assert.AreEqual(0f, EnergyGridRules.BrownoutSeverity(1.3f), Tol);
        }

        [Test]
        public void BrownoutSeverity_RisesAsSupplyFalls()
        {
            float mild = EnergyGridRules.BrownoutSeverity(0.9f);   // 0.1
            float severe = EnergyGridRules.BrownoutSeverity(0.4f); // 0.6
            Assert.Greater(severe, mild);
            Assert.AreEqual(0.1f, mild, Tol);
            Assert.AreEqual(0.6f, severe, Tol);
        }

        [Test]
        public void BrownoutSeverity_Clamped()
        {
            // 供給比が負/0でも上限1
            Assert.AreEqual(1f, EnergyGridRules.BrownoutSeverity(0f), Tol);
            Assert.AreEqual(1f, EnergyGridRules.BrownoutSeverity(-2f), Tol);
            // 範囲内で常に[0,1]
            float s = EnergyGridRules.BrownoutSeverity(0.5f);
            Assert.GreaterOrEqual(s, 0f);
            Assert.LessOrEqual(s, 1f);
        }

        [Test]
        public void OutputFactor_OneWhenNoBrownout()
        {
            Assert.AreEqual(1f, EnergyGridRules.OutputFactor(0f), Tol);
        }

        [Test]
        public void OutputFactor_FallsWithSeverity()
        {
            float a = EnergyGridRules.OutputFactor(0.2f);
            float b = EnergyGridRules.OutputFactor(0.6f);
            Assert.Less(b, a);
            // 既定penalty1.0 → 1-severity
            Assert.AreEqual(0.8f, a, Tol);
            Assert.AreEqual(0.4f, b, Tol);
        }

        [Test]
        public void OutputFactor_Clamped()
        {
            // 深刻度1かつペナルティ大でも0未満にならない
            var harsh = new EnergyGridRules.Params(0.15f, 5f);
            Assert.AreEqual(0f, EnergyGridRules.OutputFactor(1f, harsh), Tol);
            // 上限1（負の深刻度入力もclamp）
            Assert.AreEqual(1f, EnergyGridRules.OutputFactor(-0.5f), Tol);
        }

        [Test]
        public void OutputFactor_Deterministic()
        {
            Assert.AreEqual(
                EnergyGridRules.OutputFactor(0.3f),
                EnergyGridRules.OutputFactor(0.3f), Tol);
        }
    }
}
