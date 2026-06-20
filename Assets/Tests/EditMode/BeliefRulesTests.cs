using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>信念の更新と誤謬コスト（IHER-4 #2400）：反証成立・更新コスト・ロック閾値・更新リセット・強化クランプ・null安全。</summary>
    public class BeliefRulesTests
    {
        private static BeliefState Belief(float commitment = 0.5f, bool locked = false)
        {
            return new BeliefState(1, "起源", "在来説", commitment, locked);
        }

        // 確証的な反証（複数分野で収束し閾値を超える）。
        private static EvidenceBody ConclusiveContrary()
        {
            var b = new EvidenceBody();
            b.Add(new EvidenceFragment("起源", ResearchField.軍事, 0.6f, 1f));
            b.Add(new EvidenceFragment("起源", ResearchField.生産, 0.6f, 1f));
            b.Add(new EvidenceFragment("起源", ResearchField.情報, 0.6f, 1f));
            return b;
        }

        // 確証に至らない反証（単一・弱い）。
        private static EvidenceBody WeakContrary()
        {
            var b = new EvidenceBody();
            b.Add(new EvidenceFragment("起源", ResearchField.軍事, 0.1f, 1f));
            return b;
        }

        [Test]
        public void IsRefuted_TrueOnlyWhenContraryEvidenceConclusive()
        {
            float threshold = 1f;
            Assert.IsTrue(BeliefRules.IsRefuted(Belief(), ConclusiveContrary(), threshold));
            Assert.IsFalse(BeliefRules.IsRefuted(Belief(), WeakContrary(), threshold));
        }

        [Test]
        public void IsRefuted_MatchesEvidenceRulesIsConclusive()
        {
            float threshold = 1f;
            var ev = ConclusiveContrary();
            Assert.AreEqual(EvidenceRules.IsConclusive(ev, threshold),
                BeliefRules.IsRefuted(Belief(), ev, threshold));
        }

        [Test]
        public void IsRefuted_NullSafe()
        {
            Assert.IsFalse(BeliefRules.IsRefuted(null, ConclusiveContrary(), 1f));
            Assert.IsFalse(BeliefRules.IsRefuted(Belief(), null, 1f));
        }

        [Test]
        public void UpdateCost_ProportionalToCommitment()
        {
            Assert.AreEqual(0f, BeliefRules.UpdateCost(Belief(0f)), 1e-4f);
            float low = BeliefRules.UpdateCost(Belief(0.3f));
            float high = BeliefRules.UpdateCost(Belief(0.9f));
            Assert.Greater(high, low);
            Assert.AreEqual(0.9f * BeliefRules.CostPerCommitment, high, 1e-4f);
        }

        [Test]
        public void UpdateCost_NullIsZero()
        {
            Assert.AreEqual(0f, BeliefRules.UpdateCost(null), 1e-4f);
        }

        [Test]
        public void ShouldLock_AtAndAboveThreshold()
        {
            var b = Belief();
            Assert.IsTrue(BeliefRules.ShouldLock(b, BeliefRules.LockThreshold));
            Assert.IsTrue(BeliefRules.ShouldLock(b, BeliefRules.LockThreshold + 0.1f));
            Assert.IsFalse(BeliefRules.ShouldLock(b, BeliefRules.LockThreshold - 0.1f));
        }

        [Test]
        public void Update_SetsHypothesisAndResetsCommitmentAndUnlocks()
        {
            var b = Belief(0.8f, locked: true);
            BeliefRules.Update(b, "新説");
            Assert.AreEqual("新説", b.hypothesis);
            Assert.AreEqual(0f, b.commitment, 1e-4f);
            Assert.IsFalse(b.locked);
        }

        [Test]
        public void Update_NullSafe()
        {
            Assert.DoesNotThrow(() => BeliefRules.Update(null, "x"));
        }

        [Test]
        public void Reinforce_AddsAndClamps()
        {
            var b = Belief(0.5f);
            BeliefRules.Reinforce(b, 0.2f);
            Assert.AreEqual(0.7f, b.commitment, 1e-4f);

            BeliefRules.Reinforce(b, 5f);
            Assert.AreEqual(1f, b.commitment, 1e-4f);

            BeliefRules.Reinforce(b, -5f);
            Assert.AreEqual(0f, b.commitment, 1e-4f);
        }

        [Test]
        public void Reinforce_NullSafe()
        {
            Assert.DoesNotThrow(() => BeliefRules.Reinforce(null, 0.1f));
        }
    }
}
