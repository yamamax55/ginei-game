using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 交戦規定（ROE・#2258）：CanFire / CanPursue / AdvanceFactor の全スタンス検証。
    /// </summary>
    public class RoeRulesTests
    {
        // ── CanFire ──────────────────────────────────────────────

        [Test]
        public void CanFire_攻撃的_ReturnsTrue()
        {
            Assert.IsTrue(RoeRules.CanFire(EngagementStance.攻撃的));
        }

        [Test]
        public void CanFire_防御的_ReturnsTrue()
        {
            Assert.IsTrue(RoeRules.CanFire(EngagementStance.防御的));
        }

        [Test]
        public void CanFire_射撃管制_ReturnsFalse()
        {
            Assert.IsFalse(RoeRules.CanFire(EngagementStance.射撃管制));
        }

        [Test]
        public void CanFire_退避_ReturnsFalse()
        {
            Assert.IsFalse(RoeRules.CanFire(EngagementStance.退避));
        }

        // ── CanPursue ─────────────────────────────────────────────

        [Test]
        public void CanPursue_攻撃的_ReturnsTrue()
        {
            Assert.IsTrue(RoeRules.CanPursue(EngagementStance.攻撃的));
        }

        [Test]
        public void CanPursue_防御的_ReturnsFalse()
        {
            Assert.IsFalse(RoeRules.CanPursue(EngagementStance.防御的));
        }

        [Test]
        public void CanPursue_射撃管制_ReturnsFalse()
        {
            Assert.IsFalse(RoeRules.CanPursue(EngagementStance.射撃管制));
        }

        [Test]
        public void CanPursue_退避_ReturnsFalse()
        {
            Assert.IsFalse(RoeRules.CanPursue(EngagementStance.退避));
        }

        // ── AdvanceFactor ─────────────────────────────────────────

        [Test]
        public void AdvanceFactor_攻撃的_ReturnsOne()
        {
            Assert.AreEqual(1.0f, RoeRules.AdvanceFactor(EngagementStance.攻撃的), 1e-4f);
        }

        [Test]
        public void AdvanceFactor_防御的_ReturnsHalf()
        {
            Assert.AreEqual(0.5f, RoeRules.AdvanceFactor(EngagementStance.防御的), 1e-4f);
        }

        [Test]
        public void AdvanceFactor_射撃管制_ReturnsPt3()
        {
            Assert.AreEqual(0.3f, RoeRules.AdvanceFactor(EngagementStance.射撃管制), 1e-4f);
        }

        [Test]
        public void AdvanceFactor_退避_ReturnsZero()
        {
            Assert.AreEqual(0.0f, RoeRules.AdvanceFactor(EngagementStance.退避), 1e-4f);
        }
    }
}
