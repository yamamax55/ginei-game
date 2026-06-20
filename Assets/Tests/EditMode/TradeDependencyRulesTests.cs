using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    public class TradeDependencyRulesTests
    {
        [Test]
        public void Dependence_RatioClamped()
        {
            Assert.AreEqual(0.5f, TradeDependencyRules.Dependence(50f, 100f), 1e-5f);
            // 相手からの輸入が総輸入を超えても1.0で頭打ち
            Assert.AreEqual(1f, TradeDependencyRules.Dependence(150f, 100f), 1e-5f);
            Assert.GreaterOrEqual(TradeDependencyRules.Dependence(0f, 100f), 0f);
        }

        [Test]
        public void Dependence_ZeroTotal_Guarded()
        {
            float d = TradeDependencyRules.Dependence(10f, 0f);
            Assert.IsFalse(float.IsNaN(d));
            Assert.IsFalse(float.IsInfinity(d));
            Assert.LessOrEqual(d, 1f);
            Assert.GreaterOrEqual(d, 0f);
        }

        [Test]
        public void Leverage_SymmetricDependence_NearZero()
        {
            float lev = TradeDependencyRules.Leverage(0.5f, 0.5f);
            Assert.AreEqual(0f, lev, 1e-5f);
        }

        [Test]
        public void Leverage_PartnerMoreDependent_Positive()
        {
            // 相手の依存度が高い＝こちらにてこ（正）
            float lev = TradeDependencyRules.Leverage(0.2f, 0.8f);
            Assert.Greater(lev, 0f);
            // 逆は負
            float rev = TradeDependencyRules.Leverage(0.8f, 0.2f);
            Assert.Less(rev, 0f);
        }

        [Test]
        public void Leverage_Clamped()
        {
            var p = new TradeDependencyRules.Params(10f);
            float hi = TradeDependencyRules.Leverage(0f, 1f, p);
            float lo = TradeDependencyRules.Leverage(1f, 0f, p);
            Assert.AreEqual(1f, hi, 1e-5f);
            Assert.AreEqual(-1f, lo, 1e-5f);
            Assert.LessOrEqual(hi, 1f);
            Assert.GreaterOrEqual(lo, -1f);
        }

        [Test]
        public void Leverage_Deterministic()
        {
            float a = TradeDependencyRules.Leverage(0.3f, 0.7f);
            float b = TradeDependencyRules.Leverage(0.3f, 0.7f);
            Assert.AreEqual(a, b, 1e-5f);
        }

        [Test]
        public void CoercionPotential_FloorsNegatives()
        {
            Assert.AreEqual(0f, TradeDependencyRules.CoercionPotential(-0.5f), 1e-5f);
            Assert.AreEqual(0f, TradeDependencyRules.CoercionPotential(0f), 1e-5f);
            Assert.AreEqual(0.6f, TradeDependencyRules.CoercionPotential(0.6f), 1e-5f);
        }
    }
}
