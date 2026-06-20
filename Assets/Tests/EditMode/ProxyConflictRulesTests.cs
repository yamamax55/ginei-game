using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    public class ProxyConflictRulesTests
    {
        const float Eps = 1e-5f;

        [Test]
        public void Boost_ProportionalToSupport()
        {
            var p = new ProxyConflictRules.Params(2f, 0.5f, 0.5f);
            Assert.AreEqual(0f, ProxyConflictRules.ProxyStrengthBoost(0f, p), Eps);
            Assert.AreEqual(0.6f, ProxyConflictRules.ProxyStrengthBoost(0.3f, p), Eps);
            Assert.AreEqual(2f, ProxyConflictRules.ProxyStrengthBoost(1f, p), Eps);
        }

        [Test]
        public void Boost_NonNegative()
        {
            float b = ProxyConflictRules.ProxyStrengthBoost(-1f);
            Assert.GreaterOrEqual(b, 0f);
        }

        [Test]
        public void Escalation_RisesWithSupport()
        {
            float lo = ProxyConflictRules.EscalationRisk(0.2f, 0.8f);
            float hi = ProxyConflictRules.EscalationRisk(0.9f, 0.8f);
            Assert.Greater(hi, lo);
        }

        [Test]
        public void Escalation_RisesWithRivalAwareness()
        {
            float lo = ProxyConflictRules.EscalationRisk(0.7f, 0.2f);
            float hi = ProxyConflictRules.EscalationRisk(0.7f, 0.9f);
            Assert.Greater(hi, lo);
        }

        [Test]
        public void Escalation_Clamped()
        {
            var p = new ProxyConflictRules.Params(1f, 10f, 0.5f);
            float v = ProxyConflictRules.EscalationRisk(1f, 1f, p);
            Assert.AreEqual(1f, v, Eps);
        }

        [Test]
        public void Exposure_RisesWithSupport()
        {
            float lo = ProxyConflictRules.ExposureRisk(0.2f, 0.8f);
            float hi = ProxyConflictRules.ExposureRisk(0.9f, 0.8f);
            Assert.Greater(hi, lo);
        }

        [Test]
        public void Exposure_RisesWithCounterIntel()
        {
            float lo = ProxyConflictRules.ExposureRisk(0.7f, 0.2f);
            float hi = ProxyConflictRules.ExposureRisk(0.7f, 0.9f);
            Assert.Greater(hi, lo);
        }

        [Test]
        public void Exposure_Clamped()
        {
            var p = new ProxyConflictRules.Params(1f, 0.5f, 10f);
            float v = ProxyConflictRules.ExposureRisk(1f, 1f, p);
            Assert.AreEqual(1f, v, Eps);
        }

        [Test]
        public void ZeroSupport_ZeroRisks()
        {
            Assert.AreEqual(0f, ProxyConflictRules.EscalationRisk(0f, 1f), Eps);
            Assert.AreEqual(0f, ProxyConflictRules.ExposureRisk(0f, 1f), Eps);
        }

        [Test]
        public void Risks_Deterministic()
        {
            float e1 = ProxyConflictRules.EscalationRisk(0.5f, 0.5f);
            float e2 = ProxyConflictRules.EscalationRisk(0.5f, 0.5f);
            Assert.AreEqual(e1, e2, Eps);
            float x1 = ProxyConflictRules.ExposureRisk(0.5f, 0.5f);
            float x2 = ProxyConflictRules.ExposureRisk(0.5f, 0.5f);
            Assert.AreEqual(x1, x2, Eps);
        }
    }
}
